using Microsoft.Extensions.Options;

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
            providedKey != _apiKey
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
    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        _next = next;
        _apiKey = options.Value.Key;
    }

    /// <summary>
    /// 鍵を入れるヘッダーの名前
    /// </summary>
    private const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    #endregion
}