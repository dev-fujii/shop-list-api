namespace ShopListApi.Transactions;

/// <summary>
/// 取引
/// </summary>
public class Transaction
{

    /// <summary>
    /// ユーザーID
    /// </summary>
    public Guid UserId { get; set; }

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
    /// 店舗名
    /// </summary>
    public required string ShopName { get; set; }

    /// <summary>
    /// 日付
    /// </summary>
    public DateOnly Date{ get; set; }

    /// <summary>
    /// 入力元
    /// </summary>
    public TransactionSource InputSource { get; set; }

    /// <summary>
    /// 種類（支出・収入）
    /// </summary>
    public TransactionType Type { get; set; }
}
