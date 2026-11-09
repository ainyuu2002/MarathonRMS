using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Runners;

/// <summary>UC-64 – hồ sơ thành tích công khai (khi người chạy bật "hiện tên công khai" – BR-37).</summary>
public class ProfileModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public ProfileModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int ParticipantId { get; set; }
    public AchievementProfileDto? Profile { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Profile = await _results.GetRunnerAsync(ParticipantId, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
