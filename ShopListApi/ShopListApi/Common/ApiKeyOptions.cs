using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Common;

/// <summary>
/// 仮の鍵（API キー）の設定
/// </summary>
/// <remarks>
/// 設定の ApiKey セクションに対応する。環境変数では ApiKey__Key・ApiKey__UserId。
/// iPhone のショートカット用の1つの鍵と、その鍵の持ち主
/// </remarks>
public class ApiKeyOptions
{
    /// <summary>
    /// 正しい API キー。リクエストの X-Api-Key ヘッダーと比べる
    /// </summary>
    /// <remarks>
    /// 値は秘密なので appsettings.json には書かず、ユーザーシークレット・.env・Azure のシークレットに置く。
    /// 設定が無いと string.Empty のままになり、[Required] が空の文字列を通さないので、起動時の検証で止まる
    /// </remarks>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 鍵の持ち主のユーザー ID。鍵で来たリクエストは、このユーザーとして扱う
    /// </summary>
    /// <remarks>
    /// 秘密ではないので appsettings.json に置く。環境ごとに変えるときは環境変数で上書きする。
    /// Guid のままだと設定が無くても Guid.Emptyが入り、[Required] で止まらない。
    /// Guid? にして、設定が無いときは null になり、起動時の検証で止まるようにした
    /// </remarks>
    [Required]
    public Guid? UserId { get; set; } = null;
}
