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
}
