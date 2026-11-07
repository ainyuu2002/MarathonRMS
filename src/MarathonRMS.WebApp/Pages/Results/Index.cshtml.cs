using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Results;

/// <summary>UC-62 – bảng xếp hạng công khai theo giải, cự ly, giới tính, nhóm tuổi.</summary>
public class IndexModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public IndexModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Gender { get; set; }
    [BindProperty(SupportsGet = true)] public int? AgeGroupId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<ResultsEventDto> Events { get; private set; } = new();
    public ResultsEventDto? Event { get; private set; }
    public IEnumerable<AgeGroupOptionDto> AgeGroups =>
        Event?.AgeGroups.Where(g => g.CategoryId is null || g.CategoryId == CategoryId) ?? Enumerable.Empty<AgeGroupOptionDto>();
    public PagedResult<LeaderboardRowDto> Rows { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Events = await _results.GetPublishedEventsAsync(ct);
            Event = Events.FirstOrDefault(e => e.EventId == EventId) ?? Events.FirstOrDefault();
            if (Event is null) return Page();

            EventId = Event.EventId;
            if (CategoryId is null || Event.Categories.All(c => c.Id != CategoryId))
                CategoryId = Event.Categories.FirstOrDefault()?.Id;
            if (AgeGroupId is int ageGroupId && AgeGroups.All(g => g.Id != ageGroupId))
                AgeGroupId = null;

            Rows = await _results.GetLeaderboardAsync(Event.EventId, CategoryId, Gender, AgeGroupId, Keyword, P, ct);
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
