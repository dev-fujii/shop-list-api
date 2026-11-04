using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ShopListApi.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// HTTP を通したテストの共通部分。
/// テストごとに専用のDBを作り、そこにつながるアプリをメモリの中で起動する。
/// ルーティング・認証・認可・入力チェック・エラーの形まで、本番と同じ流れで確かめられる
/// </summary>
/// <remarks>
/// DBの準備と後片付けは PostgresTestBase がする。
/// MSTest は、親の TestInitialize を先に、子の TestCleanup を先に呼ぶので、
/// アプリはDBができてから起動し、DBを消す前に止まる
/// </remarks>
public abstract class ApiTestBase : PostgresTestBase
{
    /// <summary>
    /// テスト用DBにつながるアプリを起動する準備をする
    /// </summary>
    /// <remarks>
    /// アプリが実際に起動するのは、最初に CreateClient を呼んだとき
    /// </remarks>
    [TestInitialize]
    public void StartApp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Development にすると、手元のユーザーシークレットを読んでしまう。
            // テストの結果が PC ごとに変わらないよう、テスト用の環境名にする
            builder.UseEnvironment("Testing");

            // 設定はテスト用の値だけを渡す。
            // ここで足した設定は、appsettings.json や環境変数より後に読まれるので優先される
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = DatabaseConnectionString,
                    ["ApiKey:Key"] = ApiKey,
                    ["ApiKey:UserId"] = User.Id.ToString(),
                    ["Jwt:PrivateKey"] = TestPrivateKey
                }));
        });
    }

    /// <summary>
    /// アプリを止める。止めてから、親がDBを消す
    /// </summary>
    [TestCleanup]
    public async Task StopAppAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    #region // --- テストから使う道具 --- //


    /// <summary>
    /// テストで使う鍵
    /// </summary>
    protected const string ApiKey = "test-api-key-0123456789abcdef";

    /// <summary>
    /// テストで作るアカウントのパスワード。15文字以上
    /// </summary>
    protected const string Password = "test-password-123";

    /// <summary>
    /// テストのアプリがトークンに署名する秘密鍵。
    /// 本物の鍵を使わないよう、テストを動かすたびに作る
    /// </summary>
    protected static readonly string TestPrivateKey = CreatePrivateKey();

    /// <summary>
    /// 何も付けずに呼ぶクライアント
    /// </summary>
    protected HttpClient CreateClient()
    {
        return _factory?.CreateClient() ?? throw new InvalidOperationException("アプリが未準備です。");
    }

    /// <summary>
    /// 鍵（X-Api-Key）を付けて呼ぶクライアント。鍵の持ち主は User
    /// </summary>
    protected HttpClient CreateClientWithApiKey(string key = ApiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    /// <summary>
    /// アカウントを作ってログインし、受け取ったトークンを付けて呼ぶクライアント
    /// </summary>
    protected async Task<HttpClient> CreateClientWithLoginAsync(string userName)
    {
        using var anonymous = CreateClient();
        var account = new { userName, password = Password };

        var created = await anonymous.PostAsJsonAsync("/accounts", account);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);

        var login = await anonymous.PostAsJsonAsync("/tokens", account);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    #endregion

    #region // --- 内部処理 --- //

    /// <summary>
    /// RSA の秘密鍵を作り、アプリの設定と同じ形にする
    /// </summary>
    /// <remarks>
    /// 形は PKCS#8 を Base64 にした1行の文字列。JwtOptions が読む形と同じ
    /// </remarks>
    private static string CreatePrivateKey()
    {
        using var rsa = RSA.Create(2048);
        return Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
    }

    /// <summary>
    /// メモリの中でアプリを起動する道具
    /// </summary>
    private WebApplicationFactory<Program>? _factory;

    #endregion
}
