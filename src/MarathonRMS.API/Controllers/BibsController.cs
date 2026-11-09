using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-49 – gán BIB theo dải số của từng cự ly.</summary>
[ApiController]
[Route("api/events/{eventId:int}/bibs")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class BibsController : ControllerBase
{
    private readonly IBibService _bibs;
    public BibsController(IBibService bibs) => _bibs = bibs;

    [HttpGet]
    public async Task<ActionResult<BibOverviewDto>> GetOverview(int eventId, CancellationToken ct) =>
        Ok(await _bibs.GetOverviewAsync(eventId, ct));

    [HttpGet("assignments")]
    public async Task<ActionResult<PagedResult<BibListItemDto>>> GetAssignments(int eventId, [FromQuery] int? categoryId,
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _bibs.GetAssignmentsAsync(eventId, categoryId, keyword, page, pageSize, ct));

    [HttpPost("assign")]
    public async Task<ActionResult<BibAssignmentResultDto>> Assign(int eventId, AssignBibsRequest request, CancellationToken ct) =>
        Ok(await _bibs.AssignAsync(eventId, request, ct));
}
