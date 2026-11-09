using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>UC-18 – quản lý giải của Organizer.</summary>
public class EventApiClient : ApiClientBase
{
    public EventApiClient(HttpClient http) : base(http) { }

    /// <summary>UC-34 – danh sách giải công khai qua OData.</summary>
    public Task<MarathonRMS.DTOs.Common.PagedResult<EventSummaryDto>> SearchPublicAsync(string? filter, string orderBy, int page, int pageSize,
        CancellationToken ct = default) => GetODataPageAsync<EventSummaryDto>("Events", filter, orderBy, page, pageSize, ct);

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

    public Task ChangeStatusAsync(int id, string status, CancellationToken ct = default) =>
        PatchAsync($"api/events/{id}/status", new ChangeEventStatusRequest { Status = status }, ct);

    public Task<EventPolicyDto> GetPolicyAsync(int id, CancellationToken ct = default) =>
        GetAsync<EventPolicyDto>($"api/events/{id}/policies", ct);

    public Task<EventPolicyDto> UpdatePolicyAsync(int id, UpdateEventPolicyRequest request, CancellationToken ct = default) =>
        PutAsync<EventPolicyDto>($"api/events/{id}/policies", request, ct);

    public Task<EventWaiverDto> GetWaiverAsync(int id, CancellationToken ct = default) =>
        GetAsync<EventWaiverDto>($"api/events/{id}/waiver", ct);

    public Task<EventWaiverDto> UpdateWaiverAsync(int id, UpdateEventWaiverRequest request, CancellationToken ct = default) =>
        PutAsync<EventWaiverDto>($"api/events/{id}/waiver", request, ct);

    public Task<List<RaceCategoryDto>> GetCategoriesAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<RaceCategoryDto>>($"api/events/{eventId}/categories", ct);

    public Task<RaceCategoryDto> CreateCategoryAsync(int eventId, UpsertRaceCategoryRequest request,
        CancellationToken ct = default) => PostAsync<RaceCategoryDto>($"api/events/{eventId}/categories", request, ct);

    public Task<RaceCategoryDto> UpdateCategoryAsync(int categoryId, UpsertRaceCategoryRequest request,
        CancellationToken ct = default) => PutAsync<RaceCategoryDto>($"api/categories/{categoryId}", request, ct);

    public Task DeleteCategoryAsync(int categoryId, CancellationToken ct = default) =>
        DeleteAsync($"api/categories/{categoryId}", ct);

    // UC-22 – mức giá
    public Task<PriceTierDto> CreatePriceTierAsync(int categoryId, UpsertPriceTierRequest request, CancellationToken ct = default) =>
        PostAsync<PriceTierDto>($"api/categories/{categoryId}/price-tiers", request, ct);

    public Task<PriceTierDto> UpdatePriceTierAsync(int id, UpsertPriceTierRequest request, CancellationToken ct = default) =>
        PutAsync<PriceTierDto>($"api/price-tiers/{id}", request, ct);

    public Task DeletePriceTierAsync(int id, CancellationToken ct = default) => DeleteAsync($"api/price-tiers/{id}", ct);

    // UC-23 – nhóm tuổi
    public Task<List<AgeGroupDto>> GetAgeGroupsAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<AgeGroupDto>>($"api/events/{eventId}/age-groups", ct);

    public Task<AgeGroupDto> CreateAgeGroupAsync(int eventId, UpsertAgeGroupRequest request, CancellationToken ct = default) =>
        PostAsync<AgeGroupDto>($"api/events/{eventId}/age-groups", request, ct);

    public Task<AgeGroupDto> UpdateAgeGroupAsync(int id, UpsertAgeGroupRequest request, CancellationToken ct = default) =>
        PutAsync<AgeGroupDto>($"api/age-groups/{id}", request, ct);

    public Task DeleteAgeGroupAsync(int id, CancellationToken ct = default) => DeleteAsync($"api/age-groups/{id}", ct);

    // UC-24 – checkpoint
    public Task<CheckpointDto> CreateCheckpointAsync(int categoryId, UpsertCheckpointRequest request, CancellationToken ct = default) =>
        PostAsync<CheckpointDto>($"api/categories/{categoryId}/checkpoints", request, ct);

    public Task<CheckpointDto> UpdateCheckpointAsync(int id, UpsertCheckpointRequest request, CancellationToken ct = default) =>
        PutAsync<CheckpointDto>($"api/checkpoints/{id}", request, ct);

    public Task DeleteCheckpointAsync(int id, CancellationToken ct = default) => DeleteAsync($"api/checkpoints/{id}", ct);
}
