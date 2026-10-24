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
    /// すべての取引取得
    /// </summary>
    public async Task<List<TransactionResponse>> GetAllAsync()
    {
        var transactions = await _db.Transactions
            .Where(t => t.UserId == _currentUser.Id)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        return transactions.Select(t => new TransactionResponse
        {
            CategoryId = t.CategoryId,
            Id = t.Id,
            Amount = t.Amount,
            Merchant = t.Merchant,
            Date = t.Date,
            Source = t.Source,
            Type = t.Type
        }).ToList();
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
