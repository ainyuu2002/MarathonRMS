using System.Security.Claims;
using MarathonRMS.DTOs.Auth;
using MarathonRMS.DTOs.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Tạo cookie đăng nhập từ kết quả login của API: claims lấy từ thông tin user, JWT cất trong claim access_token.</summary>
public static class AuthCookieSignIn
{
    public static Task SignInAsync(HttpContext http, AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.Id),
            new(ClaimTypes.Name, auth.User.FullName),
            new(ClaimTypes.Email, auth.User.Email),
            new(AppClaims.AccessToken, auth.AccessToken)
        };
        claims.AddRange(auth.User.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (auth.User.OrganizationId is int orgId)
            claims.Add(new Claim(AppClaims.OrganizationId, orgId.ToString()));
        if (auth.User.OrganizationName is { } orgName)
            claims.Add(new Claim("org_name", orgName));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTime.SpecifyKind(auth.ExpiresAt, DateTimeKind.Utc)
        });
    }

    /// <summary>Trang mặc định sau đăng nhập theo role cao nhất.</summary>
    public static string HomePageFor(IEnumerable<string> roles)
    {
        var set = roles.ToHashSet();
        if (set.Contains(AppRoles.SuperAdmin)) return "/SuperAdmin/Index";
        if (set.Contains(AppRoles.Admin)) return "/Admin/Index";
        if (set.Contains(AppRoles.Organizer)) return "/Organizer/Index";
        if (set.Contains(AppRoles.Staff)) return "/Staff/Index";
        return "/Member/Index";
    }
}
