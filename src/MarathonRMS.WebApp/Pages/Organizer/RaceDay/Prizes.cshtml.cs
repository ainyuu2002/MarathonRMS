using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>UC-28 – cơ cấu giải thưởng theo cự ly (chung cuộc / nhóm tuổi / quốc tịch × giới tính × hạng).</summary>
public class PrizesModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public PrizesModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public SavePrizeRequest Input { get; set; } = new();

    public EventPrizesDto Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var prize = await _results.CreatePrizeAsync(CategoryId, Input, ct);
            StatusMessage = $"Đã thêm giải {prize.CategoryName}: hạng {prize.Rank} – {prize.Reward}.";
            return RedirectToPage(new { eventId = EventId });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int prizeId, CancellationToken ct)
    {
        try
        {
            await _results.DeletePrizeAsync(prizeId, ct);
            StatusMessage = "Đã xóa giải thưởng.";
            return RedirectToPage(new { eventId = EventId });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct);
        }
    }

    public static string TypeLabel(string type) => type switch
    {
        "AgeGroup" => "Nhóm tuổi",
        "Nationality" => "Quốc tịch",
        _ => "Chung cuộc"
    };

    public static string Describe(PrizeDto p) =>
        $"{TypeLabel(p.AwardType)}{(p.AgeGroupName is null ? "" : " " + p.AgeGroupName)}" +
        $"{(p.Nationality is null ? "" : " " + p.Nationality)} · {RaceTime.GenderLabel(p.Gender)} · hạng {p.Rank}";

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct) => Data = await _results.GetEventPrizesAsync(EventId, ct);
}
