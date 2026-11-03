using Microsoft.AspNetCore.Identity;

namespace ShopListApi.Accounts;

/// <summary>
/// ログインする人のアカウント
/// </summary>
/// <remarks>
/// ユーザー名・パスワードのハッシュ・ロックアウトの状態などは、親クラスの IdentityUser が持っている。
/// 自分で足す項目は今は無い。表は AspNetUsers になる。
/// IdentityUser のままだと ID が string になる。
/// 取引や分類の UserId と同じ Guid にする。
/// ID は保存するときに EF Core が作るので、自分では入れない
/// </remarks>
public class AppUser : IdentityUser<Guid>
{
    /// <summary>
    /// ユーザー名の最大文字数
    /// </summary>
    /// <remarks>
    /// Identity が作る UserName の列の長さと同じ
    /// </remarks>
    public const int UserNameMaxLength = 256;

    /// <summary>
    /// ユーザー名に使える文字
    /// </summary>
    /// <remarks>
    /// Identity の既定と同じ。英数字と - . _ @ +
    /// </remarks>
    public const string UserNamePattern = "^[a-zA-Z0-9._@+-]+$";

    /// <summary>
    /// パスワードの最小文字数
    /// </summary>
    /// <remarks>
    /// 入力チェックと Identity の設定の両方で使う
    /// </remarks>
    public const int PasswordMinLength = 15;
}
