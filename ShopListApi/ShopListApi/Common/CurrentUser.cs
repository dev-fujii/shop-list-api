namespace ShopListApi.Common;

/// <summary>
/// 今のリクエストのユーザー
/// </summary>
/// <remarks>
/// ID を受け取って持つだけの入れ物。名札から ID を読むのは Program.cs の DI の登録。
/// サービスは Id しか知らないので、鍵で来ても JWT で来ても、サービスは変えずに済む
/// </remarks>
public class CurrentUser
{
    /// <summary>
    /// 今のユーザーの ID
    /// </summary>
    /// <remarks>
    /// 1回のリクエストの途中でユーザーが変わると、他人のデータを触るおそれがあるので、読むだけにする
    /// </remarks>
    public Guid Id => _id;

    /// <summary>
    /// ユーザー ID を受け取る
    /// </summary>
    public CurrentUser(Guid id)
    {
        _id = id;
    }

    /// <summary>
    /// 受け取ったユーザー ID。コンストラクターでしか入れられない
    /// </summary>
    private readonly Guid _id;
}
