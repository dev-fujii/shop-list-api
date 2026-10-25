using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ShopListApi.Common;

/// <summary>
/// 仮の鍵（API キー）を確かめるミドルウェア。
/// X-Api-Key ヘッダーが無い・設定の鍵と違うときは 401 を返し、先に進ませない
/// </summary>
/// <remarks>
/// ログインを作るまでのつなぎ。Program.cs で UseStatusCodePages より後ろに並べ、
/// 本文のない 401 を Problem Details の形にしてもらう
/// </remarks>
public class ApiKeyMiddleware
{
    /// <summary>
    /// リクエストごとに呼ばれる。鍵が合っていれば次のミドルウェアへ渡す
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // ヘッダーが無い、または鍵が違うなら 401 で止める
        if
        (
            !context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !IsSameKey(providedKey.ToString())
        )
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 鍵が合っていれば、次のミドルウェアへ渡す
        await _next(context);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 初期化時に鍵を受け取る
    /// </summary>
    /// <remarks>
    /// 比べるときは byte の並びが要るので、正しい鍵はここで1回だけ変換して持っておく
    /// </remarks>
    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        _next = next;
        _apiKeyBytes = Encoding.UTF8.GetBytes(options.Value.Key);
    }

    /// <summary>
    /// 送られた鍵が、正しい鍵と同じかを確かめる
    /// </summary>
    /// <remarks>
    /// != は違う文字が見つかった所で止まるので、答えが返るまでの時間から鍵を1文字ずつ当てられる。
    /// FixedTimeEquals は中身によらず同じ時間で比べる。長さが違うときだけすぐ false を返すが、
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

    private readonly RequestDelegate _next;

    /// <summary>
    /// 正しい鍵を UTF-8 の byte にしたもの
    /// </summary>
    private readonly byte[] _apiKeyBytes;

    #endregion
}
