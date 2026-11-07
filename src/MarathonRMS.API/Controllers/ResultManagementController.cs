using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-59 – Organizer tính và rà soát kết quả của giải.</summary>
[ApiController]
[Route("api/events/{eventId:int}/results")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class ResultManagementController : ControllerBase
{
    private readonly IResultService _results;
    public ResultManagementController(IResultService results) => _results = results;

    [HttpGet]
    public async Task<ActionResult<ResultsOverviewDto>> GetOverview(int eventId, CancellationToken ct) =>
        Ok(await _results.GetOverviewAsync(eventId, ct));

    [HttpGet("rows")]
    public async Task<ActionResult<PagedResult<LeaderboardRowDto>>> GetRows(int eventId, [FromQuery] int? categoryId,
        [FromQuery] string? status, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken ct = default) =>
        Ok(await _results.GetForOrganizerAsync(eventId, categoryId, status, keyword, page, pageSize, ct));

    [HttpPost("calculate")]
    public async Task<ActionResult<ResultCalculationSummaryDto>> Calculate(int eventId, CancellationToken ct) =>
        Ok(await _results.CalculateAsync(eventId, ct));
}
