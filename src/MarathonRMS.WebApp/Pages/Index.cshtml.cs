using System.Globalization;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages;

/// <summary>UC-34 – danh sách, lọc và phân trang giải công khai qua OData.</summary>
public class IndexModel : BasePageModel
{
    private readonly EventApiClient _events;
    public IndexModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    [BindProperty(SupportsGet = true)] public string? Month { get; set; }
    [BindProperty(SupportsGet = true)] public string? Discipline { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinDistanceKm { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MaxDistanceKm { get; set; }
    [BindProperty(SupportsGet = true)] public string? Organizer { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; } = "date";
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public PagedResult<EventSummaryDto> Events { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var keyword = Keyword?.Trim();
        var city = City?.Trim();
        var organizer = Organizer?.Trim();
        var discipline = Discipline is "Road" or "Trail" ? Discipline : null;
        DateOnly? monthStart = null;
        if (!string.IsNullOrWhiteSpace(Month) &&
            DateOnly.TryParseExact($"{Month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedMonth))
            monthStart = parsedMonth;

        static string DecimalLiteral(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        static string DateLiteral(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var filter = ODataQuery.And(
            string.IsNullOrEmpty(keyword) ? null :
                $"contains(Name,{ODataQuery.Str(keyword)}) or contains(OrganizationName,{ODataQuery.Str(keyword)}) or contains(VenueName,{ODataQuery.Str(keyword)})",
            string.IsNullOrEmpty(city) ? null : $"contains(City,{ODataQuery.Str(city)})",
            monthStart is DateOnly start ? $"EventDate ge {DateLiteral(start)} and EventDate lt {DateLiteral(start.AddMonths(1))}" : null,
            discipline is null ? null : $"contains(Disciplines,{ODataQuery.Str(discipline)})",
            MinDistanceKm is decimal min ? $"MaxDistanceKm ge {DecimalLiteral(min)}" : null,
            MaxDistanceKm is decimal max ? $"MinDistanceKm le {DecimalLiteral(max)}" : null,
            string.IsNullOrEmpty(organizer) ? null : $"contains(OrganizationName,{ODataQuery.Str(organizer)})");

        var orderBy = Sort switch
        {
            "date_desc" => "EventDate desc,Name",
            "distance" => "MinDistanceKm,EventDate,Name",
            _ => "EventDate,Name"
        };

        try { Events = await _events.SearchPublicAsync(filter, orderBy, P, 12, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
