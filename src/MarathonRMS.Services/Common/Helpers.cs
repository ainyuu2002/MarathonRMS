using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MarathonRMS.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Common;

public static class SlugHelper
{
    /// <summary>"Hà Nội Marathon 2026" ⇒ "ha-noi-marathon-2026".</summary>
    public static string ToSlug(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        var slug = Regex.Replace(sb.ToString(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 180 ? slug[..180].Trim('-') : slug;
    }
}

public static class PagingExtensions
{
    public const int MaxPageSize = 100;

    /// <summary>Phân trang phía server cho các endpoint REST (OData dùng $top/$skip).</summary>
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}

public static class EnumParser
{
    /// <summary>Chuyển chuỗi từ DTO sang enum; chuỗi null/rỗng ⇒ null. DTO đã có [AllowedValues] nên lỗi ở đây là lỗi lập trình.</summary>
    public static TEnum? ParseOptional<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<TEnum>(value, ignoreCase: true);
}
