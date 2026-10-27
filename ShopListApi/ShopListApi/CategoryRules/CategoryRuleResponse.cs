namespace ShopListApi.CategoryRules;

/// <summary>
/// 分類ルールの取得・登録APIで返すデータ。
/// </summary>
/// <remarks>
/// ユーザーIDはレスポンスに含めない。
/// </remarks>
public class CategoryRuleResponse
{
    /// <summary>
    /// 分類ルールのID。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 自動分類で店名との照合に使うキーワード。
    /// </summary>
    /// <remarks>
    /// 店名にこの文字列が含まれているかで判定する。
    /// </remarks>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// このルールで取引に自動で付ける分類のID。
    /// </summary>
    public int CategoryId { get; set; }
}
