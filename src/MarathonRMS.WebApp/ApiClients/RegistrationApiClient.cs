using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

public class RegistrationApiClient : ApiClientBase
{
    public RegistrationApiClient(HttpClient http) : base(http) { }

    public Task<MyRegistrationDto> CreateAsync(CreateRegistrationRequest request, CancellationToken ct = default) =>
        PostAsync<MyRegistrationDto>("api/registrations", request, ct);

    public Task<List<MyRegistrationDto>> GetMineAsync(CancellationToken ct = default) =>
        GetAsync<List<MyRegistrationDto>>("api/registrations/me", ct);

    public Task<PaymentResultDto> PayAsync(CreatePaymentRequest request, CancellationToken ct = default) =>
        PostAsync<PaymentResultDto>("api/payments", request, ct);

    public Task<MyRegistrationDto> UpdateInfoAsync(int id, UpdateRegistrationInfoRequest request,
        CancellationToken ct = default) => PutAsync<MyRegistrationDto>($"api/registrations/{id}/info", request, ct);

    public Task CancelAsync(int id, CancellationToken ct = default) =>
        PostAsync($"api/registrations/{id}/cancel", new { }, ct);

    public Task<MarathonRMS.DTOs.Common.PagedResult<RegistrationListItemDto>> SearchForOrganizerAsync(
        string? filter, int page, int pageSize = 20, CancellationToken ct = default) =>
        GetODataPageAsync<RegistrationListItemDto>("Registrations", filter, "RegisteredAt desc,Id desc", page, pageSize, ct);

    public Task UpdateLockedInfoAsync(int id, UpdateLockedInfoRequest request, CancellationToken ct = default) =>
        PutAsync($"api/organizer/registrations/{id}/locked-info", request, ct);
}
