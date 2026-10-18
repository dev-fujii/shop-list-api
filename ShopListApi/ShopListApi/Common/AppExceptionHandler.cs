using Microsoft.AspNetCore.Diagnostics;

namespace ShopListApi.Common;

/// <summary>
/// AppException を受け取り、Problem Details（title・status・code）で返す
/// それ以外の例外は何もしない（ASP.NET Core が 500 を返す）
/// </summary>
public class AppExceptionHandler : IExceptionHandler
{

    /// <summary>
    /// 例外が起きたときに ASP.NET Core が呼ぶ。
    /// AppException なら Problem Details を書いて true を返す。それ以外なら false（500 に任せる）
    /// </summary>
    /// <param name="httpContext">今のリクエストとレスポンス</param>
    /// <param name="exception">投げられた例外</param>
    /// <param name="cancellationToken">途中でやめる合図（使っていない）</param>
    /// <returns>処理したら true、担当外なら false</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        //  アプリの決まりのエラー以外のASP.NET Coreに任せる例外は、何もしないで false を返す
        if (exception is not AppException appException)
        {
            return false;
        }

        // --- アプリの決まりのエラー: ステータスコードを設定し、Problem Details で返す --- //

        httpContext.Response.StatusCode = appException.StatusCode;


        // エラーの内容を渡して、IProblemDetailsService に JSON を書いてもらう。
        // 書けたら true
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = appException.Message,
                Status = appException.StatusCode,
                Extensions = { ["code"] = appException.Code }
            }
        });
    }

    public AppExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    private readonly IProblemDetailsService _problemDetailsService;
}