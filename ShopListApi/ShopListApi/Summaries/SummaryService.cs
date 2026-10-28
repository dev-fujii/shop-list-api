using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopListApi.Common;
using ShopListApi.Data;
using ShopListApi.Transactions;

namespace ShopListApi.Summaries;

/// <summary>
/// 取引の集計の処理
/// 今のユーザーの取引だけを扱う
/// </summary>
public class SummaryService
{
    /// <summary>
    /// 月か期間を指定して、自分の取引を集計する
    /// </summary>
    /// <remarks>
    /// 条件は検証済みとし、指定が無ければ今月を含む既定の月数を使う
    /// </remarks>
    public async Task<SummaryResponse> GetAsync(SummaryQuery request)
    {
        var japanNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JapanTimeZone);
        var period = SummaryPeriod.Resolve
        (
            request,
            DateOnly.FromDateTime(japanNow),
            _options.DefaultMonthCount
        );

        var query = _db.Transactions.AsNoTracking();

        // 自分の取引だけを集計する
        query = query.Where(t => t.UserId == _currentUser.Id);
        // 開始日と終了日の取引も含める
        query = query.Where(t => t.Date >= period.From);
        query = query.Where(t => t.Date <= period.To);

        var byCategory = await query
            // 同じ分類でも支出と収入は分ける。未分類も集計する
            .GroupBy(t => new { t.CategoryId, t.Type })
            .Select(group => new CategorySummaryResponse
            {
                CategoryId = group.Key.CategoryId,
                Type = group.Key.Type,
                // 合計が int の範囲を超えてもよいよう、足す前に long にする
                Total = group.Sum(t => (long)t.Amount)
            })
            .OrderBy(row => row.CategoryId)
            .ThenBy(row => row.Type)
            .ToListAsync();

        // DB で集計した結果から全体の合計を出す。対象の行が無ければ 0
        var totalExpense = byCategory
            .Where(row => row.Type == TransactionType.Expense)
            .Sum(row => row.Total);
        var totalIncome = byCategory
            .Where(row => row.Type == TransactionType.Income)
            .Sum(row => row.Total);

        return new SummaryResponse
        {
            From = period.From,
            To = period.To,
            TotalExpense = totalExpense,
            TotalIncome = totalIncome,
            // 支出が収入より多ければ、残りはマイナスになる
            Balance = totalIncome - totalExpense,
            ByCategory = byCategory
        };
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 集計に使う DB・今のユーザー情報・月数の設定を受け取る
    /// </summary>
    public SummaryService
    (
        AppDbContext db,
        CurrentUser currentUser,
        IOptions<SummaryOptions> options
    )
    {
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    /// <summary>
    /// 今日の日付を決めるときに使う、日本のタイムゾーン
    /// </summary>
    private static readonly TimeZoneInfo JapanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;
    private readonly SummaryOptions _options;

    #endregion
}
