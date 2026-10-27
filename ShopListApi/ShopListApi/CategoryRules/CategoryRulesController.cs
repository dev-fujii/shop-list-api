using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShopListApi.Common;

namespace ShopListApi.CategoryRules;

/// <summary>
/// 分類ルールの一覧・1件取得・登録を受け付けるAPI。
/// </summary>
/// <remarks>
/// データの取得・登録は CategoryRuleService に任せ、HTTPレスポンスの形を決める。
/// </remarks>
[ApiController]
[Route("category-rules")]
public class CategoryRulesController : ControllerBase
{
    /// <summary>
    /// 今のユーザーの分類ルールをページ単位で取得する。
    /// </summary>
    /// <remarks>
    /// GET /category-rules に対応する。
    /// ページ番号と件数を検証してから、サービスに一覧の取得を依頼する。
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CategoryRuleResponse>>> GetAll
    (
        int page = 1, 
        int? pageSize = null    
    )
    {
        var actualPageSize = pageSize ?? _paginationOptions.DefaultPageSize;

        // ページ番号は1から始める。
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "page は1以上で指定してください。");
            return ValidationProblem(ModelState);
        }

        // 1ページの件数は1以上、設定の上限以下に制限する。
        if (actualPageSize < 1 || actualPageSize > _paginationOptions.MaxPageSize)
        {
            ModelState.AddModelError
            (
                nameof(pageSize),
                $"pageSize は1～{_paginationOptions.MaxPageSize}で指定してください。"
            );
            return ValidationProblem(ModelState);
        }

        return Ok(await _categoryRuleService.GetAllAsync(page, actualPageSize));
    }

    /// <summary>
    /// 指定IDの分類ルールを、今のユーザーのものに限って取得する。
    /// </summary>
    /// <remarks>
    /// GET /category-rules/{id} に対応する。
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryRuleResponse>> GetById(int id)
    {
        return await _categoryRuleService.GetByIdAsync(id);
    }

    /// <summary>
    /// 今のユーザーの分類ルールを登録する。
    /// </summary>
    /// <remarks>
    /// POST /category-rules に対応する。
    /// 入力形式が不正なら400、分類が無い・他人の分類なら422、同じユーザーのキーワードが重複していれば409を返す。
    /// 分類が無い・他人の分類で、キーワードも重複している場合は422を優先する。
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<CategoryRuleResponse>> Create
    (
        CreateCategoryRuleRequest request
    )
    {
        var created = await _categoryRuleService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 分類ルールの処理と、ページ分割の設定を受け取る。
    /// </summary>
    public CategoryRulesController
    (
        CategoryRuleService categoryRuleService,
        IOptions<PaginationOptions> paginationOptions
    )
    {
        _categoryRuleService = categoryRuleService;
        _paginationOptions = paginationOptions.Value;
    }

    private readonly CategoryRuleService _categoryRuleService;
    private readonly PaginationOptions _paginationOptions;

    #endregion
}
