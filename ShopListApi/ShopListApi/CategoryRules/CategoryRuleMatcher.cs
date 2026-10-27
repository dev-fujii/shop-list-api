namespace ShopListApi.CategoryRules;

/// <summary>
/// 店名とルールから分類を決める。DBを使わずテストできる判定部分。
/// 呼び出す側が、今のユーザーのルールだけを渡す。
/// </summary>
public static class CategoryRuleMatcher
{
    public static int? FindCategoryId(string merchant, IEnumerable<CategoryRule> rules)
    {
        return rules
            // 店名にキーワードが含まれるルールを選ぶ。大小文字も区別する。
            .Where(r => merchant.Contains(r.Keyword, StringComparison.Ordinal))
            // 複数のルールが一致したら、より具体的な長いキーワードを優先する。
            .OrderByDescending(r => r.Keyword.Length)
            // キーワードが同じ長さなら、IDが小さいルールを優先する。
            .ThenBy(r => r.Id)
            // int? にしてから先頭を取る。一致が無い場合、0ではなく null になる。
            .Select(r => (int?)r.CategoryId)
            .FirstOrDefault();
    }
}
