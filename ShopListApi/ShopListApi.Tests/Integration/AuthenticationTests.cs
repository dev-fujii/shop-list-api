using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShopListApi.Categories;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// 認証と認可のテスト。
/// HTTP を通し、トークン・鍵・回数制限・他人のデータの扱いが、設計どおりに止まるかを確かめる
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AuthenticationTests : ApiTestBase
{
    /// <summary>
    /// トークンも鍵も無いと 401 になる
    /// </summary>
    [TestMethod]
    public async Task RequestWithoutCredentialsIsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/categories");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 違う鍵では 401 になる
    /// </summary>
    [TestMethod]
    public async Task WrongApiKeyIsUnauthorized()
    {
        using var client = CreateClientWithApiKey("wrong-key");

        var response = await client.PostAsJsonAsync("/transactions", NewTransaction());

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 鍵で取引を登録でき、鍵の持ち主の取引として保存される
    /// </summary>
    [TestMethod]
    public async Task ApiKeyCanCreateTransactionForKeyOwner()
    {
        using var client = CreateClientWithApiKey();

        var response = await client.PostAsJsonAsync("/transactions", NewTransaction());

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        // 持ち主は、設定に書いた鍵のユーザーになっている
        await using var db = CreateDbContext();
        var saved = db.Transactions.Single();
        Assert.AreEqual(User.Id, saved.UserId);
    }

    /// <summary>
    /// 鍵では、取引の登録以外の API を呼べない（403）
    /// </summary>
    /// <remarks>
    /// 401 ではなく 403 になるのは、鍵そのものは正しく、誰なのかは分かっているため
    /// </remarks>
    [TestMethod]
    public async Task ApiKeyCannotCallOtherApis()
    {
        using var client = CreateClientWithApiKey();

        var response = await client.GetAsync("/categories");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// アカウントを作ってログインすると、トークンで自分の初期分類を読める
    /// </summary>
    [TestMethod]
    public async Task LoggedInUserCanReadOwnInitialCategories()
    {
        using var client = await CreateClientWithLoginAsync("jiro");

        var response = await client.GetAsync("/categories");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        // 初期分類は、食費・日用品・交通費・趣味・固定費・給料の6つ
        Assert.HasCount(6, categories!);
    }

    /// <summary>
    /// パスワードが違うと 401 になり、エラーコードで理由の種類が分かる
    /// </summary>
    [TestMethod]
    public async Task WrongPasswordIsUnauthorizedWithErrorCode()
    {
        using var setup = await CreateClientWithLoginAsync("jiro");
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/tokens", new { userName = "jiro", password = "wrong-password-123" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("token.invalid_credentials", body.GetProperty("code").GetString());
    }

    /// <summary>
    /// アプリと別の鍵で署名したトークンは、中身が正しくても 401 になる
    /// </summary>
    /// <remarks>
    /// 署名を確かめていなければ、だれでも好きなユーザーのトークンを作れてしまう
    /// </remarks>
    [TestMethod]
    public async Task TokenSignedWithOtherKeyIsUnauthorized()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTokenSignedWithOtherKey(User.Id));

        var response = await client.GetAsync("/categories");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 他人の取引は、ID を知っていても読めない
    /// </summary>
    [TestMethod]
    public async Task OtherUsersTransactionIsNotFound()
    {
        // jiro が取引を登録する
        using var jiro = await CreateClientWithLoginAsync("jiro");
        var created = await jiro.PostAsJsonAsync("/transactions", NewTransaction());
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);

        // 返ってきた JSON から id だけを読む。
        // source などの enum は名前で返るので、TransactionResponse に読み込むには enum の設定が要る
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("id").GetInt32();

        // taro が、jiro の取引の ID で読もうとする
        using var taro = await CreateClientWithLoginAsync("taro");
        var response = await taro.GetAsync($"/transactions/{transactionId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// ログインは1分に5回まで。6回目は 429 になり、また試せるまでの秒数が返る
    /// </summary>
    [TestMethod]
    public async Task SixthLoginInAMinuteIsTooManyRequests()
    {
        using var client = CreateClient();
        var login = new { userName = "nobody", password = "wrong-password-123" };

        for (var i = 0; i < 5; i++)
        {
            var allowed = await client.PostAsJsonAsync("/tokens", login);
            Assert.AreEqual(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var response = await client.PostAsJsonAsync("/tokens", login);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.IsNotNull(response.Headers.RetryAfter);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 登録する取引。金額・店名・入力元だけを送る
    /// </summary>
    private static object NewTransaction()
    {
        return new { amount = 1200, merchant = "ローソン", source = "auto" };
    }

    /// <summary>
    /// アプリと同じ発行者・対象者で、別の秘密鍵で署名したトークンを作る
    /// </summary>
    private static string CreateTokenSignedWithOtherKey(Guid userId)
    {
        using var otherKey = RSA.Create(2048);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "shop-list-api",
            Audience = "shop-list-api",
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(otherKey), SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    #endregion
}
