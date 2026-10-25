using System.ComponentModel.DataAnnotations;

namespace ShopListApi.Common;

/// <summary>
/// 仮の鍵（API キー）の設定
/// </summary>
/// <remarks>
/// 設定の ApiKey セクションに対応する（環境変数では ApiKey__Key）。
/// 値は秘密なので appsettings.json には書かず、ユーザーシークレット・.env・Azure のシークレットに置く。
/// ログインを作るまでのつなぎで、全員共通の1つの鍵
/// </remarks>
public class ApiKeyOptions
{
    /// <summary>
    /// 正しい API キー。リクエストの X-Api-Key ヘッダーと比べる
    /// </summary>
    /// <remarks>
    /// 設定が無いと string.Empty のままになり、[Required] が空の文字列を通さないので、起動時の検証で止まる
    /// </remarks>
    [Required]
    public string Key { get; set; } = string.Empty;
}
