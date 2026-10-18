using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Categories;

/// <summary>
/// 分類を更新するときの形
/// </summary>
public class UpdateCategoryRequest
{
    /// <summary>
    /// 分類の名前
    /// </summary>
    [Required]
    [MaxLength(Category.NameMaxLength)]
    public string Name { get; set; } = string.Empty;
}
