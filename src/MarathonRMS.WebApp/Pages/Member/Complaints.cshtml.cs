using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-66 – khiếu nại kết quả đã gửi và trả lời của ban tổ chức.</summary>
public class ComplaintsModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public ComplaintsModel(ResultApiClient results) => _results = results;

    public List<ComplaintDto> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Items = await _results.GetMyComplaintsAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
