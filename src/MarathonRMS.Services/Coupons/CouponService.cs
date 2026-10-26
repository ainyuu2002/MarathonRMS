using System.Globalization;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Coupons;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Coupons;

/// <summary>
/// UC-27 – coupon của BTC (BR-11). Quản lý: Organizer. Áp mã: B gọi QuoteAsync + RedeemAsync trong transaction đăng ký,
/// ReleaseAsync khi đăng ký hết hạn / bị hủy (BR-13).
/// </summary>
public interface ICouponService
{
    // ---- Organizer quản lý ----
    Task<PagedResult<CouponDto>> SearchMineAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);
    Task<CouponDto> GetAsync(int couponId, CancellationToken ct = default);
    Task<CouponDto> CreateAsync(UpsertCouponRequest request, CancellationToken ct = default);
    Task<CouponDto> UpdateAsync(int couponId, UpsertCouponRequest request, CancellationToken ct = default);
    /// <summary>Xóa nếu chưa dùng lần nào; đã dùng ⇒ chỉ được ngừng kích hoạt (IsActive = false).</summary>
    Task DeleteAsync(int couponId, CancellationToken ct = default);
    Task<IReadOnlyList<CouponEventOptionDto>> GetEventOptionsAsync(CancellationToken ct = default);

    // ---- Áp mã (dùng chung cho B) ----
    /// <summary>Member xem trước số tiền giảm cho một cự ly theo giá hiện hành (không trừ lượt).</summary>
    Task<CouponQuoteDto> PreviewAsync(ValidateCouponRequest request, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra BR-11 và tính số tiền giảm cho giá gốc <paramref name="originalPrice"/>. Không thay đổi dữ liệu.
    /// B gọi trong IRegistrationService.CreateAsync với giá đã tính theo BR-10.
    /// </summary>
    Task<CouponQuoteDto> QuoteAsync(string code, int categoryId, decimal originalPrice, string userId, CancellationToken ct = default);

    /// <summary>Tăng UsedCount (concurrency token RowVersion). Gọi trong CÙNG transaction tạo đăng ký; hết lượt ⇒ 409.</summary>
    Task RedeemAsync(int couponId, CancellationToken ct = default);

    /// <summary>Trả lại 1 lượt (đăng ký hết hạn thanh toán / bị hủy – BR-13). Không SaveChanges.</summary>
    Task ReleaseAsync(int couponId, CancellationToken ct = default);
}

public class CouponService : ICouponService
{
    private readonly ICouponRepository _coupons;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public CouponService(ICouponRepository coupons, IOrganizationScope scope, ICurrentUserService currentUser,
        ISystemSettingsProvider settings, IAuditLogger audit, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _coupons = coupons;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
        _clock = clock;
        _uow = uow;
    }

    // ============================ Quản lý ============================

    public async Task<PagedResult<CouponDto>> SearchMineAsync(string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        var query = _coupons.QueryWithScope().Where(c => c.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(c => c.Code.Contains(k) || (c.PartnerName != null && c.PartnerName.Contains(k)));
        }
        var paged = await query.OrderByDescending(c => c.CreatedAt).ToPagedAsync(page, pageSize, ct);
        return new PagedResult<CouponDto>
        {
            Items = paged.Items.Select(ToDto).ToList(), Page = paged.Page, PageSize = paged.PageSize, TotalCount = paged.TotalCount
        };
    }

    public async Task<CouponDto> GetAsync(int couponId, CancellationToken ct = default)
    {
        var coupon = await _coupons.QueryWithScope().FirstOrDefaultAsync(c => c.CouponId == couponId, ct)
                     ?? throw new NotFoundException("Không tìm thấy coupon.");
        EnsureOwned(coupon);
        return ToDto(coupon);
    }

