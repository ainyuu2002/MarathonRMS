using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Admin;

/// <summary>UC-09 – duyệt / từ chối hồ sơ BTC (BR-33).</summary>
public class OrganizationApplicationsModel : BasePageModel
{
    private readonly AdminApiClient _admin;
    public OrganizationApplicationsModel(AdminApiClient admin) => _admin = admin;

    [BindProperty(SupportsGet = true)] public string? Status { get; set; } = "Pending";
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public PagedResult<OrganizationApplicationDto> Data { get; private set; } = new();

    /// <summary>Tab "Tất cả" dùng giá trị All ⇒ không lọc.</summary>
    private string? ApiStatus => Status == "All" ? null : Status;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Data = await _admin.SearchApplicationsAsync(ApiStatus, P, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, CancellationToken ct)
    {
        try
        {
            var app = await _admin.ApproveApplicationAsync(id, ct);
            StatusMessage = $"Đã duyệt \"{app.OrganizationName}\". {app.ApplicantName} đã có quyền Ban tổ chức.";
        }
        catch (ApiException ex)
        {
            await HandleApiErrorAsync(ex);
            return await ReloadAsync(ct);
        }
        return RedirectToPage(new { Status, P });
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason, CancellationToken ct)
    {
        try
        {
            await _admin.RejectApplicationAsync(id, new RejectApplicationRequest { Reason = reason ?? "" }, ct);
            StatusMessage = "Đã từ chối hồ sơ.";
        }
        catch (ApiException ex)
        {
            await HandleApiErrorAsync(ex);
            return await ReloadAsync(ct);
        }
        return RedirectToPage(new { Status, P });
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Data = await _admin.SearchApplicationsAsync(ApiStatus, P, ct);
        return Page();
    }
}
