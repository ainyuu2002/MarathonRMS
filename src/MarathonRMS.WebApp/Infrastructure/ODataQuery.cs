using System.Globalization;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Dựng biểu thức $filter OData an toàn (escape dấu nháy, định dạng ngày giờ VN +07:00).</summary>
public static class ODataQuery
{
    /// <summary>Chuỗi OData: 'O''Brien'.</summary>
    public static string Str(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>Thời điểm theo giờ VN, VD 2026-10-26T00:00:00+07:00.</summary>
    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+07:00";

    /// <summary>Nối các điều kiện khác rỗng bằng "and"; trả null nếu không có điều kiện nào.</summary>
    public static string? And(params string?[] conditions)
    {
        var parts = conditions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        return parts.Count == 0 ? null : string.Join(" and ", parts.Select(p => $"({p})"));
    }
}
