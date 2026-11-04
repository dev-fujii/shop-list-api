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
        // 持ち主: 必須
        builder.Property(t => t.UserId)
            .IsRequired();

        // 金額: 必須
        builder.Property(t => t.Amount)
            .IsRequired();

        // 店名: 50文字まで・必須
        builder.Property(t => t.Merchant)
            .HasMaxLength(Transaction.MerchantMaxLength)
            .IsRequired();

        // 日付: 必須
        builder.Property(t => t.Date)
            .IsRequired();

        // 種類・入力元: 必須。DB には enum の番号（区分コード）で入れる
        // 番号は enum の側（TransactionType・TransactionSource）で固定してある
        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.Source)
            .IsRequired();

        // 冪等キー: 64文字まで・省略できる
        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(Transaction.IdempotencyKeyMaxLength);

        // 同じユーザーの中で、同じ冪等キーは1つだけ。
        // 同じキーのリクエストが同時に来ても、DB が2件目の登録を断る。
        // PostgreSQL の一意インデックスは null どうしを別の値として扱うので、キーの無い取引はいくつでも入る
        builder.HasIndex(t => new { t.UserId, t.IdempotencyKey })
            .IsUnique();

        // 分類への外部キー: 取引に使われている分類は消せない
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
