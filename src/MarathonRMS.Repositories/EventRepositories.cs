using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IEventRepository
{
    IQueryable<Event> QueryWithReferences();
    Task<Event?> GetDetailedAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetWithCategoriesAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetForStatusChangeAsync(int eventId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetActiveRegistrationCountsAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(Event entity, CancellationToken ct = default);
    void Remove(Event entity);
}

public class EventRepository : IEventRepository
{
    private readonly EventDAO _dao;
    public EventRepository(EventDAO dao) => _dao = dao;

    public IQueryable<Event> QueryWithReferences() => _dao.QueryWithReferences();
    public Task<Event?> GetDetailedAsync(int eventId, CancellationToken ct = default) => _dao.GetDetailedAsync(eventId, ct);
    public Task<Event?> GetWithCategoriesAsync(int eventId, CancellationToken ct = default) => _dao.GetWithCategoriesAsync(eventId, ct);
    public Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default) => _dao.GetTrackedAsync(eventId, ct);
    public Task<Event?> GetForStatusChangeAsync(int eventId, CancellationToken ct = default) => _dao.GetForStatusChangeAsync(eventId, ct);
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => _dao.SlugExistsAsync(slug, ct);
    public Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default) => _dao.HasRegistrationsAsync(eventId, ct);
    public Task<Dictionary<int, int>> GetActiveRegistrationCountsAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetActiveRegistrationCountsAsync(eventId, ct);
    public Task AddAsync(Event entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(Event entity) => _dao.Remove(entity);
}

public interface IRaceCategoryRepository
{
    Task<List<RaceCategory>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<RaceCategory?> GetTrackedAsync(int categoryId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(int eventId, string name, int? exceptCategoryId = null, CancellationToken ct = default);
    Task<bool> BibRangeOverlapsAsync(int eventId, int bibFrom, int bibTo, int? exceptCategoryId = null,
        CancellationToken ct = default);
    Task<int> CountActiveRegistrationsAsync(int categoryId, CancellationToken ct = default);
    Task<bool> HasRegistrationsAsync(int categoryId, CancellationToken ct = default);
    Task<List<DateOnly>> GetActiveRegistrationBirthDatesAsync(int categoryId, CancellationToken ct = default);
    Task<bool> HasAssignedBibOutsideRangeAsync(int categoryId, int bibFrom, int bibTo, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(RaceCategory entity, CancellationToken ct = default);
    void Remove(RaceCategory entity);
    void SetOriginalRowVersion(RaceCategory entity, byte[] rowVersion);
}

public class RaceCategoryRepository : IRaceCategoryRepository
{
    private readonly RaceCategoryDAO _dao;
    public RaceCategoryRepository(RaceCategoryDAO dao) => _dao = dao;

    public Task<List<RaceCategory>> GetByEventAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, ct);
    public Task<RaceCategory?> GetTrackedAsync(int categoryId, CancellationToken ct = default) => _dao.GetTrackedAsync(categoryId, ct);
    public Task<bool> NameExistsAsync(int eventId, string name, int? exceptCategoryId = null, CancellationToken ct = default) =>
        _dao.NameExistsAsync(eventId, name, exceptCategoryId, ct);
    public Task<bool> BibRangeOverlapsAsync(int eventId, int bibFrom, int bibTo, int? exceptCategoryId = null,
        CancellationToken ct = default) => _dao.BibRangeOverlapsAsync(eventId, bibFrom, bibTo, exceptCategoryId, ct);
    public Task<int> CountActiveRegistrationsAsync(int categoryId, CancellationToken ct = default) =>
        _dao.CountActiveRegistrationsAsync(categoryId, ct);
    public Task<bool> HasRegistrationsAsync(int categoryId, CancellationToken ct = default) => _dao.HasRegistrationsAsync(categoryId, ct);
    public Task<List<DateOnly>> GetActiveRegistrationBirthDatesAsync(int categoryId, CancellationToken ct = default) =>
        _dao.GetActiveRegistrationBirthDatesAsync(categoryId, ct);
    public Task<bool> HasAssignedBibOutsideRangeAsync(int categoryId, int bibFrom, int bibTo, CancellationToken ct = default) =>
        _dao.HasAssignedBibOutsideRangeAsync(categoryId, bibFrom, bibTo, ct);
    public Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetActiveCountsAsync(eventId, ct);
    public Task AddAsync(RaceCategory entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(RaceCategory entity) => _dao.Remove(entity);
    public void SetOriginalRowVersion(RaceCategory entity, byte[] rowVersion) => _dao.SetOriginalRowVersion(entity, rowVersion);
}
