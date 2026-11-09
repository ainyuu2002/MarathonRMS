using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IComplaintRepository
{
    Task<Result?> GetResultForComplaintAsync(int resultId, CancellationToken ct = default);
    Task<bool> HasOpenAsync(int resultId, string userId, CancellationToken ct = default);
    Task<ResultComplaint?> GetTrackedAsync(int complaintId, CancellationToken ct = default);
    Task ReloadAsync(ResultComplaint complaint, CancellationToken ct = default);
    IQueryable<ResultComplaint> QueryByUser(string userId);
    IQueryable<ResultComplaint> QueryByEvent(int eventId);
    Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(ResultComplaint complaint, CancellationToken ct = default);
}

public class ComplaintRepository : IComplaintRepository
{
    private readonly ComplaintDAO _dao;
    public ComplaintRepository(ComplaintDAO dao) => _dao = dao;

    public Task<Result?> GetResultForComplaintAsync(int resultId, CancellationToken ct = default) =>
        _dao.GetResultForComplaintAsync(resultId, ct);
    public Task<bool> HasOpenAsync(int resultId, string userId, CancellationToken ct = default) =>
        _dao.HasOpenAsync(resultId, userId, ct);
    public Task<ResultComplaint?> GetTrackedAsync(int complaintId, CancellationToken ct = default) =>
        _dao.GetTrackedAsync(complaintId, ct);
    public Task ReloadAsync(ResultComplaint complaint, CancellationToken ct = default) => _dao.ReloadAsync(complaint, ct);
    public IQueryable<ResultComplaint> QueryByUser(string userId) => _dao.QueryByUser(userId);
    public IQueryable<ResultComplaint> QueryByEvent(int eventId) => _dao.QueryByEvent(eventId);
    public Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetEventWithPolicyAsync(eventId, ct);
    public Task AddAsync(ResultComplaint complaint, CancellationToken ct = default) => _dao.AddAsync(complaint, ct);
}
