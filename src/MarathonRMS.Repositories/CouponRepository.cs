using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface ICouponRepository
{
    IQueryable<Coupon> QueryWithScope();
    Task<Coupon?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, int? exceptId = null, CancellationToken ct = default);
    Task<bool> IsReferencedAsync(int couponId, CancellationToken ct = default);
    Task<List<Event>> GetEventsWithCategoriesAsync(int organizationId, CancellationToken ct = default);
    Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<RaceCategory?> GetCategoryWithEventAsync(int categoryId, CancellationToken ct = default);
    Task<decimal?> GetCurrentPriceAsync(int categoryId, DateTime at, CancellationToken ct = default);
    Task AddAsync(Coupon coupon, CancellationToken ct = default);
    void Remove(Coupon coupon);
}

public class CouponRepository : ICouponRepository
{
    private readonly CouponDAO _dao;
    public CouponRepository(CouponDAO dao) => _dao = dao;

    public IQueryable<Coupon> QueryWithScope() => _dao.QueryWithScope();
    public Task<Coupon?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default) => _dao.GetByCodeAsync(code, ct);
    public Task<bool> CodeExistsAsync(string code, int? exceptId = null, CancellationToken ct = default) => _dao.CodeExistsAsync(code, exceptId, ct);
    public Task<bool> IsReferencedAsync(int couponId, CancellationToken ct = default) => _dao.IsReferencedAsync(couponId, ct);
    public Task<List<Event>> GetEventsWithCategoriesAsync(int organizationId, CancellationToken ct = default) => _dao.GetEventsWithCategoriesAsync(organizationId, ct);
    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) => _dao.GetEventAsync(eventId, ct);
    public Task<RaceCategory?> GetCategoryWithEventAsync(int categoryId, CancellationToken ct = default) => _dao.GetCategoryWithEventAsync(categoryId, ct);
    public Task<decimal?> GetCurrentPriceAsync(int categoryId, DateTime at, CancellationToken ct = default) => _dao.GetCurrentPriceAsync(categoryId, at, ct);
    public Task AddAsync(Coupon coupon, CancellationToken ct = default) => _dao.AddAsync(coupon, ct);
    public void Remove(Coupon coupon) => _dao.Remove(coupon);
}
