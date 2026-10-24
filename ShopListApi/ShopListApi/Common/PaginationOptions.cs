namespace ShopListApi.Common;

/// <summary>
/// ページ分割で使う件数の設定
/// </summary>
/// <remarks>
/// appsettings.json の Pagination セクションに対応する
/// </remarks>
public class PaginationOptions
{
    /// <summary>
    /// pageSize を省略したときに使う、1ページあたりの件数
    /// </summary>
    public int DefaultPageSize { get; set; }

    /// <summary>
    /// pageSize に指定できる最大件数
    /// </summary>
    public int MaxPageSize { get; set; }
}
