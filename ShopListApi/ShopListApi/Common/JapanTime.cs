namespace ShopListApi.Common;

/// <summary>
/// 日本時間の日付
/// 取引の「省略したら今日」と、集計の既定の期間で同じ今日を使うため、1か所にまとめる
/// </summary>
public static class JapanTime
{
    /// <summary>
    /// 日本時間の今日
    /// </summary>
    /// <remarks>
    /// サーバーの時計（Azure では UTC のことが多い）に頼らず、UTC の今を日本時間に変えてから日付を取る。
    /// </remarks>
    public static DateOnly Today()
    {
        var japanNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JapanTimeZone);
        return DateOnly.FromDateTime(japanNow);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 日本のタイムゾーン。
    /// "Asia/Tokyo" は Windows でも Linux（Docker・Azure）でも使える名前
    /// </summary>
    private static readonly TimeZoneInfo JapanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    #endregion
}
