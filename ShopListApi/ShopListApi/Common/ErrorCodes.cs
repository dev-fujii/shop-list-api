namespace ShopListApi.Common;

/// <summary>
/// エラーコード一覧
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// 分類のエラー
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// 分類が見つからない（404・422）
        /// </summary>
        public const string NotFound = "category.not_found";

        /// <summary>
        /// 同じ名前の分類がある（409）
        /// </summary>
        public const string DuplicateName = "category.duplicate_name";

        /// <summary>
        /// 取引に使われているので消せない（409）
        /// </summary>
        public const string InUse = "category.in_use";
    }

    /// <summary>
    /// 取引のエラー
    /// </summary>
    public static class Transactions
    {
        /// <summary>
        /// 取引が見つからない（404）
        /// </summary>
        public const string NotFound = "transaction.not_found";
    }

    /// <summary>
    /// 分類ルールのエラー
    /// </summary>
    public static class CategoryRules
    {
        /// <summary>
        /// 分類ルールが見つからない（404）
        /// </summary>
        public const string NotFound = "category_rule.not_found";

        /// <summary>
        /// 同じキーワードのルールがある（409）
        /// </summary>
        public const string DuplicateKeyword = "category_rule.duplicate_keyword";
    }

    /// <summary>
    /// アカウントのエラー
    /// </summary>
    public static class Accounts
    {
        /// <summary>
        /// 同じユーザー名のアカウントがある（409）
        /// </summary>
        public const string DuplicateUserName = "account.duplicate_user_name";
    }

    /// <summary>
    /// ログインのエラー
    /// </summary>
    public static class Tokens
    {
        /// <summary>
        /// ユーザー名かパスワードが違う。ロック中も同じ（401）
        /// </summary>
        public const string InvalidCredentials = "token.invalid_credentials";
    }
}