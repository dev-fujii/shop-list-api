using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
using ShopListApi.CategoryRules;
using ShopListApi.Data;

namespace ShopListApi.Transactions;

/// <summary>
/// 取引の処理（一覧・1件・作成・更新・削除）
/// 今のユーザーの取引だけを扱う
/// </summary>
public class TransactionService
{

    /// <summary>
    /// 取引を作る。
    /// 日付を省略したら日本時間の今日、種類を省略したら支出にする
    /// </summary>
    /// <remarks>
    /// 冪等キーがあり、同じキーの取引がもうあれば、新しく作らずにその取引を返す。
    /// 同じキーなのに金額・店名・入力元が違えば 422
    /// </remarks>
    public async Task<TransactionResponse> CreateAsync(CreateTransactionRequest request, string? idempotencyKey = null)
    {
        // 送り直しなら、前に登録した取引を返す
        if (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey);
            if (existing is not null)
            {
                return ToRetryResponse(existing, request);
            }
        }

        // 他人のルールを使わないように、今のユーザーのものだけ取得する。
        var rules = await _db.CategoryRules
            .AsNoTracking()
            .Where(r => r.UserId == _currentUser.Id)
            .ToListAsync();

        // 店名に一致するルールから分類を決める。一致が無ければ未分類にする。
        var categoryId = CategoryRuleMatcher.FindCategoryId(request.Merchant, rules);

        var transaction = new Transaction
        {
            UserId = _currentUser.Id,
            CategoryId = categoryId,
            Amount = request.Amount!.Value,
            Merchant = request.Merchant,
            Date = request.Date ?? JapanTime.Today(),
            Source = request.Source!.Value,
            Type = request.Type ?? TransactionType.Expense,
            IdempotencyKey = idempotencyKey
        };

        _db.Transactions.Add(transaction);
        try
        {
            await SaveNewTransactionAsync(transaction);
        }
        catch (DbUpdateException ex) when (idempotencyKey is not null && ex.IsUniqueViolation())
        {
            // 同じキーのリクエストが同時に来て、先に別のリクエストが登録した。
            // 上の確認のあとなので、一意インデックスが2件目を断った。
            // 失敗した取引は保存しないので追跡から外し、先に登録された取引を返す
            _db.Entry(transaction).State = EntityState.Detached;
            var saved = await FindByIdempotencyKeyAsync(idempotencyKey)
                ?? throw new InvalidOperationException("一意インデックスに断られたのに、同じ冪等キーの取引が見つかりません。");
            return ToRetryResponse(saved, request);
        }

