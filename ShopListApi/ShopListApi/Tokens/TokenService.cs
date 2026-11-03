using Microsoft.AspNetCore.Identity;
using ShopListApi.Accounts;
using ShopListApi.Common;

namespace ShopListApi.Tokens;

/// <summary>
/// ログインの処理
/// </summary>
public class TokenService
{
    /// <summary>
    /// ユーザー名とパスワードを確かめ、合っていればアクセストークンを返す。
    /// 違うときは、理由によらず同じ 401
    /// </summary>
    public async Task<TokenResponse> CreateAsync(CreateTokenRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);

        // ユーザーが見つからないときも、パスワード違い・ロック中と同じ 401 にする。
        // 文を変えると、その名前の人がいるかどうかが外から分かってしまうため
        if (user is null)
        {
            throw new AppException
            (
                statusCode: 401,
                code: ErrorCodes.Tokens.InvalidCredentials,
                title: "ユーザー名またはパスワードが正しくありません"
            );
        }

        // パスワードを確かめる。lockoutOnFailure: true で、失敗を数えて5回でロックする。
        // ロック中は、パスワードが合っていても失敗になる
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        // パスワードが違う・ロック中のときも、上と同じ 401
        if (!result.Succeeded)
        {
            throw new AppException
            (
                statusCode: 401,
                code: ErrorCodes.Tokens.InvalidCredentials,
                title: "ユーザー名またはパスワードが正しくありません"
            );
        }

        return new TokenResponse
        {
            AccessToken = _accessTokenIssuer.Create(user),
            ExpiresIn = _accessTokenIssuer.LifetimeSeconds
        };
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// アカウント係、パスワードを確かめる係、トークンを作る係を受け取る
    /// </summary>
    public TokenService
    (
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        AccessTokenIssuer accessTokenIssuer
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _accessTokenIssuer = accessTokenIssuer;
    }

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly AccessTokenIssuer _accessTokenIssuer;

    #endregion
}
