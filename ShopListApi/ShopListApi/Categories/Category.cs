namespace ShopListApi.Categories;

/// <summary>
/// DBに保存するデータ
/// 分類を表すクラス
/// </summary>
public class Category
{
    /// <summary>
    /// 名前の最大文字数
    /// </summary>
    /// <remarks>
    /// constなのでDBに保存されない。
    /// </remarks>
    public const int NameMaxLength = 20;

    /// <summary>
    /// 分類
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名前
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// ユーザーID
    /// </summary>
    public Guid UserId { get; set; }
}
