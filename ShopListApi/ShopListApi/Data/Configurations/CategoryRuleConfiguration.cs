using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Transactions;

namespace ShopListApi.Data.Configurations;

/// <summary>
/// 分類ルールの表の決まり
/// </summary>
public class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> builder)
    {
        builder.Property(r => r.UserId)
            .IsRequired();

        // 店名をそのままキーワードにできるよう、文字数の上限をそろえる。
        builder.Property(r => r.Keyword)
            .HasMaxLength(Transaction.MerchantMaxLength)
            .IsRequired();

        builder.Property(r => r.CategoryId)
            .IsRequired();

        // 同じユーザーは同じキーワードを複数登録できない。
        builder.HasIndex(r => new { r.UserId, r.Keyword })
            .IsUnique();

        // 分類を削除したら、その分類を指すルールもDB側で一緒に削除する。
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
