namespace ShopListApi.Transactions;

/// <summary>
/// 取引の入力元（どこから入ってきたか）
/// </summary>
/// <remarks>
/// DB には番号で入る（区分コード。一覧は docs/設計.md）。
/// 番号はあとから変えない。値を足すときは、新しい番号を付ける。
/// </remarks>
public enum TransactionSource
{
    /// <summary>
    /// 自動（Apple Pay のショートカット）
    /// </summary>
    Auto = 0,

    /// <summary>
    /// 手入力
    /// </summary>
    Manual = 1,
}
