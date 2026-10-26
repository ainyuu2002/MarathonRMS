using System.Security.Claims;
using MarathonRMS.DTOs.Auth;
using MarathonRMS.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>UC-01: đăng ký tài khoản Member (tạo luôn hồ sơ người chạy chính chủ).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await _auth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), null, user);
    }

    /// <summary>UC-02: đăng nhập, trả JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    /// <summary>Cấp lại JWT với quyền hiện tại (sau khi được duyệt làm BTC, được mời làm Staff…).</summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct) =>
        Ok(await _auth.RefreshAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));

    /// <summary>UC-06: đổi mật khẩu.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct);
        return NoContent();
    }

    /// <summary>Thông tin tài khoản đang đăng nhập.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) =>
        Ok(await _auth.GetCurrentUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
}
