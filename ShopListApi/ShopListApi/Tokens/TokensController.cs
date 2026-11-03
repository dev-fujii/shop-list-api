using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShopListApi.Common;

namespace ShopListApi.Tokens;

/// <summary>
/// ログインの API
/// 受け取って TokenService に頼み、結果を返すだけ
/// </summary>
/// <remarks>
/// ログインする前の人も呼べるように、ここだけ [AllowAnonymous] で開ける
/// </remarks>
[ApiController]
[Route("tokens")]
[AllowAnonymous]
public class TokensController : ControllerBase
{
    /// <summary>
    /// POST /tokens: ログインしてアクセストークンを受け取る（200。違えば 401、入力が無ければ 400、多すぎれば 429）
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<TokenResponse>> Create(CreateTokenRequest request)
    {
        return await _tokenService.CreateAsync(request);
    }

    #region // --- 内部処理 --- //

    public TokensController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    private readonly TokenService _tokenService;

    #endregion
}
