using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Phần A – UC-69, UC-71: truy vấn tổng hợp, chỉ đọc. Mỗi hàm trả về dữ liệu thô đã gom nhóm trong SQL.</summary>
public class DashboardDAO : BaseDAO<Registration>
{
    public DashboardDAO(MarathonDbContext context) : base(context) { }

    public sealed record CategoryRow(int CategoryId, string Name, int Capacity);
    public sealed record RegistrationRow(int CategoryId, RegistrationStatus Status, Gender Gender, TShirtSize Size,
        bool KitCollected, bool HasCoupon, decimal Discount, DateTime RegisteredAt);
    public sealed record ResultRow(int CategoryId, ResultStatus Status);
    public sealed record PaymentRow(PaymentPurpose Purpose, decimal Amount, DateTime PaidAt);

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<List<CategoryRow>> GetCategoriesAsync(int eventId, CancellationToken ct = default) =>
        Context.RaceCategories.AsNoTracking().Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).ThenBy(c => c.Name)
            .Select(c => new CategoryRow(c.CategoryId, c.Name, c.Capacity)).ToListAsync(ct);

    /// <summary>Một dòng nhỏ cho mỗi đăng ký của giải (vài nghìn dòng là cùng) – gom nhóm ở Service.</summary>
    public Task<List<RegistrationRow>> GetRegistrationRowsAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(r => r.EventId == eventId)
            .Select(r => new RegistrationRow(r.CategoryId, r.Status, r.Gender, r.TShirtSize, r.RaceKitPickup != null,
                r.CouponId != null, r.DiscountAmount, r.RegisteredAt))
            .ToListAsync(ct);

    public Task<List<ResultRow>> GetResultRowsAsync(int eventId, CancellationToken ct = default) =>
        Context.Results.AsNoTracking()
            .Where(x => x.Registration.EventId == eventId && x.Registration.Status == RegistrationStatus.Confirmed)
            .Select(x => new ResultRow(x.Registration.CategoryId, x.Status)).ToListAsync(ct);

    /// <summary>Thanh toán thành công; eventId null = toàn hệ thống, from = chỉ từ thời điểm này.</summary>
    public Task<List<PaymentRow>> GetSuccessfulPaymentsAsync(int? eventId, DateTime? from = null, CancellationToken ct = default) =>
        Context.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && (eventId == null || p.Registration.EventId == eventId) &&
                        (from == null || p.PaidAt >= from))
            .Select(p => new PaymentRow(p.Purpose, p.Amount, p.PaidAt ?? p.CreatedAt)).ToListAsync(ct);

    public Task<decimal> GetRefundedAsync(int? eventId, CancellationToken ct = default) =>
        Context.Refunds.AsNoTracking()
            .Where(x => x.Status == RefundStatus.Completed && (eventId == null || x.Registration.EventId == eventId))
            .SumAsync(x => x.Amount, ct);

    // ---------- UC-71 ----------

    public async Task<(int Total, int Locked)> GetUserCountsAsync(CancellationToken ct = default) =>
        (await Context.Users.CountAsync(ct), await Context.Users.CountAsync(u => !u.IsActive, ct));

    public Task<Dictionary<string, int>> GetUsersByRoleAsync(CancellationToken ct = default) =>
        (from ur in Context.UserRoles
         join role in Context.Roles on ur.RoleId equals role.Id
         group ur by role.Name into g
         select new { Name = g.Key ?? "", Count = g.Count() })
        .ToDictionaryAsync(x => x.Name, x => x.Count, ct);

    public async Task<(int Active, int Locked, int PendingApplications)> GetOrganizationCountsAsync(CancellationToken ct = default) =>
        (await Context.Organizations.CountAsync(o => o.IsActive, ct),
         await Context.Organizations.CountAsync(o => !o.IsActive, ct),
         await Context.OrganizationApplications.CountAsync(a => a.Status == ApplicationStatus.Pending, ct));

    public Task<Dictionary<EventStatus, int>> GetEventsByStatusAsync(CancellationToken ct = default) =>
        Context.Events.AsNoTracking().GroupBy(e => e.Status).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task<int> CountActiveRegistrationsAsync(CancellationToken ct = default) =>
        Set.CountAsync(r => r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed, ct);

    /// <summary>Ngày đăng ký của các đăng ký không bị hủy / hết hạn, từ thời điểm cho trước.</summary>
    public Task<List<DateTime>> GetRegistrationDatesAsync(DateTime from, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(r => r.RegisteredAt >= from && r.Status != RegistrationStatus.Cancelled && r.Status != RegistrationStatus.Expired)
            .Select(r => r.RegisteredAt).ToListAsync(ct);

    public async Task<(int Count, decimal Amount)> GetPendingRefundsAsync(CancellationToken ct = default) =>
        (await Context.Refunds.CountAsync(x => x.Status == RefundStatus.Pending, ct),
         await Context.Refunds.Where(x => x.Status == RefundStatus.Pending).SumAsync(x => x.Amount, ct));
}
