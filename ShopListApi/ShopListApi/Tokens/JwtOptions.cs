using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace ShopListApi.Tokens;

/// <summary>
/// JWT の設定
/// </summary>
/// <remarks>
/// 設定の Jwt セクションに対応する。環境変数では Jwt__PrivateKey など。
/// 秘密鍵だけは秘密なので appsettings.json には書かない。
/// ユーザーシークレット・Azure のシークレットに置く
/// </remarks>
public class JwtOptions
{
    /// <summary>
    /// 発行者。トークンの iss に入れ、確かめるときも同じ値と比べる
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 対象者。トークンの aud に入れ、確かめるときも同じ値と比べる
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// トークンの期限（分）
    /// </summary>
    /// <remarks>
    /// 設定が無いと 0 のままになり、Range で起動時の検証で止まる
    /// </remarks>
    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; }

    /// <summary>
    /// 署名に使う RSA の秘密鍵。PKCS#8 の形を Base64 にした1行の文字列
    /// </summary>
    [Required]
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// 秘密鍵の最小の長さ（ビット）
    /// </summary>
    public const int MinKeySizeInBits = 2048;

    /// <summary>
    /// 秘密鍵を、RS256 の署名に使える RSA の鍵として読めるか確かめる
    /// </summary>
    /// <remarks>
    /// 起動時の検証で使う。[Required] は空でないかしか見ないので、
    /// 形が違う鍵を入れても起動でき、最初のログインで初めて 500 になってしまう。
    /// ここで読んでみて、読めない・短すぎるなら起動の時点で止める
    /// </remarks>
    public static bool IsUsablePrivateKey(string privateKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
            return rsa.KeySize >= MinKeySizeInBits;
        }
        // Base64 として読めない
        catch (FormatException)
        {
            return false;
        }
        // PKCS#8 の RSA の鍵として読めない
        catch (CryptographicException)
        {
            return false;
        }
    }
}
