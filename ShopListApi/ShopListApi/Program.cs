using Microsoft.EntityFrameworkCore;
using ShopListApi.Categories;
using ShopListApi.Common;
using ShopListApi.Data;
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

// 今のユーザー: ログインを作るまでは仮のユーザーを返す。
// リクエストごとに1つ作る
builder.Services.AddScoped<CurrentUser>();

// 分類の処理
builder.Services.AddScoped<CategoryService>();

// 取引の処理
builder.Services.AddScoped<TransactionService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
