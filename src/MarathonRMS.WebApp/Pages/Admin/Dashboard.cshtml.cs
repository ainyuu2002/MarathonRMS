using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Admin;

/// <summary>Phần A – UC-71 (GĐ3): dashboard toàn hệ thống.</summary>
public class DashboardModel : BasePageModel
{
    private readonly DashboardApiClient _api;
    public DashboardModel(DashboardApiClient api) => _api = api;

    public SystemDashboardDto Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Data = await _api.GetSystemAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
