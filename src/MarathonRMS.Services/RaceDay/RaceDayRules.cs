using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.RaceDay;

/// <summary>Quy tắc dùng chung của phần C (tuổi theo chính sách giải, nhãn trạng thái).</summary>
public static class RaceDayRules
{
    /// <summary>Tuổi dùng để xét điều kiện tại giải: theo ngày thi hoặc theo năm sinh (EventPolicy.AgeCalculationMode).</summary>
    public static int AgeForEvent(DateOnly dateOfBirth, DateOnly eventDate, AgeCalculationMode mode) =>
        mode == AgeCalculationMode.CalendarYear
            ? eventDate.Year - dateOfBirth.Year
            : AgeCalculator.AgeAt(dateOfBirth, eventDate);

    /// <summary>VĐV dưới tuổi GuardianConsentUnderAge phải có đồng ý của người giám hộ.</summary>
    public static bool NeedsGuardianConsent(DateOnly dateOfBirth, Event ev) =>
        AgeForEvent(dateOfBirth, ev.EventDate, ev.Policy.AgeCalculationMode) < ev.Policy.GuardianConsentUnderAge;

    public static string RegistrationStatusLabel(RegistrationStatus status) => status switch
    {
        RegistrationStatus.PendingPayment => "chờ thanh toán",
        RegistrationStatus.Confirmed => "đã xác nhận",
        RegistrationStatus.Cancelled => "đã hủy",
        RegistrationStatus.Expired => "hết hạn",
        RegistrationStatus.Transferred => "đã chuyển nhượng",
        RegistrationStatus.RefundPending => "chờ hoàn tiền",
        RegistrationStatus.Refunded => "đã hoàn tiền",
        _ => status.ToString()
    };

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch ngang – để so khớp số điện thoại nhập tay.</summary>
    public static string Compact(string value) =>
        new(value.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray());
}
