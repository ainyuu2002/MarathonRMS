using MarathonRMS.DTOs.Auth;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

public class AuthApiClient : ApiClientBase
{
    public AuthApiClient(HttpClient http) : base(http) { }

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        PostAsync<AuthResponse>("api/auth/login", request, ct);

    public Task<CurrentUserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        PostAsync<CurrentUserDto>("api/auth/register", request, ct);

    public Task<CurrentUserDto> GetMeAsync(CancellationToken ct = default) =>
        GetAsync<CurrentUserDto>("api/auth/me", ct);
}

public class LegalApiClient : ApiClientBase
{
    public LegalApiClient(HttpClient http) : base(http) { }

    public Task<LegalDocumentDto> GetCurrentAsync(string docType, CancellationToken ct = default) =>
        GetAsync<LegalDocumentDto>($"api/legal/{docType}", ct);
}
