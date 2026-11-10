using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Phần A – UC-25 hoãn / hủy giải, UC-50 hoàn tiền. Chỉ dựa vào các bảng, không SaveChanges.</summary>
public class EventLifecycleDAO : BaseDAO<Refund>
{
    public EventLifecycleDAO(MarathonDbContext context) : base(context) { }

    public Task<Event?> GetEventHeaderAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Giải (theo dõi) + chính sách + cự ly + checkpoint – để dời mốc khi hoãn.</summary>
    public Task<Event?> GetEventForUpdateAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsSplitQuery()
            .Include(e => e.Policy)
            .Include(e => e.Categories).ThenInclude(c => c.Checkpoints)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Đăng ký còn hiệu lực của giải kèm thanh toán – để xử lý khi hủy giải.</summary>
    public Task<List<Registration>> GetActiveRegistrationsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.Include(r => r.Payments)
            .Where(r => r.EventId == eventId &&
                        (r.Status == RegistrationStatus.PendingPayment || r.Status == RegistrationStatus.Confirmed))
            .ToListAsync(ct);

    /// <summary>Mã các đăng ký chờ thanh toán của giải – để lấy khóa thanh toán của phần B trước khi hủy giải.</summary>
    public Task<List<int>> GetPendingPaymentIdsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.PendingPayment)
            .OrderBy(r => r.RegistrationId).Select(r => r.RegistrationId).ToListAsync(ct);

    /// <summary>Chuyển nhượng đã hoàn tất của giải: đăng ký mới ⇒ đăng ký cũ (để lần theo tiền đã trả trước khi chuyển nhượng).</summary>
    public Task<Dictionary<int, int>> GetTransferLinksAsync(int eventId, CancellationToken ct = default) =>
        Context.BibTransfers.AsNoTracking()
            .Where(t => t.Registration.EventId == eventId && t.Status == TransferStatus.Accepted && t.NewRegistrationId != null)
            .ToDictionaryAsync(t => t.NewRegistrationId!.Value, t => t.RegistrationId, ct);

    /// <summary>Các khoản đổi cự ly đã thanh toán thành công của giải (RegistrationId, Amount).</summary>
    public async Task<List<(int RegistrationId, decimal Amount)>> GetCategoryChangePaymentsAsync(int eventId, CancellationToken ct = default) =>
        (await Context.Payments.AsNoTracking()
            .Where(p => p.Registration.EventId == eventId && p.Purpose == PaymentPurpose.CategoryChange && p.Status == PaymentStatus.Success)
            .Select(p => new { p.RegistrationId, p.Amount }).ToListAsync(ct))
        .Select(p => (p.RegistrationId, p.Amount)).ToList();

    public Task<List<BibTransfer>> GetPendingTransfersAsync(int eventId, CancellationToken ct = default) =>
        Context.BibTransfers.Where(t => t.Registration.EventId == eventId &&
                                        (t.Status == TransferStatus.Pending || t.Status == TransferStatus.AwaitingFee))
            .ToListAsync(ct);

    private IQueryable<Refund> WithDetails() =>
        Set.Include(x => x.Registration).ThenInclude(r => r.Event)
            .Include(x => x.Registration).ThenInclude(r => r.Category)
            .Include(x => x.Registration).ThenInclude(r => r.Participant);

    public Task<List<Refund>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking().Where(x => x.Registration.EventId == eventId)
            .OrderBy(x => x.Status).ThenBy(x => x.Registration.FullName).ToListAsync(ct);

    /// <summary>Hoàn tiền của đăng ký do tôi tạo hoặc của chính tôi.</summary>
    public Task<List<Refund>> GetForUserAsync(string userId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking()
            .Where(x => x.Registration.RegisteredByUserId == userId || x.Registration.Participant.LinkedUserId == userId)
            .OrderByDescending(x => x.RefundId).ToListAsync(ct);

    public Task<Refund?> GetTrackedAsync(int refundId, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(x => x.RefundId == refundId, ct);
}
