using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.SuperAdmin;

/// <summary>UC-14 – tài khoản Admin / SuperAdmin (BR-35).</summary>
public class AdminsModel : BasePageModel
{
    private readonly SuperAdminApiClient _superAdmin;
    public AdminsModel(SuperAdminApiClient superAdmin) => _superAdmin = superAdmin;

    [BindProperty] public CreateAdminRequest Input { get; set; } = new();
    public List<UserListItemDto> Admins { get; private set; } = new();
    public string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Admins = (await _superAdmin.GetAdminsAsync(ct)).Items.ToList(); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var admin = await _superAdmin.CreateAdminAsync(Input, ct);
            StatusMessage = $"Đã tạo tài khoản Admin {admin.Email}.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public Task<IActionResult> OnPostSetActiveAsync(string id, bool isActive, CancellationToken ct) =>
        RunAsync(() => _superAdmin.SetAdminActiveAsync(id, new SetActiveRequest { IsActive = isActive }, ct),
            isActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.", ct);

    public Task<IActionResult> OnPostChangeRoleAsync(string id, string role, CancellationToken ct) =>
        RunAsync(() => _superAdmin.ChangeAdminRoleAsync(id, new ChangeAdminRoleRequest { Role = role }, ct),
            $"Đã đổi quyền thành {role}. Người này cần đăng nhập lại.", ct);

    private async Task<IActionResult> RunAsync(Func<Task<UserListItemDto>> action, string message, CancellationToken ct)
    {
        try
        {
            await action();
            StatusMessage = message;
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Admins = (await _superAdmin.GetAdminsAsync(ct)).Items.ToList();
        return Page();
    }
}
