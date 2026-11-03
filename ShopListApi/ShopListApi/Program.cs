using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShopListApi.Accounts;
using ShopListApi.Categories;
using ShopListApi.CategoryRules;
using ShopListApi.Common;
using ShopListApi.Data;
using ShopListApi.Summaries;
using ShopListApi.Tokens;
using ShopListApi.Transactions;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#region // ---------- コントローラーと JSON ---------- //

builder.Services.AddControllers().AddJsonOptions
(
    // JSON の enum は、数字ではなく名前（"expense"・"auto" など）で読み書きする。数字は受け付けない
    options =>
    options.JsonSerializerOptions.Converters.Add
    (
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    )
);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

#endregion

#region // ---------- 設定 ---------- //

// appsettings.json とシークレットから読む。
// ValidateOnStart のものは、設定が足りないと起動の時点で止まる

// ページ分割
builder.Services.Configure<PaginationOptions>(
    builder.Configuration.GetSection("Pagination"));

// 集計
builder.Services.AddOptions<SummaryOptions>()
    .Bind(builder.Configuration.GetSection("Summary"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// (仮の鍵)ApiKey セクションから読む。
// 鍵はシークレット、鍵の持ち主の ID は appsettings.json に置く
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection("ApiKey"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// JWT の設定。Jwt セクションから読む。
// 秘密鍵はシークレット、それ以外は appsettings.json に置く
// 秘密鍵は、空でないかに加えて、RSA の鍵として読めるかも起動時に確かめる
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .Validate(
        options => JwtOptions.IsUsablePrivateKey(options.PrivateKey),
        $"Jwt:PrivateKey を {JwtOptions.MinKeySizeInBits} ビット以上の PKCS#8 の RSA 秘密鍵として読めません。")
    .ValidateOnStart();

#endregion

#region // ---------- DB とアカウント ---------- //

// DB の窓口
// PostgreSQL につなぐ
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// アカウントの登録やパスワードの確認をする道具。保存先は上の DB
// 画面もクッキーも使わないので AddIdentityCore にする。トークンは自分で出す
builder.Services.AddIdentityCore<AppUser>(options =>
{
    // パスワードは15文字以上。混ぜる文字の決まりは付けない
    // NIST の今の指針に合わせる
    options.Password.RequiredLength = AppUser.PasswordMinLength;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    // 5回続けて失敗したら5分止める。作ったばかりのアカウントも同じ
    // Identity の既定と同じ値
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
// ログインでパスワードを確かめ、失敗を数えてロックする係。クッキーは使わない
.AddSignInManager();

#endregion

#region // ---------- 認証と認可 ---------- //

// アクセストークンを作る係。鍵の読み込みは1回でよいので、アプリ全体で1つ。
// JWT を確かめるときの公開鍵も、ここからもらう
builder.Services.AddSingleton<AccessTokenIssuer>();

// 認証: 鍵か JWT で誰かの名札を付ける。
// 既定は振り分け役で、リクエストごとに鍵の係か JWT の係に回す
const string ApiKeyOrJwtScheme = "ApiKeyOrJwt";
builder.Services.AddAuthentication(ApiKeyOrJwtScheme)
.AddPolicyScheme(ApiKeyOrJwtScheme, displayName: null, options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
            ? ApiKeyAuthenticationHandler.SchemeName      // 鍵が付いていたら、鍵の係
            : JwtBearerDefaults.AuthenticationScheme;     // 付いていなかったら、JWT の係
})
// 鍵の係。設定は ApiKeyOptions から読むので、スキーム自体の設定は無し
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
// JWT の係。確かめ方の設定はすぐ下
.AddJwtBearer();

// JWT の確かめ方。署名・発行者・対象者・期限を全部確かめる。
// 公開鍵は AccessTokenIssuer から、発行者と対象者は JwtOptions からもらう
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
.Configure<IOptions<JwtOptions>, AccessTokenIssuer>((options, jwtOptions, issuer) =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = issuer.ValidationKey,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Value.Issuer,

        ValidateAudience = true,
        ValidAudience = jwtOptions.Value.Audience,

        ValidateLifetime = true,

        // 期限を過ぎてから許す時間。既定は5分だが30秒にする。
        // 同じサーバーで作って確かめるので、時計のずれはほぼ無い
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// 認可: 何も付いていない所は全部、名札が必要。無ければ 401
// [Authorize] を付けて回る形と違い、付け忘れても閉じたままになる。
// 開ける所だけ [AllowAnonymous] を付ける
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        // 鍵で来た名札は通さない。名札の発行元が鍵のスキームなら、鍵で来たと分かる。
        // 鍵で呼んでよい所には AllowApiKey を付けて開ける。鍵が漏れても、できることを小さくするため
        .RequireAssertion(context =>
            context.User.Identity?.AuthenticationType != ApiKeyAuthenticationHandler.SchemeName)
        .Build();

    // 鍵で来ても通してよい決まり。名札があればよい
    options.AddPolicy(AuthorizationPolicies.AllowApiKey, policy =>
        policy.RequireAuthenticatedUser());
});

// 今のリクエストを取り出す道具を登録する。中身は AddSingleton。
// コントローラーの外から、今のリクエストの名札を読むために使う
builder.Services.AddHttpContextAccessor();

// 今のユーザー: リクエストの名札の ID から作る。リクエストごとに1つ作る
// 鍵で来ても JWT で来ても、名札の形が同じならここは変えずに済む
builder.Services.AddScoped<CurrentUser>(sp =>
{
    // 今のリクエストを取り出す道具をもらう
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();

    // 名札から ID の文字列を読む。
    // リクエストの外では HttpContext が null なので ?. で受ける
    var idText = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    // FallbackPolicy があるので、名札の無いリクエストはここまで来ない。
    // 来たら仕組みのバグなので、空の ID で進めずに 500 で止める
    if (!Guid.TryParse(idText, out var id))
    {
        throw new InvalidOperationException("無効なユーザー ID が指定されました。");
    }

    return new CurrentUser(id);
});

#endregion

#region // ---------- 処理 ---------- //

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

// アカウントの処理
builder.Services.AddScoped<AccountService>();

// ログインの処理
builder.Services.AddScoped<TokenService>();

#endregion

#region // ---------- 回数制限 ---------- //

// 送り元の IP アドレスを、Azure の入口が付ける X-Forwarded-For から読む。
// そのままだと、全員が入口の IP に見えてしまうため。
// 入口がいつも一番右に本当の送り元を足すので、右から1つだけ読む
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    // Container Apps の入口の IP は決まっていないので、どこから来たものも読む。
    // アプリには入口を通らないと届かないので、入口が付けた値を信じてよい
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ログインとアカウント作成の回数を、IP アドレスごとに数える。
// 超えたら 429。中身は UseStatusCodePages が Problem Details の形にする
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 何秒後にまた試せるかを Retry-After で伝える
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
        }

        return ValueTask.CompletedTask;
    };

    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RateLimitPolicies.LoginPermitLimit,
                Window = RateLimitPolicies.LoginWindow,
                QueueLimit = 0
            }));

    options.AddPolicy(RateLimitPolicies.CreateAccount, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RateLimitPolicies.CreateAccountPermitLimit,
                Window = RateLimitPolicies.CreateAccountWindow,
                QueueLimit = 0
            }));
});

