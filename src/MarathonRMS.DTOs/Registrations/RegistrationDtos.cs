using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Registrations;

/// <summary>
/// UC-39 / UC-40: đăng ký một cự ly cho một người chạy (chính chủ hoặc người phụ thuộc).
/// Form điền sẵn từ hồ sơ Participant; giá trị gửi lên được chụp lại vào đăng ký (BR-07).
/// Ràng buộc phụ thuộc dữ liệu (tuổi, giám hộ, chạy kèm, còn suất, đúng hạn, số người phụ thuộc – BR-01…06) kiểm tra trong Service.
/// </summary>
public class CreateRegistrationRequest
{
    [Required] public int CategoryId { get; set; }
    [Required] public int ParticipantId { get; set; }

    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, StringLength(16, ErrorMessage = "Tên in BIB tối đa 16 ký tự")] public string BibName { get; set; } = "";
    [Required] public DateOnly DateOfBirth { get; set; }
    [Required, AllowedValues("M", "F")] public string Gender { get; set; } = "";
    [Required, StringLength(60)] public string Nationality { get; set; } = "Việt Nam";
    [AllowedValues(null, "CCCD", "Passport", "BirthCertificate")] public string? IdType { get; set; }
    [StringLength(20)] public string? IdNumber { get; set; }
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Required, AllowedValues("XS", "S", "M", "L", "XL", "XXL")] public string TShirtSize { get; set; } = "";
    [Required, StringLength(100)] public string EmergencyContactName { get; set; } = "";
    [Required, Phone, StringLength(20)] public string EmergencyContactPhone { get; set; } = "";
    [StringLength(5)] public string? BloodType { get; set; }
    [StringLength(500)] public string? MedicalNotes { get; set; }
    [StringLength(100)] public string? ClubName { get; set; }

    // Giám hộ / chạy kèm (BR-06) – bắt buộc hay không do Service quyết theo tuổi
    [StringLength(100)] public string? GuardianName { get; set; }
    [Phone, StringLength(20)] public string? GuardianPhone { get; set; }
    [StringLength(20)] public string? GuardianIdNumber { get; set; }
    public bool GuardianConsent { get; set; }
    [StringLength(100)] public string? EscortName { get; set; }
    [Phone, StringLength(20)] public string? EscortPhone { get; set; }

    // Đồng ý (BR-09): bắt buộc tick, không tick sẵn
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý văn bản miễn trừ trách nhiệm")]
    public bool WaiverAccepted { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải cam kết đủ sức khỏe tham gia")]
    public bool HealthDeclarationAccepted { get; set; }
    /// <summary>Đồng ý xử lý dữ liệu sức khỏe – bắt buộc nếu điền nhóm máu / ghi chú y tế (Service kiểm tra).</summary>
    public bool HealthDataConsent { get; set; }
    public bool PhotoMarketingConsent { get; set; }
    public bool HideFromPublicList { get; set; }

    [StringLength(30)] public string? CouponCode { get; set; }
}

/// <summary>Đăng ký của tôi (UC-43) – gồm cả đăng ký cho người phụ thuộc.</summary>
public class MyRegistrationDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int ParticipantId { get; set; }
    public string RunnerName { get; set; } = "";
    public bool IsDependent { get; set; }
    public string Status { get; set; } = "";
    public int? BibNumber { get; set; }
    public string BibName { get; set; } = "";
    public decimal FinalPrice { get; set; }
    public DateTime RegisteredAt { get; set; }
    /// <summary>Hạn thanh toán khi đang PendingPayment (BR-13).</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Mã cho QR nhận race kit – chỉ có khi Confirmed.</summary>
    public string? QrToken { get; set; }
    public bool CanEditInfo { get; set; }
    public bool CanCancel { get; set; }
    public bool RaceKitCollected { get; set; }
}

/// <summary>UC-44: các trường người đăng ký tự sửa được trước InfoEditDeadline (BR-08).</summary>
public class UpdateRegistrationInfoRequest
{
    [Required, StringLength(16)] public string BibName { get; set; } = "";
    [Required, AllowedValues("XS", "S", "M", "L", "XL", "XXL")] public string TShirtSize { get; set; } = "";
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
    [Required, StringLength(100)] public string EmergencyContactName { get; set; } = "";
    [Required, Phone, StringLength(20)] public string EmergencyContactPhone { get; set; } = "";
    [StringLength(100)] public string? ClubName { get; set; }
    public bool HideFromPublicList { get; set; }
}

/// <summary>UC-48: Organizer sửa trường đã khóa (BR-08), bắt buộc lý do – ghi nhật ký (BR-36).</summary>
public class UpdateLockedInfoRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required] public DateOnly DateOfBirth { get; set; }
    [Required, AllowedValues("M", "F")] public string Gender { get; set; } = "";
    [AllowedValues(null, "CCCD", "Passport", "BirthCertificate")] public string? IdType { get; set; }
    [StringLength(20)] public string? IdNumber { get; set; }
    [Required(ErrorMessage = "Phải ghi lý do sửa"), StringLength(500)] public string Reason { get; set; } = "";
}

/// <summary>Dòng trong danh sách đăng ký của Organizer – nguồn của GET /odata/Registrations (UC-48). Không chứa dữ liệu y tế.</summary>
public class RegistrationListItemDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string BibName { get; set; } = "";
    public int? BibNumber { get; set; }
    public string Gender { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Nationality { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string TShirtSize { get; set; } = "";
    public string? ClubName { get; set; }
    public string EntryType { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal FinalPrice { get; set; }
    public DateTime RegisteredAt { get; set; }
    public bool RaceKitCollected { get; set; }
}
