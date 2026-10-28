using System.Globalization;

namespace ShopListApi.Summaries;

/// <summary>
/// 集計する期間。開始日と終了日も含む
/// </summary>
/// <param name="From">集計の開始日</param>
/// <param name="To">集計の終了日</param>
public readonly record struct SummaryPeriod(DateOnly From, DateOnly To)
{
    /// <summary>
    /// 月・開始日と終了日・既定の月数から、集計する期間を決める
    /// </summary>
    /// <remarks>
    /// 条件の組み合わせは検証済みとし、today には日本時間の今日を渡す
    /// </remarks>
    public static SummaryPeriod Resolve
    (
        SummaryQuery query,
        DateOnly today,
        int defaultMonthCount
    )
    {
        if (query.Month is not null)
        {
            var firstDay = DateOnly.ParseExact
            (
                query.Month + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );

            // 月の日数から末日を求める。うるう年の2月も、この日数に従う
            DateOnly lastDay = new DateOnly
            (
                firstDay.Year,
                firstDay.Month,
                DateTime.DaysInMonth(firstDay.Year, firstDay.Month)
            );
            return new SummaryPeriod(firstDay, lastDay);
        }

        if (query.From.HasValue && query.To.HasValue)
        {
            return new SummaryPeriod(query.From.Value, query.To.Value);
        }

        var currentMonth = new DateOnly(today.Year, today.Month, 1);

        // 今月も1か月に数えるので、戻す月数は既定の月数より1少なくする
        // 例: 今月が1月で3か月分なら、開始日は前年11月1日
        DateOnly from = currentMonth.AddMonths(-defaultMonthCount + 1);
        var to = new DateOnly
        (
            today.Year,
            today.Month,
            DateTime.DaysInMonth(today.Year, today.Month)
        );
        return new SummaryPeriod(from, to);
    }
}
