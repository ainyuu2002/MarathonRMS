using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Organizations;

// ============================ Hồ sơ đăng ký làm BTC (UC-08, UC-09) ============================

public class CreateOrganizationApplicationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên tổ chức"), StringLength(200, MinimumLength = 3)]
    public string OrganizationName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã số thuế")]
    [RegularExpression(@"^\d{10}(-\d{3})?$", ErrorMessage = "Mã số thuế gồm 10 số (hoặc 10 số + '-' + 3 số với chi nhánh)")]
    public string TaxCode { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập người đại diện"), StringLength(100)]
    public string RepresentativeName { get; set; } = "";

    [Required, EmailAddress, StringLength(256)] public string ContactEmail { get; set; } = "";
    [Required, Phone, StringLength(20)] public string ContactPhone { get; set; } = "";
    [StringLength(300)] public string? Address { get; set; }
    [StringLength(2000)] public string? Description { get; set; }

    /// <summary>Link giấy phép kinh doanh / quyết định thành lập (không phải giấy tờ cá nhân – mục 7.6 BA).</summary>
    [StringLength(500), Url] public string? DocumentUrl { get; set; }
}

public class OrganizationApplicationDto
{
    public int Id { get; set; }
    public string ApplicantId { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string RepresentativeName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? DocumentUrl { get; set; }
    public string Status { get; set; } = "";
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public int? OrganizationId { get; set; }
}

public class RejectApplicationRequest
{
    [Required(ErrorMessage = "Phải ghi lý do từ chối"), StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = "";
}

// ============================ Ban tổ chức (UC-10) ============================

public class OrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string ContactEmail { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public int EventCount { get; set; }
}

public class UpdateOrganizationRequest
{
    [Required, StringLength(200, MinimumLength = 3)] public string Name { get; set; } = "";
    [StringLength(500), Url] public string? LogoUrl { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [Required, EmailAddress, StringLength(256)] public string ContactEmail { get; set; } = "";
    [Phone, StringLength(20)] public string? ContactPhone { get; set; }
}

// ============================ Staff của BTC (UC-13) ============================

public class StaffMemberDto
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    /// <summary>"Organizer" hoặc "Staff".</summary>
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
}

public class InviteStaffRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email tài khoản cần mời"), EmailAddress]
    public string Email { get; set; } = "";
}
