using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
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

    // ---------- Vận hành ngày thi (Staff/Organizer) ----------

    public Task<List<EventSummaryDto>> GetOperationalEventsAsync(CancellationToken ct = default) =>
        GetAsync<List<EventSummaryDto>>("api/race-day/events", ct);

    // ---------- UC-52 tra cứu race kit ----------

    public Task<List<RaceKitLookupItemDto>> LookupRaceKitAsync(int eventId, string keyword, CancellationToken ct = default) =>
        GetAsync<List<RaceKitLookupItemDto>>(Query($"api/race-day/events/{eventId}/race-kit", ("keyword", keyword)), ct);

    // ---------- UC-53 phát race kit ----------

    public Task<RaceKitPickupDto> PickupRaceKitAsync(int registrationId, RaceKitPickupRequest request, CancellationToken ct = default) =>
        PostAsync<RaceKitPickupDto>($"api/race-day/race-kit/{registrationId}/pickup", request, ct);

    // ---------- UC-57 bấm giờ ----------

    public Task<TimingRecordDto> RecordTimingAsync(RecordTimingRequest request, CancellationToken ct = default) =>
        PostAsync<TimingRecordDto>("api/race-day/timing", request, ct);

    public Task<List<TimingRecordDto>> GetTimingByBibAsync(int eventId, int bibNumber, CancellationToken ct = default) =>
        GetAsync<List<TimingRecordDto>>(Query($"api/race-day/events/{eventId}/timing", ("bib", bibNumber)), ct);

    public Task<List<TimingRecordDto>> GetRecentTimingAsync(int eventId, int? checkpointId, CancellationToken ct = default) =>
        GetAsync<List<TimingRecordDto>>(Query($"api/race-day/events/{eventId}/timing", ("checkpointId", checkpointId), ("take", 15)), ct);

    public Task DeleteTimingAsync(int timingRecordId, CancellationToken ct = default) =>
        DeleteAsync($"api/race-day/timing/{timingRecordId}", ct);

    // ---------- UC-58 bỏ cuộc ----------

    public Task RecordWithdrawalAsync(RecordWithdrawalRequest request, CancellationToken ct = default) =>
        PostAsync("api/race-day/withdrawals", request, ct);

    public Task<List<WithdrawalDto>> GetWithdrawalsAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<WithdrawalDto>>($"api/race-day/events/{eventId}/withdrawals", ct);

    public Task CancelWithdrawalAsync(int registrationId, CancellationToken ct = default) =>
        DeleteAsync($"api/race-day/withdrawals/{registrationId}", ct);
}
