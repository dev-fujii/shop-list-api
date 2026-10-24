using ShopListApi.Data;
using ShopListApi.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ShopListApi.Common;

/// <summary>
/// 一覧データとページ情報をまとめたレスポンス
/// </summary>
/// <typeparam name="T">一覧に入れるデータの型</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// 結果を入れる
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// 現在のページ数
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// ページのサイズ
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// すべての数
    /// </summary>
    public int TotalCount { get; set; }



    #region // --- 内部処理 --- //

    /// <summary>
    /// 一覧データとページ情報を設定する
    /// </summary>
    public PagedResponse(List<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    #endregion
}