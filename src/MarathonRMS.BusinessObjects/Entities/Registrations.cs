using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

/// <summary>
/// Vé của một người chạy. Người mua (RegisteredByUserId) có thể khác người chạy (ParticipantId).
/// Thông tin người chạy được chụp lại (snapshot) khi tạo – BR-07.
/// </summary>
public class Registration
{
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public string RegisteredByUserId { get; set; } = "";
    public AppUser RegisteredBy { get; set; } = null!;

    public EntryType EntryType { get; set; } = EntryType.Public;
    public RegistrationStatus Status { get; set; } = RegistrationStatus.PendingPayment;
    public int? BibNumber { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CoinDiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    public int? CouponId { get; set; }
    public string QrToken { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int ChangeCount { get; set; }
    public int TransferCount { get; set; }
    public bool HideFromPublicList { get; set; }

    // ---- Snapshot người chạy ----
    public string FullName { get; set; } = "";
    public string BibName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public IdType? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string Nationality { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public TShirtSize TShirtSize { get; set; }
    public string EmergencyContactName { get; set; } = "";
    public string EmergencyContactPhone { get; set; } = "";
    public string? BloodType { get; set; }
    public string? MedicalNotes { get; set; }
    public string? ClubName { get; set; }

    // ---- Giám hộ / chạy kèm (BR-06) ----
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianIdNumber { get; set; }
    public bool GuardianConsent { get; set; }
    public string? EscortName { get; set; }
    public string? EscortPhone { get; set; }

    public bool WaiverAccepted { get; set; }
    public bool HealthDeclarationAccepted { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public RaceKitPickup? RaceKitPickup { get; set; }
    public ICollection<TimingRecord> TimingRecords { get; set; } = new List<TimingRecord>();
    public Result? Result { get; set; }
}

public class Payment
{
    public int PaymentId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public PaymentPurpose Purpose { get; set; }
    public PaymentProvider Provider { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string TxnRef { get; set; } = "";
    public string? ProviderTransactionNo { get; set; }
    public string? ResponseCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class RaceKitPickup
{
    public int PickupId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public DateTime PickedUpAt { get; set; }
    public string StaffId { get; set; } = "";
    public AppUser Staff { get; set; } = null!;
    public bool IdChecked { get; set; }
    public bool IsProxy { get; set; }
    public string? ProxyName { get; set; }
    public string? ProxyIdNumber { get; set; }
    public string? Note { get; set; }
}
