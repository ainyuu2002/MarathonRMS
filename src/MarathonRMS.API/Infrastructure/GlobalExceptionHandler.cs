using MarathonRMS.DTOs.Common;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Chuyển exception thành ProblemDetails thống nhất. Extension "code" = mã BR-xx hoặc ErrorCodes
/// để WebApp hiển thị thông báo phù hợp (BA mục 5).
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    {
        _logger = logger;
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            BusinessRuleException br => (br.StatusCode, br.Code, br.Message),
            ForbiddenException fe => (StatusCodes.Status403Forbidden, fe.Code, fe.Message),
            NotFoundException nf => (StatusCodes.Status404NotFound, ErrorCodes.NotFound, nf.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.Conflict,
                "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại và thử lại."),
            _ when IsTruncation(exception) => (StatusCodes.Status400BadRequest, ErrorCodes.Validation,
                "Dữ liệu nhập quá dài."),
            _ => (StatusCodes.Status500InternalServerError, "SERVER_ERROR", "Đã có lỗi hệ thống.")
        };

        // Lỗi nghiệp vụ (4xx) chỉ ghi Debug; middleware mặc định đã được tắt log ở appsettings (Logging) để log chỉ còn lỗi 500 thật
        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogDebug("{Status} {Code}: {Title}", status, code, title);

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    /// <summary>
    /// SQL "String or binary data would be truncated" (2628, 8152) – vd chuỗi rất dài trong $filter của OData công khai
    /// (EF tham số hóa '%' + x + '%' vượt nvarchar(4000)) ⇒ lỗi đầu vào, không phải lỗi hệ thống.
    /// </summary>
    private static bool IsTruncation(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 2628 or 8152 })
                return true;
        return false;
    }
}
