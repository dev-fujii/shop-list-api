using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopListApi.Categories;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// 初期分類の投入と再実行のテスト。
/// 実際のPostgreSQLに保存し、別の接続から読み直して確認する。
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class CategoryInitializerTests : PostgresTestBase
{
    /// <summary>
    /// 空のDBには6件追加し、別の接続から再実行しても重複しないことを確認する。
    /// </summary>
    [TestMethod]
    public async Task InitializationAddsSixCategoriesAndReexecutionAddsNone()
    {
        // 1回目は、初期分類の6件すべてを追加する
        await using (var db = CreateDbContext())
        {
            Assert.AreEqual(6, await new CategoryInitializer(db, User).InitializeAsync());
        }

        // 2回目は別の接続から実行する。DBにある分類を見て、何も追加しない
        await using (var db = CreateDbContext())
        {
            Assert.AreEqual(0, await new CategoryInitializer(db, User).InitializeAsync());
        }

        // DBには6件だけがあり、すべて今のユーザーのもの
        await using var verification = CreateDbContext();
        var categories = await verification.Categories.ToListAsync();
        Assert.HasCount(6, categories);
        Assert.IsTrue(categories.All(c => c.UserId == User.Id));
        CollectionAssert.AreEquivalent(DefaultNames, categories.Select(c => c.Name).ToArray());
    }

    /// <summary>
    /// 自分の既存分類は再利用し、他人が持つ同じ名前の分類とは別に不足分を追加することを確認する。
    /// </summary>
    [TestMethod]
    public async Task InitializationAddsOnlyMissingOwnCategoriesAndKeepsExistingIds()
    {
        // 自分は「食費」と独自の分類を持ち、他人は初期分類の6件をすべて持っている
        var existingFood = new Category { Name = "食費", UserId = User.Id };
        var custom = new Category { Name = "自分の分類", UserId = User.Id };
        await using (var db = CreateDbContext())
        {
            db.Categories.AddRange(existingFood, custom);
            db.Categories.AddRange(DefaultNames.Select(name => new Category { Name = name, UserId = OtherUserId }));
            await db.SaveChangesAsync();
        }

        // 他人の分類は数えず、自分に足りない5件だけを追加する
        await using (var db = CreateDbContext())
        {
            Assert.AreEqual(5, await new CategoryInitializer(db, User).InitializeAsync());
        }

        // 既存の分類はIDが変わらず、他人の6件もそのまま残る
        await using var verification = CreateDbContext();
        var ownCategories = await verification.Categories.Where(c => c.UserId == User.Id).ToListAsync();
        Assert.HasCount(7, ownCategories);
        Assert.AreEqual(existingFood.Id, ownCategories.Single(c => c.Name == "食費").Id);
        Assert.AreEqual(custom.Id, ownCategories.Single(c => c.Name == "自分の分類").Id);
        CollectionAssert.AreEquivalent(DefaultNames.Append("自分の分類").ToArray(), ownCategories.Select(c => c.Name).ToArray());
        Assert.AreEqual(6, await verification.Categories.CountAsync(c => c.UserId == OtherUserId));
    }

    /// <summary>
    /// 初期分類の名前（CategoryInitializer と同じ6件）
    /// </summary>
    private static readonly string[] DefaultNames = ["食費", "日用品", "交通費", "趣味", "固定費", "給料"];
}
