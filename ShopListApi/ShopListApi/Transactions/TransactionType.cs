namespace ShopListApi.Transactions;


/// <summary>
/// 取引の種類（支出・収入）
/// </summary>
/// <remarks>
/// DB には番号で入る（区分コード。一覧は docs/設計.md）。
/// 番号はあとから変えない。値を足すときは、新しい番号を付ける。
/// </remarks>
public enum TransactionType
{
    /// <summary>
    /// 支出
    /// </summary>
    Expense = 0,

    /// <summary>
    /// 収入
    /// </summary>
    Income = 1
}
