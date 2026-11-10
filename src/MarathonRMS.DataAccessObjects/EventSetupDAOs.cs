using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn mức giá phục vụ UC-22. Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class PriceTierDAO : BaseDAO<PriceTier>
{
    public PriceTierDAO(MarathonDbContext context) : base(context) { }

    public Task<List<PriceTier>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(t => t.CategoryId == categoryId)
            .OrderBy(t => t.ValidFrom).ToListAsync(ct);

    public Task<PriceTier?> GetTrackedAsync(int priceTierId, CancellationToken ct = default) =>
        Set.AsSplitQuery()
            .Include(t => t.Category).ThenInclude(c => c.Event)
            .Include(t => t.Category).ThenInclude(c => c.PriceTiers)
            .FirstOrDefaultAsync(t => t.PriceTierId == priceTierId, ct);

    public Task<bool> HasShorterCategoryWithHigherOverlappingTierAsync(int eventId, int categoryId,
        decimal distanceKm, decimal price, DateTime validFrom, DateTime validTo, CancellationToken ct = default) =>
        Set.AsNoTracking().AnyAsync(t => t.Category.EventId == eventId && t.CategoryId != categoryId &&
            t.Category.DistanceKm < distanceKm && t.Price > price &&
            t.ValidFrom < validTo && validFrom < t.ValidTo, ct);
}

/// <summary>Truy vấn nhóm tuổi phục vụ UC-23.</summary>
public class AgeGroupDAO : BaseDAO<AgeGroup>
{
    public AgeGroupDAO(MarathonDbContext context) : base(context) { }

    public Task<List<AgeGroup>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(g => g.EventId == eventId)
            .OrderBy(g => g.CategoryId).ThenBy(g => g.MinAge).ThenBy(g => g.Name).ToListAsync(ct);

    public Task<AgeGroup?> GetTrackedAsync(int ageGroupId, CancellationToken ct = default) =>
        Set.Include(g => g.Event).FirstOrDefaultAsync(g => g.AgeGroupId == ageGroupId, ct);

    public Task<bool> IsReferencedAsync(int ageGroupId, CancellationToken ct = default) =>
        Context.Results.AnyAsync(r => r.AgeGroupId == ageGroupId, ct);
}

/// <summary>Truy vấn checkpoint phục vụ UC-24.</summary>
public class CheckpointDAO : BaseDAO<Checkpoint>
{
    public CheckpointDAO(MarathonDbContext context) : base(context) { }

    public Task<List<Checkpoint>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.Sequence).ToListAsync(ct);

    public Task<Checkpoint?> GetTrackedAsync(int checkpointId, CancellationToken ct = default) =>
        Set.AsSplitQuery()
            .Include(p => p.Category).ThenInclude(c => c.Event)
            .Include(p => p.Category).ThenInclude(c => c.Checkpoints)
            .FirstOrDefaultAsync(p => p.CheckpointId == checkpointId, ct);

    public Task<bool> IsReferencedAsync(int checkpointId, CancellationToken ct = default) =>
        Context.TimingRecords.AnyAsync(r => r.CheckpointId == checkpointId, ct);
}
