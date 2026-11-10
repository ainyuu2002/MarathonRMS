using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-48 – danh sách đăng ký của đúng BTC qua OData.</summary>
public class RegistrationsModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    private readonly EventApiClient _events;
    public RegistrationsModel(RegistrationApiClient registrations, EventApiClient events)
    {
        _registrations = registrations; _events = events;
    }

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<EventSummaryDto> Events { get; private set; } = new();
    public List<RaceCategoryDto> Categories { get; private set; } = new();
    public PagedResult<RegistrationListItemDto> Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Events = await _events.GetMineAsync(ct);
            if (EventId is int eventId && Events.Any(e => e.Id == eventId))
                Categories = await _events.GetCategoriesAsync(eventId, ct);
            else
            {
                EventId = null;
                CategoryId = null;
            }
            var keyword = Keyword?.Trim();
            var filter = ODataQuery.And(
                EventId is int e ? $"EventId eq {e}" : null,
                CategoryId is int c ? $"CategoryId eq {c}" : null,
                string.IsNullOrWhiteSpace(Status) ? null : $"Status eq {ODataQuery.Str(Status)}",
                string.IsNullOrWhiteSpace(keyword) ? null
                    : $"contains(FullName,{ODataQuery.Str(keyword)}) or contains(BibName,{ODataQuery.Str(keyword)}) or contains(Phone,{ODataQuery.Str(keyword)}) or contains(Email,{ODataQuery.Str(keyword)})");
            Data = await _registrations.SearchForOrganizerAsync(filter, P, 20, ct);
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
