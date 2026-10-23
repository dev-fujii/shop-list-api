namespace ShopListApi.Transactions;

/// <summary>
/// 取引
/// </summary>
public class Transaction
{
    /// <summary>
    /// 店名の最大文字数
    /// </summary>
    /// <remarks>
    /// constなのでDBに保存されない。
    /// 分類ルールのキーワードも、この上限にそろえる（店名をそのままキーワードにするため）。
    /// </remarks>
    public const int MerchantMaxLength = 50;

    /// <summary>
    /// 金額の最小値（1円）
    /// </summary>
    public const int AmountMin = 1;

    /// <summary>
    /// 金額の最大値（1,000万円未満なので 9,999,999円）
    /// </summary>
    public const int AmountMax = 9_999_999;

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
