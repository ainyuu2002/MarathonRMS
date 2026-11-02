using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-18/20 – danh sách, xóa và chuyển trạng thái giải của BTC.</summary>
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

    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string status, CancellationToken ct)
    {
        try
        {
            await _events.ChangeStatusAsync(id, status, ct);
            StatusMessage = "Đã chuyển trạng thái giải.";
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
