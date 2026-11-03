using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Tokens;

/// <summary>
/// ログインするときに受け取るもの
/// </summary>
/// <remarks>
/// 長さや文字の決まりはここでは見ない。
/// 見ると、どこが違うかのヒントを返してしまうため
/// </remarks>
public class CreateTokenRequest
{
    /// <summary>
    /// ユーザー名
    /// </summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// パスワード
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
