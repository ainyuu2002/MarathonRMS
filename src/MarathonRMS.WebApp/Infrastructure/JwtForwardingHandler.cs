using System.Net.Http.Headers;
using MarathonRMS.DTOs.Common;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>
/// Gắn JWT (cất trong cookie đăng nhập) vào mọi request gửi tới API (BA mục 7.3).
/// Trình duyệt không bao giờ cầm JWT.
/// </summary>
public class JwtForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    public JwtForwardingHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _accessor.HttpContext?.User.FindFirst(AppClaims.AccessToken)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