    public async Task<CouponDto> CreateAsync(UpsertCouponRequest request, CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        _scope.EnsureCanManage(orgId);
        await _scope.EnsureOrganizationActiveAsync(orgId, ct);

        var code = NormalizeCode(request.Code);
        if (await _coupons.CodeExistsAsync(code, null, ct))
            throw new BusinessRuleException("BR-11", $"Mã {code} đã tồn tại.");
        await ValidateAsync(request, orgId, ct);

        var coupon = new Coupon
        {
            OrganizationId = orgId,
            Code = code,
            CreatedById = _currentUser.UserId,
            CreatedAt = _clock.Now
        };
        Apply(coupon, request);
        await _coupons.AddAsync(coupon, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(coupon.CouponId, ct);
    }

    public async Task<CouponDto> UpdateAsync(int couponId, UpsertCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await _coupons.GetTrackedAsync(couponId, ct) ?? throw new NotFoundException("Không tìm thấy coupon.");
        EnsureOwned(coupon);
        await _scope.EnsureOrganizationActiveAsync(coupon.OrganizationId!.Value, ct);

        var code = NormalizeCode(request.Code);
        if (coupon.UsedCount > 0)
        {
            // Đã có người dùng: không đổi mã, mức giảm, phạm vi – chỉ gia hạn, thêm lượt, bật/tắt
            if (code != coupon.Code || request.DiscountType != coupon.DiscountType.ToString() || request.DiscountValue != coupon.DiscountValue
                || request.MaxDiscountAmount != coupon.MaxDiscountAmount || request.EventId != coupon.EventId
                || request.CategoryId != coupon.CategoryId || request.ValidFrom != coupon.ValidFrom)
                throw new BusinessRuleException("BR-11", "Coupon đã được dùng – chỉ sửa được hạn kết thúc, số lượt, đối tác và trạng thái.");
            if (request.MaxUses < coupon.UsedCount)
                throw new BusinessRuleException("BR-11", $"Số lượt tối đa không được nhỏ hơn số đã dùng ({coupon.UsedCount}).");
            if (request.ValidTo <= coupon.ValidFrom)
                throw new BusinessRuleException("BR-11", "Hạn kết thúc phải sau ngày bắt đầu.", 400);
            coupon.ValidTo = request.ValidTo;
            coupon.MaxUses = request.MaxUses;
            coupon.PartnerName = request.PartnerName?.Trim();
            coupon.IsActive = request.IsActive;
        }
        else
        {
            if (await _coupons.CodeExistsAsync(code, couponId, ct))
                throw new BusinessRuleException("BR-11", $"Mã {code} đã tồn tại.");
            await ValidateAsync(request, coupon.OrganizationId!.Value, ct);
            coupon.Code = code;
            Apply(coupon, request);
        }
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(couponId, ct);
    }

    public async Task DeleteAsync(int couponId, CancellationToken ct = default)
    {
        var coupon = await _coupons.GetTrackedAsync(couponId, ct) ?? throw new NotFoundException("Không tìm thấy coupon.");
        EnsureOwned(coupon);
        if (coupon.UsedCount > 0 || await _coupons.IsReferencedAsync(couponId, ct))
            throw new BusinessRuleException("BR-11", "Coupon đã được dùng – hãy ngừng kích hoạt thay vì xóa.");
        _coupons.Remove(coupon);
        await _audit.LogAsync("Coupon.Delete", nameof(Coupon), couponId.ToString(), new { coupon.Code }, null, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CouponEventOptionDto>> GetEventOptionsAsync(CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        var events = await _coupons.GetEventsWithCategoriesAsync(orgId, ct);
        return events.Select(e => new CouponEventOptionDto
        {
            EventId = e.EventId,
            EventName = e.Name,
            Status = e.Status.ToString(),
            Categories = e.Categories.OrderBy(c => c.DistanceKm)
                .Select(c => new CouponCategoryOptionDto { CategoryId = c.CategoryId, Name = c.Name }).ToList()
        }).ToList();
    }

    // ============================ Áp mã ============================

    public async Task<CouponQuoteDto> PreviewAsync(ValidateCouponRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");
        var price = await _coupons.GetCurrentPriceAsync(request.CategoryId, _clock.Now, ct)
                    ?? throw new BusinessRuleException("BR-10", "Cự ly hiện không trong thời gian bán.");
        return await QuoteAsync(request.Code, request.CategoryId, price, userId, ct);
    }

    public async Task<CouponQuoteDto> QuoteAsync(string code, int categoryId, decimal originalPrice, string userId, CancellationToken ct = default)
    {
        var coupon = await _coupons.GetByCodeAsync(NormalizeCode(code), ct)
                     ?? throw new BusinessRuleException("BR-11", "Mã giảm giá không tồn tại.");
        var category = await _coupons.GetCategoryWithEventAsync(categoryId, ct)
                       ?? throw new NotFoundException("Không tìm thấy cự ly.");
        var now = _clock.Now;

        if (!coupon.IsActive) throw new BusinessRuleException("BR-11", "Mã giảm giá đã ngừng áp dụng.");
        if (now < coupon.ValidFrom) throw new BusinessRuleException("BR-11", $"Mã giảm giá có hiệu lực từ {coupon.ValidFrom:dd/MM/yyyy HH:mm}.");
        if (now >= coupon.ValidTo) throw new BusinessRuleException("BR-11", "Mã giảm giá đã hết hạn.");
        if (coupon.UsedCount >= coupon.MaxUses) throw new BusinessRuleException("BR-11", "Mã giảm giá đã hết lượt sử dụng.");
        if (coupon.OrganizationId is null)
            throw new BusinessRuleException("BR-11", "Mã của nền tảng chưa được hỗ trợ cho giải này."); // GĐ3 – Loyalty Coin
        if (coupon.OrganizationId != category.Event.OrganizationId
            || (coupon.EventId is not null && coupon.EventId != category.EventId)
            || (coupon.CategoryId is not null && coupon.CategoryId != categoryId))
            throw new BusinessRuleException("BR-11", "Mã giảm giá không áp dụng cho cự ly này.");
        if (coupon.OwnerMemberId is not null && coupon.OwnerMemberId != userId)
            throw new BusinessRuleException("BR-11", "Mã giảm giá này dành riêng cho tài khoản khác.");

        var discount = CalculateDiscount(coupon, originalPrice);
        return new CouponQuoteDto
        {
            CouponId = coupon.CouponId,
            Code = coupon.Code,
            OriginalPrice = originalPrice,
            DiscountAmount = discount,
            FinalPrice = originalPrice - discount,
            Description = Describe(coupon)
        };
    }

    public async Task RedeemAsync(int couponId, CancellationToken ct = default)
    {
        var coupon = await _coupons.GetTrackedAsync(couponId, ct) ?? throw new NotFoundException("Không tìm thấy coupon.");
        if (coupon.UsedCount >= coupon.MaxUses)
            throw new BusinessRuleException("BR-11", "Mã giảm giá đã hết lượt sử dụng.");
        coupon.UsedCount++;
        // RowVersion: hai người dùng lượt cuối cùng lúc ⇒ người sau nhận DbUpdateConcurrencyException (→ 409)
    }

    public async Task ReleaseAsync(int couponId, CancellationToken ct = default)
    {
        var coupon = await _coupons.GetTrackedAsync(couponId, ct);
        if (coupon is { UsedCount: > 0 }) coupon.UsedCount--;
    }

    // ============================ Hỗ trợ ============================

    /// <summary>Làm tròn xuống bội 1.000đ (giữ giá cuối là bội 1.000đ – BR-48), không vượt trần, không vượt giá.</summary>
    public static decimal CalculateDiscount(Coupon coupon, decimal price)
    {
        var raw = coupon.DiscountType == DiscountType.Percent
            ? Math.Floor(price * coupon.DiscountValue / 100m / 1000m) * 1000m
            : coupon.DiscountValue;
        if (coupon.MaxDiscountAmount is decimal cap) raw = Math.Min(raw, cap);
        return Math.Max(0, Math.Min(raw, price));
    }

    private static string Describe(Coupon c)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var text = c.DiscountType == DiscountType.Percent
            ? $"Giảm {c.DiscountValue:0}%" + (c.MaxDiscountAmount is decimal cap ? $" (tối đa {cap.ToString("N0", vi)}đ)" : "")
            : $"Giảm {c.DiscountValue.ToString("N0", vi)}đ";
        return string.IsNullOrEmpty(c.PartnerName) ? text : $"{text} – {c.PartnerName}";
    }

    private async Task ValidateAsync(UpsertCouponRequest r, int orgId, CancellationToken ct)
    {
        if (r.ValidFrom >= r.ValidTo)
            throw new BusinessRuleException("BR-11", "Hạn kết thúc phải sau ngày bắt đầu.", 400);
        if (r.DiscountType == nameof(DiscountType.Percent))
        {
            if (r.DiscountValue is < 1 or > 100 || r.DiscountValue != Math.Floor(r.DiscountValue))
                throw new BusinessRuleException("BR-11", "Phần trăm giảm là số nguyên từ 1 đến 100.", 400);
        }
        else
        {
            var maxPrice = await _settings.GetDecimalAsync("Price.Max", 20_000_000m, ct);
            if (r.DiscountValue % 1000 != 0 || r.DiscountValue > maxPrice)
                throw new BusinessRuleException("BR-48", "Số tiền giảm phải là bội số 1.000đ và không vượt giá tối đa của hệ thống.", 400);
            if (r.MaxDiscountAmount is not null)
                throw new BusinessRuleException("BR-11", "Trần giảm chỉ dùng cho coupon phần trăm.", 400);
        }
        if (r.MaxDiscountAmount is decimal cap && cap % 1000 != 0)
            throw new BusinessRuleException("BR-48", "Trần giảm phải là bội số 1.000đ.", 400);

        if (r.CategoryId is not null && r.EventId is null)
            throw new BusinessRuleException("BR-11", "Chọn giải trước khi chọn cự ly.", 400);
        if (r.EventId is int eventId)
        {
            var ev = await _coupons.GetEventAsync(eventId, ct);
            if (ev is null || ev.OrganizationId != orgId)
                throw new ForbiddenException("Chỉ tạo coupon cho giải của ban tổ chức mình.", "BR-30");
        }
        if (r.CategoryId is int categoryId)
        {
            var category = await _coupons.GetCategoryWithEventAsync(categoryId, ct);
            if (category is null || category.EventId != r.EventId)
                throw new BusinessRuleException("BR-11", "Cự ly không thuộc giải đã chọn.", 400);
        }
    }

    private static void Apply(Coupon c, UpsertCouponRequest r)
    {
        c.EventId = r.EventId;
        c.CategoryId = r.CategoryId;
        c.DiscountType = Enum.Parse<DiscountType>(r.DiscountType);
        c.DiscountValue = r.DiscountValue;
        c.MaxDiscountAmount = r.MaxDiscountAmount;
        c.MaxUses = r.MaxUses;
        c.ValidFrom = r.ValidFrom;
        c.ValidTo = r.ValidTo;
        c.PartnerName = string.IsNullOrWhiteSpace(r.PartnerName) ? null : r.PartnerName.Trim();
        c.IsActive = r.IsActive;
    }

    private void EnsureOwned(Coupon coupon)
    {
        if (coupon.OrganizationId is not int orgId)
            throw new ForbiddenException("Coupon của nền tảng không quản lý ở đây.", "BR-30");
        _scope.EnsureCanManage(orgId);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private CouponDto ToDto(Coupon c)
    {
        var now = _clock.Now;
        var status = !c.IsActive ? "Inactive"
                   : now < c.ValidFrom ? "Scheduled"
                   : now >= c.ValidTo ? "Expired"
                   : c.UsedCount >= c.MaxUses ? "Exhausted"
                   : "Active";
        return new CouponDto
        {
            Id = c.CouponId, Code = c.Code, EventId = c.EventId, EventName = c.Event?.Name,
            CategoryId = c.CategoryId, CategoryName = c.Category?.Name, DiscountType = c.DiscountType.ToString(),
            DiscountValue = c.DiscountValue, MaxDiscountAmount = c.MaxDiscountAmount, MaxUses = c.MaxUses,
            UsedCount = c.UsedCount, ValidFrom = c.ValidFrom, ValidTo = c.ValidTo, PartnerName = c.PartnerName,
            IsActive = c.IsActive, Status = status, CreatedAt = c.CreatedAt
        };
    }
}
