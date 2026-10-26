using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Coupons;
using MarathonRMS.Services.Coupons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-27 – coupon của BTC (BR-11).</summary>
[ApiController]
[Route("api/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _coupons;
    public CouponsController(ICouponService coupons) => _coupons = coupons;

    [HttpGet]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<PagedResult<CouponDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _coupons.SearchMineAsync(keyword, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<CouponDto>> Get(int id, CancellationToken ct) => Ok(await _coupons.GetAsync(id, ct));

    /// <summary>Giải + cự ly của BTC để chọn phạm vi áp dụng.</summary>
    [HttpGet("event-options")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<IReadOnlyList<CouponEventOptionDto>>> GetEventOptions(CancellationToken ct) =>
        Ok(await _coupons.GetEventOptionsAsync(ct));

    [HttpPost]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<CouponDto>> Create(UpsertCouponRequest request, CancellationToken ct)
    {
        var coupon = await _coupons.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = coupon.Id }, coupon);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<CouponDto>> Update(int id, UpsertCouponRequest request, CancellationToken ct) =>
        Ok(await _coupons.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _coupons.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Member kiểm tra mã ở form đăng ký: số tiền giảm theo giá hiện hành của cự ly (chưa trừ lượt).</summary>
    [HttpPost("validate")]
    [Authorize(Policy = AppPolicies.MemberOnly)]
    public async Task<ActionResult<CouponQuoteDto>> Validate(ValidateCouponRequest request, CancellationToken ct) =>
        Ok(await _coupons.PreviewAsync(request, ct));
}
