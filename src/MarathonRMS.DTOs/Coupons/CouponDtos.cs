using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Coupons;

/// <summary>Coupon của BTC (UC-27). Status: Active | Scheduled | Expired | Exhausted | Inactive.</summary>
public class CouponDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int? EventId { get; set; }
    public string? EventName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string DiscountType { get; set; } = "";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? PartnerName { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// UC-27. Khi coupon đã có lượt dùng, chỉ sửa được: ValidTo, MaxUses (≥ số đã dùng), PartnerName, IsActive
/// – để công bằng với người đã dùng.
/// </summary>
public class UpsertCouponRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã"), StringLength(30, MinimumLength = 4)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Mã chỉ gồm chữ không dấu, số, '-' và '_'")]
    public string Code { get; set; } = "";

    public int? EventId { get; set; }
    public int? CategoryId { get; set; }

    [Required, AllowedValues("Percent", "Amount")]
    public string DiscountType { get; set; } = "Percent";

    [Range(1, 100_000_000, ErrorMessage = "Giá trị giảm phải lớn hơn 0")]
    public decimal DiscountValue { get; set; }

    [Range(1000, 100_000_000)]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(1, 1_000_000)]
    public int MaxUses { get; set; } = 100;

    [Required] public DateTime ValidFrom { get; set; }
    [Required] public DateTime ValidTo { get; set; }

    [StringLength(200)] public string? PartnerName { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Giải + cự ly của BTC để chọn phạm vi áp dụng coupon.</summary>
public class CouponEventOptionDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string Status { get; set; } = "";
    public IReadOnlyList<CouponCategoryOptionDto> Categories { get; set; } = Array.Empty<CouponCategoryOptionDto>();
}

public class CouponCategoryOptionDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Member kiểm tra mã ở form đăng ký – chỉ xem trước, chưa trừ lượt.</summary>
public class ValidateCouponRequest
{
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required] public int CategoryId { get; set; }
}

/// <summary>Kết quả áp mã (BR-11): số tiền giảm đã làm tròn xuống bội 1.000đ, không vượt trần và không vượt giá.</summary>
public class CouponQuoteDto
{
    public int CouponId { get; set; }
    public string Code { get; set; } = "";
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    /// <summary>VD "Giảm 10% (tối đa 100.000đ) – Ngân hàng ABC".</summary>
    public string Description { get; set; } = "";
}
