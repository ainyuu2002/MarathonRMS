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
    [BindProperty] public CreateComplaintRequest Complaint { get; set; } = new();
    public ResultDetailDto? Detail { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Detail = await _results.GetDetailAsync(RegistrationId, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    /// <summary>UC-66 – chính VĐV / người đăng ký hộ gửi khiếu nại khi kết quả đang tạm thời.</summary>
    public async Task<IActionResult> OnPostComplainAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Account/Login");
        try
        {
            Detail = await _results.GetDetailAsync(RegistrationId, ct);
            if (!ModelState.IsValid) return Page();
            await _results.CreateComplaintAsync(Detail.Summary.ResultId, Complaint, ct);
            StatusMessage = "Đã gửi khiếu nại. Ban tổ chức sẽ trả lời trước khi chốt kết quả chính thức.";
            return RedirectToPage("/Member/Complaints");
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Complaint)) ?? Page();
        }
    }
}
