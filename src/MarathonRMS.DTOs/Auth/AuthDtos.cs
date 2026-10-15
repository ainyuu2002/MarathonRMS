using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
    public string Password { get; set; } = "";

    /// <summary>BR-09: bắt buộc đồng ý điều khoản – không được tick sẵn.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý Điều khoản sử dụng")]
    public bool AcceptTerms { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý Chính sách quyền riêng tư")]
    public bool AcceptPrivacy { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = "";
}

public class AuthResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public CurrentUserDto User { get; set; } = new();
}

public class CurrentUserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public IList<string> Roles { get; set; } = new List<string>();
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
}
