using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.DTOs.Results;

namespace MarathonRMS.Services.RaceDay;

// Phần C – Ngày thi & kết quả (Giai đoạn 1).
// Mọi thao tác: Staff/Organizer chỉ trên giải của BTC mình (BR-30) và KHÔNG thao tác trên đăng ký của chính mình (BR-32).

/// <summary>UC-49 – gán BIB hàng loạt theo dải số của cự ly (BR-19). Việc chọn số tách vào IBibAllocator.</summary>
public interface IBibService
{
    /// <summary>Số đăng ký đã xác nhận / đã có BIB / số còn trống theo từng cự ly.</summary>
    Task<BibOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default);

    /// <summary>Danh sách đã gán BIB; keyword là số BIB hoặc một phần họ tên.</summary>
    Task<PagedResult<BibListItemDto>> GetAssignmentsAsync(int eventId, int? categoryId, string? keyword, int page, int pageSize,
        CancellationToken ct = default);

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

    /// <summary>Mọi thời gian đã ghi của VĐV mang BIB trong giải.</summary>
    Task<IReadOnlyList<TimingRecordDto>> GetByBibAsync(int eventId, int bibNumber, CancellationToken ct = default);

    /// <summary>Bản ghi mới nhất (theo checkpoint nếu có) – để Staff đối chiếu khi nhập liên tục.</summary>
    Task<IReadOnlyList<TimingRecordDto>> GetRecentAsync(int eventId, int? checkpointId, int take, CancellationToken ct = default);

    /// <summary>Organizer xóa bản ghi nhập sai (ghi nhật ký – BR-36). Không áp dụng khi kết quả đã chính thức.</summary>
    Task DeleteAsync(int timingRecordId, CancellationToken ct = default);

    /// <summary>Danh sách VĐV đã ghi nhận bỏ cuộc (UC-58).</summary>
    Task<IReadOnlyList<WithdrawalDto>> GetWithdrawalsAsync(int eventId, CancellationToken ct = default);

    /// <summary>Organizer hủy ghi nhận bỏ cuộc nhập nhầm (ghi nhật ký). Lần tính kết quả sau sẽ xét lại VĐV.</summary>
    Task CancelWithdrawalAsync(int registrationId, CancellationToken ct = default);
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

    /// <summary>Tình hình kết quả của giải cho Organizer (số lượng theo trạng thái, thao tác được phép).</summary>
    Task<ResultsOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default);

    /// <summary>Danh sách kết quả cho Organizer – đủ họ tên, mọi trạng thái – để rà soát trước khi công bố.</summary>
    Task<PagedResult<LeaderboardRowDto>> GetForOrganizerAsync(int eventId, int? categoryId, string? status, string? keyword,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>BR-27: Draft → Provisional (công khai, nhận khiếu nại). Ghi nhật ký.</summary>
    Task<ResultsStatusDto> PublishProvisionalAsync(int eventId, CancellationToken ct = default);

    /// <summary>BR-27: Provisional → Official khi mọi khiếu nại đã xử lý. Ghi nhật ký.</summary>
    Task<ResultsStatusDto> FinalizeAsync(int eventId, CancellationToken ct = default);

    /// <summary>Nguồn cho GET /odata/Results: chỉ giải có ResultsStatus Provisional/Official; áp IPrivacyFilter (BR-37).</summary>
    IQueryable<LeaderboardRowDto> QueryLeaderboard();

    /// <summary>Kết quả cá nhân + splits + phạt (UC-63).</summary>
    Task<ResultDetailDto> GetDetailAsync(int registrationId, CancellationToken ct = default);

    Task<IReadOnlyList<PenaltyReasonDto>> GetPenaltyReasonsAsync(CancellationToken ct = default);

    /// <summary>Thông tin VĐV + mọi quyết định phạt (kể cả đã thu hồi) cho màn hình quản lý phạt.</summary>
    Task<ResultPenaltiesDto> GetPenaltiesAsync(int resultId, CancellationToken ct = default);

    /// <summary>UC-61 (DSQ) / UC-67 (phạt cộng phút – GĐ2). BR-28, BR-32, ghi nhật ký (BR-36). Sau đó tính lại hạng.</summary>
    Task<PenaltyDto> IssuePenaltyAsync(int resultId, IssuePenaltyRequest request, CancellationToken ct = default);

    /// <summary>Thu hồi phạt (đặt RevokedAt, không xóa). Ghi nhật ký.</summary>
    Task RevokePenaltyAsync(int penaltyId, CancellationToken ct = default);
}
