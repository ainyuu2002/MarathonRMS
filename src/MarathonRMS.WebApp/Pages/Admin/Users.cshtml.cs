using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Admin;

/// <summary>UC-11 – tìm kiếm, khóa / mở tài khoản (BR-35: tài khoản quản trị do SuperAdmin quản lý).</summary>
public class UsersModel : BasePageModel
{
    private readonly AdminApiClient _admin;
    public UsersModel(AdminApiClient admin) => _admin = admin;

    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public PagedResult<UserListItemDto> Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Data = await _admin.SearchUsersAsync(Keyword, Role, P, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSetActiveAsync(string id, bool isActive, string? reason, CancellationToken ct)
    {
        try
        {
            var user = await _admin.SetUserActiveAsync(id, new SetActiveRequest { IsActive = isActive, Reason = reason }, ct);
            StatusMessage = isActive ? $"Đã mở khóa {user.FullName}." : $"Đã khóa {user.FullName}. Phiên đăng nhập của người này hết hiệu lực ngay.";
            return RedirectToPage(new { Keyword, Role, P });
        }
        catch (ApiException ex)
        {
            await HandleApiErrorAsync(ex);
            Data = await _admin.SearchUsersAsync(Keyword, Role, P, ct);
            return Page();
        }
    }

    public static bool IsAdminAccount(UserListItemDto u) => u.Roles.Contains(AppRoles.Admin) || u.Roles.Contains(AppRoles.SuperAdmin);
}
