using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Events;

/// <summary>UC-34 – danh sách giải công khai: tìm theo tên / BTC, thành phố, loại đường chạy, thời gian (OData).</summary>
public class IndexModel : BasePageModel
{
    private readonly EventApiClient _events;
    public IndexModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    /// <summary>Road | Trail.</summary>
    [BindProperty(SupportsGet = true)] public string? Discipline { get; set; }
    /// <summary>upcoming (mặc định) | open | past.</summary>
    [BindProperty(SupportsGet = true)] public string? When { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public PagedResult<EventSummaryDto> Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var keyword = Keyword?.Trim();
        var when = When ?? "upcoming";
        var filter = ODataQuery.And(
            when switch
            {
                "open" => "Status eq 'RegistrationOpen'",
                "past" => $"EventDate lt {today}",
                _ => $"EventDate ge {today} and Status ne 'Cancelled'"
            },
            string.IsNullOrEmpty(keyword) ? null
                : $"contains(Name,{ODataQuery.Str(keyword)}) or contains(OrganizationName,{ODataQuery.Str(keyword)})",
            string.IsNullOrWhiteSpace(City) ? null : $"contains(City,{ODataQuery.Str(City.Trim())})",
            Discipline is "Road" or "Trail" ? $"contains(Disciplines,{ODataQuery.Str(Discipline)})" : null);
        try
        {
            Data = await _events.SearchPublicAsync(filter, when == "past" ? "EventDate desc,Id" : "EventDate,Id", P, 12, ct);
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
