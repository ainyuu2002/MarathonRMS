using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Results;

/// <summary>UC-63 – kết quả cá nhân: thành tích, hạng, thời gian từng checkpoint, quyết định phạt.</summary>
public class DetailModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public DetailModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int RegistrationId { get; set; }
    public ResultDetailDto? Detail { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Detail = await _results.GetDetailAsync(RegistrationId, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
