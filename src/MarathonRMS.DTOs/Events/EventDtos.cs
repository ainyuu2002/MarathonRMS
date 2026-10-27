using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Events;

// ============================ Đọc (public) ============================

/// <summary>Dòng trong danh sách giải – nguồn của GET /odata/Events (UC-34). Chỉ chứa kiểu đơn giản để OData lọc/sắp xếp được.</summary>
public class EventSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = "";
    public string VenueName { get; set; } = "";
    public string City { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime RegistrationOpenAt { get; set; }
    public DateTime RegistrationCloseAt { get; set; }
    public string Status { get; set; } = "";
    public string? BannerUrl { get; set; }
    /// <summary>"Road", "Trail" hoặc "Road,Trail" nếu giải có cả hai.</summary>
    public string Disciplines { get; set; } = "";
    public decimal MinDistanceKm { get; set; }
    public decimal MaxDistanceKm { get; set; }
}

/// <summary>Chi tiết giải (UC-35): cự ly, giá hiện tại, suất còn lại, chính sách.</summary>
public class EventDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = "";
    public int VenueId { get; set; }
    public string VenueName { get; set; } = "";
    public string VenueAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string? DescriptionHtml { get; set; }
    public string? BannerUrl { get; set; }
    public DateOnly EventDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime RegistrationOpenAt { get; set; }
    public DateTime RegistrationCloseAt { get; set; }
    public string Status { get; set; } = "";
    public string ResultsStatus { get; set; } = "";
    public string? EmergencyPhone { get; set; }
    public string? InsuranceNote { get; set; }
    public int WaiverVersion { get; set; }
    public EventPolicyDto Policy { get; set; } = new();
    public IReadOnlyList<RaceCategoryDto> Categories { get; set; } = Array.Empty<RaceCategoryDto>();
    public IReadOnlyList<AgeGroupDto> AgeGroups { get; set; } = Array.Empty<AgeGroupDto>();
}

// ============================ Ghi (Organizer) ============================

/// <summary>UC-18: tạo / sửa giải. Ràng buộc liên trường (đóng ≤ ngày thi…) kiểm tra trong Service.</summary>
public class UpsertEventRequest
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Name { get; set; } = "";

    [Required]
    public int VenueId { get; set; }

    /// <summary>HTML từ trình soạn thảo – Service lọc trước khi lưu (BR-50).</summary>
    [StringLength(50000)]
    public string? DescriptionHtml { get; set; }

    [StringLength(500), Url]
    public string? BannerUrl { get; set; }

    [Required] public DateOnly EventDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    [Required] public DateTime RegistrationOpenAt { get; set; }
    [Required] public DateTime RegistrationCloseAt { get; set; }

    [StringLength(20), Phone]
    public string? EmergencyPhone { get; set; }

    [StringLength(1000)]
    public string? InsuranceNote { get; set; }

}

/// <summary>UC-20: chuyển trạng thái giải (BR-38 kiểm tra khi mở đăng ký).</summary>
public class ChangeEventStatusRequest
{
    [Required]
    [AllowedValues("Draft", "Published", "RegistrationOpen", "RegistrationClosed", "Completed")]
    public string Status { get; set; } = "";
}

// ============================ Chính sách giải (UC-19) ============================

public class EventPolicyDto
{
    public string AgeCalculationMode { get; set; } = "RaceDate";
    public int GuardianConsentUnderAge { get; set; }
    public int MaxDependentsPerAccount { get; set; }
    public DateTime? InfoEditDeadline { get; set; }
    public string TransferPolicy { get; set; } = "Forbidden";
    public decimal TransferFee { get; set; }
    public int MaxTransfers { get; set; }
    public DateTime? TransferDeadline { get; set; }
    public decimal CategoryChangeFee { get; set; }
    public int MaxCategoryChanges { get; set; }
    public DateTime? CategoryChangeDeadline { get; set; }
    public bool LockSpecialEntries { get; set; }
    public string? OverallRankingBasis { get; set; }
    public string? AgeGroupRankingBasis { get; set; }
    public string TieBreakRule { get; set; } = "Shared";
    public int? LateStartToleranceMinutes { get; set; }
    public int ComplaintWindowMinutes { get; set; }
    public decimal ComplaintFee { get; set; }
    public string AwardStackingPolicy { get; set; } = "AllowAll";
    public string ForceMajeurePolicy { get; set; } = "NoRefund";
}

public class UpdateEventPolicyRequest
{
    [Required, AllowedValues("RaceDate", "CalendarYear")]
    public string AgeCalculationMode { get; set; } = "RaceDate";

    [Range(0, 21)] public int GuardianConsentUnderAge { get; set; } = 18;
    [Range(0, 10)] public int MaxDependentsPerAccount { get; set; } = 5;
    public DateTime? InfoEditDeadline { get; set; }

    [Required, AllowedValues("Forbidden", "Free", "Paid")]
    public string TransferPolicy { get; set; } = "Forbidden";
    [Range(0, 100_000_000)] public decimal TransferFee { get; set; }
    [Range(0, 10)] public int MaxTransfers { get; set; } = 1;
    public DateTime? TransferDeadline { get; set; }

    [Range(0, 100_000_000)] public decimal CategoryChangeFee { get; set; }
    [Range(0, 10)] public int MaxCategoryChanges { get; set; } = 1;
    public DateTime? CategoryChangeDeadline { get; set; }
    public bool LockSpecialEntries { get; set; } = true;

