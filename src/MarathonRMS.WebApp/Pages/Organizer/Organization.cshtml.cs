using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-16 – thông tin ban tổ chức của mình.</summary>
public class OrganizationModel : BasePageModel
{
    private readonly OrganizationApiClient _organizations;
    public OrganizationModel(OrganizationApiClient organizations) => _organizations = organizations;

    [BindProperty] public UpdateMyOrganizationRequest Input { get; set; } = new();
    public OrganizationDto? Organization { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Organization = await _organizations.GetMineAsync(ct);
            Input = new UpdateMyOrganizationRequest
            {
                LogoUrl = Organization.LogoUrl, Description = Organization.Description,
                ContactEmail = Organization.ContactEmail, ContactPhone = Organization.ContactPhone
            };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _organizations.UpdateMineAsync(Input, ct);
                StatusMessage = "Đã cập nhật thông tin ban tổ chức.";
                return RedirectToPage();
            }
        }
        catch (ApiException ex)
        {
            if (await HandleApiErrorAsync(ex, nameof(Input)) is { } redirect) return redirect;
        }
        try { Organization = await _organizations.GetMineAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }
}
