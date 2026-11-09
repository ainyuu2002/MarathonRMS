using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>UC-39, 41, 43–45 (Member) và UC-48 (Organizer).</summary>
public class RegistrationApiClient : ApiClientBase
{
    public RegistrationApiClient(HttpClient http) : base(http) { }

    /// <summary>Cự ly công khai của giải (giá hiện hành, suất còn lại) – để dựng form đăng ký.</summary>
    public Task<List<RaceCategoryDto>> GetCategoriesAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<RaceCategoryDto>>($"api/events/{eventId}/categories", ct);

    public Task<EventPolicyDto> GetPolicyAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventPolicyDto>($"api/events/{eventId}/policies", ct);

    public Task<EventWaiverDto> GetWaiverAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventWaiverDto>($"api/events/{eventId}/waiver", ct);

    public Task<MyRegistrationDto> CreateAsync(CreateRegistrationRequest request, CancellationToken ct = default) =>
        PostAsync<MyRegistrationDto>("api/registrations", request, ct);

    public Task<List<MyRegistrationDto>> GetMineAsync(CancellationToken ct = default) =>
        GetAsync<List<MyRegistrationDto>>("api/registrations/me", ct);

    public Task<MyRegistrationDto> UpdateInfoAsync(int id, UpdateRegistrationInfoRequest request, CancellationToken ct = default) =>
        PutAsync<MyRegistrationDto>($"api/registrations/{id}/info", request, ct);

    public Task CancelAsync(int id, CancellationToken ct = default) => PostAsync($"api/registrations/{id}/cancel", new { }, ct);

    public Task<PaymentResultDto> PayMockAsync(int registrationId, CancellationToken ct = default) =>
        PostAsync<PaymentResultDto>("api/payments", new CreatePaymentRequest { RegistrationId = registrationId }, ct);

    // ---------- Organizer ----------
    public Task<PagedResult<RegistrationListItemDto>> SearchAsync(string? filter, int page, int pageSize, CancellationToken ct = default) =>
        GetODataPageAsync<RegistrationListItemDto>("Registrations", filter, "RegisteredAt desc,Id desc", page, pageSize, ct);

    public Task UpdateLockedInfoAsync(int id, UpdateLockedInfoRequest request, CancellationToken ct = default) =>
        PutAsync($"api/registrations/{id}/locked-info", request, ct);
}
