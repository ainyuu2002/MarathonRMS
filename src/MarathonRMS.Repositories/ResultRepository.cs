using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IResultRepository
{
    Task<Event?> GetEventForResultsAsync(int eventId, CancellationToken ct = default);
    Task<List<Registration>> GetConfirmedWithTimingAsync(int eventId, CancellationToken ct = default);
    Task<List<Result>> GetTrackedByEventAsync(int eventId, CancellationToken ct = default);
    IQueryable<Result> QueryByEvent(int eventId);
    Task<Dictionary<ResultStatus, int>> CountByStatusAsync(int eventId, CancellationToken ct = default);
    Task<DateTime?> GetLastUpdatedAtAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(Result result, CancellationToken ct = default);
    void Remove(Result result);
    Task<bool> HasCalculatedResultsAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default);
    Task<bool> AnyForEventAsync(int eventId, CancellationToken ct = default);
    Task<int> CountOpenComplaintsAsync(int eventId, CancellationToken ct = default);
    Task<Result?> GetWithPenaltiesAsync(int resultId, CancellationToken ct = default);
    Task<ResultPenalty?> GetPenaltyTrackedAsync(int penaltyId, CancellationToken ct = default);
    Task AddPenaltyAsync(ResultPenalty penalty, CancellationToken ct = default);
    IQueryable<Result> QueryPublished();
    Task<List<Event>> GetPublishedEventsAsync(CancellationToken ct = default);
    Task<Result?> GetDetailByRegistrationAsync(int registrationId, CancellationToken ct = default);
}

public class ResultRepository : IResultRepository
{
    private readonly ResultDAO _dao;
    public ResultRepository(ResultDAO dao) => _dao = dao;

    public Task<Event?> GetEventForResultsAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetEventForResultsAsync(eventId, ct);
    public Task<List<Registration>> GetConfirmedWithTimingAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetConfirmedWithTimingAsync(eventId, ct);
    public Task<List<Result>> GetTrackedByEventAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetTrackedByEventAsync(eventId, ct);
    public IQueryable<Result> QueryByEvent(int eventId) => _dao.QueryByEvent(eventId);
    public Task<Dictionary<ResultStatus, int>> CountByStatusAsync(int eventId, CancellationToken ct = default) =>
        _dao.CountByStatusAsync(eventId, ct);
    public Task<DateTime?> GetLastUpdatedAtAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetLastUpdatedAtAsync(eventId, ct);
    public Task AddAsync(Result result, CancellationToken ct = default) => _dao.AddAsync(result, ct);
    public void Remove(Result result) => _dao.Remove(result);
    public Task<bool> HasCalculatedResultsAsync(int eventId, CancellationToken ct = default) =>
        _dao.HasCalculatedResultsAsync(eventId, ct);
    public Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default) => _dao.GetEventTrackedAsync(eventId, ct);
    public Task<bool> AnyForEventAsync(int eventId, CancellationToken ct = default) => _dao.AnyForEventAsync(eventId, ct);
    public Task<int> CountOpenComplaintsAsync(int eventId, CancellationToken ct = default) =>
        _dao.CountOpenComplaintsAsync(eventId, ct);
    public Task<Result?> GetWithPenaltiesAsync(int resultId, CancellationToken ct = default) =>
        _dao.GetWithPenaltiesAsync(resultId, ct);
    public Task<ResultPenalty?> GetPenaltyTrackedAsync(int penaltyId, CancellationToken ct = default) =>
        _dao.GetPenaltyTrackedAsync(penaltyId, ct);
    public Task AddPenaltyAsync(ResultPenalty penalty, CancellationToken ct = default) => _dao.AddPenaltyAsync(penalty, ct);
    public IQueryable<Result> QueryPublished() => _dao.QueryPublished();
    public Task<List<Event>> GetPublishedEventsAsync(CancellationToken ct = default) => _dao.GetPublishedEventsAsync(ct);
    public Task<Result?> GetDetailByRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        _dao.GetDetailByRegistrationAsync(registrationId, ct);
}
