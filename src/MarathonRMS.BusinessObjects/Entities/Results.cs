using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

public class TimingRecord
{
    public int TimingRecordId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public int CheckpointId { get; set; }
    public Checkpoint Checkpoint { get; set; } = null!;
    public DateTime RecordedAt { get; set; }
    public TimingSource Source { get; set; } = TimingSource.Manual;
    public string RecordedById { get; set; } = "";
}

public class Result
{
    public int ResultId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public long? GunTimeMs { get; set; }
    public long? ChipTimeMs { get; set; }
    public int PenaltySeconds { get; set; }
    public long? FinalTimeMs { get; set; }
    public ResultStatus Status { get; set; }
    public DnfReason? DnfReason { get; set; }
    public int? DnfCheckpointId { get; set; }
    public Checkpoint? DnfCheckpoint { get; set; }
    public int? OverallRank { get; set; }
    public int? GenderRank { get; set; }
    public int? AgeGroupId { get; set; }
    public AgeGroup? AgeGroup { get; set; }
    public int? AgeGroupRank { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ResultPenalty> Penalties { get; set; } = new List<ResultPenalty>();
}

/// <summary>Danh mục lý do phạt/DSQ (seed sẵn; Admin quản lý ở GĐ3 – UC-85).</summary>
public class PenaltyReason
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public PenaltyAction DefaultAction { get; set; }
    public int? DefaultMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ResultPenalty
{
    public int PenaltyId { get; set; }
    public int ResultId { get; set; }
    public Result Result { get; set; } = null!;
    public string ReasonCode { get; set; } = "";
    public PenaltyReason Reason { get; set; } = null!;
    public PenaltyAction Action { get; set; }
    public int? Minutes { get; set; }
    public int? CheckpointId { get; set; }
    public string? Note { get; set; }
    public string IssuedById { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
