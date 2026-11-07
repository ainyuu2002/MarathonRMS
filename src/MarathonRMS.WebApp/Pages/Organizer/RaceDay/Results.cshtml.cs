using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>UC-59 – tính và rà soát kết quả của giải.</summary>
public class ResultsModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public ResultsModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [TempData] public string? Notes { get; set; }

    public ResultsOverviewDto Overview { get; private set; } = new();
    public PagedResult<LeaderboardRowDto> Rows { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostCalculateAsync(CancellationToken ct)
    {
        try
        {
            var summary = await _results.CalculateAsync(EventId, ct);
            StatusMessage = $"Đã tính kết quả: {summary.Finished} hoàn thành, {summary.Dnf} DNF, {summary.Dns} DNS, {summary.Dsq} DSQ.";
            Notes = summary.Warnings.Count > 0 ? string.Join("\n", summary.Warnings) : null;
            return RedirectToPage(new { eventId = EventId });
        }
        catch (ApiException ex) { return await HandleAndReloadAsync(ex, ct); }
    }

    private async Task<IActionResult> HandleAndReloadAsync(ApiException ex, CancellationToken ct)
    {
        var redirect = await HandleApiErrorAsync(ex);
        if (redirect is not null) return redirect;
        try { await LoadAsync(ct); }
        catch (ApiException loadError) { await HandleApiErrorAsync(loadError); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Overview = await _results.GetOverviewAsync(EventId, ct);
        Rows = await _results.GetRowsAsync(EventId, CategoryId, Status, Keyword, P, ct);
    }
}
