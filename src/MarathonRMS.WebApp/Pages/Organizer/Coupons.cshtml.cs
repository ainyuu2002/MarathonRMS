using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Coupons;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-27 – coupon của ban tổ chức.</summary>
public class CouponsModel : BasePageModel
{
    private readonly CouponApiClient _coupons;
    public CouponsModel(CouponApiClient coupons) => _coupons = coupons;

    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int? Edit { get; set; }
    [BindProperty] public UpsertCouponRequest Input { get; set; } = new();

    public PagedResult<CouponDto> Data { get; private set; } = new();
    public List<CouponEventOptionDto> EventOptions { get; private set; } = new();
    public CouponDto? Editing { get; private set; }
    public bool ShowForm => Edit is not null || Request.Query.ContainsKey("new") || !ModelState.IsValid;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (Edit is int id)
            {
                Editing = await _coupons.GetCouponAsync(id, ct);
                Input = new UpsertCouponRequest
                {
                    Code = Editing.Code, EventId = Editing.EventId, CategoryId = Editing.CategoryId,
                    DiscountType = Editing.DiscountType, DiscountValue = Editing.DiscountValue,
                    MaxDiscountAmount = Editing.MaxDiscountAmount, MaxUses = Editing.MaxUses,
                    ValidFrom = Editing.ValidFrom, ValidTo = Editing.ValidTo, PartnerName = Editing.PartnerName,
                    IsActive = Editing.IsActive
                };
            }
            else
            {
                var today = DateTime.Today;
                Input = new UpsertCouponRequest { ValidFrom = today, ValidTo = today.AddMonths(1), MaxUses = 100, DiscountType = "Percent", DiscountValue = 10 };
            }
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var saved = Edit is int id ? await _coupons.UpdateAsync(id, Input, ct) : await _coupons.CreateAsync(Input, ct);
            StatusMessage = $"Đã lưu coupon {saved.Code}.";
            return RedirectToPage(new { Keyword, P });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _coupons.DeleteCouponAsync(id, ct);
            StatusMessage = "Đã xóa coupon.";
            return RedirectToPage(new { Keyword, P });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (Edit is int id) Editing = await _coupons.GetCouponAsync(id, ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Data = await _coupons.SearchAsync(Keyword, P, ct);
        EventOptions = await _coupons.GetEventOptionsAsync(ct);
    }
}
