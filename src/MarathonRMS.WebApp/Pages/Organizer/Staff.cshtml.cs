using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-13 – mời / gỡ Staff (BR-34).</summary>
public class StaffModel : BasePageModel
{
    private readonly OrganizationApiClient _organizations;
    public StaffModel(OrganizationApiClient organizations) => _organizations = organizations;

    [BindProperty] public InviteStaffRequest Input { get; set; } = new();
    public List<StaffMemberDto> Members { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Members = await _organizations.GetStaffAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostInviteAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var staff = await _organizations.InviteStaffAsync(Input, ct);
            StatusMessage = $"Đã thêm {staff.FullName} làm Staff. Người này cần đăng nhập lại để nhận quyền.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostRemoveAsync(string userId, CancellationToken ct)
    {
        try
        {
            await _organizations.RemoveStaffAsync(userId, ct);
            StatusMessage = "Đã gỡ Staff khỏi ban tổ chức.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Members = await _organizations.GetStaffAsync(ct);
        return Page();
    }
}
