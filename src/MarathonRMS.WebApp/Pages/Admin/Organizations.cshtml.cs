using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Admin;

/// <summary>UC-10 – danh sách, sửa, tạm khóa BTC.</summary>
public class OrganizationsModel : BasePageModel
{
    private readonly AdminApiClient _admin;
    public OrganizationsModel(AdminApiClient admin) => _admin = admin;

    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int? Edit { get; set; }
    [BindProperty] public UpdateOrganizationRequest Input { get; set; } = new();
    public PagedResult<OrganizationDto> Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Data = await _admin.SearchOrganizationsAsync(Keyword, P, ct);
            if (Edit is int id)
            {
                var org = await _admin.GetOrganizationAsync(id, ct);
                Input = new UpdateOrganizationRequest
                {
                    Name = org.Name, LogoUrl = org.LogoUrl, Description = org.Description,
                    ContactEmail = org.ContactEmail, ContactPhone = org.ContactPhone
                };
            }
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (Edit is not int id) return RedirectToPage();
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            await _admin.UpdateOrganizationAsync(id, Input, ct);
            StatusMessage = "Đã cập nhật ban tổ chức.";
            return RedirectToPage(new { Keyword, P });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostSetActiveAsync(int id, bool isActive, string? reason, CancellationToken ct)
    {
        try
        {
            await _admin.SetOrganizationActiveAsync(id, new SetActiveRequest { IsActive = isActive, Reason = reason }, ct);
            StatusMessage = isActive ? "Đã mở khóa ban tổ chức." : "Đã tạm khóa ban tổ chức.";
            return RedirectToPage(new { Keyword, P });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Data = await _admin.SearchOrganizationsAsync(Keyword, P, ct);
        return Page();
    }
}
