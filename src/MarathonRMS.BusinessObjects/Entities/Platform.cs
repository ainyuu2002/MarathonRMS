using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

/// <summary>Bằng chứng đồng ý xử lý dữ liệu theo từng mục đích (Luật 91/2025 – Điều 9; BR-09).</summary>
public class ConsentRecord
{
    public long ConsentId { get; set; }
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public int? RegistrationId { get; set; }
    public Registration? Registration { get; set; }
    public ConsentType ConsentType { get; set; }
    /// <summary>Phiên bản LegalDocuments (Terms/Privacy) hoặc Events.WaiverVersion (Waiver).</summary>
    public int DocumentVersion { get; set; }
    public bool Granted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}

/// <summary>Điều khoản sử dụng / chính sách quyền riêng tư theo phiên bản (UC-84). Chỉ thêm, không sửa.</summary>
public class LegalDocument
{
    public int DocumentId { get; set; }
    public LegalDocType DocType { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public string? PublishedById { get; set; }
}

/// <summary>Cấu hình nền tảng dạng key–value (SuperAdmin chỉnh – UC-15).</summary>
public class SystemSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public SettingDataType DataType { get; set; }
    public string? Description { get; set; }
    public string? UpdatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Nhật ký thao tác nhạy cảm (BR-36). Chỉ INSERT.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    public string? UserId { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
