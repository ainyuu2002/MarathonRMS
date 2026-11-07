using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IRaceKitRepository
{
    Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default);
    Task<List<Registration>> SearchAsync(int eventId, int? bibNumber, string keyword, string compactKeyword, int take,
        CancellationToken ct = default);
}

public class RaceKitRepository : IRaceKitRepository
{
    private readonly RaceKitDAO _dao;
    public RaceKitRepository(RaceKitDAO dao) => _dao = dao;

    public Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetEventWithPolicyAsync(eventId, ct);
    public Task<List<Registration>> SearchAsync(int eventId, int? bibNumber, string keyword, string compactKeyword, int take,
        CancellationToken ct = default) => _dao.SearchAsync(eventId, bibNumber, keyword, compactKeyword, take, ct);
}