    [AllowedValues(null, "GunTime", "ChipTime")] public string? OverallRankingBasis { get; set; }
    [AllowedValues(null, "GunTime", "ChipTime")] public string? AgeGroupRankingBasis { get; set; }
    [Required, AllowedValues("Shared", "ChipTime")] public string TieBreakRule { get; set; } = "Shared";
    [Range(1, 240)] public int? LateStartToleranceMinutes { get; set; }
    [Range(10, 20160)] public int ComplaintWindowMinutes { get; set; } = 1440;
    [Range(0, 100_000_000)] public decimal ComplaintFee { get; set; }
    [Required, AllowedValues("AllowAll", "HighestOnly")] public string AwardStackingPolicy { get; set; } = "AllowAll";
    [Required, AllowedValues("NoRefund", "CarryOver", "Refund")] public string ForceMajeurePolicy { get; set; } = "NoRefund";
}

// ============================ Cự ly, giá, nhóm tuổi, checkpoint ============================

public class RaceCategoryDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string CategoryType { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public decimal? ActualDistanceKm { get; set; }
    public int? ElevationGainM { get; set; }
    public byte? ItraPoints { get; set; }
    public int Capacity { get; set; }
    /// <summary>Capacity − số đăng ký còn hiệu lực (BR-04).</summary>
    public int RemainingSlots { get; set; }
    public DateTime StartAt { get; set; }
    public int? CutOffMinutes { get; set; }
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int? EscortRequiredUnderAge { get; set; }
    public bool IsTimed { get; set; }
    public bool IsRanked { get; set; }
    public int BibFrom { get; set; }
    public int BibTo { get; set; }
    public string? BibColor { get; set; }
    public string? MedalImageUrl { get; set; }
    /// <summary>Giá của mức giá đang áp dụng tại thời điểm hiện tại (BR-10); null nếu ngoài thời gian bán.</summary>
    public decimal? CurrentPrice { get; set; }
    public string? CurrentPriceTierName { get; set; }
    public IReadOnlyList<PriceTierDto> PriceTiers { get; set; } = Array.Empty<PriceTierDto>();
    public IReadOnlyList<CheckpointDto> Checkpoints { get; set; } = Array.Empty<CheckpointDto>();
    /// <summary>Base64 của RowVersion – gửi lại khi sửa để phát hiện sửa đồng thời.</summary>
    public string? RowVersion { get; set; }
}

/// <summary>UC-21. Ràng buộc liên trường (MaxAge ≥ MinAge, BibFrom &lt; BibTo, dải BIB không chồng lấn, StartAt – BR-39) kiểm tra trong Service.</summary>
public class UpsertRaceCategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, AllowedValues("Road", "Trail")] public string Discipline { get; set; } = "Road";
    [Required, AllowedValues("Standard", "Kids", "Elite")] public string CategoryType { get; set; } = "Standard";
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    [Range(0.1, 1000)] public decimal? ActualDistanceKm { get; set; }
    [Range(0, 20000)] public int? ElevationGainM { get; set; }
    [Range(0, 6)] public byte? ItraPoints { get; set; }
    [Range(1, 100_000)] public int Capacity { get; set; }
    [Required] public DateTime StartAt { get; set; }
    [Range(1, 6000)] public int? CutOffMinutes { get; set; }
    [Range(0, 100)] public int MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(1, 21)] public int? EscortRequiredUnderAge { get; set; }
    public bool IsTimed { get; set; } = true;
    public bool IsRanked { get; set; } = true;
    [Range(1, 999_999)] public int BibFrom { get; set; }
    [Range(1, 999_999)] public int BibTo { get; set; }
    [StringLength(20)] public string? BibColor { get; set; }
    [StringLength(500), Url] public string? MedalImageUrl { get; set; }
    public string? RowVersion { get; set; }
}

public class PriceTierDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

/// <summary>UC-22. BR-47 (phủ kín, không chồng lấn), BR-48 (bội số 1.000đ, min/max) kiểm tra trong Service.</summary>
public class UpsertPriceTierRequest
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [Range(0, 100_000_000)] public decimal Price { get; set; }
    [Required] public DateTime ValidFrom { get; set; }
    [Required] public DateTime ValidTo { get; set; }
}

public class AgeGroupDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int? CategoryId { get; set; }
    public string Name { get; set; } = "";
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
}

/// <summary>UC-23. CategoryId null = áp dụng cho mọi cự ly của giải.</summary>
public class UpsertAgeGroupRequest
{
    public int? CategoryId { get; set; }
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [Range(0, 120)] public int MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
}

public class CheckpointDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int Sequence { get; set; }
    public string Type { get; set; } = "";
    public bool IsMandatory { get; set; }
    public DateTime? CutOffAt { get; set; }
    public string StationType { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

/// <summary>UC-24. CutOffAt tăng dần theo Sequence (BR-39) kiểm tra trong Service.</summary>
public class UpsertCheckpointRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(0, 1000)] public decimal DistanceKm { get; set; }
    [Range(1, 100)] public int Sequence { get; set; }
    [Required, AllowedValues("Start", "Split", "Finish")] public string Type { get; set; } = "Split";
    public bool IsMandatory { get; set; } = true;
    public DateTime? CutOffAt { get; set; }
    [Required, AllowedValues("None", "Water", "Full", "Medical")] public string StationType { get; set; } = "None";
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
}
