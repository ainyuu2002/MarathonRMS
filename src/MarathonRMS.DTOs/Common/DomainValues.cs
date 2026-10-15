namespace MarathonRMS.DTOs.Common;

/// <summary>
/// Giá trị hợp lệ của các enum nghiệp vụ, dạng chuỗi – để DTO dùng [AllowedValues] mà không phải tham chiếu
/// BusinessObjects (WebApp chỉ tham chiếu DTOs). Phải khớp tên enum trong MarathonRMS.BusinessObjects.Enums.
/// </summary>
public static class DomainValues
{
    public static readonly string[] Genders = { "M", "F" };
    public static readonly string[] TShirtSizes = { "XS", "S", "M", "L", "XL", "XXL" };
    public static readonly string[] IdTypes = { "CCCD", "Passport", "BirthCertificate" };

    public static readonly string[] EventStatuses =
        { "Draft", "Published", "RegistrationOpen", "RegistrationClosed", "Postponed", "Completed", "Cancelled" };
    public static readonly string[] ResultsStatuses = { "Draft", "Provisional", "Official" };

    public static readonly string[] Disciplines = { "Road", "Trail" };
    public static readonly string[] CategoryTypes = { "Standard", "Kids", "Elite" };
    public static readonly string[] CheckpointTypes = { "Start", "Split", "Finish" };
    public static readonly string[] StationTypes = { "None", "Water", "Full", "Medical" };

    public static readonly string[] AgeCalculationModes = { "RaceDate", "CalendarYear" };
    public static readonly string[] TransferPolicies = { "Forbidden", "Free", "Paid" };
    public static readonly string[] RankingBases = { "GunTime", "ChipTime" };
    public static readonly string[] TieBreakRules = { "Shared", "ChipTime" };
    public static readonly string[] AwardStackingPolicies = { "AllowAll", "HighestOnly" };
    public static readonly string[] ForceMajeurePolicies = { "NoRefund", "CarryOver", "Refund" };

    public static readonly string[] EntryTypes = { "Public", "Partner", "Complimentary", "Pacer", "Charity" };
    public static readonly string[] RegistrationStatuses =
        { "PendingPayment", "Confirmed", "Cancelled", "Expired", "Transferred", "RefundPending", "Refunded" };
    public static readonly string[] PaymentPurposes = { "Registration", "CategoryChange", "TransferFee", "ComplaintFee", "AddOn" };
    public static readonly string[] PaymentProviders = { "Mock", "VnPay" };
    public static readonly string[] PaymentStatuses = { "Pending", "Success", "Failed", "Expired" };

    public static readonly string[] ResultStatuses = { "Finished", "DNF", "DNS", "DSQ" };
    public static readonly string[] DnfReasons = { "CutOff", "Voluntary", "Medical", "MissedCheckpoint" };
    public static readonly string[] WithdrawalReasons = { "Voluntary", "Medical" };
    public static readonly string[] PenaltyActions = { "Warning", "TimePenalty", "DSQ" };
}
