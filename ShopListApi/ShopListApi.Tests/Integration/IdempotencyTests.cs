using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// 取引の登録の冪等キーのテスト。
/// 送り直しても取引が1件だけになり、同じ取引が返るかを HTTP から確かめる
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class IdempotencyTests : ApiTestBase
{
    /// <summary>
    /// 同じキーで2回送ると、取引は1件だけで、2回とも同じ取引が返る
    /// </summary>
    [TestMethod]
    public async Task SameKeyCreatesOnlyOneTransaction()
    {
        using var client = CreateClientWithApiKey();
        var key = Guid.NewGuid().ToString();

        var first = await PostTransactionAsync(client, key, NewTransaction());
        var second = await PostTransactionAsync(client, key, NewTransaction());

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.AreEqual(await ReadIdAsync(first), await ReadIdAsync(second));
        Assert.AreEqual(1, await CountTransactionsAsync());
    }

    /// <summary>
    /// 同じキーのリクエストが同時に来ても、取引は1件だけになる
    /// </summary>
    /// <remarks>
    /// 先に探す確認だけだと、同時に来たリクエストはどれも「まだ無い」と判断して登録してしまう。
    /// DB の一意インデックスが2件目からを断り、先に登録された取引を返すことを確かめる
    /// </remarks>
    [TestMethod]
    public async Task SameKeyAtTheSameTimeCreatesOnlyOneTransaction()
    {
        using var client = CreateClientWithApiKey();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => PostTransactionAsync(client, key, NewTransaction())));

        foreach (var response in responses)
        {
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        var ids = await Task.WhenAll(responses.Select(ReadIdAsync));
        Assert.AreEqual(1, ids.Distinct().Count());
        Assert.AreEqual(1, await CountTransactionsAsync());
    }

    /// <summary>
    /// キーが違えば、同じ内容でも別の取引になる
    /// </summary>
    /// <remarks>
    /// 同じ店で同じ金額を2回払うことはあるので、内容だけでは送り直しと決めない
    /// </remarks>
    [TestMethod]
    public async Task DifferentKeysCreateTwoTransactions()
    {
        using var client = CreateClientWithApiKey();

        await PostTransactionAsync(client, Guid.NewGuid().ToString(), NewTransaction());
        await PostTransactionAsync(client, Guid.NewGuid().ToString(), NewTransaction());

        Assert.AreEqual(2, await CountTransactionsAsync());
    }

    /// <summary>
    /// 同じキーで内容の違う取引を送ると 422 になり、2件目は登録されない
    /// </summary>
    [TestMethod]
    public async Task SameKeyWithDifferentContentIsUnprocessable()
    {
        using var client = CreateClientWithApiKey();
        var key = Guid.NewGuid().ToString();
        await PostTransactionAsync(client, key, NewTransaction());

        var response = await PostTransactionAsync(client, key, new { amount = 3000, merchant = "ローソン", source = "auto" });

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("transaction.idempotency_key_reused", body.GetProperty("code").GetString());
        Assert.AreEqual(1, await CountTransactionsAsync());
    }

    /// <summary>
    /// キーはユーザーごと。別のユーザーが同じキーを使っても、それぞれ登録される
    /// </summary>
    [TestMethod]
    public async Task SameKeyFromOtherUserCreatesSeparateTransaction()
    {
        using var taro = await CreateClientWithLoginAsync("taro");
        using var jiro = await CreateClientWithLoginAsync("jiro");
        var key = Guid.NewGuid().ToString();

        var taroResponse = await PostTransactionAsync(taro, key, NewTransaction());
        var jiroResponse = await PostTransactionAsync(jiro, key, NewTransaction());

        Assert.AreEqual(HttpStatusCode.Created, taroResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, jiroResponse.StatusCode);
        Assert.AreEqual(2, await CountTransactionsAsync());
    }

    /// <summary>
    /// 64文字を超えるキーは 400 になる
    /// </summary>
    [TestMethod]
    public async Task TooLongKeyIsBadRequest()
    {
        using var client = CreateClientWithApiKey();

        var response = await PostTransactionAsync(client, new string('a', 65), NewTransaction());

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, await CountTransactionsAsync());
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
    /// Idempotency-Key ヘッダーを付けて、取引を登録する
    /// </summary>
    private static async Task<HttpResponseMessage> PostTransactionAsync(HttpClient client, string key, object transaction)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transactions")
        {
            Content = JsonContent.Create(transaction)
        };
        request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 返ってきた取引の id を読む
    /// </summary>
    private static async Task<int> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    /// <summary>
    /// DB にある取引の件数
    /// </summary>
    private async Task<int> CountTransactionsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Transactions.CountAsync();
    }

    #endregion
}
