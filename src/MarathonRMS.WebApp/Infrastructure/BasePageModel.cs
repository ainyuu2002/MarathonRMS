using System.Net;
using MarathonRMS.DTOs.Common;
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
    /// BR-30 phía giao diện: dữ liệu giải đọc qua endpoint công khai, nên trang quản lý phải tự chặn giải của BTC khác
    /// (API vẫn chặn khi lưu). Ném exception riêng (không phải ApiException nên không bị các khối catch của trang nuốt)
    /// ⇒ OnPageHandlerExecuted chuyển sang trang "không có quyền", không vẽ khung trang rỗng.
    /// </summary>
    protected void EnsureOwnOrganization(int organizationId)
    {
        if (User.FindFirst(AppClaims.OrganizationId)?.Value != organizationId.ToString())
            throw new OtherOrganizationException();
    }

    public override void OnPageHandlerExecuted(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutedContext context)
    {
        if (context.Exception is OtherOrganizationException)
        {
            context.Result = RedirectToPage("/Account/AccessDenied", new { reason = "organization" });
            context.ExceptionHandled = true;
        }
        base.OnPageHandlerExecuted(context);
    }

    /// <summary>Giải thuộc ban tổ chức khác (BR-30).</summary>
    protected sealed class OtherOrganizationException : Exception
    {
        public OtherOrganizationException() : base("Bạn chỉ được quản lý dữ liệu của ban tổ chức mình.") { }
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
