using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.Transactions;

/// <summary>
/// 取引の処理（作成・1件）
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
    /// 指定されたページの取引取得
    /// </summary>
    public async Task<PagedResponse<TransactionResponse>> GetAllAsync(int page, int pageSize)
    {
        var query = _db.Transactions.Where(t => t.UserId == _currentUser.Id);

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

    #region // --- 内部処理 --- //

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
