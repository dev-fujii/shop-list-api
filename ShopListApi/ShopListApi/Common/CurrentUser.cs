namespace ShopListApi.Common;

/// <summary>
/// 今のリクエストのユーザー
/// ログインを作るまでは、決まった仮のユーザーを返す
/// </summary>
public class CurrentUser
{
    /// <summary>
    /// 今のユーザーの ID
    /// </summary>
    public Guid Id => TemporaryUserId;

    /// <summary>
    /// 仮のユーザー ID
    /// </summary>
    private static readonly Guid TemporaryUserId = new("089dd050-a88f-40bc-809f-c05dc3acfef6");
}