namespace ShopListApi.Accounts;

/// <summary>
/// アカウントを返すときの形
/// </summary>
/// <remarks>
/// ユーザーの内部 ID は返さない
/// </remarks>
public class AccountResponse
{
    /// <summary>
    /// ユーザー名
    /// </summary>
    public string UserName { get; set; } = string.Empty;
}
