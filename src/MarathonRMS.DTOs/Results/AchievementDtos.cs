namespace MarathonRMS.DTOs.Results;

// ============================ Hồ sơ thành tích (UC-64) ============================

public class AchievementItemDto
{
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string CategoryName { get; set; } = "";
    /// <summary>Road / Trail.</summary>
    public string Discipline { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int? BibNumber { get; set; }
    /// <summary>Finished / DNF / DNS / DSQ.</summary>
    public string Status { get; set; } = "";
    public long? FinalTimeMs { get; set; }
    public int? OverallRank { get; set; }
    public int? GenderRank { get; set; }
    public string? AgeGroupName { get; set; }
    public int? AgeGroupRank { get; set; }
    public bool IsProvisional { get; set; }
    /// <summary>Về đích và kết quả đã chính thức.</summary>
    public bool HasCertificate { get; set; }
}

/// <summary>Thành tích tốt nhất theo loại đường chạy + cự ly (VD Road 21 km).</summary>
public class PersonalBestDto
{
    public string Discipline { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public long FinalTimeMs { get; set; }
    public int RegistrationId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
}

public class AchievementProfileDto
{
    /// <summary>Null = tài khoản chưa có hồ sơ người chạy chính chủ.</summary>
    public int? ParticipantId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? ClubName { get; set; }
    /// <summary>Đã bật đồng ý "hiện tên công khai" (PublicListing) và đủ 18 tuổi ⇒ có trang /Runners/{id}.</summary>
    public bool IsPublic { get; set; }
    public int FinishedCount { get; set; }
    public decimal TotalDistanceKm { get; set; }
    public IReadOnlyList<PersonalBestDto> PersonalBests { get; set; } = Array.Empty<PersonalBestDto>();
    public IReadOnlyList<AchievementItemDto> Items { get; set; } = Array.Empty<AchievementItemDto>();
}

/// <summary>E-certificate – chỉ khi VĐV về đích và kết quả đã chính thức.</summary>
public class CertificateDto
{
    public int RegistrationId { get; set; }
    public string RunnerName { get; set; } = "";
    public string EventName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string VenueName { get; set; } = "";
    public string City { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int? BibNumber { get; set; }
    public long? FinalTimeMs { get; set; }
    public long? GunTimeMs { get; set; }
    public long? ChipTimeMs { get; set; }
    public int? OverallRank { get; set; }
    public int? GenderRank { get; set; }
    public string Gender { get; set; } = "";
    public string? AgeGroupName { get; set; }
    public int? AgeGroupRank { get; set; }
    public int FinishersInCategory { get; set; }
    public DateTime? OfficialAt { get; set; }
}