        return ToResponse(transaction);
    }


    /// <summary>
    /// 自分の取引を1件。無い・他人の取引なら 404
    /// </summary>
    public async Task<TransactionResponse> GetByIdAsync(int id)
    {
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUser.Id);

        // 無い・他人の取引なら 404
        if (transaction is null)
        {
            throw CreateTransactionNotFoundException();
        }

        return new TransactionResponse
        {
            CategoryId = transaction.CategoryId,
            Id = transaction.Id,
            Amount = transaction.Amount,
            Merchant = transaction.Merchant,
            Date = transaction.Date,
            Source = transaction.Source,
            Type = transaction.Type
        };
    }

    /// <summary>
    /// 分類・期間で絞り、指定されたページの取引を取得する
    /// </summary>
    public async Task<PagedResponse<TransactionResponse>> GetAllAsync
    (
        int page,
        int pageSize,
        int? categoryId = null,
        DateOnly? from = null,
        DateOnly? to = null
    )
    {
        var query = _db.Transactions.Where(t => t.UserId == _currentUser.Id);
        query = ApplyFilters(query, categoryId, from, to);
        var totalCount = await query.CountAsync();

        // 大きなページ番号でも掛け算があふれないよう、先に long にする（分類ルールの一覧と同じ）
        var skip = ((long)page - 1) * pageSize;
        var items = new List<TransactionResponse>();
        if (skip < totalCount)
        {
            items = await query

                //新しい日付順に
                .OrderByDescending(t => t.Date)

                //同じ日付ならIDの大きい順
                .ThenByDescending(t => t.Id)

                //指定ページより前の件数を飛ばす（skip は totalCount より小さいので int に収まる）
                .Skip((int)skip)

                //指定ページの分を、最大 pageSize 件取得する
                .Take(pageSize)

                // 各取引を TransactionResponse の形にする
                .Select(t => new TransactionResponse
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Merchant = t.Merchant,
                    Date = t.Date,
                    Type = t.Type,
                    Source = t.Source,
                    CategoryId = t.CategoryId
                })

                // DBに問い合わせ、結果をリストにする
                .ToListAsync();
        }

        return new PagedResponse<TransactionResponse>
        (
            items,
            page,
            pageSize,
            totalCount
        );
    }

    /// <summary>
    /// 自分の取引を更新する
    /// 分類を手で変えたら、次回用の分類ルールも追加・更新する
    /// </summary>
    /// <remarks>
    /// 取引が無い・他人のものなら 404、指定した分類が無い・他人のものなら 422。
    /// 未分類に戻す場合や、金額・店名だけを変える場合はルールを変更しない。
    /// </remarks>
    public async Task UpdateAsync(int id, UpdateTransactionRequest request)
    {
        var transaction = await FindOwnedTransactionAsync(id);
        if (transaction is null)
        {
            throw CreateTransactionNotFoundException();
        }

        if (request.CategoryId.HasValue &&
            !await IsOwnedCategoryAsync(request.CategoryId.Value))
        {
            throw CreateCategoryNotFoundException();
        }

        // 分類を書き換える前に、ルールを追加・更新するか決める
        bool shouldLearn = request.CategoryId.HasValue &&
            request.CategoryId.Value != transaction.CategoryId;

        ApplyUpdate(transaction, request);

        // 取引とルールは、最後にまとめて書き込む。片方だけ更新されるのを防ぐため
        CategoryRule? learnedRule = null;
        if (shouldLearn)
        {
            learnedRule = await PrepareLearnedRuleAsync(transaction);
        }

        try
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (learnedRule is not null && IsLearnedRuleConflict(ex))
            {
                // 同じ店名のルールが同時に登録・削除された場合は、ルールを探し直して準備し直す。
                // 失敗した保存は取引の更新もロールバックされるので、両方まとめて1回だけ保存し直す（無限に再試行しない）
                _db.Entry(learnedRule).State = EntityState.Detached;
                await PrepareLearnedRuleAsync(transaction);
                await _db.SaveChangesAsync();
            }
        }
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is Transaction))
        {
            // 確かめた直後に、別のリクエストが取引そのものを消すと、更新が0行になる。そのときは 404 にする
            throw CreateTransactionNotFoundException();
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            // 確かめた直後に、別のリクエストがその分類を消すと、外部キーが更新を断る。
            // そのときも 500 ではなく 422 にする
            throw CreateCategoryNotFoundException();
        }
    }

    /// <summary>
    /// 自分の取引を完全に削除する。無い・他人の取引なら404
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var transaction = await FindOwnedTransactionAsync(id);
        if (transaction is null)
        {
            throw CreateTransactionNotFoundException();
        }

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync();
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 追加する取引を保存する。分類が直前に消されていたら、未分類にして保存し直す
    /// </summary>
    private async Task SaveNewTransactionAsync(Transaction transaction)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            // 判定の直後に行き先の分類が消されると、外部キーに断られる。
            // そのルールも Cascade で一緒に消えているので、当たるルールが無いときと同じく未分類で保存する。
            // 呼ぶ側は分類を送っていないので、エラーにすると支払いの記録だけが失われるため
            transaction.CategoryId = null;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 今のユーザーの取引を、冪等キーで探す。無ければ null
    /// </summary>
    private async Task<Transaction?> FindByIdempotencyKeyAsync(string idempotencyKey)
    {
        return await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.Id && t.IdempotencyKey == idempotencyKey);
    }

    /// <summary>
    /// 送り直されたリクエストに、前に登録した取引を返す
    /// </summary>
    /// <remarks>
    /// 金額・店名・入力元が前と違えば、キーの使い回しとして 422 にする。
    /// 別の支払いを、前の支払いの送り直しとして黙って捨てないため。
    /// 日付と種類は省略すると今日・支出になり、日をまたいだ送り直しで変わりうるので比べない
    /// </remarks>
    private static TransactionResponse ToRetryResponse(Transaction existing, CreateTransactionRequest request)
    {
        var isSameRequest =
            existing.Amount == request.Amount &&
            existing.Merchant == request.Merchant &&
            existing.Source == request.Source;

        if (!isSameRequest)
        {
            throw new AppException
            (
                StatusCodes.Status422UnprocessableEntity,
                ErrorCodes.Transactions.IdempotencyKeyReused,
                "この冪等キーは、内容の違う取引の登録にもう使われています。"
            );
        }

        return ToResponse(existing);
    }

    /// <summary>
    /// 取引を、返す形にする
    /// </summary>
    private static TransactionResponse ToResponse(Transaction transaction)
    {
        return new TransactionResponse
        {
            CategoryId = transaction.CategoryId,
            Id = transaction.Id,
            Amount = transaction.Amount,
            Merchant = transaction.Merchant,
            Date = transaction.Date,
            Source = transaction.Source,
            Type = transaction.Type
        };
    }

    /// <summary>
    /// 更新後の店名と分類から、ルールの追加・更新を準備する
    /// </summary>
    /// <remarks>
    /// 保存は呼び出し元で、取引の更新とまとめて行う
    /// </remarks>
    /// <returns>追加するルール、または分類を書き換えた既存のルール</returns>
    private async Task<CategoryRule> PrepareLearnedRuleAsync(Transaction transaction)
    {
        // ルールには分類が必須なので、値があることをここでも確認する
        if (!transaction.CategoryId.HasValue)
        {
            throw new InvalidOperationException("ルール作成には分類IDが必要です。");
        }

        // 自分のルールから、店名とキーワードが同じものを探す
        var rule = await _db.CategoryRules.FirstOrDefaultAsync
        (
            r => r.UserId == _currentUser.Id &&
            r.Keyword == transaction.Merchant
        );

        if (rule is null)
        {
            var newRule = new CategoryRule
            {
                // 更新後の店名と分類で、自分のルールを作る
                UserId = _currentUser.Id,
                Keyword = transaction.Merchant,
                CategoryId = transaction.CategoryId.Value
            };

            // 追加するルールを用意する。保存は呼び出し元で取引と一緒に行う
            _db.CategoryRules.Add(newRule);
            return newRule;
        }

        // 同じキーワードがあれば、増やさずに分類を変える
        rule.CategoryId = transaction.CategoryId.Value;
        return rule;
    }

    /// <summary>
    /// 保存の失敗が、学習のルールと別のリクエストとの衝突によるものか
    /// </summary>
    /// <remarks>
    /// 同じ店名のルールが先に登録された（一意違反）か、書き換えようとしたルールが消されていた（更新が0行）場合
    /// </remarks>
    private static bool IsLearnedRuleConflict(DbUpdateException ex)
    {
        if (ex.IsUniqueViolation())
        {
            return true;
        }

        return ex is DbUpdateConcurrencyException &&
            ex.Entries.All(e => e.Entity is CategoryRule);
    }

    /// <summary>
    /// 指定された分類・開始日・終了日で絞り込む。null の項目では絞り込まない
    /// </summary>
    private static IQueryable<Transaction> ApplyFilters
    (
        IQueryable<Transaction> query,
        int? categoryId,
        DateOnly? from,
        DateOnly? to
    )
    {
        if (from.HasValue)
        {
            query = query.Where(t => t.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.Date <= to.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        return query;
    }

    /// <summary>
    /// 今のユーザーの取引を ID で探す。無い・他人の取引なら null
    /// </summary>
    private async Task<Transaction?> FindOwnedTransactionAsync(int id)
    {
        return await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUser.Id);
    }

    /// <summary>
    /// 指定IDの分類が、今のユーザーの分類として存在するか
    /// </summary>
    private async Task<bool> IsOwnedCategoryAsync(int categoryId)
    {
        return await _db.Categories
            .AnyAsync(c => c.Id == categoryId && c.UserId == _currentUser.Id);
    }

    /// <summary>
    /// リクエストの内容で取引を書き換える（入力元は変えない）
    /// </summary>
    private static void ApplyUpdate(Transaction transaction, UpdateTransactionRequest request)
    {
        transaction.Amount = request.Amount!.Value;
        transaction.Merchant = request.Merchant;
        transaction.Date = request.Date!.Value;
        transaction.Type = request.Type!.Value;
        transaction.CategoryId = request.CategoryId;
    }

    /// <summary>
    /// 取引が無い・他人のもの（404）
    /// </summary>
    private static AppException CreateTransactionNotFoundException()
    {
        return new AppException
        (
            StatusCodes.Status404NotFound,
            ErrorCodes.Transactions.NotFound,
            "取引が見つかりません。"
        );
    }

    /// <summary>
    /// 指定された分類が無い（422）
    /// </summary>
    private static AppException CreateCategoryNotFoundException()
    {
        return new AppException
        (
            StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.Categories.NotFound,
            "分類が見つかりません。"
        );
    }

    public TransactionService(AppDbContext db, CurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    #endregion
}
