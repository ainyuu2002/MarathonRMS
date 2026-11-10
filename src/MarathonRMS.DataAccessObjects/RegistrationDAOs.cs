using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn đăng ký; không chứa nghiệp vụ và không tự SaveChanges.</summary>
public class RegistrationDAO : BaseDAO<Registration>
{
    public RegistrationDAO(MarathonDbContext context) : base(context) { }

    public Task<RaceCategory?> GetCategoryForRegistrationAsync(int categoryId, CancellationToken ct = default) =>
        Context.RaceCategories.AsSplitQuery()
            .Include(c => c.Event).ThenInclude(e => e.Policy)
            .Include(c => c.PriceTiers)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

    public Task<int> CountActiveAsync(int categoryId, CancellationToken ct = default) =>
        Set.CountAsync(r => r.CategoryId == categoryId &&
            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<bool> HasActiveForParticipantAsync(int eventId, int participantId, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.EventId == eventId && r.ParticipantId == participantId &&
            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<bool> HasActiveForIdAsync(int eventId, IdType? idType, string? idNumber, CancellationToken ct = default) =>
        string.IsNullOrEmpty(idNumber) ? Task.FromResult(false) :
        Set.AnyAsync(r => r.EventId == eventId && r.IdType == idType && r.IdNumber == idNumber &&
            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<Registration?> GetTrackedWithDetailsAsync(int registrationId, CancellationToken ct = default) =>
        Set.Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Category).Include(r => r.Participant)
            .Include(r => r.RaceKitPickup).Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Category).Include(r => r.Participant).Include(r => r.RaceKitPickup)
            .Where(r => r.RegisteredByUserId == userId)
            .OrderByDescending(r => r.RegisteredAt).ToListAsync(ct);

    public IQueryable<Registration> QueryWithDetails() =>
        Set.AsNoTracking().Include(r => r.Event).Include(r => r.Category).Include(r => r.RaceKitPickup);

    public Task<List<int>> GetOverdueIdsAsync(DateTime now, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(r => r.Status == RegistrationStatus.PendingPayment && r.ExpiresAt <= now)
            .Select(r => r.RegistrationId).ToListAsync(ct);
}

public class PaymentDAO : BaseDAO<Payment>
{
    public PaymentDAO(MarathonDbContext context) : base(context) { }
    public Task<bool> HasSuccessfulAsync(int registrationId, PaymentPurpose purpose, CancellationToken ct = default) =>
        Set.AnyAsync(p => p.RegistrationId == registrationId && p.Purpose == purpose && p.Status == PaymentStatus.Success, ct);
}
