using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShopListApi.Accounts;

/// <summary>
/// アカウントの API
/// 受け取って AccountService に頼み、結果を返すだけ
/// </summary>
/// <remarks>
/// FallbackPolicy で全部の API が認証必須になっている。
/// アカウントを作る前の人も呼べるように、ここだけ [AllowAnonymous] で開ける
/// </remarks>
[ApiController]
[Route("accounts")]
[AllowAnonymous]
public class AccountsController : ControllerBase
{
    /// <summary>
    /// POST /accounts: アカウントを作る（201。同じユーザー名なら 409、入力が正しくなければ 400）
    /// </summary>
    /// <remarks>
    /// ユーザーの ID は返さず、1件を取る API も無いので、Location は付けない
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request)
    {
        var created = await _accountService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    #region // --- 内部処理 --- //

    public AccountsController(AccountService accountService)
    {
        _accountService = accountService;
    }

    private readonly AccountService _accountService;

    #endregion
}
