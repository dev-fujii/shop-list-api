using Microsoft.AspNetCore.Mvc;

namespace ShopListApi.Summaries;

/// <summary>
/// 集計の API（/summaries）
/// 条件を受け取って SummaryService に頼み、結果を返す
/// </summary>
[ApiController]
[Route("summaries")]
public class SummariesController : ControllerBase
{
    /// <summary>
    /// GET /summaries: 自分の取引を月か期間で集計する（条件が正しくなければ 400）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SummaryResponse>> Get([FromQuery] SummaryQuery query)
    {
        // 日付の形と条件の組み合わせは、ApiController と SummaryQuery で検証する
        return Ok(await _summaryService.GetAsync(query));
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 集計の処理を受け取る
    /// </summary>
    public SummariesController(SummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    private readonly SummaryService _summaryService;

    #endregion
}
