using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Accounts;

/// <summary>Hồ sơ người chạy (Participant). UC-03 dùng cho hồ sơ chính chủ; B dùng làm giá trị điền sẵn form đăng ký (BR-07).</summary>
public class ParticipantProfileDto
{
    public int ParticipantId { get; set; }
    public string Relationship { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TShirtSize { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? BloodType { get; set; }
    public string? MedicalNotes { get; set; }
    public string? ClubName { get; set; }
    /// <summary>Đủ các trường bắt buộc để đăng ký giải (BR-07) chưa.</summary>
    public bool IsCompleteForRegistration { get; set; }
}

/// <summary>UC-03. Các trường bắt buộc khi đăng ký giải được để tùy chọn ở hồ sơ – người dùng có thể điền dần.</summary>
public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    public DateOnly? DateOfBirth { get; set; }

    [AllowedValues(null, "M", "F")] public string? Gender { get; set; }
    [StringLength(60)] public string? Nationality { get; set; }
    [AllowedValues(null, "CCCD", "Passport", "BirthCertificate")] public string? IdType { get; set; }
    [StringLength(20), RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Số giấy tờ chỉ gồm chữ và số")]
    public string? IdNumber { get; set; }
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [AllowedValues(null, "XS", "S", "M", "L", "XL", "XXL")] public string? TShirtSize { get; set; }
    [StringLength(100)] public string? EmergencyContactName { get; set; }
    [Phone, StringLength(20)] public string? EmergencyContactPhone { get; set; }
    [StringLength(5)] public string? BloodType { get; set; }
    [StringLength(500)] public string? MedicalNotes { get; set; }
    [StringLength(100)] public string? ClubName { get; set; }

    /// <summary>Bắt buộc true nếu điền nhóm máu / ghi chú y tế (dữ liệu nhạy cảm – NĐ 356 Điều 4; BR-09).</summary>
    public bool HealthDataConsent { get; set; }
}

/// <summary>Một loại đồng ý của người dùng và trạng thái hiện tại (UC-04).</summary>
public class ConsentStatusDto
{
    public string ConsentType { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Granted { get; set; }
    /// <summary>Bắt buộc (Terms, Privacy) thì không rút lại được trên giao diện – muốn rút phải xóa tài khoản.</summary>
    public bool IsRequired { get; set; }
    public int? DocumentVersion { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>UC-04: bật/tắt đồng ý tùy chọn.</summary>
public class SetConsentRequest
{
    [Required, AllowedValues("HealthData", "PhotoMarketing", "PublicListing")]
    public string ConsentType { get; set; } = "";
    public bool Granted { get; set; }
}
