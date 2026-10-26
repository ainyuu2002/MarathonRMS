using System.Net;
using MarathonRMS.WebApp.ApiClients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>PageModel cơ sở: gom xử lý lỗi API (hiển thị thông báo, đẩy lỗi validation vào ModelState).</summary>
public abstract class BasePageModel : PageModel
{
    [TempData] public string? StatusMessage { get; set; }

    /// <summary>
    /// Đưa ApiException vào ModelState để _ApiErrorAlert/asp-validation hiển thị.
    /// Trả về redirect tới trang đăng nhập nếu JWT hết hạn (401).
    /// </summary>
    protected async Task<IActionResult?> HandleApiErrorAsync(ApiException ex, string? modelPrefix = null)
    {
        if (ex.StatusCode == HttpStatusCode.Unauthorized && User.Identity?.IsAuthenticated == true)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login", new { returnUrl = Request.Path + Request.QueryString });
        }

        foreach (var (field, messages) in ex.ValidationErrors)
            foreach (var m in messages)
                ModelState.AddModelError(string.IsNullOrEmpty(modelPrefix) ? field : $"{modelPrefix}.{field}", m);

        if (ex.ValidationErrors.Count == 0)
            ModelState.AddModelError(string.Empty, ex.Message);
        return null;
    }

    /// <summary>
    /// Lấy JWT mới với role/BTC hiện tại và đăng nhập lại cookie – dùng sau khi quyền thay đổi
    /// (được duyệt làm BTC, được mời làm Staff) mà không bắt người dùng đăng xuất.
    /// </summary>
    protected async Task RefreshSessionAsync(AuthApiClient auth, CancellationToken ct = default)
    {
        var refreshed = await auth.RefreshAsync(ct);
        await AuthCookieSignIn.SignInAsync(HttpContext, refreshed);
    }
}
