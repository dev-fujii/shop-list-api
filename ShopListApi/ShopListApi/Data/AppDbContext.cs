using Microsoft.EntityFrameworkCore;
using ShopListApi.Categories;

namespace ShopListApi.Data;

/// <summary>
/// アプリ全体のデータベース
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 分類の表
    /// 分類の一覧の取得・追加・削除は、ここを通して行う
    /// </summary>
    public DbSet<Category> Categories { get; set; } = null!;

    #region // --- 内部処理 -- //

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
