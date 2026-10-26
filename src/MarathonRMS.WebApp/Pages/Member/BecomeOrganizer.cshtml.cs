using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-08 – nộp hồ sơ đăng ký làm BTC và theo dõi trạng thái.</summary>
public class BecomeOrganizerModel : BasePageModel
{
    private readonly OrganizationApiClient _organizations;
    private readonly AuthApiClient _auth;

    public BecomeOrganizerModel(OrganizationApiClient organizations, AuthApiClient auth)
    {
        _organizations = organizations;
        _auth = auth;
    }

    [BindProperty] public CreateOrganizationApplicationRequest Input { get; set; } = new();
    public List<OrganizationApplicationDto> Applications { get; private set; } = new();

    public bool HasPending => Applications.Any(a => a.Status == "Pending");
    /// <summary>Đã được duyệt nhưng cookie hiện tại chưa có role Organizer ⇒ cần làm mới phiên.</summary>
    public bool NeedsSessionRefresh => Applications.Any(a => a.Status == "Approved") && !User.IsInRole(AppRoles.Organizer);
    public bool AlreadyInOrganization => User.HasClaim(c => c.Type == AppClaims.OrganizationId);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Applications = await _organizations.GetMyApplicationsAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        Input.ContactEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
        Input.RepresentativeName = User.Identity?.Name ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            await _organizations.SubmitApplicationAsync(Input, ct);
            StatusMessage = "Đã gửi hồ sơ. Quản trị viên sẽ xem xét và phản hồi.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    /// <summary>Nhận quyền Organizer ngay sau khi được duyệt, không cần đăng xuất.</summary>
    public async Task<IActionResult> OnPostRefreshSessionAsync(CancellationToken ct)
    {
        try
        {
            await RefreshSessionAsync(_auth, ct);
            StatusMessage = "Đã kích hoạt quyền Ban tổ chức.";
            return RedirectToPage("/Organizer/Index");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Applications = await _organizations.GetMyApplicationsAsync(ct);
        return Page();
    }
}
