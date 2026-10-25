using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
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
    public async Task<TransactionResponse> CreateAsync(CreateTransactionRequest request)
    {
        var transaction = new Transaction
        {
            UserId = _currentUser.Id,
            Amount = request.Amount!.Value,
            Merchant = request.Merchant,
            Date = request.Date ?? GetTodayInJapan(),
            Source = request.Source!.Value,
            Type = request.Type ?? TransactionType.Expense
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

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
    /// 自分の取引を1件。無い・他人の取引なら 404
    /// </summary>
    public async Task<TransactionResponse> GetByIdAsync(int id)
    {
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUser.Id);

        // 無い・他人の取引なら 404
        if (transaction is null)
        {
            throw new AppException
            (
                StatusCodes.Status404NotFound,
                ErrorCodes.Transactions.NotFound,
                "取引が見つかりません。"
            );
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
    public async Task<PagedResponse<TransactionResponse>> GetAllAsync(
        int page,
        int pageSize,
        int? categoryId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = _db.Transactions.Where(t => t.UserId == _currentUser.Id);
        query = ApplyFilters(query, categoryId, from, to);

        return new PagedResponse<TransactionResponse>
        (
            await query
            
                //新しい日付順に
                .OrderByDescending(t => t.Date)

                //同じ日付ならIDの大きい順
                .ThenByDescending(t => t.Id)

                //指定ページより前の件数を飛ばす
                .Skip((page - 1) * pageSize)

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
                .ToListAsync(),
            page,
            pageSize,
            await query.CountAsync()
         );
    }

    /// <summary>
    /// 自分の取引を更新する。取引が無ければ404、指定した分類が自分のものでなければ422
    /// </summary>
    public async Task UpdateAsync(int id, UpdateTransactionRequest request)
    {
        var transaction = await FindOwnedTransactionAsync(id);
        if (transaction is null)
        {
            throw new AppException(
                StatusCodes.Status404NotFound,
                ErrorCodes.Transactions.NotFound,
                "取引が見つかりません。");
        }

        if (request.CategoryId.HasValue &&
            !await IsOwnedCategoryAsync(request.CategoryId.Value))
        {
            throw CreateCategoryNotFoundException();
        }

        ApplyUpdate(transaction, request);

        // 確かめた直後に、別のリクエストがその分類を消すと、外部キーが更新を断る。
        // そのときも 500 ではなく 422 にする
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
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
            throw new AppException(
                StatusCodes.Status404NotFound,
                ErrorCodes.Transactions.NotFound,
                "取引が見つかりません。");
        }

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync();
    }

    #region // --- 内部処理 --- //

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

    /// <summary>
    /// 日本時間の今日
    /// </summary>
    /// <remarks>
    /// サーバーの時計（Azure では UTC のことが多い）に頼らず、UTC の今を日本時間に変えてから日付を取る。
    /// </remarks>
    private static DateOnly GetTodayInJapan()
    {
        var japanNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JapanTimeZone);
        return DateOnly.FromDateTime(japanNow);
    }

    /// <summary>
    /// 日本のタイムゾーン。
    /// "Asia/Tokyo" は Windows でも Linux（Docker・Azure）でも使える名前
    /// </summary>
    private static readonly TimeZoneInfo JapanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    #endregion
}
