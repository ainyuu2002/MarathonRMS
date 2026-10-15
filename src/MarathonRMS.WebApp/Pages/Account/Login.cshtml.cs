using MarathonRMS.DTOs.Auth;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Account;

public class LoginModel : BasePageModel
{
    private readonly AuthApiClient _auth;
    public LoginModel(AuthApiClient auth) => _auth = auth;

    [BindProperty] public LoginRequest Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(Url.Content("~/"));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            var auth = await _auth.LoginAsync(Input, ct);
            await AuthCookieSignIn.SignInAsync(HttpContext, auth);
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
            return RedirectToPage(AuthCookieSignIn.HomePageFor(auth.User.Roles));
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? Page();
        }
    }
}
