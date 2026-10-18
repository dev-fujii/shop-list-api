namespace ShopListApi.Common;

/// <summary>
/// API のエラーを伝えるための例外
/// </summary>
public class AppException : Exception
{
    /// <summary>
    /// HTTP のステータスコード
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// エラーコード（ErrorCodes の定数）
    /// </summary>
    public string Code { get; }

    /// <param name="statusCode">HTTP のステータスコード</param>
    /// <param name="code">エラーコード（ErrorCodes の定数）</param>
    /// <param name="title">人が読む説明（Problem Details の title になる）</param>
    public AppException(int statusCode, string code, string title) : base(title)
    {
        StatusCode = statusCode;
        Code = code;
    }
}