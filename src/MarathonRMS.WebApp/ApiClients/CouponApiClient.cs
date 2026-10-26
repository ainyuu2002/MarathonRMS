using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Coupons;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Coupon (UC-27). B dùng ValidateAsync ở form đăng ký để xem trước số tiền giảm.</summary>
public class CouponApiClient : ApiClientBase
{
    public CouponApiClient(HttpClient http) : base(http) { }

    public Task<PagedResult<CouponDto>> SearchAsync(string? keyword, int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<CouponDto>>(Query("api/coupons", ("keyword", keyword), ("page", page)), ct);
    public Task<CouponDto> GetCouponAsync(int id, CancellationToken ct = default) => GetAsync<CouponDto>($"api/coupons/{id}", ct);
    public Task<List<CouponEventOptionDto>> GetEventOptionsAsync(CancellationToken ct = default) =>
        GetAsync<List<CouponEventOptionDto>>("api/coupons/event-options", ct);
    public Task<CouponDto> CreateAsync(UpsertCouponRequest request, CancellationToken ct = default) => PostAsync<CouponDto>("api/coupons", request, ct);
    public Task<CouponDto> UpdateAsync(int id, UpsertCouponRequest request, CancellationToken ct = default) => PutAsync<CouponDto>($"api/coupons/{id}", request, ct);
    public Task DeleteCouponAsync(int id, CancellationToken ct = default) => DeleteAsync($"api/coupons/{id}", ct);
    public Task<CouponQuoteDto> ValidateAsync(ValidateCouponRequest request, CancellationToken ct = default) =>
        PostAsync<CouponQuoteDto>("api/coupons/validate", request, ct);
}
