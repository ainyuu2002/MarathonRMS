using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IBibTransferRepository
{
    Task<BibTransfer?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task<List<BibTransfer>> GetIncomingAsync(string userId, CancellationToken ct = default);
    Task<List<BibTransfer>> GetOutgoingAsync(string userId, CancellationToken ct = default);
    Task<bool> HasOpenAsync(int registrationId, CancellationToken ct = default);
    Task<List<BibTransfer>> GetOverdueAsync(DateTime now, CancellationToken ct = default);
    Task<List<BibTransfer>> GetOpenForEventAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(BibTransfer entity, CancellationToken ct = default);
}

public class BibTransferRepository : IBibTransferRepository
{
    private readonly BibTransferDAO _dao;
    public BibTransferRepository(BibTransferDAO dao) => _dao = dao;
    public Task<BibTransfer?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public Task<List<BibTransfer>> GetIncomingAsync(string userId, CancellationToken ct = default) => _dao.GetIncomingAsync(userId, ct);
    public Task<List<BibTransfer>> GetOutgoingAsync(string userId, CancellationToken ct = default) => _dao.GetOutgoingAsync(userId, ct);
    public Task<bool> HasOpenAsync(int id, CancellationToken ct = default) => _dao.HasOpenAsync(id, ct);
    public Task<List<BibTransfer>> GetOverdueAsync(DateTime now, CancellationToken ct = default) => _dao.GetOverdueAsync(now, ct);
    public Task<List<BibTransfer>> GetOpenForEventAsync(int id, CancellationToken ct = default) => _dao.GetOpenForEventAsync(id, ct);
    public Task AddAsync(BibTransfer entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
}

public interface IRefundRepository
{
    Task<bool> ExistsAsync(int registrationId, CancellationToken ct = default);
    Task<List<Refund>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<Refund?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task AddAsync(Refund entity, CancellationToken ct = default);
}

public class RefundRepository : IRefundRepository
{
    private readonly RefundDAO _dao;
    public RefundRepository(RefundDAO dao) => _dao = dao;
    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) => _dao.ExistsAsync(id, ct);
    public Task<List<Refund>> GetByEventAsync(int id, CancellationToken ct = default) => _dao.GetByEventAsync(id, ct);
    public Task<Refund?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public Task AddAsync(Refund entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
}
