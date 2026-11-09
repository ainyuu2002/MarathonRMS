using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IBibRepository
{
    Task<Event?> GetEventWithCategoriesAsync(int eventId, CancellationToken ct = default);
    Task<List<BibCategoryCount>> GetCategoryCountsAsync(int eventId, CancellationToken ct = default);
    Task<List<int>> GetUsedBibNumbersAsync(int eventId, CancellationToken ct = default);
    Task<List<Registration>> GetUnassignedConfirmedAsync(int categoryId, CancellationToken ct = default);
    IQueryable<Registration> QueryAssigned(int eventId);
}

public class BibRepository : IBibRepository
{
    private readonly BibDAO _dao;
    public BibRepository(BibDAO dao) => _dao = dao;

    public Task<Event?> GetEventWithCategoriesAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetEventWithCategoriesAsync(eventId, ct);
    public Task<List<BibCategoryCount>> GetCategoryCountsAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetCategoryCountsAsync(eventId, ct);
    public Task<List<int>> GetUsedBibNumbersAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetUsedBibNumbersAsync(eventId, ct);
    public Task<List<Registration>> GetUnassignedConfirmedAsync(int categoryId, CancellationToken ct = default) =>
        _dao.GetUnassignedConfirmedAsync(categoryId, ct);
    public IQueryable<Registration> QueryAssigned(int eventId) => _dao.QueryAssigned(eventId);
}
