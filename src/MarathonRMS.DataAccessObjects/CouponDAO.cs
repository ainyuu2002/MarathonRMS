using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

public class CouponDAO : BaseDAO<Coupon>
{
    public CouponDAO(MarathonDbContext context) : base(context) { }

    public IQueryable<Coupon> QueryWithScope() =>
        Set.AsNoTracking().Include(c => c.Event).Include(c => c.Category);

    public Task<Coupon?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(c => c.CouponId == id, ct);

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct);

    public Task<bool> CodeExistsAsync(string code, int? exceptId = null, CancellationToken ct = default) =>
        Set.AnyAsync(c => c.Code == code && c.CouponId != exceptId, ct);

    public Task<bool> IsReferencedAsync(int couponId, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.CouponId == couponId, ct);

    /// <summary>Giải (kèm cự ly) của một BTC – để chọn phạm vi coupon.</summary>
    public Task<List<Event>> GetEventsWithCategoriesAsync(int organizationId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().Include(e => e.Categories)
            .Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.EventDate).ToListAsync(ct);

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<RaceCategory?> GetCategoryWithEventAsync(int categoryId, CancellationToken ct = default) =>
        Context.RaceCategories.AsNoTracking().Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

    /// <summary>Giá của mức giá đang hiệu lực tại thời điểm <paramref name="at"/> (BR-10).</summary>
    public Task<decimal?> GetCurrentPriceAsync(int categoryId, DateTime at, CancellationToken ct = default) =>
        Context.PriceTiers.AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.ValidFrom <= at && at < p.ValidTo)
            .Select(p => (decimal?)p.Price).FirstOrDefaultAsync(ct);
}
