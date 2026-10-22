using Microsoft.AspNetCore.Mvc;

namespace ShopListApi.Categories;

/// <summary>
/// 分類の API（/categories）
/// 受け取って CategoryService に頼み、結果を返すだけ。処理の中身はサービスにある
/// </summary>
[ApiController]
[Route("categories")]
public class CategoriesController : ControllerBase
{

    /// <summary>
    /// GET /categories: 自分の分類の一覧
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll()
    {
        return await _categoryService.GetAllAsync();
    }

    /// <summary>
    /// GET /categories/{id}: 自分の分類を1件（無ければ 404）
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        return await _categoryService.GetByIdAsync(id);
    }

    /// <summary>
    /// POST /categories: 分類を作る（201。同じ名前なら 409、入力が正しくなければ 400）
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request)
    {
        var created = await _categoryService.CreateAsync(request);

        // 201 と、Location: /categories/{id} を返す
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// PUT /categories/{id}: 分類の名前を変える（204。無ければ 404、同じ名前なら 409、入力が正しくなければ 400）
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, UpdateCategoryRequest request)
    {
        await _categoryService.UpdateAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// DELETE /categories/{id}: 分類を消す（204。無ければ 404）
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        await _categoryService.DeleteAsync(id);
        return NoContent();
    }

    #region // --- 内部処理 --- //

    public CategoriesController(CategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    private readonly CategoryService _categoryService;

    #endregion
}