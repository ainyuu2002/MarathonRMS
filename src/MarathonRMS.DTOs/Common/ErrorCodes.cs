namespace MarathonRMS.DTOs.Common;

/// <summary>
/// Mã lỗi trả về trong ProblemDetails (extension "code"). Lỗi nghiệp vụ dùng mã BR-xx của BA;
/// các mã dưới đây là lỗi chung. WebApp dựa vào mã để hiển thị thông báo.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "VALIDATION";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string Conflict = "CONFLICT";
    public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AccountDisabled = "AUTH_ACCOUNT_DISABLED";
    public const string EmailTaken = "AUTH_EMAIL_TAKEN";
}
