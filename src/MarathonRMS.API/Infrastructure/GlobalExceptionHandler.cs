using MarathonRMS.DTOs.Common;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
            _ => (StatusCodes.Status500InternalServerError, "SERVER_ERROR", "Đã có lỗi hệ thống.")
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception");

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
}
