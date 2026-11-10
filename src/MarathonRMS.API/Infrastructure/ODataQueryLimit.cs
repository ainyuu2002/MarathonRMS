using MarathonRMS.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Chặn $filter / $orderby / $select quá dài trên route OData trước khi xuống DB: literal rất dài làm SQL báo
/// "String or binary data would be truncated" (vượt nvarchar(4000)). GlobalExceptionHandler vẫn đổi lỗi đó thành 400,
/// nhưng chặn sớm thì không tốn truy vấn và log không còn dòng lỗi của EF.
/// </summary>
public static class ODataQueryLimit
{
    public const int MaxOptionLength = 1000;

    public static IApplicationBuilder UseODataQueryLimit(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/odata") &&
            context.Request.Query.Any(q => q.Key.StartsWith('$') && q.Value.ToString().Length > MaxOptionLength))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = $"Điều kiện truy vấn quá dài (tối đa {MaxOptionLength} ký tự)."
            };
            problem.Extensions["code"] = ErrorCodes.Validation;
            await context.RequestServices.GetRequiredService<IProblemDetailsService>()
                .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }
        await next();
    });
}
