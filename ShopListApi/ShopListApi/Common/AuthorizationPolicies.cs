namespace ShopListApi.Common;

/// <summary>
/// 認可の決まりの名前の一覧
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// 鍵で来ても通してよい決まり
    /// </summary>
    /// <remarks>
    /// 何も付いていない所は、鍵で来た名札を通さない。
    /// 鍵で呼んでよい所にだけ、この決まりを付ける
    /// </remarks>
    public const string AllowApiKey = "AllowApiKey";
}
