using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần A – UC-46 đổi cự ly, UC-47 chuyển nhượng BIB.</summary>
public class RegistrationChangeApiClient : ApiClientBase
{
    public RegistrationChangeApiClient(HttpClient http) : base(http) { }

    public Task<RegistrationChangeOptionsDto> GetOptionsAsync(int registrationId, CancellationToken ct = default) =>
        GetAsync<RegistrationChangeOptionsDto>($"api/registrations/{registrationId}/change-options", ct);

    public Task<CategoryChangeResultDto> ChangeCategoryAsync(int registrationId, ChangeCategoryRequest request, CancellationToken ct = default) =>
        PostAsync<CategoryChangeResultDto>($"api/registrations/{registrationId}/category-change", request, ct);

    public Task<TransferDto> CreateTransferAsync(int registrationId, CreateTransferRequest request, CancellationToken ct = default) =>
        PostAsync<TransferDto>($"api/registrations/{registrationId}/transfers", request, ct);

    public Task<List<TransferDto>> GetMyTransfersAsync(CancellationToken ct = default) =>
        GetAsync<List<TransferDto>>("api/transfers/me", ct);

    public Task<TransferDto> GetTransferAsync(int transferId, CancellationToken ct = default) =>
        GetAsync<TransferDto>($"api/transfers/{transferId}", ct);

    public Task<TransferDto> AcceptAsync(int transferId, AcceptTransferRequest request, CancellationToken ct = default) =>
        PostAsync<TransferDto>($"api/transfers/{transferId}/accept", request, ct);

    public Task RejectAsync(int transferId, CancellationToken ct = default) => PostAsync($"api/transfers/{transferId}/reject", new { }, ct);

    public Task CancelAsync(int transferId, CancellationToken ct = default) => PostAsync($"api/transfers/{transferId}/cancel", new { }, ct);
}
