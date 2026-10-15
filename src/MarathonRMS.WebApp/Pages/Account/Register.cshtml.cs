using MarathonRMS.DTOs.Auth;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Account;

public class RegisterModel : BasePageModel
{
    private readonly AuthApiClient _auth;
    public RegisterModel(AuthApiClient auth) => _auth = auth;

    [BindProperty] public RegisterRequest Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await _auth.RegisterAsync(Input, ct);
            var auth = await _auth.LoginAsync(new LoginRequest { Email = Input.Email, Password = Input.Password }, ct);
            await AuthCookieSignIn.SignInAsync(HttpContext, auth);
            StatusMessage = "Đăng ký thành công. Hãy cập nhật hồ sơ để sẵn sàng đăng ký giải.";
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
            return RedirectToPage("/Member/Index");
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? Page();
        }
    }
}
