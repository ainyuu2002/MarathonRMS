using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Admin;

// ============================ Tài khoản (UC-11, UC-14) ============================

public class UserListItemDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>UC-14: SuperAdmin tạo tài khoản Admin.</summary>
public class CreateAdminRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")] public string Password { get; set; } = "";
}

/// <summary>UC-14: đổi quyền giữa Admin và SuperAdmin (BR-35).</summary>
public class ChangeAdminRoleRequest
{
    [Required, AllowedValues("Admin", "SuperAdmin")] public string Role { get; set; } = "";
}

public class SetActiveRequest
{
    public bool IsActive { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
}

// ============================ Địa điểm (UC-12) ============================

public class VenueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int EventCount { get; set; }
}

public class UpsertVenueRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên địa điểm"), StringLength(200)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ"), StringLength(300)] public string Address { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập tỉnh/thành phố"), StringLength(100)] public string City { get; set; } = "";
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
}

// ============================ Cấu hình hệ thống (UC-15) ============================

public class SystemSettingDto
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateSettingRequest
{
    [Required(AllowEmptyStrings = false), StringLength(1000)] public string Value { get; set; } = "";
}

// ============================ Văn bản pháp lý (UC-84) ============================

public class PublishLegalDocumentRequest
{
    [Required, AllowedValues("Terms", "Privacy")] public string DocType { get; set; } = "";
    [Required(ErrorMessage = "Nội dung không được trống"), StringLength(100_000, MinimumLength = 20)]
    public string Content { get; set; } = "";
}

// ============================ Danh mục lý do phạt / DSQ (UC-85) ============================

/// <summary>Lý do phạt trong trang quản trị (có cả lý do đã ngừng dùng). C dùng PenaltyReasonDto (Results) cho form phạt.</summary>
public class PenaltyReasonAdminDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Warning | TimePenalty | DSQ.</summary>
    public string DefaultAction { get; set; } = "";
    public int? DefaultMinutes { get; set; }
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
}

/// <summary>Code chỉ dùng khi tạo (không đổi được sau đó). TimePenalty bắt buộc số phút 1–600.</summary>
public class UpsertPenaltyReasonRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã"), StringLength(40, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Mã chỉ gồm chữ không dấu, số và dấu gạch dưới")]
    public string Code { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập tên lý do"), StringLength(200)] public string Name { get; set; } = "";
    [Required, AllowedValues("Warning", "TimePenalty", "DSQ")] public string DefaultAction { get; set; } = "DSQ";
    [Range(1, 600, ErrorMessage = "Số phút từ 1 đến 600")] public int? DefaultMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
