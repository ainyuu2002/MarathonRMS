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
    [StringLength(500)] public string? Note { get; set; }
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
}
