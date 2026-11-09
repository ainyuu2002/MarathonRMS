using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần A – GĐ3: UC-69 dashboard giải, UC-71 dashboard hệ thống.</summary>
public class DashboardApiClient : ApiClientBase
{
    public DashboardApiClient(HttpClient http) : base(http) { }

    public Task<EventDashboardDto> GetEventAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventDashboardDto>($"api/events/{eventId}/dashboard", ct);

    public Task<SystemDashboardDto> GetSystemAsync(CancellationToken ct = default) =>
        GetAsync<SystemDashboardDto>("api/admin/dashboard", ct);
}
