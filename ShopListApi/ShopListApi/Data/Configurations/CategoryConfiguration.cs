using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopListApi.Categories;

namespace ShopListApi.Data.Configurations;

/// <summary>
/// 分類の表（Categories）の決まり
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // 名前: 20文字まで・必須
        builder.Property(c => c.Name)
            .HasMaxLength(Category.NameMaxLength)
            .IsRequired();

        // 持ち主: 必須
        builder.Property(c => c.UserId)
            .IsRequired();

        // 同じ人の中で名前は重複禁止
        builder.HasIndex(c => new { c.UserId, c.Name })
            .IsUnique();
    }
}