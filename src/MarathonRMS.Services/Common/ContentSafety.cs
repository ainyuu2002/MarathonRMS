using Ganss.Xss;

namespace MarathonRMS.Services.Common;

/// <summary>
/// Lọc HTML do người dùng nhập trước khi lưu (BR-50): chỉ giữ thẻ định dạng an toàn,
/// bỏ script, iframe, thuộc tính sự kiện, javascript: URL. Dùng chung: văn bản pháp lý (A), mô tả giải (B).
/// </summary>
public interface IHtmlContentSanitizer
{
    string? Sanitize(string? html);
}

public class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li",
                     "blockquote", "a", "img", "span", "table", "thead", "tbody", "tr", "th", "td", "hr" })
            _sanitizer.AllowedTags.Add(tag);
        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href", "src", "alt", "title", "class", "target", "rel" })
            _sanitizer.AllowedAttributes.Add(attr);
        _sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "https", "http", "mailto" })
            _sanitizer.AllowedSchemes.Add(scheme);
        _sanitizer.AllowedCssProperties.Clear();
    }

    public string? Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : _sanitizer.Sanitize(html).Trim();
}

/// <summary>
/// Quyền riêng tư trên trang công khai (BR-37). Dùng chung: bảng xếp hạng (C), danh sách VĐV công khai (B),
/// màn hình tra cứu race kit (C – che số giấy tờ).
/// </summary>
public interface IPrivacyFilter
{
    /// <summary>Tên công khai: ẩn khi người chạy chọn ẩn hoặc dưới 18 tuổi tại ngày thi ⇒ "VĐV #&lt;BIB&gt;".</summary>
    string PublicDisplayName(string fullName, bool hideFromPublic, DateOnly dateOfBirth, DateOnly eventDate, int? bibNumber);

    bool IsMinor(DateOnly dateOfBirth, DateOnly eventDate);

    /// <summary>"079201001234" ⇒ "********1234".</summary>
    string? MaskIdNumber(string? idNumber);
}

public class PrivacyFilter : IPrivacyFilter
{
    public const int AdultAge = 18;

    public string PublicDisplayName(string fullName, bool hideFromPublic, DateOnly dateOfBirth, DateOnly eventDate, int? bibNumber)
    {
        if (!hideFromPublic && !IsMinor(dateOfBirth, eventDate)) return fullName;
        return bibNumber is int bib ? $"VĐV #{bib}" : "VĐV ẩn danh";
    }

    public bool IsMinor(DateOnly dateOfBirth, DateOnly eventDate) => AgeCalculator.AgeAt(dateOfBirth, eventDate) < AdultAge;

    public string? MaskIdNumber(string? idNumber)
    {
        if (string.IsNullOrEmpty(idNumber)) return null;
        var visible = Math.Min(4, idNumber.Length);
        return new string('*', idNumber.Length - visible) + idNumber[^visible..];
    }
}

/// <summary>Tính tuổi tròn tại một ngày. (Cách tính theo năm sinh – AgeCalculationMode.CalendarYear – do B áp dụng theo EventPolicy.)</summary>
public static class AgeCalculator
{
    public static int AgeAt(DateOnly dateOfBirth, DateOnly onDate)
    {
        var age = onDate.Year - dateOfBirth.Year;
        if (dateOfBirth > onDate.AddYears(-age)) age--;
        return age;
    }
}
