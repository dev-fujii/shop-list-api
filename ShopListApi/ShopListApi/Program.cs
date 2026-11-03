using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Common;
using ShopListApi.Data;
using ShopListApi.Summaries;
using ShopListApi.Transactions;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    // JSON の enum は、数字ではなく名前（"expense"・"auto" など）で読み書きする。数字は受け付けない
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// --- 自分で作ったサービスの登録（DI） --- //

// DB の窓口
// PostgreSQL につなぐ
builder.Services.AddDbContext<AppDbContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<PaginationOptions>(
builder.Configuration.GetSection("Pagination"));

builder.Services.AddOptions<SummaryOptions>()
    .Bind(builder.Configuration.GetSection("Summary"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// (仮の鍵)ApiKey セクションから読む。鍵はシークレット、鍵の持ち主（UserId）は appsettings.json に置く
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection("ApiKey"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 認証: X-Api-Key の鍵で誰かの名札を付ける。スキームの指定が無いときはこれを使う
// 設定は ApiKeyOptions から読むので、スキーム自体の設定は無し
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

// 認可: 何も付いていない所は全部、名札が必要。無ければ 401
// [Authorize] を付けて回る形と違い、付け忘れても閉じたままになる。
// 開ける所だけ [AllowAnonymous] を付ける
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 今のユーザー: ログインを作るまでは仮のユーザーを返す。
// リクエストごとに1つ作る
builder.Services.AddScoped<CurrentUser>();

// 分類の処理
builder.Services.AddScoped<CategoryService>();

// 取引の処理
builder.Services.AddScoped<TransactionService>();

// 分類ルールの処理
builder.Services.AddScoped<CategoryRuleService>();

// 集計の処理
builder.Services.AddScoped<SummaryService>();

// 初期分類を用意する処理
builder.Services.AddScoped<CategoryInitializer>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

var app = builder.Build();

// 初期分類の投入は専用コマンドで明示的に行い、Webサーバーは起動せず終了する。
// 通常起動では、本人が削除・改名した初期分類を勝手に作り直さない。
if (args.Contains("--initialize-categories", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var addedCount = await scope.ServiceProvider.GetRequiredService<CategoryInitializer>()
        .InitializeAsync();
    app.Logger.LogInformation("初期分類を{Count}件追加しました。", addedCount);
    await app.DisposeAsync();
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 例外をつかまえて、AppExceptionHandlerに渡す
app.UseExceptionHandler();

// 本文のないエラー（無い URL の 404 など）も、Problem Details の形にする
app.UseStatusCodePages();

app.UseHttpsRedirection();

// 先に確かめ係が名札を付け、そのあと門番が名札を見る。この順番でないと、門番はいつも名札なしと判断する
// 止めるときの 401 は本文なしなので、UseStatusCodePages が Problem Details の形にする
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
