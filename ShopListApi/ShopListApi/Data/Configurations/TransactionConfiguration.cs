using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopListApi.Categories;
using ShopListApi.Transactions;

namespace ShopListApi.Data.Configurations;


/// <summary>
/// 取引の表（Transactions）の決まり
/// </summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        // TODO:
        //一旦、ハードコーディング明日直す
        builder.Property(c => c.ShopName)
            .HasMaxLength(50)
            .IsRequired();

        // 分類への外部キー: 取引に使われている分類は消せない
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
