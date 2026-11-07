using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>UC-61 – ban hành / thu hồi quyết định phạt, DSQ cho một kết quả.</summary>
public class PenaltiesModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public PenaltiesModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int ResultId { get; set; }
    [BindProperty] public IssuePenaltyRequest Input { get; set; } = new();

    public ResultPenaltiesDto Data { get; private set; } = new();
    public List<PenaltyReasonDto> Reasons { get; private set; } = new();
    public bool CanChange => Data.ResultsStatus != "Official";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostIssueAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var issued = await _results.IssuePenaltyAsync(ResultId, Input, ct);
            StatusMessage = $"Đã ban hành quyết định {ActionLabel(issued.Action)}: {issued.ReasonName}. Kết quả của giải đã được tính lại.";
            return RedirectToPage(new { resultId = ResultId });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostRevokeAsync(int penaltyId, CancellationToken ct)
    {
        try
        {
            await _results.RevokePenaltyAsync(penaltyId, ct);
            StatusMessage = "Đã thu hồi quyết định phạt. Kết quả của giải đã được tính lại.";
            return RedirectToPage(new { resultId = ResultId });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct);
        }
    }

    public static string ActionLabel(string action) => action switch
    {
        "DSQ" => "truất quyền thi đấu (DSQ)",
        "TimePenalty" => "phạt cộng giờ",
        _ => "cảnh cáo"
    };

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Data = await _results.GetPenaltiesAsync(ResultId, ct);
        Reasons = await _results.GetPenaltyReasonsAsync(ct);
    }
}
