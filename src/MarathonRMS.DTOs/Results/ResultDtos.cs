using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Results;

/// <summary>
/// Dòng bảng xếp hạng – nguồn của GET /odata/Results (UC-62). Chỉ dữ liệu công khai tối thiểu (BR-37):
/// DisplayName đã ẩn nếu người chạy chọn ẩn tên hoặc dưới 18 tuổi. Thời gian là mili giây – hiển thị làm tròn lên giây (BR-22).
/// </summary>
public class LeaderboardRowDto
{
    public int ResultId { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int? BibNumber { get; set; }
    public string DisplayName { get; set; } = "";
    public string Gender { get; set; } = "";
    public int? AgeGroupId { get; set; }
    public string? AgeGroupName { get; set; }
    public string? ClubName { get; set; }
    public long? GunTimeMs { get; set; }
    public long? ChipTimeMs { get; set; }
    public long? FinalTimeMs { get; set; }
    public int PenaltySeconds { get; set; }
    public string Status { get; set; } = "";
    public int? OverallRank { get; set; }
    public int? GenderRank { get; set; }
    public int? AgeGroupRank { get; set; }
    /// <summary>True khi kết quả của giải còn "Tạm thời" (BR-27).</summary>
    public bool IsProvisional { get; set; }
    /// <summary>Khóa sắp xếp bảng xếp hạng: VĐV hoàn thành theo hạng, sau đó DNF, DSQ, DNS ($orderby=SortOrder).</summary>
    public int SortOrder { get; set; }
}

/// <summary>Giải đã công bố kết quả – để chọn trên trang bảng xếp hạng công khai (UC-62).</summary>
public class ResultsEventDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string ResultsStatus { get; set; } = "";
    public IReadOnlyList<ResultCategoryOptionDto> Categories { get; set; } = Array.Empty<ResultCategoryOptionDto>();
    public IReadOnlyList<AgeGroupOptionDto> AgeGroups { get; set; } = Array.Empty<AgeGroupOptionDto>();
}

public class AgeGroupOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Null = áp dụng cho mọi cự ly.</summary>
    public int? CategoryId { get; set; }
}

/// <summary>Kết quả cá nhân + split (UC-63).</summary>
public class ResultDetailDto
{
    public LeaderboardRowDto Summary { get; set; } = new();
    public string EventName { get; set; } = "";
    public string? DnfReason { get; set; }
    public string? DnfCheckpointName { get; set; }
    public IReadOnlyList<SplitDto> Splits { get; set; } = Array.Empty<SplitDto>();
    /// <summary>Chỉ phạt còn hiệu lực; lý do hiển thị theo danh mục (BR-28).</summary>
    public IReadOnlyList<PenaltyDto> Penalties { get; set; } = Array.Empty<PenaltyDto>();

    // ---------- UC-66 khiếu nại (chỉ có nghĩa với chính VĐV / người đăng ký hộ) ----------
    /// <summary>Người xem là chủ đăng ký, kết quả đang tạm thời và còn trong thời gian nhận khiếu nại (BR-27).</summary>
    public bool CanComplain { get; set; }
    public DateTime? ComplaintWindowEndsAt { get; set; }
    public decimal ComplaintFee { get; set; }
}

// ============================ Khiếu nại kết quả (UC-66) ============================

public class CreateComplaintRequest
{
    [Required(ErrorMessage = "Nhập nội dung khiếu nại"), StringLength(2000, MinimumLength = 10, ErrorMessage = "Nội dung từ 10 đến 2.000 ký tự")]
    public string Content { get; set; } = "";

    /// <summary>Đường dẫn ảnh / video / file GPS làm bằng chứng (tùy chọn).</summary>
    [Url(ErrorMessage = "Đường dẫn bằng chứng không hợp lệ"), StringLength(500)]
    public string? EvidenceUrl { get; set; }
}

public class ResolveComplaintRequest
{
    [Required, AllowedValues("Accepted", "Rejected")] public string Decision { get; set; } = "Rejected";

    [Required(ErrorMessage = "Phải trả lời người khiếu nại"), StringLength(2000)]
    public string Response { get; set; } = "";
}

