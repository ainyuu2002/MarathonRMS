using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>Phần A – UC-69 (GĐ3): dashboard của một giải.</summary>
public class DashboardModel : BasePageModel
{
    private readonly DashboardApiClient _api;
    public DashboardModel(DashboardApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    public EventDashboardDto Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Data = await _api.GetEventAsync(Id, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
