using ShopListApi.Transactions;

namespace ShopListApi.Summaries;

/// <summary>
/// 集計の結果を返すときの形
/// </summary>
/// <remarks>
/// 合計が int の範囲を超えてもよいよう、金額には long を使う
/// </remarks>
public class SummaryResponse
{
    /// <summary>
    /// 実際に集計した期間の開始日
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// 実際に集計した期間の終了日
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// 支出の合計
    /// </summary>
    public long TotalExpense { get; set; }

    /// <summary>
    /// 収入の合計
    /// </summary>
    public long TotalIncome { get; set; }

    /// <summary>
    /// 収入から支出を引いた残り。マイナスになることもある
    /// </summary>
    public long Balance { get; set; }

    /// <summary>
    /// 分類と支出・収入の種類ごとの合計
    /// </summary>
    public List<CategorySummaryResponse> ByCategory { get; set; } = [];
}

/// <summary>
/// 分類と支出・収入の種類が同じ取引をまとめた結果
/// </summary>
public class CategorySummaryResponse
{
    /// <summary>
    /// 分類の ID。null は未分類
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// 支出・収入の種類
    /// </summary>
    public TransactionType Type { get; set; }

    /// <summary>
    /// この分類・種類に当てはまる取引の金額の合計
    /// </summary>
    public long Total { get; set; }
}
