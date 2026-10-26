using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.EventContent;

/// <summary>
/// Nội dung trang giới thiệu giải (UC-88) + album (UC-89). B nhúng vào trang chi tiết giải bằng partial _EventContent / _EventGallery.
/// Bản công khai chỉ gồm khối, ảnh đang hiển thị.
/// </summary>
public class EventContentDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string? ThemeColor { get; set; }
    public IReadOnlyList<ContentSectionDto> Sections { get; set; } = Array.Empty<ContentSectionDto>();
    public IReadOnlyList<ScheduleItemDto> Schedule { get; set; } = Array.Empty<ScheduleItemDto>();
    public IReadOnlyList<EventMediaDto> Gallery { get; set; } = Array.Empty<EventMediaDto>();
}

public class ContentSectionDto
{
    public int Id { get; set; }
    public string SectionType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? BodyHtml { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; }
}

/// <summary>SectionType: Intro | Schedule (hiện bảng lịch trình) | Faq | Custom. BodyHtml được lọc phía server (BR-50).</summary>
public class UpsertContentSectionRequest
{
    [Required, AllowedValues("Intro", "Schedule", "Faq", "Custom")] public string SectionType { get; set; } = "Custom";
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề"), StringLength(200)] public string Title { get; set; } = "";
    [StringLength(50000)] public string? BodyHtml { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>Thứ tự mới của các khối (danh sách Id từ trên xuống).</summary>
public class ReorderSectionsRequest
{
    [Required, MinLength(1)] public List<int> SectionIds { get; set; } = new();
}

public class ScheduleItemDto
{
    public int Id { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string Title { get; set; } = "";
    public string? Location { get; set; }
    public string? Note { get; set; }
}

public class UpsertScheduleItemRequest
{
    [Required] public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập nội dung"), StringLength(200)] public string Title { get; set; } = "";
    [StringLength(200)] public string? Location { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}

public class UpdateThemeRequest
{
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Màu dạng #RRGGBB")]
    public string? ThemeColor { get; set; }
}

/// <summary>Ảnh của giải. Kind: Inline (chèn trong mô tả) | Gallery (album). FileUrl là URL tuyệt đối tới API.</summary>
public class EventMediaDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string FileUrl { get; set; } = "";
    public string? Caption { get; set; }
    public string Kind { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UpdateMediaRequest
{
    [StringLength(300)] public string? Caption { get; set; }
    public bool IsVisible { get; set; } = true;
    [Range(0, 10000)] public int DisplayOrder { get; set; }
}
