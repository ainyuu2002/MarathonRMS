using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface ITimingRepository
{
    Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<Registration?> GetConfirmedByBibAsync(int eventId, int bibNumber, CancellationToken ct = default);
    Task<Registration?> GetRegistrationAsync(int registrationId, CancellationToken ct = default);
    Task<List<TimingRecord>> GetByRegistrationAsync(int registrationId, CancellationToken ct = default);
    Task<List<TimingRecord>> GetRecentAsync(int eventId, int? checkpointId, int take, CancellationToken ct = default);
    Task<TimingRecord?> GetTrackedAsync(int timingRecordId, CancellationToken ct = default);
    Task AddAsync(TimingRecord record, CancellationToken ct = default);
    void Remove(TimingRecord record);
}

public class TimingRepository : ITimingRepository
{
    private readonly TimingDAO _dao;
    public TimingRepository(TimingDAO dao) => _dao = dao;

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) => _dao.GetEventAsync(eventId, ct);
    public Task<Registration?> GetConfirmedByBibAsync(int eventId, int bibNumber, CancellationToken ct = default) =>
        _dao.GetConfirmedByBibAsync(eventId, bibNumber, ct);
    public Task<Registration?> GetRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        _dao.GetRegistrationAsync(registrationId, ct);
    public Task<List<TimingRecord>> GetByRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        _dao.GetByRegistrationAsync(registrationId, ct);
    public Task<List<TimingRecord>> GetRecentAsync(int eventId, int? checkpointId, int take, CancellationToken ct = default) =>
        _dao.GetRecentAsync(eventId, checkpointId, take, ct);
    public Task<TimingRecord?> GetTrackedAsync(int timingRecordId, CancellationToken ct = default) =>
        _dao.GetTrackedAsync(timingRecordId, ct);
    public Task AddAsync(TimingRecord record, CancellationToken ct = default) => _dao.AddAsync(record, ct);
    public void Remove(TimingRecord record) => _dao.Remove(record);
}
