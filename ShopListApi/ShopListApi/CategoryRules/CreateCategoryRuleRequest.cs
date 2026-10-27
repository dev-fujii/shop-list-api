using System.ComponentModel.DataAnnotations;
using ShopListApi.Transactions;

namespace ShopListApi.CategoryRules;

/// <summary>
/// 分類ルールの登録APIで受け取る入力データ。
/// </summary>
/// <remarks>
/// 必須項目と文字数は属性で検証する。
/// 分類の存在・所有者やキーワードの重複は、CategoryRuleService で確認する。
/// </remarks>
public class CreateCategoryRuleRequest
{
    /// <summary>
    /// 自動分類で店名との照合に使うキーワード。
    /// </summary>
    /// <remarks>
    /// 入力必須で、空文字や空白だけの文字列は指定できない。
    /// 文字数の上限は <see cref="Transaction.MerchantMaxLength"/> とし、店名・DBの制約とそろえる。
    /// </remarks>
    [Required]
    [MaxLength(Transaction.MerchantMaxLength)]
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// キーワードが一致した取引に、自動で付ける分類のID。
    /// </summary>
    /// <remarks>
    /// 未指定をnullとして受け取り、Required で入力漏れを検出するため、int? 型にする。
    /// </remarks>
    [Required]
    public int? CategoryId { get; set; }
}
