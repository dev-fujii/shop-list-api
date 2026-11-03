using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Accounts;

/// <summary>
/// アカウントを作るときに受け取るもの
/// </summary>
public class CreateAccountRequest
{
    /// <summary>
    /// ユーザー名
    /// </summary>
    [Required]
    [MaxLength(AppUser.UserNameMaxLength)]
    [RegularExpression(AppUser.UserNamePattern)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// パスワード
    /// </summary>
    [Required]
    [MinLength(AppUser.PasswordMinLength)]
    public string Password { get; set; } = string.Empty;
}
