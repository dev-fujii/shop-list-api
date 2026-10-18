using Microsoft.EntityFrameworkCore;
using ShopListApi.Categories;
using ShopListApi.Common;
using ShopListApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
