using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần C – ngày thi: gán BIB, race kit, bấm giờ.</summary>
public class RaceDayApiClient : ApiClientBase
{
    public RaceDayApiClient(HttpClient http) : base(http) { }

    // ---------- UC-49 gán BIB ----------

    public Task<BibOverviewDto> GetBibOverviewAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<BibOverviewDto>($"api/events/{eventId}/bibs", ct);

    public Task<PagedResult<BibListItemDto>> GetBibAssignmentsAsync(int eventId, int? categoryId, string? keyword, int page,
        CancellationToken ct = default) =>
        GetAsync<PagedResult<BibListItemDto>>(Query($"api/events/{eventId}/bibs/assignments",
            ("categoryId", categoryId), ("keyword", keyword), ("page", page), ("pageSize", 20)), ct);

    public Task<BibAssignmentResultDto> AssignBibsAsync(int eventId, int? categoryId, CancellationToken ct = default) =>
        PostAsync<BibAssignmentResultDto>($"api/events/{eventId}/bibs/assign", new AssignBibsRequest { CategoryId = categoryId }, ct);
}
