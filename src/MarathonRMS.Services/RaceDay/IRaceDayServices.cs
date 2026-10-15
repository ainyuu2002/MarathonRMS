using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.DTOs.Results;

namespace MarathonRMS.Services.RaceDay;

// Phần C – Ngày thi & kết quả (Giai đoạn 1). Chỉ có interface; C viết implementation, DAO/Repository
// tương ứng và đăng ký DI trong ServiceCollectionExtensions.
// Mọi thao tác: Staff/Organizer chỉ trên giải của BTC mình (BR-30) và KHÔNG thao tác trên đăng ký của chính mình (BR-32).

/// <summary>UC-49 – gán BIB hàng loạt theo dải số của cự ly (BR-19). Gợi ý: tách IBibAllocator để test riêng.</summary>
public interface IBibService
{
    Task<BibAssignmentResultDto> AssignAsync(int eventId, AssignBibsRequest request, CancellationToken ct = default);
}

/// <summary>UC-52, UC-53 – phát race kit (BR-20).</summary>
public interface IRaceKitService
{
    /// <summary>Tìm theo BIB, QrToken, số giấy tờ hoặc SĐT trong một giải.</summary>
    Task<IReadOnlyList<RaceKitLookupItemDto>> LookupAsync(int eventId, string keyword, CancellationToken ct = default);

    /// <summary>Chỉ đăng ký Confirmed có BIB, chưa nhận; người &lt; GuardianConsentUnderAge phải có đồng ý giám hộ.</summary>
    Task<RaceKitPickupDto> PickupAsync(int registrationId, RaceKitPickupRequest request, CancellationToken ct = default);
}

/// <summary>UC-57, UC-58 – bấm giờ và ghi nhận bỏ cuộc.</summary>
public interface ITimingService
{
    /// <summary>BR-21: thời gian checkpoint sau phải muộn hơn checkpoint trước; trùng (RegistrationId, CheckpointId) ⇒ 409.</summary>
    Task<TimingRecordDto> RecordAsync(RecordTimingRequest request, CancellationToken ct = default);

    /// <summary>BR-23: tạo/cập nhật Result = DNF với DnfReason (Voluntary/Medical) và DnfCheckpointId.</summary>
    Task RecordWithdrawalAsync(RecordWithdrawalRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<TimingRecordDto>> GetByRegistrationAsync(int registrationId, CancellationToken ct = default);
}

/// <summary>UC-59 → UC-63 – tính, công bố, xem kết quả; DSQ/phạt.</summary>
public interface IResultService
{
    /// <summary>
    /// Tính lại toàn bộ kết quả của giải (chỉ khi ResultsStatus ≠ Official):
    /// BR-21 (thiếu checkpoint bắt buộc), BR-22 (Gun/Chip, làm tròn), BR-23 (COT tổng theo Gun + cut-off tại trạm),
    /// BR-24 (DNS, xuất phát muộn), BR-25 (FinalTime, xếp hạng theo EventPolicy, hòa), BR-26 (IsTimed/IsRanked).
    /// Gợi ý: logic thuần tách vào IResultCalculator để test không cần DB.
    /// </summary>
    Task<ResultCalculationSummaryDto> CalculateAsync(int eventId, CancellationToken ct = default);

    /// <summary>BR-27: Draft → Provisional (công khai, nhận khiếu nại). Ghi nhật ký.</summary>
    Task<ResultsStatusDto> PublishProvisionalAsync(int eventId, CancellationToken ct = default);

    /// <summary>BR-27: Provisional → Official khi mọi khiếu nại đã xử lý. Ghi nhật ký.</summary>
    Task<ResultsStatusDto> FinalizeAsync(int eventId, CancellationToken ct = default);

    /// <summary>Nguồn cho GET /odata/Results: chỉ giải có ResultsStatus Provisional/Official; áp IPrivacyFilter (BR-37).</summary>
    IQueryable<LeaderboardRowDto> QueryLeaderboard();

    /// <summary>Kết quả cá nhân + splits + phạt (UC-63).</summary>
    Task<ResultDetailDto> GetDetailAsync(int registrationId, CancellationToken ct = default);

    Task<IReadOnlyList<PenaltyReasonDto>> GetPenaltyReasonsAsync(CancellationToken ct = default);

    /// <summary>UC-61 (DSQ) / UC-67 (phạt cộng phút – GĐ2). BR-28, BR-32, ghi nhật ký (BR-36). Sau đó tính lại hạng.</summary>
    Task<PenaltyDto> IssuePenaltyAsync(int resultId, IssuePenaltyRequest request, CancellationToken ct = default);

    /// <summary>Thu hồi phạt (đặt RevokedAt, không xóa). Ghi nhật ký.</summary>
    Task RevokePenaltyAsync(int penaltyId, CancellationToken ct = default);
}
