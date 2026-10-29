using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Common;
using ShopListApi.Transactions;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// 分類の手動変更からのルール作成（手動学習）のテスト。
/// 実際のPostgreSQLを通し、所有者の分離・同時操作の競合・保存失敗時の巻き戻しも確認する。
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class TransactionLearningTests : PostgresTestBase
{
    /// <summary>
    /// 未分類の取引を直すとルールが保存され、別の接続で登録した次の取引にも使われることを確認する。
    /// </summary>
    [TestMethod]
    public async Task ManualCategoryChangeCreatesRuleAndClassifiesNextTransaction()
    {
        // ルールが無いので、最初の取引は未分類になる
        var category = await AddCategoryAsync("食費");
        TransactionResponse first;
        await using (var db = CreateDbContext())
        {
            first = await new TransactionService(db, User).CreateAsync(CreateRequest());
        }

        Assert.IsNull(first.CategoryId);

        // 分類を手で直す
        await using (var db = CreateDbContext())
        {
            await new TransactionService(db, User).UpdateAsync(first.Id, UpdateRequest(category.Id));
        }

        // 取引の分類と、店名をキーワードにした自分のルールが保存されている
        await using (var db = CreateDbContext())
        {
            var saved = await db.Transactions.SingleAsync(t => t.Id == first.Id);
            var rule = await db.CategoryRules.SingleAsync();
            Assert.AreEqual((int?)category.Id, saved.CategoryId);
            Assert.AreEqual(User.Id, rule.UserId);
            Assert.AreEqual(Merchant, rule.Keyword);
            Assert.AreEqual(category.Id, rule.CategoryId);
        }

        // 同じ店名の次の取引は、作ったルールで自動分類される
        await using (var db = CreateDbContext())
        {
            var next = await new TransactionService(db, User).CreateAsync(CreateRequest());
            Assert.AreEqual((int?)category.Id, next.CategoryId);
        }
    }

    /// <summary>
    /// 同じ店名の自分のルールは、件数とIDを変えずに分類だけ更新することを確認する。
    /// </summary>
    [TestMethod]
    public async Task ExistingExactKeywordIsUpdatedWithoutAddingAnotherRule()
    {
        var oldCategory = await AddCategoryAsync("食費");
        var newCategory = await AddCategoryAsync("日用品");
        var transaction = await AddTransactionAsync(oldCategory.Id);
        var rule = await AddRuleAsync(oldCategory.Id);

        await using (var db = CreateDbContext())
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, UpdateRequest(newCategory.Id));
        }

        await using (var db = CreateDbContext())
        {
            // ルールは1件のまま、同じIDで分類だけが変わる
            var savedRule = await db.CategoryRules.SingleAsync();
            Assert.AreEqual(rule.Id, savedRule.Id);
            Assert.AreEqual(newCategory.Id, savedRule.CategoryId);
            Assert.AreEqual((int?)newCategory.Id,
                (await db.Transactions.SingleAsync(t => t.Id == transaction.Id)).CategoryId);

            // 次の取引は、変更後の分類になる
            var next = await new TransactionService(db, User).CreateAsync(CreateRequest());
            Assert.AreEqual((int?)newCategory.Id, next.CategoryId);
        }
    }

    /// <summary>
    /// 分類と店名を一緒に直した場合は、更新後の店名をそのままキーワードにすることを確認する。
    /// </summary>
    [TestMethod]
    public async Task LearningUsesTheUpdatedMerchant()
    {
        var category = await AddCategoryAsync("食費");
        var transaction = await AddTransactionAsync();
        var request = UpdateRequest(category.Id);
        request.Merchant = "変更後の店";

        await using (var db = CreateDbContext())
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, request);
        }

        // 変更前の店名ではなく、変更後の店名でルールができる
        await using var verification = CreateDbContext();
        var rule = await verification.CategoryRules.SingleAsync();
        Assert.AreEqual("変更後の店", rule.Keyword);
        Assert.AreEqual("変更後の店", (await verification.Transactions.SingleAsync()).Merchant);
    }

    /// <summary>
    /// 分類が変わらない変更と未分類への変更では、既存ルールを変更・追加しないことを確認する。
    /// </summary>
    /// <param name="clearCategory">true なら未分類に戻す。false なら同じ分類のまま、金額と店名だけ変える</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SameCategoryOrClearingCategoryDoesNotLearn(bool clearCategory)
    {
        var category = await AddCategoryAsync("食費");
        var transaction = await AddTransactionAsync(category.Id);
        var rule = await AddRuleAsync(category.Id);

        // 店名も変えて、学習していれば「別の店」のルールが増えるようにする
        var request = UpdateRequest(clearCategory ? null : category.Id);
        request.Merchant = "別の店";
        request.Amount = 200;

        await using (var db = CreateDbContext())
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, request);
        }

        // ルールは元のまま1件で、取引だけが更新されている
        await using var verification = CreateDbContext();
        var savedRule = await verification.CategoryRules.SingleAsync();
        var savedTransaction = await verification.Transactions.SingleAsync();
        Assert.AreEqual(rule.Id, savedRule.Id);
        Assert.AreEqual(Merchant, savedRule.Keyword);
        Assert.AreEqual(category.Id, savedRule.CategoryId);
        Assert.AreEqual(request.CategoryId, savedTransaction.CategoryId);
        Assert.AreEqual(200, savedTransaction.Amount);
        Assert.AreEqual("別の店", savedTransaction.Merchant);
    }

    /// <summary>
    /// 他人の同じキーワードのルールは書き換えず、自分のルールを別に作ることを確認する。
    /// </summary>
    [TestMethod]
    public async Task LearningDoesNotOverwriteAnotherUsersRule()
    {
        // 他人が、同じ店名のルールを先に持っている
        var ownCategory = await AddCategoryAsync("食費");
        var otherCategory = await AddCategoryAsync("他人の分類", OtherUserId);
        var otherRule = await AddRuleAsync(otherCategory.Id, OtherUserId);
        var transaction = await AddTransactionAsync();

        await using (var db = CreateDbContext())
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, UpdateRequest(ownCategory.Id));
        }

        // 他人のルールはそのまま残り、自分のルールが1件増える
        await using var verification = CreateDbContext();
        Assert.AreEqual(2, await verification.CategoryRules.CountAsync());
        var untouched = await verification.CategoryRules.SingleAsync(r => r.Id == otherRule.Id);
        Assert.AreEqual(OtherUserId, untouched.UserId);
        Assert.AreEqual(otherCategory.Id, untouched.CategoryId);
        var ownRule = await verification.CategoryRules.SingleAsync(r => r.UserId == User.Id);
        Assert.AreEqual(ownCategory.Id, ownRule.CategoryId);

        // 次の取引には、自分のルールが使われる
        var next = await new TransactionService(verification, User).CreateAsync(CreateRequest());
        Assert.AreEqual((int?)ownCategory.Id, next.CategoryId);
    }

    /// <summary>
    /// 店名に一致しても、他人のルールだけなら未分類で保存することを確認する。
    /// </summary>
    [TestMethod]
    public async Task CreationIgnoresAnotherUsersMatchingRule()
    {
        var otherCategory = await AddCategoryAsync("他人の分類", OtherUserId);
        await AddRuleAsync(otherCategory.Id, OtherUserId);

        TransactionResponse response;
        await using (var db = CreateDbContext())
        {
            response = await new TransactionService(db, User).CreateAsync(CreateRequest());
        }

        // 返す値とDBの両方で、未分類になっている
        Assert.IsNull(response.CategoryId);
        await using var verification = CreateDbContext();
        var saved = await verification.Transactions.SingleAsync();
        Assert.AreEqual(User.Id, saved.UserId);
        Assert.IsNull(saved.CategoryId);
    }

    /// <summary>
    /// 他人の取引は更新できず（404）、ルールも作らないことを確認する。
    /// </summary>
    [TestMethod]
    public async Task UpdatingAnotherUsersTransactionIsRejectedWithoutSaving()
    {
        var category = await AddCategoryAsync("食費");
        var transaction = await AddTransactionAsync(userId: OtherUserId);

        await using (var db = CreateDbContext())
        {
            var service = new TransactionService(db, User);
            var exception = await Assert.ThrowsExactlyAsync<AppException>
            (
                () => service.UpdateAsync(transaction.Id, UpdateRequest(category.Id))
            );
            Assert.AreEqual(404, exception.StatusCode);
            Assert.AreEqual(ErrorCodes.Transactions.NotFound, exception.Code);
        }

        // 他人の取引は未分類のまま、ルールも0件
        await using var verification = CreateDbContext();
        Assert.IsNull((await verification.Transactions.SingleAsync()).CategoryId);
        Assert.AreEqual(0, await verification.CategoryRules.CountAsync());
    }

    /// <summary>
    /// 実在する分類でも、他人の分類なら取引にもルールにも使わない（422）ことを確認する。
    /// </summary>
    [TestMethod]
    public async Task AssigningAnotherUsersCategoryIsRejectedWithoutSaving()
    {
        var otherCategory = await AddCategoryAsync("他人の分類", OtherUserId);
        var transaction = await AddTransactionAsync();

        await using (var db = CreateDbContext())
        {
            var service = new TransactionService(db, User);
            var exception = await Assert.ThrowsExactlyAsync<AppException>(
                () => service.UpdateAsync(transaction.Id, UpdateRequest(otherCategory.Id)));
            Assert.AreEqual(422, exception.StatusCode);
            Assert.AreEqual(ErrorCodes.Categories.NotFound, exception.Code);
        }

        // 取引は未分類のまま、ルールも0件
        await using var verification = CreateDbContext();
        Assert.IsNull((await verification.Transactions.SingleAsync()).CategoryId);
        Assert.AreEqual(0, await verification.CategoryRules.CountAsync());
    }

    /// <summary>
    /// ルールを探した後に別接続が同じルールを登録しても、1件にまとめて保存することを確認する。
    /// </summary>
    [TestMethod]
    public async Task ConcurrentRuleInsertRetriesAndSavesTransactionAndRule()
    {
        var originalCategory = await AddCategoryAsync("食費");
        var targetCategory = await AddCategoryAsync("日用品");
        var transaction = await AddTransactionAsync();

        // 「ルールが無い」と判断した後、保存の直前に別の接続が同じ店名のルールを登録する
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await AddRuleAsync(originalCategory.Id);
        });

        await using (var db = CreateDbContext(interceptor))
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, UpdateRequest(targetCategory.Id));
        }

        // 1回目は一意制約で失敗し、探し直した2回目で保存できている
        Assert.AreEqual(2, interceptor.SaveCount);

        // 別の接続が登録したルールを、選んだ分類に書き換えて1件にしている
        await using var verification = CreateDbContext();
        Assert.AreEqual(targetCategory.Id, (await verification.CategoryRules.SingleAsync()).CategoryId);
        Assert.AreEqual((int?)targetCategory.Id, (await verification.Transactions.SingleAsync()).CategoryId);
    }

    /// <summary>
    /// 更新予定のルールが別接続で消されても、ルールを作り直して取引と保存することを確認する。
    /// </summary>
    [TestMethod]
    public async Task ConcurrentRuleDeleteRetriesAndRecreatesRule()
    {
        var originalCategory = await AddCategoryAsync("食費");
        var targetCategory = await AddCategoryAsync("日用品");
        var transaction = await AddTransactionAsync(originalCategory.Id);
        var oldRule = await AddRuleAsync(originalCategory.Id);

        // 既存のルールを書き換えると決めた後、保存の直前に別の接続がそのルールを消す
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var competingDb = CreateDbContext();
            var rule = await competingDb.CategoryRules.SingleAsync(r => r.Id == oldRule.Id);
            competingDb.CategoryRules.Remove(rule);
            await competingDb.SaveChangesAsync();
        });

        await using (var db = CreateDbContext(interceptor))
        {
            await new TransactionService(db, User).UpdateAsync(transaction.Id, UpdateRequest(targetCategory.Id));
        }

        // 1回目は更新が0行で失敗し、探し直した2回目で保存できている
        Assert.AreEqual(2, interceptor.SaveCount);

        // 消されたルールとは別のIDで、新しいルールが作られている
        await using var verification = CreateDbContext();
        var savedRule = await verification.CategoryRules.SingleAsync();
        Assert.AreNotEqual(oldRule.Id, savedRule.Id);
        Assert.AreEqual(targetCategory.Id, savedRule.CategoryId);
        Assert.AreEqual((int?)targetCategory.Id, (await verification.Transactions.SingleAsync()).CategoryId);
    }

    /// <summary>
    /// 存在確認後に分類が消えた場合、実際の外部キー違反で保存が失敗し、取引も元のまま残ることを確認する。
    /// </summary>
    [TestMethod]
    public async Task CategoryDeletedBeforeSaveRollsBackTransactionAndRule()
    {
        var originalCategory = await AddCategoryAsync("食費");
        var targetCategory = await AddCategoryAsync("日用品");
        var transaction = await AddTransactionAsync(originalCategory.Id);

        // 分類があると確かめた後、保存の直前に別の接続がその分類を消す
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var competingDb = CreateDbContext();
            var category = await competingDb.Categories.SingleAsync(c => c.Id == targetCategory.Id);
            competingDb.Categories.Remove(category);
            await competingDb.SaveChangesAsync();
        });

        // 金額も変えて、取引の更新が巻き戻されたかを見分けられるようにする
        var request = UpdateRequest(targetCategory.Id);
        request.Amount = 200;

        await using (var db = CreateDbContext(interceptor))
        {
            var service = new TransactionService(db, User);
            var exception = await Assert.ThrowsExactlyAsync<AppException>(
                () => service.UpdateAsync(transaction.Id, request));
            Assert.AreEqual(422, exception.StatusCode);
            Assert.AreEqual(ErrorCodes.Categories.NotFound, exception.Code);
        }

        // 取引は分類も金額も元のままで、ルールも作られていない
        await using var verification = CreateDbContext();
        var saved = await verification.Transactions.SingleAsync();
        Assert.AreEqual((int?)originalCategory.Id, saved.CategoryId);
        Assert.AreEqual(100, saved.Amount);
        Assert.AreEqual(0, await verification.CategoryRules.CountAsync());
    }

    /// <summary>
    /// ルールで分類を決めた後に分類が消えても、エラーにせず未分類で取引を保存することを確認する。
    /// </summary>
    [TestMethod]
    public async Task CategoryDeletedBeforeCreationSavesTransactionUncategorized()
    {
        var category = await AddCategoryAsync("食費");
        await AddRuleAsync(category.Id);

        // ルールで分類を決めた後、保存の直前に別の接続がその分類を消す（ルールも Cascade で消える）
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var competingDb = CreateDbContext();
            var deleted = await competingDb.Categories.SingleAsync(c => c.Id == category.Id);
            competingDb.Categories.Remove(deleted);
            await competingDb.SaveChangesAsync();
        });

        TransactionResponse response;
        await using (var db = CreateDbContext(interceptor))
        {
            response = await new TransactionService(db, User).CreateAsync(CreateRequest());
        }

        // 1回目は外部キー違反で失敗し、未分類にした2回目で保存できている
        Assert.AreEqual(2, interceptor.SaveCount);
        Assert.IsNull(response.CategoryId);

        // 支払いの記録は失われず、未分類で1件保存されている
        await using var verification = CreateDbContext();
        var saved = await verification.Transactions.SingleAsync();
        Assert.AreEqual(response.Id, saved.Id);
        Assert.IsNull(saved.CategoryId);
        Assert.AreEqual(0, await verification.CategoryRules.CountAsync());
    }

    /// <summary>
    /// 存在確認後に取引が消えた場合は404にし、学習のルールも残さないことを確認する。
    /// </summary>
    [TestMethod]
    public async Task TransactionDeletedBeforeSaveReturnsNotFoundWithoutRule()
    {
        var category = await AddCategoryAsync("食費");
        var transaction = await AddTransactionAsync();

        // 取引があると確かめた後、保存の直前に別の接続がその取引を消す
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var competingDb = CreateDbContext();
            var deleted = await competingDb.Transactions.SingleAsync(t => t.Id == transaction.Id);
            competingDb.Transactions.Remove(deleted);
            await competingDb.SaveChangesAsync();
        });

        await using (var db = CreateDbContext(interceptor))
        {
            var service = new TransactionService(db, User);
            var exception = await Assert.ThrowsExactlyAsync<AppException>(
                () => service.UpdateAsync(transaction.Id, UpdateRequest(category.Id)));
            Assert.AreEqual(404, exception.StatusCode);
            Assert.AreEqual(ErrorCodes.Transactions.NotFound, exception.Code);
        }

        // 取引が無いことはルールの競合ではないので、保存し直さない
        Assert.AreEqual(1, interceptor.SaveCount);

        // 一緒に追加しようとしたルールも、取引の更新と一緒に巻き戻されている
        await using var verification = CreateDbContext();
        Assert.AreEqual(0, await verification.Transactions.CountAsync());
        Assert.AreEqual(0, await verification.CategoryRules.CountAsync());
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 分類を1件保存する。ユーザーを省略したら今のユーザーの分類にする
    /// </summary>
    private async Task<Category> AddCategoryAsync(string name, Guid? userId = null)
    {
        await using var db = CreateDbContext();
        var category = new Category { Name = name, UserId = userId ?? User.Id };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    /// <summary>
    /// 確認用の店名で、金額100円の支出を1件保存する。分類を省略したら未分類にする
    /// </summary>
    private async Task<Transaction> AddTransactionAsync(int? categoryId = null, Guid? userId = null)
    {
        await using var db = CreateDbContext();
        var transaction = new Transaction
        {
            UserId = userId ?? User.Id,
            CategoryId = categoryId,
            Amount = 100,
            Merchant = Merchant,
            Date = TestDate,
            Source = TransactionSource.Manual,
            Type = TransactionType.Expense
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    /// <summary>
    /// 確認用の店名をキーワードにしたルールを1件保存する
    /// </summary>
    private async Task<CategoryRule> AddRuleAsync(int categoryId, Guid? userId = null)
    {
        await using var db = CreateDbContext();
        var rule = new CategoryRule { UserId = userId ?? User.Id, Keyword = Merchant, CategoryId = categoryId };
        db.CategoryRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    /// <summary>
    /// 確認用の店名で取引を作るリクエスト（分類はルールで決まる）
    /// </summary>
    private static CreateTransactionRequest CreateRequest() => new()
    {
        Amount = 100,
        Merchant = Merchant,
        Date = TestDate,
        Source = TransactionSource.Manual,
        Type = TransactionType.Expense
    };

    /// <summary>
    /// 金額・店名・日付は保存済みの取引と同じで、分類だけを指定する更新リクエスト
    /// </summary>
    private static UpdateTransactionRequest UpdateRequest(int? categoryId) => new()
    {
        Amount = 100,
        Merchant = Merchant,
        Date = TestDate,
        Type = TransactionType.Expense,
        CategoryId = categoryId
    };

    /// <summary>
    /// 最初の保存直前にだけ別接続の操作を挟み、競合を待ち時間なしで再現する。
    /// 保存処理とDBの制約は差し替えない。
    /// </summary>
    private sealed class BeforeFirstSaveInterceptor(Func<Task> beforeSave) : SaveChangesInterceptor
    {
        /// <summary>
        /// SaveChanges が呼ばれた回数。再試行したかどうかの確認に使う
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// 保存の直前に呼ばれる。1回目だけ別接続の操作を実行し、保存はそのまま続ける
        /// </summary>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync
        (
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            SaveCount++;

            // 再試行の2回目に同じ操作をすると、競合がもう一度起きてしまうため
            if (SaveCount == 1)
            {
                await beforeSave();
            }

            return result;
        }
    }

    /// <summary>
    /// 確認用の店名。ルールのキーワードにも使う
    /// </summary>
    private const string Merchant = "駅前スーパー";

    /// <summary>
    /// 確認用の取引の日付
    /// </summary>
    private static readonly DateOnly TestDate = new(2026, 10, 15);

    #endregion
}
