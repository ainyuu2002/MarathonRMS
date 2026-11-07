using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Staff;

/// <summary>UC-52 – tra cứu VĐV tại quầy race kit.</summary>
public class RaceKitModel : BasePageModel
{
    private readonly RaceDayApiClient _raceDay;
    public RaceKitModel(RaceDayApiClient raceDay) => _raceDay = raceDay;

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }

    public List<EventSummaryDto> Events { get; private set; } = new();
    public List<RaceKitLookupItemDto> Results { get; private set; } = new();
    public bool Searched { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Events = await _raceDay.GetOperationalEventsAsync(ct);
        EventId ??= Events.FirstOrDefault()?.Id;
        if (EventId is int eventId && !string.IsNullOrWhiteSpace(Keyword))
        {
            Results = await _raceDay.LookupRaceKitAsync(eventId, Keyword.Trim(), ct);
            Searched = true;
        }
    }
}
