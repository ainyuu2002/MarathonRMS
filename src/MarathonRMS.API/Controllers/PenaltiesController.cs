using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-61 – DSQ / phạt cộng phút (UC-67) và thu hồi quyết định phạt.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class PenaltiesController : ControllerBase
{
    private readonly IResultService _results;
    public PenaltiesController(IResultService results) => _results = results;

    /// <summary>Danh mục lý do phạt đang áp dụng (BR-28).</summary>
    [HttpGet("results/penalty-reasons")]
    public async Task<ActionResult<IReadOnlyList<PenaltyReasonDto>>> GetReasons(CancellationToken ct) =>
        Ok(await _results.GetPenaltyReasonsAsync(ct));

    [HttpGet("results/{resultId:int}/penalties")]
    public async Task<ActionResult<ResultPenaltiesDto>> GetPenalties(int resultId, CancellationToken ct) =>
        Ok(await _results.GetPenaltiesAsync(resultId, ct));

    [HttpPost("results/{resultId:int}/penalties")]
    public async Task<ActionResult<PenaltyDto>> Issue(int resultId, IssuePenaltyRequest request, CancellationToken ct) =>
        Ok(await _results.IssuePenaltyAsync(resultId, request, ct));

    /// <summary>Thu hồi: đặt RevokedAt, không xóa quyết định.</summary>
    [HttpPost("penalties/{penaltyId:int}/revoke")]
    public async Task<IActionResult> Revoke(int penaltyId, CancellationToken ct)
    {
        await _results.RevokePenaltyAsync(penaltyId, ct);
        return NoContent();
    }
}
