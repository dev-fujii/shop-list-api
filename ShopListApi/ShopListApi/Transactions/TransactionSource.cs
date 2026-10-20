namespace ShopListApi.Transactions;

/// <summary>
/// 取引の入力元（どこから入ってきたか）
/// </summary>
public enum TransactionSource
{
    /// <summary>
    /// 自動（Apple Pay のショートカット）
    /// </summary>
    Auto,

    /// <summary>
    /// 手入力
    /// </summary>
    Manual,
}
