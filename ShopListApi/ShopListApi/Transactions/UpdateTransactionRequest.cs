using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Transactions;

/// <summary>
/// 取引の更新で受け取る項目。入力元は変更しないため含めない
/// </summary>
public class UpdateTransactionRequest
{
    /// <summary>
    /// 金額。必須で、登録時と同じ範囲
    /// </summary>
    [Required]
    [Range(Transaction.AmountMin, Transaction.AmountMax)]
    public int? Amount { get; set; }

    /// <summary>
    /// 店名。必須で、登録時と同じ最大文字数
    /// </summary>
    [Required]
    [MaxLength(Transaction.MerchantMaxLength)]
    public string Merchant { get; set; } = string.Empty;

    /// <summary>
    /// 日付。必須
    /// </summary>
    [Required]
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 支出・収入。必須
    /// </summary>
    [Required]
    public TransactionType? Type { get; set; }

    /// <summary>
    /// 変更後の分類。null または省略なら未分類にする
    /// </summary>
    public int? CategoryId { get; set; }
}
