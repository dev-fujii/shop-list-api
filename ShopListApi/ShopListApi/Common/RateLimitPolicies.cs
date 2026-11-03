namespace ShopListApi.Common;

/// <summary>
/// 回数制限の決まりの名前と回数の一覧
/// </summary>
/// <remarks>
/// どれも送り元の IP アドレスごとに数える。
/// 回数はアプリのメモリで数えるので、アプリが2つ以上動くとそれぞれ別に数える
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>
    /// ログイン。1分に5回まで
    /// </summary>
    /// <remarks>
    /// ロックアウトは1人のアカウントしか守れない。
    /// たくさんのアカウントに少しずつ試す攻撃を、ここで止める
    /// </remarks>
    public const string Login = "Login";

    /// <summary>
    /// ログインで許す回数
    /// </summary>
    public const int LoginPermitLimit = 5;

    /// <summary>
    /// ログインの回数を数える時間の長さ
    /// </summary>
    public static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// アカウント作成。1時間に3回まで
    /// </summary>
    /// <remarks>
    /// アカウントを大量に作られるのを止める
    /// </remarks>
    public const string CreateAccount = "CreateAccount";

    /// <summary>
    /// アカウント作成で許す回数
    /// </summary>
    public const int CreateAccountPermitLimit = 3;

    /// <summary>
    /// アカウント作成の回数を数える時間の長さ
    /// </summary>
    public static readonly TimeSpan CreateAccountWindow = TimeSpan.FromHours(1);
}
