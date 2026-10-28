using System.ComponentModel.DataAnnotations;
using ShopListApi.Transactions;

namespace ShopListApi.CategoryRules;

/// <summary>
/// 分類ルールを更新するときの形
/// </summary>
public class UpdateCategoryRuleRequest
{
    /// <summary>
    /// 自動分類で店名と比べるキーワード。必須で、登録時と同じ最大文字数
    /// </summary>
    [Required]
    [MaxLength(Transaction.MerchantMaxLength)]
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// 変更後の分類の ID。必須
    /// </summary>
    /// <remarks>
    /// 未指定を null として受け取り、Required で入力漏れを検出するため int? にする
    /// </remarks>
    [Required]
    public int? CategoryId { get; set; }
}
