namespace ShopListApi.Tokens;

/// <summary>
/// ログインできたときに返すもの
/// </summary>
/// <remarks>
/// OAuth のトークンの返し方に近い形にする
/// </remarks>
public class TokenResponse
{
    /// <summary>
    /// アクセストークン。Authorization: Bearer のあとに付けて送る
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// トークンの種類。いつも Bearer
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 期限までの秒数
    /// </summary>
    public int ExpiresIn { get; set; }
}