#endregion

#region // ---------- エラーの返し方 ---------- //

builder.Services.AddProblemDetails(options =>
{
    // 429 は ASP.NET Core の既定の一覧に無く、題名が付かない。
    // ほかのエラーとそろえて、題名と説明のページを付ける
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Status == StatusCodes.Status429TooManyRequests)
        {
            context.ProblemDetails.Title ??= "Too Many Requests";
            context.ProblemDetails.Type ??= "https://tools.ietf.org/html/rfc6585#section-4";
        }
    };
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();

#endregion

var app = builder.Build();

#region // ---------- 初期分類のコマンド ---------- //

// 初期分類の投入は専用コマンドで明示的に行い、Webサーバーは起動せず終了する。
// 通常起動では、本人が削除・改名した初期分類を勝手に作り直さない。
if (args.Contains("--initialize-categories", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();

    // 誰の初期分類を作るか: 鍵の持ち主。コマンドはリクエストの外なので名札が無い。
    // このコマンドは app.Run() を通らないので ValidateOnStart は走らない。
    // それでも .Value を読んだときに検証される
    var apiKeyOptions = scope.ServiceProvider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;

    // ID が無いまま進むと、全部 0 などの決まっていないユーザーに分類ができるので止める
    if (!apiKeyOptions.UserId.HasValue)
    {
        throw new InvalidOperationException("ユーザー ID が指定されていません。");
    }

    var userId = apiKeyOptions.UserId.Value;
    var addedCount = await scope.ServiceProvider.GetRequiredService<CategoryInitializer>()
        .InitializeAsync(userId);
    app.Logger.LogInformation("初期分類を{Count}件追加しました。", addedCount);
    await app.DisposeAsync();
    return;
}

#endregion

#region // ---------- リクエストの流れ ---------- //
// 上から順に通る

// 送り元の IP を X-Forwarded-For から読み直す。
// ほかの処理が IP を使う前に、いちばん最初に置く
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 例外をつかまえて、AppExceptionHandlerに渡す
app.UseExceptionHandler();

// 本文のないエラー（無い URL の 404 など）も、Problem Details の形にする
app.UseStatusCodePages();

app.UseHttpsRedirection();

// 回数制限。EnableRateLimiting を付けた口だけ数える
app.UseRateLimiter();

// 先に確かめ係が名札を付け、そのあと門番が名札を見る。
// この順番でないと、門番はいつも名札なしと判断する。
// 止めるときの 401 は本文なし。UseStatusCodePages が Problem Details の形にする
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

#endregion

app.Run();
