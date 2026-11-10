using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>
/// Phần A – UC-46 đổi cự ly, UC-47 chuyển nhượng BIB. Chỉ dựa vào bảng Registrations / Payments / BibTransfers,
/// không phụ thuộc DAO đăng ký của phần B. Không chứa quy tắc nghiệp vụ, không SaveChanges.
/// </summary>
public class RegistrationChangeDAO : BaseDAO<BibTransfer>
{
    public RegistrationChangeDAO(MarathonDbContext context) : base(context) { }

    // ---------- Đăng ký ----------

    public Task<Registration?> GetRegistrationHeaderAsync(int registrationId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking().FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    /// <summary>Đăng ký (theo dõi) kèm giải, chính sách, cự ly + mức giá, race kit.</summary>
    public Task<Registration?> GetRegistrationTrackedAsync(int registrationId, CancellationToken ct = default) =>
        Context.Registrations.AsSplitQuery()
            .Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Category).ThenInclude(c => c.PriceTiers)
            .Include(r => r.RaceKitPickup)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<List<RaceCategory>> GetCategoriesWithTiersAsync(int eventId, CancellationToken ct = default) =>
        Context.RaceCategories.AsNoTracking().Include(c => c.PriceTiers)
            .Where(c => c.EventId == eventId).OrderBy(c => c.DistanceKm).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId &&
                        (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed))
            .GroupBy(r => r.CategoryId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.EventId == eventId && r.ParticipantId == participantId &&
            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.EventId == eventId && r.IdType == idType && r.IdNumber == idNumber &&
            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    /// <summary>Số BIB nhỏ nhất còn trống trong dải của cự ly (BR-17: gán lại BIB khi đổi cự ly).</summary>
    public async Task<int?> GetNextFreeBibAsync(int eventId, int bibFrom, int bibTo, CancellationToken ct = default)
    {
        var used = await Context.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.BibNumber != null && r.BibNumber >= bibFrom && r.BibNumber <= bibTo)
            .Select(r => r.BibNumber!.Value).ToListAsync(ct);
        var set = used.ToHashSet();
        for (var n = bibFrom; n <= bibTo; n++)
            if (!set.Contains(n)) return n;
        return null;
    }

    public async Task AddRegistrationAsync(Registration registration, CancellationToken ct = default) =>
        await Context.Registrations.AddAsync(registration, ct);

    public async Task AddPaymentAsync(Payment payment, CancellationToken ct = default) =>
        await Context.Payments.AddAsync(payment, ct);

    public async Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default) =>
        await Context.ConsentRecords.AddAsync(record, ct);

    // ---------- Người nhận ----------

    public Task<AppUser?> FindActiveUserByEmailAsync(string email, CancellationToken ct = default) =>
        Context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant() && u.IsActive, ct);

    public Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default) =>
        Context.Participants.FirstOrDefaultAsync(p => p.OwnerUserId == userId && p.Relationship == ParticipantRelationship.Self && !p.IsDeleted, ct);

    public async Task AddParticipantAsync(Participant participant, CancellationToken ct = default) =>
        await Context.Participants.AddAsync(participant, ct);

    // ---------- Chuyển nhượng ----------

    private IQueryable<BibTransfer> WithDetails() =>
        Set.Include(t => t.Registration).ThenInclude(r => r.Event).ThenInclude(e => e.Policy)
            .Include(t => t.Registration).ThenInclude(r => r.Category)
            .Include(t => t.Registration).ThenInclude(r => r.RaceKitPickup);

    public Task<BibTransfer?> GetTrackedAsync(int transferId, CancellationToken ct = default) =>
        WithDetails().AsSplitQuery().FirstOrDefaultAsync(t => t.TransferId == transferId, ct);

    /// <summary>Đọc không theo dõi – dùng trước khi khóa.</summary>
    public Task<BibTransfer?> GetDetailsAsync(int transferId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(t => t.TransferId == transferId, ct);

    public Task<BibTransfer?> GetPendingForRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(t => t.RegistrationId == registrationId &&
            (t.Status == TransferStatus.Pending || t.Status == TransferStatus.AwaitingFee), ct);

    /// <summary>Yêu cầu tôi gửi hoặc gửi cho tôi, mới nhất trước.</summary>
    public Task<List<BibTransfer>> GetForUserAsync(string userId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking().AsSplitQuery()
            .Where(t => t.FromUserId == userId || t.ToUserId == userId)
            .OrderByDescending(t => t.RequestedAt).Take(100).ToListAsync(ct);

    /// <summary>Họ tên + email theo Id tài khoản (để hiện người gửi / người nhận).</summary>
    public Task<Dictionary<string, (string Name, string Email)>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        return Context.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FullName, u.Email ?? ""), ct);
    }
}
