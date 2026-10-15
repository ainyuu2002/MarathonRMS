using MarathonRMS.DTOs.Common;

namespace MarathonRMS.Services.Common;

/// <summary>Vi phạm quy tắc nghiệp vụ. Code = mã BR-xx của BA hoặc mã trong ErrorCodes. → HTTP 409 (mặc định) / 400.</summary>
public class BusinessRuleException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public BusinessRuleException(string code, string message, int statusCode = 409) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

/// <summary>Không đủ quyền: sai role, khác BTC (BR-30) hoặc xung đột lợi ích (BR-32). → HTTP 403.</summary>
public class ForbiddenException : Exception
{
    public string Code { get; }
    public ForbiddenException(string message, string code = ErrorCodes.Forbidden) : base(message) => Code = code;
}

/// <summary>→ HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
