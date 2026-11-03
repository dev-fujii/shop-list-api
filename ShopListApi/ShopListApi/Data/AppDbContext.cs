using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShopListApi.Accounts;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Transactions;

namespace ShopListApi.Data;

/// <summary>
/// アプリ全体のデータベース
/// </summary>
/// <remarks>
/// 親クラスを IdentityDbContext にして、アカウントの表も一緒に持つ。分類・取引・分類ルールの表はそのまま。
/// アカウントは AppUser、役職は IdentityRole、ID はどちらも Guid。
/// 役職は今は使わないが、表だけ用意しておく
/// </remarks>
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// 分類の表
    /// 分類の一覧の取得・追加・削除は、ここを通して行う
    /// </summary>
    public DbSet<Category> Categories { get; set; } = null!;

    /// <summary>
    /// 取引の表
    /// 取引の一覧の取得・追加・削除は、ここを通して行う
    /// </summary>
    /// <remarks>
    /// 表の名前（Transactions）は、このプロパティの名前から決まる。
    /// </remarks>
    public DbSet<Transaction> Transactions { get; set; } = null!;

    /// <summary>
    /// 分類ルールの表
    /// </summary>
    public DbSet<CategoryRule> CategoryRules { get; set; } = null!;

    #region // --- 内部処理 --- //

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// DBに保存するデータのルール作成
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Data/Configurations の設定クラスを全部読み込む
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    #endregion
}
