using System.Net;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Lỗi trả về từ API (đọc từ ProblemDetails). Code = mã BR-xx hoặc ErrorCodes.</summary>
public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? Code { get; }
    public IDictionary<string, string[]> ValidationErrors { get; }

    public ApiException(HttpStatusCode statusCode, string message, string? code = null,
        IDictionary<string, string[]>? validationErrors = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }
}
