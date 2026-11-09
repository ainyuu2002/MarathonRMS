using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>UC-49 – gán BIB theo dải số của từng cự ly.</summary>
public class BibsModel : BasePageModel
{
    private readonly RaceDayApiClient _raceDay;
    public BibsModel(RaceDayApiClient raceDay) => _raceDay = raceDay;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [TempData] public string? AssignWarnings { get; set; }

    public BibOverviewDto Overview { get; private set; } = new();
    public PagedResult<BibListItemDto> Assignments { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync(int? assignCategoryId, CancellationToken ct)
    {
        try
        {
            var result = await _raceDay.AssignBibsAsync(EventId, assignCategoryId, ct);
            StatusMessage = result.AssignedCount > 0
                ? $"Đã gán {result.AssignedCount} BIB."
                : "Không còn đăng ký đã xác nhận nào cần gán BIB.";
            AssignWarnings = result.Warnings.Count > 0 ? string.Join("\n", result.Warnings) : null;
            return RedirectToPage(new { eventId = EventId, CategoryId, Keyword });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            if (redirect is not null) return redirect;
            try { await LoadAsync(ct); }
            catch (ApiException loadError) { await HandleApiErrorAsync(loadError); }
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Overview = await _raceDay.GetBibOverviewAsync(EventId, ct);
        Assignments = await _raceDay.GetBibAssignmentsAsync(EventId, CategoryId, Keyword, P, ct);
    }
}
