using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-18 – danh sách và xóa giải của BTC.</summary>
public class IndexModel : BasePageModel
{
    private readonly EventApiClient _events;
    public IndexModel(EventApiClient events) => _events = events;

    public List<EventSummaryDto> Events { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Events = await _events.GetMineAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _events.DeleteEventAsync(id, ct);
            StatusMessage = "Đã xóa giải.";
            return RedirectToPage();
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            if (redirect is not null) return redirect;
            Events = await _events.GetMineAsync(ct);
            return Page();
        }
    }
}
