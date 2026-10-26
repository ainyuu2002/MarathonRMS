using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

// Bảng Giai đoạn 2 (BA mục 4.3). Schema có sẵn để A, B, C làm song song – không ai phải chờ migration.

/// <summary>Mã giảm giá (UC-27, BR-11). OrganizationId null = coupon nền tảng; OwnerMemberId có giá trị = coupon cá nhân.</summary>
public class Coupon
{
    public int CouponId { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int? EventId { get; set; }
    public Event? Event { get; set; }
    public int? CategoryId { get; set; }
    public RaceCategory? Category { get; set; }
    public string? OwnerMemberId { get; set; }
    public AppUser? OwnerMember { get; set; }
    public string Code { get; set; } = "";
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    /// <summary>Trần số tiền giảm cho coupon % (null = không giới hạn).</summary>
    public decimal? MaxDiscountAmount { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? PartnerName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Chuyển nhượng BIB (UC-47, BR-16).</summary>
public class BibTransfer
{
    public int TransferId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public int? ToParticipantId { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    public decimal FeeAmount { get; set; }
    public int? NewRegistrationId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>Khiếu nại kết quả (UC-66, BR-27).</summary>
public class ResultComplaint
{
    public int ComplaintId { get; set; }
    public int ResultId { get; set; }
    public Result Result { get; set; } = null!;
    public string UserId { get; set; } = "";
    public string Content { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public decimal FeeAmount { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public string? Response { get; set; }
    public string? HandledById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
}

/// <summary>Hoàn tiền khi BTC hủy giải (UC-50, BR-15). Thông tin ngân hàng là dữ liệu nhạy cảm (NĐ 356 Điều 4 – k).</summary>
public class Refund
{
    public int RefundId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
    public decimal Amount { get; set; }
    public RefundReason Reason { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string? BankInfo { get; set; }
    public string? ProcessedById { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? TransactionRef { get; set; }
}

/// <summary>Cơ cấu giải thưởng (UC-28, BR-29).</summary>
public class Prize
{
    public int PrizeId { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public AwardType AwardType { get; set; }
    public Gender Gender { get; set; }
    public int? AgeGroupId { get; set; }
    public AgeGroup? AgeGroup { get; set; }
    public string? Nationality { get; set; }
    public int Rank { get; set; }
    public string Reward { get; set; } = "";
    public decimal? CashAmount { get; set; }
}

/// <summary>Đường chạy của cự ly dạng GeoJSON (UC-26, BR-40).</summary>
public class RaceRoute
{
    public int RouteId { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public string RouteGeoJson { get; set; } = "";
    public decimal CalculatedDistanceKm { get; set; }
    public RouteSource Source { get; set; }
    public string? UpdatedById { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RoutePoi
{
    public int PoiId { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory Category { get; set; } = null!;
    public PoiType Type { get; set; }
    public string Name { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? DistanceFromStartKm { get; set; }
}

/// <summary>Khối nội dung trang giới thiệu giải (UC-88). BodyHtml đã lọc (BR-50).</summary>
public class EventContentSection
{
    public int SectionId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public SectionType SectionType { get; set; }
    public string Title { get; set; } = "";
    public string? BodyHtml { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>Lịch trình giải (UC-88): nhận race kit, xuất phát từng cự ly, lễ trao giải…</summary>
public class EventScheduleItem
{
    public int ItemId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string Title { get; set; } = "";
    public string? Location { get; set; }
    public string? Note { get; set; }
}
