using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Results;

/// <summary>UC-65 – Vinh danh (công khai khi kết quả chính thức; BTC xem trước danh sách nhận giải).</summary>
public class WinnersModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public WinnersModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    public EventWinnersDto? Data { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Data = await _results.GetWinnersAsync(EventId, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
