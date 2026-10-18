namespace ShopListApi.Categories;

/// <summary>
/// 分類を返すときの形
/// </summary>
public class CategoryResponse
{
    /// <summary>
    /// 分類の ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 分類の名前
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
