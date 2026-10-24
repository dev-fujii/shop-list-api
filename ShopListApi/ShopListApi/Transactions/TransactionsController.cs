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
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll(int page = 1, int? pageSize = null)
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

        var transactions = await _transactionService.GetAllAsync(page, actualPageSize);
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
    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(CreateTransactionRequest request)
    {
        var created = await _transactionService.CreateAsync(request);

        // 201 と、Location: /transactions/{id} を返す
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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
