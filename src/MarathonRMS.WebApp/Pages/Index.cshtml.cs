using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarathonRMS.WebApp.Pages;

/// <summary>Trang chủ: giới thiệu + các giải sắp diễn ra (UC-34).</summary>
public class IndexModel : PageModel
{
    private readonly EventApiClient _events;
    public IndexModel(EventApiClient events) => _events = events;

    public IReadOnlyList<EventSummaryDto> Upcoming { get; private set; } = Array.Empty<EventSummaryDto>();
    public bool ApiUnavailable { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            Upcoming = (await _events.SearchPublicAsync($"EventDate ge {today} and Status ne 'Cancelled'", "EventDate,Id", 1, 8, ct)).Items;
        }
        catch (ApiException) { ApiUnavailable = true; }
    }
}
