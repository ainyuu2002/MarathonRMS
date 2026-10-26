using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Admin;

/// <summary>Một dòng nhật ký hệ thống (UC-17, BR-36) – nguồn của GET /odata/AuditLogs. Chỉ đọc.</summary>
public class AuditLogDto
{
    [Key] public long Id { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    /// <summary>VD "Admin.ChangeRole", "Setting.Update".</summary>
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Giá trị để lọc nhanh trên trang nhật ký.</summary>
public class AuditLogFacetsDto
{
    public IReadOnlyList<string> Actions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> EntityTypes { get; set; } = Array.Empty<string>();
}
