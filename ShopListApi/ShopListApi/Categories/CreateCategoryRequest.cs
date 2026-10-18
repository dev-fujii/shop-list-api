using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Categories;

/// <summary>
/// 分類を作るときに受け取るもの
/// </summary>
public class CreateCategoryRequest
{
    /// <summary>
    /// 分類の名前
    /// </summary>
    [Required]
    [MaxLength(Category.NameMaxLength)]
    public string Name { get; set; } = string.Empty;
}
