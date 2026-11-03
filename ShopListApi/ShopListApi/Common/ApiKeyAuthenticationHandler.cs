using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace ShopListApi.Common;

/// <summary>
/// 仮の鍵で「この人は誰か」を確かめる認証ハンドラー
/// </summary>
/// <remarks>
/// 鍵の確かめを、自分で書いたミドルウェアから ASP.NET Core の認証の仕組みに移したもの。
/// ここは名札を付けるだけで、401 で止めるかは認可が決める。
/// 止めるときの 401 は、親クラスの HandleChallengeAsync が本文なしで返し、
/// UseStatusCodePages が Problem Details の形にする
/// </remarks>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// このスキームの名前
    /// 登録するときに使う
    /// </summary>
    public const string SchemeName = "ApiKey";

    /// <summary>
    /// リクエストごとに呼ばれる。鍵を確かめ、結果を返す
    /// </summary>
    /// <remarks>
    /// 待つ処理が無いので async にせず、Task.FromResult で包んで返す
    /// </remarks>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // ヘッダーが無いときは判定しない。鍵で来た人ではないだけで、失敗ではない。
        // JWT を足したとき、鍵を持たない人は JWT のほうで確かめるため
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // 鍵が違うときは失敗。この文はログに出るので、送られた鍵そのものは入れない
        if (!IsSameKey(providedKey.ToString()))
        {
            return Task.FromResult(AuthenticateResult.Fail("鍵が違います。"));
        }

        // 鍵が合ったので、鍵の持ち主として名札を作る
        var claim = new Claim(ClaimTypes.NameIdentifier, _apiKeyOptions.UserId!.Value.ToString());

        // 身分証
        var identity = new ClaimsIdentity([claim], Scheme.Name);

        // 本人
        var principal = new ClaimsPrincipal(identity);

        // 証明
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // 成功を返す     
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 初期化時に鍵の設定を受け取る
    /// </summary>
    public ApiKeyAuthenticationHandler
    (
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeyOptions> apiKeyOptions
    ) : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions.Value;
        _apiKeyBytes = Encoding.UTF8.GetBytes(_apiKeyOptions.Key);
    }

    /// <summary>
    /// 送られた鍵が、正しい鍵と同じかを確かめる
    /// </summary>
    /// <remarks>
    /// != は違う文字が見つかった所で止まるので、答えが返るまでの時間から鍵を1文字ずつ当てられる。
    /// FixedTimeEquals は中身によらず同じ時間で比べる。
    /// 長さが違うときだけすぐ false を返すが、
    /// 鍵はランダムで長いので、長さが分かっても当てられない
    /// </remarks>
    private bool IsSameKey(string providedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        return CryptographicOperations.FixedTimeEquals(providedBytes, _apiKeyBytes);
    }

    /// <summary>
    /// 鍵を入れるヘッダーの名前
    /// </summary>
    private const string HeaderName = "X-Api-Key";

    /// <summary>
    /// 鍵の設定
    /// </summary>
    private readonly ApiKeyOptions _apiKeyOptions;

    /// <summary>
    /// 正しい鍵を UTF-8 の byte にしたもの
    /// </summary>
    private readonly byte[] _apiKeyBytes;

    #endregion
}
