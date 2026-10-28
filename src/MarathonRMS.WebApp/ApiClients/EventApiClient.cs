using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>UC-18 – quản lý giải của Organizer.</summary>
public class EventApiClient : ApiClientBase
{
    public EventApiClient(HttpClient http) : base(http) { }

    public Task<List<EventSummaryDto>> GetMineAsync(CancellationToken ct = default) =>
        GetAsync<List<EventSummaryDto>>("api/events/mine", ct);

    public Task<EventDetailDto> GetAsync(int id, CancellationToken ct = default) =>
        GetAsync<EventDetailDto>($"api/events/{id}", ct);

    public Task<EventDetailDto> CreateAsync(UpsertEventRequest request, CancellationToken ct = default) =>
        PostAsync<EventDetailDto>("api/events", request, ct);

    public Task<EventDetailDto> UpdateAsync(int id, UpsertEventRequest request, CancellationToken ct = default) =>
        PutAsync<EventDetailDto>($"api/events/{id}", request, ct);

    public Task DeleteEventAsync(int id, CancellationToken ct = default) =>
        DeleteAsync($"api/events/{id}", ct);

    public Task<EventPolicyDto> GetPolicyAsync(int id, CancellationToken ct = default) =>
        GetAsync<EventPolicyDto>($"api/events/{id}/policies", ct);

    public Task<EventPolicyDto> UpdatePolicyAsync(int id, UpdateEventPolicyRequest request, CancellationToken ct = default) =>
        PutAsync<EventPolicyDto>($"api/events/{id}/policies", request, ct);

    public Task<EventWaiverDto> GetWaiverAsync(int id, CancellationToken ct = default) =>
        GetAsync<EventWaiverDto>($"api/events/{id}/waiver", ct);

    public Task<EventWaiverDto> UpdateWaiverAsync(int id, UpdateEventWaiverRequest request, CancellationToken ct = default) =>
        PutAsync<EventWaiverDto>($"api/events/{id}/waiver", request, ct);
}
