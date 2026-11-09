using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IPriceTierRepository
{
    Task<List<PriceTier>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<List<PriceTier>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<PriceTier?> GetTrackedAsync(int priceTierId, CancellationToken ct = default);
    Task AddAsync(PriceTier entity, CancellationToken ct = default);
    void Remove(PriceTier entity);
}

public class PriceTierRepository : IPriceTierRepository
{
    private readonly PriceTierDAO _dao;
    public PriceTierRepository(PriceTierDAO dao) => _dao = dao;

    public Task<List<PriceTier>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) => _dao.GetByCategoryAsync(categoryId, ct);
    public Task<List<PriceTier>> GetByEventAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, ct);
    public Task<PriceTier?> GetTrackedAsync(int priceTierId, CancellationToken ct = default) => _dao.GetTrackedAsync(priceTierId, ct);
    public Task AddAsync(PriceTier entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(PriceTier entity) => _dao.Remove(entity);
}

public interface IAgeGroupRepository
{
    Task<List<AgeGroup>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<AgeGroup?> GetTrackedAsync(int ageGroupId, CancellationToken ct = default);
    Task<bool> IsInUseAsync(int ageGroupId, CancellationToken ct = default);
    Task AddAsync(AgeGroup entity, CancellationToken ct = default);
    void Remove(AgeGroup entity);
}

public class AgeGroupRepository : IAgeGroupRepository
{
    private readonly AgeGroupDAO _dao;
    public AgeGroupRepository(AgeGroupDAO dao) => _dao = dao;

    public Task<List<AgeGroup>> GetByEventAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, ct);
    public Task<AgeGroup?> GetTrackedAsync(int ageGroupId, CancellationToken ct = default) => _dao.GetTrackedAsync(ageGroupId, ct);
    public Task<bool> IsInUseAsync(int ageGroupId, CancellationToken ct = default) => _dao.IsInUseAsync(ageGroupId, ct);
    public Task AddAsync(AgeGroup entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(AgeGroup entity) => _dao.Remove(entity);
}

public interface ICheckpointRepository
{
    Task<List<Checkpoint>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<Checkpoint?> GetTrackedAsync(int checkpointId, CancellationToken ct = default);
    Task<bool> IsInUseAsync(int checkpointId, CancellationToken ct = default);
    Task AddAsync(Checkpoint entity, CancellationToken ct = default);
    void Remove(Checkpoint entity);
}

public class CheckpointRepository : ICheckpointRepository
{
    private readonly CheckpointDAO _dao;
    public CheckpointRepository(CheckpointDAO dao) => _dao = dao;

    public Task<List<Checkpoint>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) => _dao.GetByCategoryAsync(categoryId, ct);
    public Task<Checkpoint?> GetTrackedAsync(int checkpointId, CancellationToken ct = default) => _dao.GetTrackedAsync(checkpointId, ct);
    public Task<bool> IsInUseAsync(int checkpointId, CancellationToken ct = default) => _dao.IsInUseAsync(checkpointId, ct);
    public Task AddAsync(Checkpoint entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public void Remove(Checkpoint entity) => _dao.Remove(entity);
}
