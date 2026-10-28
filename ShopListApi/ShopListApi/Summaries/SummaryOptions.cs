using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Summaries;

/// <summary>
/// 集計で使う月数の設定
/// </summary>
/// <remarks>
/// appsettings.json の Summary セクションに対応する
/// </remarks>
public class SummaryOptions
{
    /// <summary>
    /// 集計条件を省略したときに使う月数。今月も含める
    /// </summary>
    [Range(1, 120)]
    public int DefaultMonthCount { get; set; } = 3;
}