public class ComplaintDto
{
    public int Id { get; set; }
    public int ResultId { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int? BibNumber { get; set; }
    public string RunnerName { get; set; } = "";
    /// <summary>Trạng thái kết quả hiện tại của VĐV (Finished/DNF/DNS/DSQ).</summary>
    public string ResultStatus { get; set; } = "";
    public string Content { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public decimal FeeAmount { get; set; }
    public string Status { get; set; } = "";
    public string? Response { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
}

public class EventComplaintsDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string ResultsStatus { get; set; } = "";
    public DateTime? ComplaintWindowEndsAt { get; set; }
    /// <summary>Còn xử lý được (kết quả chưa chính thức).</summary>
    public bool CanResolve { get; set; }
    public IReadOnlyList<ComplaintDto> Items { get; set; } = Array.Empty<ComplaintDto>();
}

public class SplitDto
{
    public int CheckpointId { get; set; }
    public string CheckpointName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int Sequence { get; set; }
    public DateTime RecordedAt { get; set; }
    /// <summary>Thời gian từ xuất phát (theo cơ sở hiển thị của giải).</summary>
    public long ElapsedMs { get; set; }
    /// <summary>Pace của đoạn từ checkpoint trước (giây/km).</summary>
    public int? SegmentPaceSecondsPerKm { get; set; }
}

/// <summary>Kết quả của lệnh tính kết quả (UC-59).</summary>
public class ResultCalculationSummaryDto
{
    public int EventId { get; set; }
    public int Finished { get; set; }
    public int Dnf { get; set; }
    public int Dns { get; set; }
    public int Dsq { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>Tình hình kết quả của một giải trên màn hình Organizer (UC-59).</summary>
public class ResultsOverviewDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string EventStatus { get; set; } = "";
    public string ResultsStatus { get; set; } = "";
    public DateTime? ProvisionalAt { get; set; }
    public DateTime? OfficialAt { get; set; }
    public int Finished { get; set; }
    public int Dnf { get; set; }
    public int Dns { get; set; }
    public int Dsq { get; set; }
    /// <summary>Lần cập nhật kết quả gần nhất (tính lại, ghi nhận bỏ cuộc…).</summary>
    public DateTime? LastUpdatedAt { get; set; }
    public bool CanCalculate { get; set; }
    public bool CanPublishProvisional { get; set; }
    public bool CanFinalize { get; set; }
    /// <summary>Hết hạn nhận khiếu nại = lúc công bố tạm thời + ComplaintWindowMinutes (BR-27).</summary>
    public DateTime? ComplaintWindowEndsAt { get; set; }
    public int OpenComplaints { get; set; }
    public IReadOnlyList<ResultCategoryOptionDto> Categories { get; set; } = Array.Empty<ResultCategoryOptionDto>();
}

public class ResultCategoryOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsTimed { get; set; }
    public bool IsRanked { get; set; }
}

public class ResultsStatusDto
{
    public int EventId { get; set; }
    public string ResultsStatus { get; set; } = "";
    public DateTime? ProvisionalAt { get; set; }
    public DateTime? OfficialAt { get; set; }
}

// ============================ Phạt / DSQ (UC-61, UC-67) ============================

public class PenaltyReasonDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string DefaultAction { get; set; } = "";
    public int? DefaultMinutes { get; set; }
}

/// <summary>GĐ1 chỉ dùng Action = DSQ; TimePenalty ở GĐ2. Lý do bắt buộc từ danh mục (BR-28), không tự phạt mình (BR-32).</summary>
public class IssuePenaltyRequest
{
    [Required, StringLength(40)] public string ReasonCode { get; set; } = "";
    [Required, AllowedValues("Warning", "TimePenalty", "DSQ")] public string Action { get; set; } = "DSQ";
    [Range(1, 600)] public int? Minutes { get; set; }
    public int? CheckpointId { get; set; }
    /// <summary>BR-28: bắt buộc ghi chú (diễn biến, bằng chứng).</summary>
    [Required(ErrorMessage = "Phải ghi chú lý do cụ thể của quyết định phạt (BR-28)"), StringLength(500)]
    public string Note { get; set; } = "";
}

public class PenaltyDto
{
    public int Id { get; set; }
    public string ReasonCode { get; set; } = "";
    public string ReasonName { get; set; } = "";
    public string Action { get; set; } = "";
    public int? Minutes { get; set; }
    public string? CheckpointName { get; set; }
    public string? Note { get; set; }
    public DateTime IssuedAt { get; set; }
    /// <summary>Có giá trị khi quyết định đã bị thu hồi (chỉ xuất hiện ở màn hình quản lý phạt).</summary>
    public DateTime? RevokedAt { get; set; }
}

/// <summary>Màn hình quản lý phạt của một kết quả (UC-61): thông tin VĐV, checkpoint của cự ly, mọi quyết định đã ban hành.</summary>
public class ResultPenaltiesDto
{
    public LeaderboardRowDto Result { get; set; } = new();
    public string ResultsStatus { get; set; } = "";
    public IReadOnlyList<ResultCheckpointOptionDto> Checkpoints { get; set; } = Array.Empty<ResultCheckpointOptionDto>();
    public IReadOnlyList<PenaltyDto> Penalties { get; set; } = Array.Empty<PenaltyDto>();
}

public class ResultCheckpointOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
