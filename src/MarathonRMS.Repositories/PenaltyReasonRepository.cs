using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IPenaltyReasonRepository
{
    IQueryable<PenaltyReason> Query();
    Task<PenaltyReason?> GetTrackedAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, CancellationToken ct = default);
    Task<int> CountUsageAsync(string code, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetUsageCountsAsync(CancellationToken ct = default);
    Task AddAsync(PenaltyReason reason, CancellationToken ct = default);
    void Remove(PenaltyReason reason);
}

public class PenaltyReasonRepository : IPenaltyReasonRepository
{
    private readonly PenaltyReasonDAO _dao;
    public PenaltyReasonRepository(PenaltyReasonDAO dao) => _dao = dao;

    public IQueryable<PenaltyReason> Query() => _dao.Query();
    public Task<PenaltyReason?> GetTrackedAsync(string code, CancellationToken ct = default) => _dao.GetTrackedAsync(code, ct);
    public Task<bool> ExistsAsync(string code, CancellationToken ct = default) => _dao.ExistsAsync(code, ct);
    public Task<int> CountUsageAsync(string code, CancellationToken ct = default) => _dao.CountUsageAsync(code, ct);
    public Task<Dictionary<string, int>> GetUsageCountsAsync(CancellationToken ct = default) => _dao.GetUsageCountsAsync(ct);
    public Task AddAsync(PenaltyReason reason, CancellationToken ct = default) => _dao.AddAsync(reason, ct);
    public void Remove(PenaltyReason reason) => _dao.Remove(reason);
}
