using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.Controllers;

/// <summary>
/// UC-62 – bảng xếp hạng công khai.
/// GET /odata/Results?$filter=EventId eq 2 and CategoryId eq 5 and Gender eq 'F'&amp;$orderby=SortOrder&amp;$top=50&amp;$count=true
/// </summary>
[AllowAnonymous]
public class ResultsController : ODataController
{
    private readonly IResultService _results;
    public ResultsController(IResultService results) => _results = results;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<LeaderboardRowDto> Get() => _results.QueryLeaderboard();
}

/// <summary>Giải đã công bố kết quả (bộ lọc bảng xếp hạng) và kết quả cá nhân – UC-63 (REST).</summary>
[ApiController]
[Route("api/results")]
[AllowAnonymous]
public class PublicResultsController : ControllerBase
{
    private readonly IResultService _results;
    public PublicResultsController(IResultService results) => _results = results;

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<ResultsEventDto>>> GetEvents(CancellationToken ct) =>
        Ok(await _results.GetPublishedEventsAsync(ct));

    [HttpGet("registrations/{registrationId:int}")]
    public async Task<ActionResult<ResultDetailDto>> GetDetail(int registrationId, CancellationToken ct) =>
        Ok(await _results.GetDetailAsync(registrationId, ct));
}
