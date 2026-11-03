using Microsoft.AspNetCore.Identity;

namespace ShopListApi.Accounts;

/// <summary>
/// ログインする人のアカウント
/// </summary>
/// <remarks>
/// ユーザー名・パスワードのハッシュ・ロックアウトの状態などは、親クラスの IdentityUser が持っている。
/// 自分で足す項目は今は無い。表は AspNetUsers になる。
/// IdentityUser のままだと ID が string になるので、取引や分類の UserId と同じ Guid にする。
/// ID は保存するときに EF Core が作るので、自分では入れない
/// </remarks>
public class AppUser : IdentityUser<Guid>
{
}
