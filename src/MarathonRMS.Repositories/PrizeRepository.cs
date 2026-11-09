using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IPrizeRepository
{
    Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<RaceCategory?> GetCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<List<Prize>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<Prize?> GetTrackedAsync(int prizeId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int categoryId, AwardType type, Gender gender, int? ageGroupId, int rank, int? exceptId,
        CancellationToken ct = default);
    Task<List<Result>> GetFinishersAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(Prize prize, CancellationToken ct = default);
    void Remove(Prize prize);
}

public class PrizeRepository : IPrizeRepository
{
    private readonly PrizeDAO _dao;
    public PrizeRepository(PrizeDAO dao) => _dao = dao;

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) => _dao.GetEventAsync(eventId, ct);
    public Task<RaceCategory?> GetCategoryAsync(int categoryId, CancellationToken ct = default) => _dao.GetCategoryAsync(categoryId, ct);
    public Task<List<Prize>> GetByEventAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, ct);
    public Task<Prize?> GetTrackedAsync(int prizeId, CancellationToken ct = default) => _dao.GetTrackedAsync(prizeId, ct);
    public Task<bool> ExistsAsync(int categoryId, AwardType type, Gender gender, int? ageGroupId, int rank, int? exceptId,
        CancellationToken ct = default) => _dao.ExistsAsync(categoryId, type, gender, ageGroupId, rank, exceptId, ct);
    public Task<List<Result>> GetFinishersAsync(int eventId, CancellationToken ct = default) => _dao.GetFinishersAsync(eventId, ct);
    public Task AddAsync(Prize prize, CancellationToken ct = default) => _dao.AddAsync(prize, ct);
    public void Remove(Prize prize) => _dao.Remove(prize);
}
