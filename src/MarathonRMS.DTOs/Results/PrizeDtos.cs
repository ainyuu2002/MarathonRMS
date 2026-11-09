using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Results;

// ============================ Cơ cấu giải thưởng (UC-28) ============================

public class PrizeDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    /// <summary>Overall / AgeGroup / Nationality.</summary>
    public string AwardType { get; set; } = "";
    public string Gender { get; set; } = "";
    public int? AgeGroupId { get; set; }
    public string? AgeGroupName { get; set; }
    public string? Nationality { get; set; }
    public int Rank { get; set; }
    public string Reward { get; set; } = "";
    public decimal? CashAmount { get; set; }
}

public class SavePrizeRequest
{
    [Required, AllowedValues("Overall", "AgeGroup", "Nationality")] public string AwardType { get; set; } = "Overall";
    [Required, AllowedValues("M", "F")] public string Gender { get; set; } = "M";
    /// <summary>Bắt buộc khi AwardType = AgeGroup.</summary>
    public int? AgeGroupId { get; set; }
    /// <summary>Bắt buộc khi AwardType = Nationality (VD "Việt Nam").</summary>
    [StringLength(60)] public string? Nationality { get; set; }
    [Range(1, 50, ErrorMessage = "Hạng từ 1 đến 50")] public int Rank { get; set; } = 1;
    [Required(ErrorMessage = "Nhập phần thưởng"), StringLength(300)] public string Reward { get; set; } = "";
    [Range(0, 1_000_000_000, ErrorMessage = "Tiền thưởng không hợp lệ")] public decimal? CashAmount { get; set; }
}

public class PrizeCategoryOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsRanked { get; set; }
}

public class EventPrizesDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string ResultsStatus { get; set; } = "";
    /// <summary>AllowAll / HighestOnly (EventPolicies.AwardStackingPolicy).</summary>
    public string AwardStackingPolicy { get; set; } = "";
    /// <summary>Sửa được khi kết quả chưa chính thức.</summary>
    public bool CanEdit { get; set; }
    public IReadOnlyList<PrizeCategoryOptionDto> Categories { get; set; } = Array.Empty<PrizeCategoryOptionDto>();
    public IReadOnlyList<AgeGroupOptionDto> AgeGroups { get; set; } = Array.Empty<AgeGroupOptionDto>();
    public IReadOnlyList<PrizeDto> Prizes { get; set; } = Array.Empty<PrizeDto>();
}

// ============================ Danh sách nhận giải / Vinh danh (UC-65, BR-29) ============================

public class WinnerDto
{
    public int RegistrationId { get; set; }
    public int? BibNumber { get; set; }
    /// <summary>Trang công khai: tên theo BR-37 (ẩn tên / dưới 18 ⇒ "VĐV #BIB").</summary>
    public string DisplayName { get; set; } = "";
    public string? ClubName { get; set; }
    public long? FinalTimeMs { get; set; }
}

public class AwardDto
{
    public PrizeDto Prize { get; set; } = new();
    /// <summary>Null = chưa đủ VĐV về đích để trao giải ở hạng này.</summary>
    public WinnerDto? Winner { get; set; }
}

public class CategoryWinnersDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public IReadOnlyList<AwardDto> Awards { get; set; } = Array.Empty<AwardDto>();
}

public class EventWinnersDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string ResultsStatus { get; set; } = "";
    /// <summary>False ⇒ bản xem trước cho BTC (kết quả chưa chính thức).</summary>
    public bool IsOfficial { get; set; }
    public string AwardStackingPolicy { get; set; } = "";
    public IReadOnlyList<CategoryWinnersDto> Categories { get; set; } = Array.Empty<CategoryWinnersDto>();
}
