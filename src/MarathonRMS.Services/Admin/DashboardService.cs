using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Admin;

/// <summary>Phần A – GĐ3: UC-69 dashboard giải (Organizer, BR-30), UC-71 dashboard hệ thống (Admin). Chỉ đọc.</summary>
public interface IDashboardService
{
    Task<EventDashboardDto> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<SystemDashboardDto> GetSystemAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private static readonly TShirtSize[] Sizes = Enum.GetValues<TShirtSize>();
    private readonly IDashboardRepository _repo;
    private readonly IOrganizationScope _scope;
    private readonly IDateTimeProvider _clock;

    public DashboardService(IDashboardRepository repo, IOrganizationScope scope, IDateTimeProvider clock)
    {
        _repo = repo;
        _scope = scope;
        _clock = clock;
    }

    public async Task<EventDashboardDto> GetEventAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _repo.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);

        var categories = await _repo.GetCategoriesAsync(eventId, ct);
        var regs = await _repo.GetRegistrationRowsAsync(eventId, ct);
        var results = await _repo.GetResultRowsAsync(eventId, ct);
        var payments = await _repo.GetSuccessfulPaymentsAsync(eventId, null, ct);
        var refunded = await _repo.GetRefundedAsync(eventId, ct);

        var confirmed = regs.Where(r => r.Status == RegistrationStatus.Confirmed).ToList();
        decimal Paid(PaymentPurpose p) => payments.Where(x => x.Purpose == p).Sum(x => x.Amount);
        var gross = payments.Sum(x => x.Amount);
        // Đăng ký từng giữ suất / đã trả tiền (không gồm hủy khi chưa trả, hết hạn) – cho coupon và biểu đồ theo ngày
        var counted = regs.Where(r => r.Status is not (RegistrationStatus.Cancelled or RegistrationStatus.Expired)).ToList();

        return new EventDashboardDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            EventDate = ev.EventDate,
            Status = ev.Status.ToString(),
            ResultsStatus = ev.ResultsStatus.ToString(),
            Capacity = categories.Sum(c => c.Capacity),
            Confirmed = confirmed.Count,
            PendingPayment = regs.Count(r => r.Status == RegistrationStatus.PendingPayment),
            KitsCollected = confirmed.Count(r => r.KitCollected),
            Male = confirmed.Count(r => r.Gender == Gender.M),
            Female = confirmed.Count(r => r.Gender == Gender.F),
            RegistrationRevenue = Paid(PaymentPurpose.Registration),
            CategoryChangeRevenue = Paid(PaymentPurpose.CategoryChange),
            TransferFeeRevenue = Paid(PaymentPurpose.TransferFee),
            OtherRevenue = gross - Paid(PaymentPurpose.Registration) - Paid(PaymentPurpose.CategoryChange) - Paid(PaymentPurpose.TransferFee),
            Refunded = refunded,
            NetRevenue = gross - refunded,
            CouponUses = counted.Count(r => r.HasCoupon),
            CouponDiscount = counted.Sum(r => r.Discount),
            Categories = categories.Select(c =>
            {
                var cr = confirmed.Where(r => r.CategoryId == c.CategoryId).ToList();
                var res = results.Where(r => r.CategoryId == c.CategoryId).ToList();
                return new CategoryDashboardDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Capacity = c.Capacity,
                    Confirmed = cr.Count,
                    PendingPayment = regs.Count(r => r.CategoryId == c.CategoryId && r.Status == RegistrationStatus.PendingPayment),
                    KitsCollected = cr.Count(r => r.KitCollected),
                    ShirtSizes = Sizes.ToDictionary(s => s.ToString(), s => cr.Count(r => r.Size == s)),
                    Finished = res.Count(r => r.Status == ResultStatus.Finished),
                    Dnf = res.Count(r => r.Status == ResultStatus.DNF),
                    Dns = res.Count(r => r.Status == ResultStatus.DNS),
                    Dsq = res.Count(r => r.Status == ResultStatus.DSQ)
                };
            }).ToList(),
            RegistrationsByDay = counted.GroupBy(r => DateOnly.FromDateTime(r.RegisteredAt)).OrderBy(g => g.Key)
                .Select(g => new DailyCountDto { Date = g.Key, Count = g.Count() }).ToList()
        };
    }

    public async Task<SystemDashboardDto> GetSystemAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var from = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var (total, locked) = await _repo.GetUserCountsAsync(ct);
        var (activeOrgs, lockedOrgs, pendingApps) = await _repo.GetOrganizationCountsAsync(ct);
        var byStatus = await _repo.GetEventsByStatusAsync(ct);
        var payments = await _repo.GetSuccessfulPaymentsAsync(null, null, ct);
        var regDates = await _repo.GetRegistrationDatesAsync(from, ct);
        var (pendingRefunds, pendingAmount) = await _repo.GetPendingRefundsAsync(ct);
        var roles = await _repo.GetUsersByRoleAsync(ct);

        return new SystemDashboardDto
        {
            TotalUsers = total,
            LockedUsers = locked,
            UsersByRole = roles.OrderByDescending(x => x.Value).Select(x => new NamedCountDto { Name = x.Key, Count = x.Value }).ToList(),
            ActiveOrganizations = activeOrgs,
            LockedOrganizations = lockedOrgs,
            PendingApplications = pendingApps,
            EventsByStatus = Enum.GetValues<EventStatus>()
                .Select(s => new NamedCountDto { Name = s.ToString(), Count = byStatus.GetValueOrDefault(s) }).ToList(),
            ActiveRegistrations = await _repo.CountActiveRegistrationsAsync(ct),
            TotalRevenue = payments.Sum(x => x.Amount),
            TotalRefunded = await _repo.GetRefundedAsync(null, ct),
            PendingRefunds = pendingRefunds,
            PendingRefundAmount = pendingAmount,
            Monthly = Enumerable.Range(0, 6).Select(i => from.AddMonths(i)).Select(m => new MonthlyStatDto
            {
                Month = m.ToString("yyyy-MM"),
                Registrations = regDates.Count(d => d.Year == m.Year && d.Month == m.Month),
                Revenue = payments.Where(p => p.PaidAt.Year == m.Year && p.PaidAt.Month == m.Month).Sum(p => p.Amount)
            }).ToList()
        };
    }
}
