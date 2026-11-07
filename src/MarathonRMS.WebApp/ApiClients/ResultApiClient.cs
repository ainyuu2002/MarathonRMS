using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần C – kết quả: tính, công bố, xếp hạng, phạt.</summary>
public class ResultApiClient : ApiClientBase
{
    public ResultApiClient(HttpClient http) : base(http) { }

    // ---------- UC-59 tính kết quả (Organizer) ----------

    public Task<ResultsOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<ResultsOverviewDto>($"api/events/{eventId}/results", ct);

    public Task<PagedResult<LeaderboardRowDto>> GetRowsAsync(int eventId, int? categoryId, string? status, string? keyword,
        int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<LeaderboardRowDto>>(Query($"api/events/{eventId}/results/rows",
            ("categoryId", categoryId), ("status", status), ("keyword", keyword), ("page", page), ("pageSize", 30)), ct);

    public Task<ResultCalculationSummaryDto> CalculateAsync(int eventId, CancellationToken ct = default) =>
        PostAsync<ResultCalculationSummaryDto>($"api/events/{eventId}/results/calculate", ct);

    // ---------- UC-60 công bố ----------

    public Task<ResultsStatusDto> PublishProvisionalAsync(int eventId, CancellationToken ct = default) =>
        PostAsync<ResultsStatusDto>($"api/events/{eventId}/results/provisional", ct);

    public Task<ResultsStatusDto> FinalizeAsync(int eventId, CancellationToken ct = default) =>
        PostAsync<ResultsStatusDto>($"api/events/{eventId}/results/official", ct);

    // ---------- UC-61 DSQ / phạt ----------

    public Task<List<PenaltyReasonDto>> GetPenaltyReasonsAsync(CancellationToken ct = default) =>
        GetAsync<List<PenaltyReasonDto>>("api/results/penalty-reasons", ct);

    public Task<ResultPenaltiesDto> GetPenaltiesAsync(int resultId, CancellationToken ct = default) =>
        GetAsync<ResultPenaltiesDto>($"api/results/{resultId}/penalties", ct);

    public Task<PenaltyDto> IssuePenaltyAsync(int resultId, IssuePenaltyRequest request, CancellationToken ct = default) =>
        PostAsync<PenaltyDto>($"api/results/{resultId}/penalties", request, ct);

    public Task RevokePenaltyAsync(int penaltyId, CancellationToken ct = default) =>
        PostAsync($"api/penalties/{penaltyId}/revoke", new { }, ct);

    // ---------- UC-62 bảng xếp hạng công khai ----------

    public Task<List<ResultsEventDto>> GetPublishedEventsAsync(CancellationToken ct = default) =>
        GetAsync<List<ResultsEventDto>>("api/results/events", ct);

    /// <summary>keyword: số BIB hoặc một phần tên công khai.</summary>
    public Task<PagedResult<LeaderboardRowDto>> GetLeaderboardAsync(int eventId, int? categoryId, string? gender, int? ageGroupId,
        string? keyword, int page, CancellationToken ct = default)
    {
        var k = keyword?.Trim();
        var filter = ODataQuery.And(
            $"EventId eq {eventId}",
            categoryId is int c ? $"CategoryId eq {c}" : null,
            gender is "M" or "F" ? $"Gender eq {ODataQuery.Str(gender)}" : null,
            ageGroupId is int a ? $"AgeGroupId eq {a}" : null,
            string.IsNullOrEmpty(k) ? null
                : int.TryParse(k, out var bib) ? $"BibNumber eq {bib}"
                : $"contains(DisplayName,{ODataQuery.Str(k)})");
        return GetODataPageAsync<LeaderboardRowDto>("Results", filter, "SortOrder,BibNumber", page, 50, ct);
    }
}
