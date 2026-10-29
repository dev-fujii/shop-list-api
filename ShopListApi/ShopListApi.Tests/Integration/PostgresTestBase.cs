using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.Tests.Integration;

/// <summary>
/// DB統合テストの共通部分。
/// 各テスト専用のPostgreSQLデータベースを作り、終了時に削除する。
/// 接続先はローカルのテスト用サーバーに限定し、アプリの接続設定は読まない。
/// </summary>
public abstract class PostgresTestBase
{
    /// <summary>
    /// テストごとに空のDBを作り、アプリと同じマイグレーションを適用する。
    /// </summary>
    /// <remarks>
    /// テストごとにDBを分けるので、並列に動かしても他のテストのデータと混ざらない。
    /// </remarks>
    [TestInitialize]
    public async Task InitializeDatabaseAsync()
    {
        // 接続先は専用の環境変数だけから読む。未設定なら成功扱いにせず失敗させる
        var connectionString = Environment.GetEnvironmentVariable("SHOPLIST_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail("統合テストにはSHOPLIST_TEST_POSTGRESが必要です。READMEのテスト用DBの手順を確認してください。");
        }

        // AzureなどのDBを誤って指定しても、DBを作ったり消したりしないように止める
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            settings.Database != "shoplist_tests")
        {
            Assert.Fail("統合テストの接続先はlocalhostのshoplist_testsに限定しています。");
        }

        // 接続を使い回さない。使い回すと、終了時にDBを削除するときに接続が残る
        settings.Pooling = false;
        settings.Timeout = 5;
        settings.CommandTimeout = 30;

        // 削除対象には、ここで新しく作ったランダムな名前のDBだけを使う。
        var databaseName = "shoplist_it_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(settings.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        // 以降の接続は、作ったDBに向ける
        settings.Database = databaseName;
        _databaseConnectionString = settings.ConnectionString;

        // 表・一意制約・外部キーは、アプリと同じマイグレーションで作る
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// 自分で作ったDBだけを削除する。準備の途中で失敗した場合にも呼ばれる。
    /// </summary>
    [TestCleanup]
    public async Task DeleteDatabaseAsync()
    {
        // DBを作る前に失敗した場合は、消すものが無い
        if (_databaseConnectionString is not null)
        {
            await using var db = CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// テスト用DBにつながる新しいDbContextを作る。
    /// 準備・実行・確認で別々に作り、別の接続から読み直して、メモリ上の変更だけで成功しないようにする。
    /// </summary>
    /// <param name="interceptors">保存の直前に処理を挟むときに渡す（競合を再現するテスト用）</param>
    protected AppDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().
            UseNpgsql(_databaseConnectionString ?? throw new InvalidOperationException("テスト用DBが未準備です。"));

        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new AppDbContext(options.Options);
    }

    /// <summary>
    /// 今のユーザー（アプリと同じ仮のユーザー）
    /// </summary>
    protected static readonly CurrentUser User = new();

    /// <summary>
    /// 他人のユーザーID。所有者ごとに分かれていることを確かめるために使う
    /// </summary>
    protected static readonly Guid OtherUserId = new("c837d989-735d-4c18-a781-f8b7bf794527");

    /// <summary>
    /// このテスト用に作ったDBの接続文字列。DBを作る前は null
    /// </summary>
    private string? _databaseConnectionString;

    #endregion
}
