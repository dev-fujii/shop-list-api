namespace ShopListApi.CategoryRules;

/// <summary>
/// 店名に含まれるキーワードと、自動で付ける分類を対応付けるルール。
/// ユーザーごとに登録し、取引の作成時に店名から分類を決めるために使う。
/// </summary>
public class CategoryRule
{
    /// <summary>
    /// 分類ルールのID。
    /// </summary>
    /// <remarks>
    /// 同じ長さのキーワードが複数一致した場合は、
    /// IDが小さいルールを優先する。
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// ルールを登録したユーザーのID。
    /// </summary>
    /// <remarks>
    /// 自分のルールだけを取得・適用するために使う。
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// 店名に含まれているかを調べる文字列。
    /// </summary>
    /// <remarks>
    /// 例えば「セブン」は「セブンイレブン新宿店」に一致する。
    /// </remarks>
    public required string Keyword { get; set; }

    /// <summary>
    /// キーワードが一致した取引に、自動で付ける分類のID。
    /// </summary>
    /// <remarks>
    /// ルールには行き先の分類が必須のため、null は許可しない。
    /// </remarks>
    public int CategoryId { get; set; }
}
