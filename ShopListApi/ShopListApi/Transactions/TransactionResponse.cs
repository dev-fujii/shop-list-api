namespace ShopListApi.Transactions;

/// <summary>
/// 取引を登録するときに返すもの
/// </summary>
public class TransactionResponse
{
    /// <summary>
    /// 分類
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// 取引の id
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 値段
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 店名
    /// </summary>
    public required string Merchant { get; set; }

    /// <summary>
    /// 日付
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 入力元
    /// </summary>
    public TransactionSource Source { get; set; }

    /// <summary>
    /// 種類（支出・収入）
    /// </summary>
    public TransactionType Type { get; set; }
}
