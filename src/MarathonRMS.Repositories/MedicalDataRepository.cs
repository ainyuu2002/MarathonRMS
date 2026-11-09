using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IMedicalDataRepository
{
    Task<List<Event>> GetDueEventsAsync(DateTime officialBefore, CancellationToken ct = default);
    Task<int> PurgeEventAsync(int eventId, CancellationToken ct = default);
}

public class MedicalDataRepository : IMedicalDataRepository
{
    private readonly MedicalDataDAO _dao;
    public MedicalDataRepository(MedicalDataDAO dao) => _dao = dao;

    public Task<List<Event>> GetDueEventsAsync(DateTime officialBefore, CancellationToken ct = default) =>
        _dao.GetDueEventsAsync(officialBefore, ct);
    public Task<int> PurgeEventAsync(int eventId, CancellationToken ct = default) => _dao.PurgeEventAsync(eventId, ct);
}
