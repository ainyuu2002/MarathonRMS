using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

public class BibTransferDAO : BaseDAO<BibTransfer>
{
    public BibTransferDAO(MarathonDbContext context) : base(context) { }

    private IQueryable<BibTransfer> WithDetails() => Set.AsSplitQuery()
        .Include(t => t.Registration).ThenInclude(r => r.Event).ThenInclude(e => e.Policy)
        .Include(t => t.Registration).ThenInclude(r => r.Category)
        .Include(t => t.Registration).ThenInclude(r => r.RaceKitPickup);

    public Task<BibTransfer?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(t => t.TransferId == id, ct);

    public Task<List<BibTransfer>> GetIncomingAsync(string userId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking().Where(t => t.ToUserId == userId)
            .OrderByDescending(t => t.RequestedAt).ToListAsync(ct);

    public Task<List<BibTransfer>> GetOutgoingAsync(string userId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking().Where(t => t.FromUserId == userId)
            .OrderByDescending(t => t.RequestedAt).ToListAsync(ct);

    public Task<bool> HasOpenAsync(int registrationId, CancellationToken ct = default) =>
        Set.AnyAsync(t => t.RegistrationId == registrationId
            && (t.Status == TransferStatus.Pending || t.Status == TransferStatus.AwaitingFee), ct);

    public Task<List<BibTransfer>> GetOverdueAsync(DateTime now, CancellationToken ct = default) =>
        Set.Include(t => t.Registration).ThenInclude(r => r.Event).ThenInclude(e => e.Policy)
            .Where(t => (t.Status == TransferStatus.Pending || t.Status == TransferStatus.AwaitingFee)
                && t.Registration.Event.Policy.TransferDeadline != null
                && t.Registration.Event.Policy.TransferDeadline <= now).ToListAsync(ct);

    public Task<List<BibTransfer>> GetOpenForEventAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(t => t.Registration).Where(t => t.Registration.EventId == eventId
            && (t.Status == TransferStatus.Pending || t.Status == TransferStatus.AwaitingFee)).ToListAsync(ct);
}

public class RefundDAO : BaseDAO<Refund>
{
    public RefundDAO(MarathonDbContext context) : base(context) { }

    public Task<bool> ExistsAsync(int registrationId, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.RegistrationId == registrationId, ct);

    public Task<List<Refund>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(r => r.Registration)
            .Where(r => r.Registration.EventId == eventId).OrderBy(r => r.Status).ThenBy(r => r.RefundId).ToListAsync(ct);

    public Task<Refund?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Set.Include(r => r.Registration).ThenInclude(x => x.Event)
            .FirstOrDefaultAsync(r => r.RefundId == id, ct);
}
