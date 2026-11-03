using Microsoft.AspNetCore.Identity;
using ShopListApi.Categories;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.Accounts;

/// <summary>
/// アカウントの処理
/// </summary>
public class AccountService
{
    /// <summary>
    /// アカウントを作り、その人の初期分類も作る。
    /// 同じユーザー名がもうあれば 409
    /// </summary>
    /// <remarks>
    /// アカウントと初期分類は1つのトランザクションでまとめて保存する。
    /// 途中で失敗したら、どちらも保存されない
    /// </remarks>
    public async Task<AccountResponse> CreateAsync(CreateAccountRequest request)
    {
        // ここから先の書き込みを1つにまとめる。
        // Commit まで行かずに抜けたら、using の終わりで取り消される
        await using var transaction = await _db.Database.BeginTransactionAsync();

        // パスワードのハッシュ化と、Program.cs の決まりでの確認は Identity がする
        var user = new AppUser { UserName = request.UserName };
        var result = await _userManager.CreateAsync(user, request.Password);

        //作成失敗なのでエラー返す
        if (!result.Succeeded)
        {
            // 同じユーザー名のときだけ 409
            if (result.Errors.Any(e => e.Code == "DuplicateUserName"))
            {
                 throw new AppException
                 (
                     statusCode: 409,
                     code: ErrorCodes.Accounts.DuplicateUserName,
                     title: "同じユーザー名のアカウントが既に存在します"
                 );
            }

            // それ以外は、受け取る形のチェックで先に止まるはずなので来ない。
            // 来たら仕組みのバグなので 500 で止める
            throw new InvalidOperationException("アカウントを作れませんでした。");
        }

        // 新しい人の初期分類を作る
        await _categoryInitializer.InitializeAsync(user.Id);

        // ここまで全部うまくいったので、まとめて確定する
        await transaction.CommitAsync();

        return new AccountResponse
        {
            UserName = user.UserName
        };
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// アカウント係、初期分類を作る処理、DB を受け取る
    /// </summary>
    /// <remarks>
    /// 3つとも同じ AppDbContext を使うので、上のトランザクションに全部入る
    /// </remarks>
    public AccountService
    (
        UserManager<AppUser> userManager,
        CategoryInitializer categoryInitializer,
        AppDbContext db
    )
    {
        _userManager = userManager;
        _categoryInitializer = categoryInitializer;
        _db = db;
    }

    private readonly UserManager<AppUser> _userManager;
    private readonly CategoryInitializer _categoryInitializer;
    private readonly AppDbContext _db;

    #endregion
}
