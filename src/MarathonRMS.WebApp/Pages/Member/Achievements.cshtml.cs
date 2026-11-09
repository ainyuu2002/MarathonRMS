using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-64 – hồ sơ thành tích của tôi.</summary>
public class AchievementsModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public AchievementsModel(ResultApiClient results) => _results = results;

    public AchievementProfileDto Profile { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Profile = await _results.GetMyAchievementsAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
