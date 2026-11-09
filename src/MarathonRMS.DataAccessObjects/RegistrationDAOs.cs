using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>UC-39, 43–45, 48 – đăng ký. Không chứa quy tắc nghiệp vụ, không SaveChanges.</summary>
public class RegistrationDAO : BaseDAO<Registration>
{
    public RegistrationDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Cự ly + giải + chính sách + mức giá – đủ để kiểm tra BR-03…10.</summary>
    public Task<RaceCategory?> GetCategoryForRegistrationAsync(int categoryId, CancellationToken ct = default) =>
        Context.RaceCategories.AsNoTracking().AsSplitQuery()
            .Include(c => c.Event).ThenInclude(e => e.Policy)
            .Include(c => c.PriceTiers)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

    public Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default) =>
        Context.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.ParticipantId == participantId && !p.IsDeleted, ct);

    public Task<int> CountActiveInCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Set.CountAsync(r => r.CategoryId == categoryId &&
                            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, int? exceptRegistrationId = null, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.EventId == eventId && r.ParticipantId == participantId && r.RegistrationId != exceptRegistrationId &&
                          (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    public Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, int? exceptRegistrationId = null,
        CancellationToken ct = default) =>
        Set.AnyAsync(r => r.EventId == eventId && r.IdType == idType && r.IdNumber == idNumber && r.RegistrationId != exceptRegistrationId &&
                          (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    /// <summary>Số người phụ thuộc (không phải chính chủ) mà tài khoản đã đăng ký còn hiệu lực trong giải – BR-02.</summary>
    public Task<int> CountActiveDependentsAsync(int eventId, string userId, CancellationToken ct = default) =>
        Set.CountAsync(r => r.EventId == eventId && r.RegisteredByUserId == userId &&
                            r.Participant.Relationship != ParticipantRelationship.Self &&
                            (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed), ct);

    /// <summary>Đăng ký do tôi tạo hoặc của chính tôi (người khác đăng ký hộ / chuyển nhượng cho tôi).</summary>
    public Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Category)
            .Include(r => r.Participant)
            .Include(r => r.RaceKitPickup)
            .Where(r => r.RegisteredByUserId == userId || r.Participant.LinkedUserId == userId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(ct);

    public Task<Registration?> GetTrackedAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsSplitQuery()
            .Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Category)
            .Include(r => r.Participant)
            .Include(r => r.RaceKitPickup)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    /// <summary>Đọc nhanh không theo dõi (chủ sở hữu, giải) – dùng trước khi khóa rồi mới tải bản theo dõi.</summary>
    public Task<Registration?> GetHeaderAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public IQueryable<Registration> QueryAll() => Set.AsNoTracking();

    /// <summary>(RegistrationId, EventId) của đăng ký chờ thanh toán đã quá hạn giữ chỗ (BR-13) – không theo dõi.</summary>
    public async Task<List<(int RegistrationId, int EventId)>> GetOverdueAsync(DateTime now, int take, CancellationToken ct = default) =>
        (await Set.AsNoTracking()
            .Where(r => r.Status == RegistrationStatus.PendingPayment && r.ExpiresAt != null && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt).Take(take)
            .Select(r => new { r.RegistrationId, r.EventId })
            .ToListAsync(ct))
        .Select(x => (x.RegistrationId, x.EventId)).ToList();

    public async Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default) =>
        await Context.ConsentRecords.AddAsync(record, ct);
}

/// <summary>UC-41 – thanh toán.</summary>
public class PaymentDAO : BaseDAO<Payment>
{
    public PaymentDAO(MarathonDbContext context) : base(context) { }

    public Task<bool> TxnRefExistsAsync(string txnRef, CancellationToken ct = default) =>
        Set.AnyAsync(p => p.TxnRef == txnRef, ct);
}
