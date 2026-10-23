using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Transactions;

/// <summary>
/// 取引を登録するときに受け取るもの
/// </summary>
/// <remarks>
/// 分類（categoryId）は受け取らない。サーバーが分類ルールで決める。
/// 必須の項目と省略できる項目は、型に ? を付けて「送られなかった」を null で見分ける。
/// </remarks>
public class CreateTransactionRequest
{
    /// <summary>
    /// 金額: 必須。1円以上・1,000万円未満
    /// </summary>
    [Required]
    [Range(Transaction.AmountMin, Transaction.AmountMax)]
    public int? Amount { get; set; }

    /// <summary>
    /// 店名: 必須。50文字まで
    /// </summary>
    [Required]
    [MaxLength(Transaction.MerchantMaxLength)]
    public string Merchant { get; set; } = string.Empty;

    /// <summary>
    /// 日付: 省略できる。省略したら日本時間の今日
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 種類: 省略できる。省略したら支出
    /// </summary>
    public TransactionType? Type { get; set; }

    /// <summary>
    /// 入力元: 必須
    /// </summary>
    [Required]
    public TransactionSource? Source { get; set; }
}
