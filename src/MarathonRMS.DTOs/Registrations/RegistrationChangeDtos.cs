using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Registrations;

// Phần A – UC-46 đổi cự ly (BR-17), UC-47 chuyển nhượng BIB (BR-16), BR-18 suất đặc biệt.

/// <summary>Những gì chủ đăng ký có thể làm với một đăng ký đã xác nhận – để dựng trang "Chuyển nhượng / đổi cự ly".</summary>
public class RegistrationChangeOptionsDto
{
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string RunnerName { get; set; } = "";
    public int? BibNumber { get; set; }

    /// <summary>Null = được chuyển nhượng; khác null = lý do không được.</summary>
    public string? TransferBlockedReason { get; set; }
    public decimal TransferFee { get; set; }
    public DateTime? TransferDeadline { get; set; }
    public int TransfersLeft { get; set; }
    public TransferDto? PendingTransfer { get; set; }

    public string? CategoryChangeBlockedReason { get; set; }
    public decimal CategoryChangeFee { get; set; }
    public DateTime? CategoryChangeDeadline { get; set; }
    public int CategoryChangesLeft { get; set; }
    public IReadOnlyList<CategoryChangeQuoteDto> CategoryOptions { get; set; } = Array.Empty<CategoryChangeQuoteDto>();
}

/// <summary>Báo giá đổi sang một cự ly: chênh lệch theo giá hiện hành (đổi xuống không hoàn) + phí đổi.</summary>
public class CategoryChangeQuoteDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int RemainingSlots { get; set; }
    public decimal PriceDifference { get; set; }
    public decimal Fee { get; set; }
    public decimal Total { get; set; }
    /// <summary>Null = đổi được; khác null = lý do (hết suất, không đúng tuổi…).</summary>
    public string? BlockedReason { get; set; }
}

public class ChangeCategoryRequest
{
    [Required] public int NewCategoryId { get; set; }
    /// <summary>Số tiền người dùng đã thấy trên báo giá – lệch với giá lúc thực hiện ⇒ 409 để xem lại.</summary>
    [Range(0, 100_000_000)] public decimal ExpectedTotal { get; set; }
}

public class CategoryChangeResultDto
{
    public int RegistrationId { get; set; }
    public string NewCategoryName { get; set; } = "";
    public decimal AmountPaid { get; set; }
    public string? TxnRef { get; set; }
    public int? NewBibNumber { get; set; }
}

public class CreateTransferRequest
{
    [Required(ErrorMessage = "Nhập email tài khoản người nhận"), EmailAddress, StringLength(256)]
    public string RecipientEmail { get; set; } = "";
}

public class TransferDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string CategoryName { get; set; } = "";
    /// <summary>"Outgoing" (tôi gửi) | "Incoming" (gửi cho tôi).</summary>
    public string Direction { get; set; } = "";
    public string FromName { get; set; } = "";
    public string ToEmail { get; set; } = "";
    public string ToName { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal FeeAmount { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? Deadline { get; set; }
    public int? NewRegistrationId { get; set; }
}

/// <summary>
/// Người nhận chấp nhận chuyển nhượng: khai thông tin của chính mình (snapshot mới – BR-07) và đồng ý (BR-09).
/// Giải thu phí chuyển nhượng ⇒ người nhận thanh toán (Mock) ngay khi chấp nhận.
/// </summary>
public class AcceptTransferRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, StringLength(16, ErrorMessage = "Tên in BIB tối đa 16 ký tự")] public string BibName { get; set; } = "";
    [Required] public DateOnly DateOfBirth { get; set; }
    [Required, AllowedValues("M", "F")] public string Gender { get; set; } = "";
    [Required, StringLength(60)] public string Nationality { get; set; } = "Việt Nam";
    [AllowedValues(null, "CCCD", "Passport", "BirthCertificate")] public string? IdType { get; set; }
    [StringLength(20), RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Số giấy tờ chỉ gồm chữ cái không dấu và chữ số")] public string? IdNumber { get; set; }
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Required, AllowedValues("XS", "S", "M", "L", "XL", "XXL")] public string TShirtSize { get; set; } = "";
    [Required, StringLength(100)] public string EmergencyContactName { get; set; } = "";
    [Required, Phone, StringLength(20)] public string EmergencyContactPhone { get; set; } = "";
    [StringLength(5)] public string? BloodType { get; set; }
    [StringLength(500)] public string? MedicalNotes { get; set; }
    [StringLength(100)] public string? ClubName { get; set; }

    [StringLength(100)] public string? GuardianName { get; set; }
    [Phone, StringLength(20)] public string? GuardianPhone { get; set; }
    [StringLength(20), RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Số giấy tờ chỉ gồm chữ cái không dấu và chữ số")] public string? GuardianIdNumber { get; set; }
    public bool GuardianConsent { get; set; }
    [StringLength(100)] public string? EscortName { get; set; }
    [Phone, StringLength(20)] public string? EscortPhone { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý văn bản miễn trừ trách nhiệm")]
    public bool WaiverAccepted { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải cam kết đủ sức khỏe tham gia")]
    public bool HealthDeclarationAccepted { get; set; }
    public bool HealthDataConsent { get; set; }
    public bool PhotoMarketingConsent { get; set; }
    public bool HideFromPublicList { get; set; }
}
