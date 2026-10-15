using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.RaceDay;

// ============================ Gán BIB (UC-49) ============================

public class AssignBibsRequest
{
    /// <summary>Null = mọi cự ly của giải.</summary>
    public int? CategoryId { get; set; }
}

public class BibAssignmentResultDto
{
    public int EventId { get; set; }
    public int AssignedCount { get; set; }
    public int AlreadyAssignedCount { get; set; }
    /// <summary>Cảnh báo, VD "Cự ly 42K: hết số trong dải BIB, còn 12 đăng ký chưa gán".</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

// ============================ Race kit (UC-52, UC-53) ============================

/// <summary>Kết quả tra cứu tại quầy. Số giấy tờ được che bớt (VD ******1234) – BR-37.</summary>
public class RaceKitLookupItemDto
{
    public int RegistrationId { get; set; }
    public int? BibNumber { get; set; }
    public string FullName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string TShirtSize { get; set; } = "";
    public string Status { get; set; } = "";
    public string? IdNumberMasked { get; set; }
    public bool IsMinor { get; set; }
    public bool HasGuardianConsent { get; set; }
    public bool PickedUp { get; set; }
    public DateTime? PickedUpAt { get; set; }
    /// <summary>Cảnh báo cho Staff: chưa có BIB, thiếu đồng ý giám hộ, là đăng ký của chính Staff (BR-32)…</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>UC-53 (+ UC-54 nhận hộ – GĐ3). BR-20.</summary>
public class RaceKitPickupRequest
{
    [Range(typeof(bool), "true", "true", ErrorMessage = "Phải đối chiếu giấy tờ trước khi phát kit")]
    public bool IdChecked { get; set; }
    public bool IsProxy { get; set; }
    [StringLength(100)] public string? ProxyName { get; set; }
    [StringLength(20)] public string? ProxyIdNumber { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}

public class RaceKitPickupDto
{
    public int RegistrationId { get; set; }
    public int? BibNumber { get; set; }
    public string FullName { get; set; } = "";
    public DateTime PickedUpAt { get; set; }
    public string StaffName { get; set; } = "";
    public bool IsProxy { get; set; }
    public string? ProxyName { get; set; }
}

// ============================ Bấm giờ (UC-57, UC-58) ============================

/// <summary>UC-57: ghi thời gian tại checkpoint theo BIB. BR-21 (thứ tự thời gian), BR-32 (không tự ghi cho mình).</summary>
public class RecordTimingRequest
{
    [Required] public int EventId { get; set; }
    [Range(1, 999_999)] public int BibNumber { get; set; }
    [Required] public int CheckpointId { get; set; }
    /// <summary>Giờ VN, chính xác tới mili giây.</summary>
    [Required] public DateTime RecordedAt { get; set; }
}

public class TimingRecordDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int BibNumber { get; set; }
    public string FullName { get; set; } = "";
    public int CheckpointId { get; set; }
    public string CheckpointName { get; set; } = "";
    public DateTime RecordedAt { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>UC-58: VĐV bỏ cuộc tại trạm ⇒ DNF với lý do (BR-23).</summary>
public class RecordWithdrawalRequest
{
    [Required] public int EventId { get; set; }
    [Range(1, 999_999)] public int BibNumber { get; set; }
    [Required] public int CheckpointId { get; set; }
    [Required, AllowedValues("Voluntary", "Medical")] public string Reason { get; set; } = "Voluntary";
    [StringLength(500)] public string? Note { get; set; }
}
