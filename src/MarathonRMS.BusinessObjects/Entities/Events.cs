using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

public class Event
{
    public int EventId { get; set; }
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    /// <summary>HTML đã lọc phía server (BR-50).</summary>
    public string? DescriptionHtml { get; set; }
    public string? BannerUrl { get; set; }
    public string WaiverText { get; set; } = "";
    public int WaiverVersion { get; set; } = 1;

    public DateOnly EventDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime RegistrationOpenAt { get; set; }
    public DateTime RegistrationCloseAt { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateOnly? PostponedFromDate { get; set; }
    public string? CancelReason { get; set; }

    public ResultsStatus ResultsStatus { get; set; } = ResultsStatus.Draft;
    public DateTime? ResultsProvisionalAt { get; set; }
    public DateTime? ResultsOfficialAt { get; set; }

    public string? EmergencyPhone { get; set; }
    public string? InsuranceNote { get; set; }
    public string CreatedById { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public EventPolicy Policy { get; set; } = null!;
    public ICollection<RaceCategory> Categories { get; set; } = new List<RaceCategory>();
    public ICollection<AgeGroup> AgeGroups { get; set; } = new List<AgeGroup>();
    public ICollection<EventMedia> Media { get; set; } = new List<EventMedia>();
}

/// <summary>Chính sách cấu hình theo giải (D15, mục 0.3 BA). Quan hệ 1–1 với Event.</summary>
public class EventPolicy
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public AgeCalculationMode AgeCalculationMode { get; set; } = AgeCalculationMode.RaceDate;
    public int GuardianConsentUnderAge { get; set; } = 18;
    public int MaxDependentsPerAccount { get; set; } = 5;
    public DateTime? InfoEditDeadline { get; set; }

    public TransferPolicy TransferPolicy { get; set; } = TransferPolicy.Forbidden;
    public decimal TransferFee { get; set; }
    public int MaxTransfers { get; set; } = 1;
    public DateTime? TransferDeadline { get; set; }

    public decimal CategoryChangeFee { get; set; }
    public int MaxCategoryChanges { get; set; } = 1;
    public DateTime? CategoryChangeDeadline { get; set; }
    public bool LockSpecialEntries { get; set; } = true;

    /// <summary>Bắt buộc chọn trước khi mở đăng ký (BR-38) – không có mặc định.</summary>
    public RankingBasis? OverallRankingBasis { get; set; }
    public RankingBasis? AgeGroupRankingBasis { get; set; }
    public TieBreakRule TieBreakRule { get; set; } = TieBreakRule.Shared;
    public int? LateStartToleranceMinutes { get; set; }
    public int ComplaintWindowMinutes { get; set; } = 1440;
    public decimal ComplaintFee { get; set; }
    public AwardStackingPolicy AwardStackingPolicy { get; set; } = AwardStackingPolicy.AllowAll;
    public ForceMajeurePolicy ForceMajeurePolicy { get; set; } = ForceMajeurePolicy.NoRefund;
}

public class RaceCategory
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string Name { get; set; } = "";
    public Discipline Discipline { get; set; } = Discipline.Road;
    public CategoryType CategoryType { get; set; } = CategoryType.Standard;
    public decimal DistanceKm { get; set; }
    public decimal? ActualDistanceKm { get; set; }
    public int? ElevationGainM { get; set; }
    public byte? ItraPoints { get; set; }
    public int Capacity { get; set; }
    public DateTime StartAt { get; set; }
    public int? CutOffMinutes { get; set; }
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int? EscortRequiredUnderAge { get; set; }
    public bool IsTimed { get; set; } = true;
    public bool IsRanked { get; set; } = true;
    public int BibFrom { get; set; }
    public int BibTo { get; set; }
    public string? BibColor { get; set; }
    public string? MedalImageUrl { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();
    public ICollection<Checkpoint> Checkpoints { get; set; } = new List<Checkpoint>();
}

public class PriceTier
{
    public int PriceTierId { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public class AgeGroup
{
    public int AgeGroupId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    /// <summary>Null = áp dụng cho mọi cự ly của giải.</summary>
    public int? CategoryId { get; set; }
    public RaceCategory? Category { get; set; }
    public string Name { get; set; } = "";
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
}

public class Checkpoint
{
    public int CheckpointId { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int Sequence { get; set; }
    public CheckpointType Type { get; set; } = CheckpointType.Split;
    public bool IsMandatory { get; set; } = true;
    /// <summary>Giờ đồng hồ phải rời trạm (trail). Null = không có cut-off tại trạm.</summary>
    public DateTime? CutOffAt { get; set; }
    public StationType StationType { get; set; } = StationType.None;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class EventMedia
{
    public int MediaId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string FileUrl { get; set; } = "";
    public string? Caption { get; set; }
    public MediaKind Kind { get; set; } = MediaKind.Inline;
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string UploadedById { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}
