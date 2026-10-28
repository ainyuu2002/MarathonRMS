using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IEventRepository
{
    IQueryable<Event> QueryWithReferences();
    Task<Event?> GetDetailedAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(Event entity, CancellationToken ct = default);
    void Remove(Event entity);
}

public class EventRepository : IEventRepository
{
    private readonly EventDAO _dao;
    public EventRepository(EventDAO dao) => _dao = dao;

    public IQueryable<Event> QueryWithReferences() => _dao.QueryWithReferences();
    public Task<Event?> GetDetailedAsync(int eventId, CancellationToken ct = default) => _dao.GetDetailedAsync(eventId, ct);
    public Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default) => _dao.GetTrackedAsync(eventId, ct);
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => _dao.SlugExistsAsync(slug, ct);
    public Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default) => _dao.HasRegistrationsAsync(eventId, ct);
    public Task AddAsync(Event entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(Event entity) => _dao.Remove(entity);
}
