using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShopListApi.Accounts;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ShopListApi.Tokens;

/// <summary>
/// アクセストークン（JWT）を作る
/// </summary>
/// <remarks>
/// RSA の秘密鍵で署名する。確かめる側は公開鍵だけで確かめられる。
/// 公式が、署名には必ず非対称の鍵を使うように勧めているため。
/// 鍵の読み込みは1回でよいので、アプリ全体で1つだけ作る
/// </remarks>
public class AccessTokenIssuer
{
    /// <summary>
    /// トークンの期限（秒）。返すときに一緒に伝える
    /// </summary>
    public int LifetimeSeconds => _options.LifetimeMinutes * 60;


    /// <summary>
    /// 署名を確かめるための公開鍵。
    /// JWT を確かめる設定で使う
    /// </summary>
    public RsaSecurityKey ValidationKey { get; }

    /// <summary>
    /// このユーザーのアクセストークンを作る
    /// </summary>
    /// <remarks>
    /// 中身は最小限。JWT は誰でも中身を読めるので、要らないものは入れない
    /// </remarks>
    public string Create(AppUser user)
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            // iss・aud: だれが出した、だれ向けのトークンか
            Issuer = _options.Issuer,
            Audience = _options.Audience,

            // sub: ユーザーの ID。
            // jti: このトークンの番号
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),

            // iat: 出した時刻。exp: 期限
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.LifetimeMinutes),

            SigningCredentials = _signingCredentials
        };

        return _handler.CreateToken(descriptor);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 設定から秘密鍵を読み込み、署名の準備をする
    /// </summary>
    public AccessTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_options.PrivateKey), out _);
        _signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        // 確かめる側には公開鍵だけを渡す。秘密鍵の部分は入れない
        ValidationKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));
    }


    private readonly JwtOptions _options;

    /// <summary>
    /// RS256 で署名するための鍵と方式
    /// </summary>
    private readonly SigningCredentials _signingCredentials;

    private readonly JsonWebTokenHandler _handler = new();

    #endregion
}
