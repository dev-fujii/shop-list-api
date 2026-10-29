using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopListApi.Categories;
using ShopListApi.Summaries;
using ShopListApi.Transactions;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// 集計のテスト。
/// PostgreSQL上で、期間指定・所有者の絞り込み・分類別の合計を確認する。
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class SummaryDatabaseTests : PostgresTestBase
{
    /// <summary>
    /// 期間の両端と未分類を含め、期間外・他人の取引を除いて、支出と収入を別に集計することを確認する。
    /// </summary>
    [TestMethod]
    public async Task SummaryIncludesBoundariesAndUncategorizedRowsButExcludesOtherUsersAndDates()
    {
        var food = new Category { Name = "食費", UserId = User.Id };
        var salary = new Category { Name = "給料", UserId = User.Id };
        await using (var db = CreateDbContext())
        {
            // 取引から分類IDを使うので、先に分類を保存する
            db.Categories.AddRange(food, salary);
            await db.SaveChangesAsync();
            db.Transactions.AddRange
            (
                // 対象: 月の初日と末日の支出（食費）
                Row(100, new DateOnly(2026, 10, 1), food.Id),
                Row(200, new DateOnly(2026, 10, 31), food.Id),

                // 対象: 未分類の支出
                Row(50, new DateOnly(2026, 10, 15)),

                // 対象: 収入。同じ食費でも、支出とは別の行に分ける
                Row(600, new DateOnly(2026, 10, 15), food.Id, TransactionType.Income),
                Row(1000, new DateOnly(2026, 10, 15), salary.Id, TransactionType.Income),

                // 対象外: 期間の前後の日と、他人の取引
                Row(999, new DateOnly(2026, 9, 30), food.Id),
                Row(999, new DateOnly(2026, 11, 1), food.Id),
                Row(2000, new DateOnly(2026, 10, 15), userId: OtherUserId)
            );
            await db.SaveChangesAsync();
        }

        await using var verification = CreateDbContext();
        var service = new SummaryService(verification, User, Options.Create(new SummaryOptions()));
        // 月指定と開始日・終了日の指定の両方で、同じ保存済みデータを集計する。
        SummaryQuery[] queries =
        [
            new() { Month = "2026-10" },
            new() { From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 31) }
        ];
        foreach (var query in queries)
        {
            var actual = await service.GetAsync(query);
            Assert.AreEqual(new DateOnly(2026, 10, 1), actual.From);
            Assert.AreEqual(new DateOnly(2026, 10, 31), actual.To);

            // 支出 100 + 200 + 50、収入 600 + 1000、収支は収入 - 支出
            Assert.AreEqual(350L, actual.TotalExpense);
            Assert.AreEqual(1600L, actual.TotalIncome);
            Assert.AreEqual(1250L, actual.Balance);

            // 分類別は「分類 × 種類」ごとの4行になる
            Assert.HasCount(4, actual.ByCategory);
            Assert.AreEqual(300L, actual.ByCategory.Single(c => c.CategoryId == food.Id && c.Type == TransactionType.Expense).Total);
            Assert.AreEqual(600L, actual.ByCategory.Single(c => c.CategoryId == food.Id && c.Type == TransactionType.Income).Total);
            Assert.AreEqual(50L, actual.ByCategory.Single(c => c.CategoryId is null && c.Type == TransactionType.Expense).Total);
            Assert.AreEqual(1000L, actual.ByCategory.Single(c => c.CategoryId == salary.Id && c.Type == TransactionType.Income).Total);
        }
    }

    /// <summary>
    /// 対象の取引が無ければ、分類別の一覧は空で、合計と収支は0になることを確認する。
    /// </summary>
    [TestMethod]
    public async Task EmptyPeriodReturnsZeroTotalsAndNoGroups()
    {
        await using var db = CreateDbContext();
        var service = new SummaryService(db, User, Options.Create(new SummaryOptions()));
        var actual = await service.GetAsync(new SummaryQuery { Month = "2026-10" });
        Assert.AreEqual(0L, actual.TotalExpense);
        Assert.AreEqual(0L, actual.TotalIncome);
        Assert.AreEqual(0L, actual.Balance);
        Assert.IsEmpty(actual.ByCategory);
    }

    /// <summary>
    /// 1件の金額は範囲内でも、合計がintを超えるデータをDBで正しく集計できることを確認する。
    /// </summary>
    [TestMethod]
    public async Task DatabaseSumSupportsTotalsGreaterThanIntMaxValue()
    {
        // 上限の金額を220件。合計は int の最大値（約21億）を超える
        await using (var db = CreateDbContext())
        {
            db.Transactions.AddRange(Enumerable.Range(0, 220)
                .Select(_ => Row(Transaction.AmountMax, new DateOnly(2026, 10, 15))));
            await db.SaveChangesAsync();
        }

        await using var verification = CreateDbContext();
        var service = new SummaryService(verification, User, Options.Create(new SummaryOptions()));
        var actual = await service.GetAsync(new SummaryQuery { Month = "2026-10" });

        // 9,999,999 × 220。int では表せない値を、long で正しく受け取れているかを確かめる
        const long expected = 2_199_999_780L;
        Assert.AreEqual(expected, actual.TotalExpense);
        Assert.AreEqual(0L, actual.TotalIncome);
        Assert.AreEqual(-expected, actual.Balance);
        Assert.AreEqual(expected, actual.ByCategory.Single().Total);
    }

    /// <summary>
    /// 集計の確認用の取引を作る。省略したら今のユーザーの未分類の支出にする
    /// </summary>
    private static Transaction Row
    (
        int amount,
        DateOnly date,
        int? categoryId = null,
        TransactionType type = TransactionType.Expense,
        Guid? userId = null
    ) => new()
    {
        UserId = userId ?? User.Id,
        CategoryId = categoryId,
        Amount = amount,
        Merchant = "集計の確認用",
        Date = date,
        Type = type,
        Source = TransactionSource.Manual
    };
}
