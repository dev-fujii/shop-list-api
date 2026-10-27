using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopListApi.CategoryRules;

namespace ShopListApi.Tests;

/// <summary>
/// 店名と分類ルールから分類IDを決める処理のテスト。
/// DBを使わず、キーワードの一致条件と複数一致した場合の優先順位を確認する。
/// </summary>
[TestClass]
public class CategoryRuleMatcherTests
{
    /// <summary>
    /// 複数のキーワードが店名に含まれる場合、後から作ったルールでも
    /// 長いキーワードの分類IDが返ることを確認する。
    /// </summary>
    [TestMethod]
    public void LongerKeywordWinsEvenWhenItWasCreatedLater()
    {
        CategoryRule[] rules =
        [
            new() { Id = 1, Keyword = "スーパー", CategoryId = 10 },
            new() { Id = 2, Keyword = "駅前スーパー", CategoryId = 20 }
        ];

        var actual = CategoryRuleMatcher.FindCategoryId("駅前スーパー本店", rules);

        // 後から作ったルールでも、長いキーワードの分類IDが返る。
        Assert.AreEqual((int?)rules[1].CategoryId, actual);
    }

    /// <summary>
    /// キーワードが同じ長さの場合、渡したルールの並び順にかかわらず
    /// IDが小さいルールの分類IDが返ることを確認する。
    /// </summary>
    [TestMethod]
    public void EarlierIdWinsWhenKeywordLengthsAreEqualRegardlessOfInputOrder()
    {
        CategoryRule[] rules =
        [
            new() { Id = 8, Keyword = "AB", CategoryId = 80 },
            new() { Id = 3, Keyword = "CD", CategoryId = 30 }
        ];

        Assert.AreEqual((int?)30, CategoryRuleMatcher.FindCategoryId("ABCD店", rules));
    }

    /// <summary>
    /// 店名に一致するルールが無い場合と、ルールが空の場合は null が返ることを確認する。
    /// </summary>
    [TestMethod]
    public void NoMatchAndEmptyRulesReturnNull()
    {
        CategoryRule[] rules = [new() { Id = 1, Keyword = "スーパー", CategoryId = 10 }];
        Assert.IsNull(CategoryRuleMatcher.FindCategoryId("書店", rules));
        Assert.IsNull(CategoryRuleMatcher.FindCategoryId("書店", []));
    }

    /// <summary>
    /// 店名にキーワードがそのまま含まれる場合に一致し、
    /// 大小文字が違う場合は一致しないことを確認する。
    /// </summary>
    [TestMethod]
    public void KeywordMatchingUsesExactCharacters()
    {
        CategoryRule[] rules = [new() { Id = 1, Keyword = "ABC", CategoryId = 10 }];
        Assert.AreEqual((int?)10, CategoryRuleMatcher.FindCategoryId("駅前ABC本店", rules));
        Assert.IsNull(CategoryRuleMatcher.FindCategoryId("abc", rules));
    }
}
