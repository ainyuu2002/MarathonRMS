using MarathonRMS.DTOs.Auth;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Account;

/// <summary>UC-06 – đổi mật khẩu (mọi role).</summary>
public class ChangePasswordModel : BasePageModel
{
    private readonly AuthApiClient _auth;
    public ChangePasswordModel(AuthApiClient auth) => _auth = auth;

    [BindProperty] public ChangePasswordRequest Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await _auth.ChangePasswordAsync(Input, ct);
            StatusMessage = "Đã đổi mật khẩu.";
            return RedirectToPage("/Index");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? Page(); }
    }
}
