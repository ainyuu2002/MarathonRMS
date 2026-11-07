using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>Phần C – chọn giải để vào các chức năng ngày thi.</summary>
public class IndexModel : BasePageModel
{
    private readonly EventApiClient _events;
    public IndexModel(EventApiClient events) => _events = events;

    public List<EventSummaryDto> Events { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Events = (await _events.GetMineAsync(ct)).Where(e => e.Status != "Draft").ToList();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
