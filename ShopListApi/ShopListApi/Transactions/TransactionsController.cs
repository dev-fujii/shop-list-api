using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopListApi.Common;
namespace ShopListApi.Transactions;
using Microsoft.Extensions.Options;

/// <summary>
/// 取引の API（/transactions）
/// 受け取って TransactionService に頼み、結果を返すだけ。処理の中身はサービスにある
/// </summary>
[ApiController]
[Route("transactions")]
public class TransactionsController : ControllerBase
{

    /// <summary>
    /// GET /transactions: 自分の取引の一覧（日付の新しい順。同じ日付は id の大きい順）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll
    (
        int page = 1,
        int? pageSize = null,
        int? categoryId = null,
        DateOnly? from = null,
        DateOnly? to = null
    )
    {
        // --- バリデーションチェック --- //

        var actualPageSize = pageSize ?? _paginationOptions.DefaultPageSize;

        // ページ番号のチェック
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "page は1以上で指定してください。");
            return ValidationProblem(ModelState);
        }

        // 1ページの件数のチェック
        if (actualPageSize < 1 || actualPageSize > _paginationOptions.MaxPageSize)
        {
            ModelState.AddModelError(nameof(pageSize), $"pageSize は1～{_paginationOptions.MaxPageSize}で指定してください。");
            return ValidationProblem(ModelState);
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            ModelState.AddModelError(nameof(from), "from は to 以前の日付で指定してください。");
            return ValidationProblem(ModelState);
        }

        var transactions = await _transactionService.GetAllAsync
        (
            page,
            actualPageSize,
            categoryId,
            from,
            to
        );
        return Ok(transactions);
    }

    /// <summary>
    /// GET /transactions/{id}: 自分の取引を1件（無ければ 404）
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> GetById(int id)
    {
        return await _transactionService.GetByIdAsync(id);
    }

    /// <summary>
    /// POST /transactions: 取引を登録する（201。入力が正しくなければ 400）
    /// </summary>
    /// <remarks>
    /// iPhone のショートカットが鍵で呼ぶので、ここだけ鍵でも通す。
    /// Idempotency-Key ヘッダーを付けると、同じキーで送り直しても取引は1件だけになり、同じ取引が返る
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AllowApiKey)]
    public async Task<ActionResult<TransactionResponse>> Create
    (
        CreateTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey
    )
    {
        // --- バリデーションチェック --- //

        // 冪等キーは省略できる。付けるなら、空でなく64文字まで
        if (idempotencyKey is not null &&
            (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > Transaction.IdempotencyKeyMaxLength))
        {
            ModelState.AddModelError("Idempotency-Key", $"Idempotency-Key は1～{Transaction.IdempotencyKeyMaxLength}文字で指定してください。");
            return ValidationProblem(ModelState);
        }

        var created = await _transactionService.CreateAsync(request, idempotencyKey);

        // 201 と、Location: /transactions/{id} を返す
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// PUT /transactions/{id}: 自分の取引を更新する（204）
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTransactionRequest request)
    {
        await _transactionService.UpdateAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// DELETE /transactions/{id}: 自分の取引を削除する（204）
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _transactionService.DeleteAsync(id);
        return NoContent();
    }

    #region // --- 内部処理 --- //

    public TransactionsController
    (
        TransactionService transactionService,
        IOptions<PaginationOptions> paginationOptions
    )
    {
        _transactionService = transactionService;
        _paginationOptions = paginationOptions.Value;
    }

    private readonly TransactionService _transactionService;
    private readonly PaginationOptions _paginationOptions;

    #endregion
}
