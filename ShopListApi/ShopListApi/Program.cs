using Microsoft.EntityFrameworkCore;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Common;
using ShopListApi.Data;
using ShopListApi.Transactions;
using ShopListApi.Summaries;
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

// (仮の鍵)ApiKey セクションから読む。
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection("ApiKey"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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

// 仮の鍵を確かめる。無い・違えば 401（UseStatusCodePages が Problem Details の形にする）
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
