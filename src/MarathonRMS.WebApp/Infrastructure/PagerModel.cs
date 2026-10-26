namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Model cho partial _Pager. RouteValues: các tham số lọc giữ lại khi chuyển trang (VD keyword, status).</summary>
public record PagerModel(int Page, int TotalPages, int TotalCount, IDictionary<string, string?> RouteValues)
{
    public static PagerModel From<T>(MarathonRMS.DTOs.Common.PagedResult<T> paged, object? routeValues = null) =>
        new(paged.Page, paged.TotalPages, paged.TotalCount,
            routeValues?.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(routeValues)?.ToString())
            ?? new Dictionary<string, string?>());
}
