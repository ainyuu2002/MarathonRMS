using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>UC-22 – mức giá của cự ly. Không chứa quy tắc nghiệp vụ, không SaveChanges.</summary>
public class PriceTierDAO : BaseDAO<PriceTier>
{
    public PriceTierDAO(MarathonDbContext context) : base(context) { }

    public Task<List<PriceTier>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(t => t.CategoryId == categoryId).OrderBy(t => t.ValidFrom).ToListAsync(ct);

    public Task<PriceTier?> GetTrackedAsync(int priceTierId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(t => t.PriceTierId == priceTierId, ct);

    /// <summary>Mức giá của mọi cự ly trong giải kèm cự ly – để cảnh báo BR-49 (cự ly dài rẻ hơn cự ly ngắn).</summary>
    public Task<List<PriceTier>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(t => t.Category)
            .Where(t => t.Category.EventId == eventId).ToListAsync(ct);
}

/// <summary>UC-23 – nhóm tuổi của giải.</summary>
public class AgeGroupDAO : BaseDAO<AgeGroup>
{
    public AgeGroupDAO(MarathonDbContext context) : base(context) { }

    public Task<List<AgeGroup>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(g => g.EventId == eventId)
            .OrderBy(g => g.CategoryId).ThenBy(g => g.MinAge).ToListAsync(ct);

    public Task<AgeGroup?> GetTrackedAsync(int ageGroupId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(g => g.AgeGroupId == ageGroupId, ct);

    /// <summary>Nhóm tuổi đã được dùng trong kết quả hoặc cơ cấu giải thưởng ⇒ không xóa được.</summary>
    public async Task<bool> IsInUseAsync(int ageGroupId, CancellationToken ct = default) =>
        await Context.Results.AnyAsync(r => r.AgeGroupId == ageGroupId, ct) ||
        await Context.Prizes.AnyAsync(p => p.AgeGroupId == ageGroupId, ct);
}

/// <summary>UC-24 – checkpoint của cự ly.</summary>
public class CheckpointDAO : BaseDAO<Checkpoint>
{
    public CheckpointDAO(MarathonDbContext context) : base(context) { }

    public Task<List<Checkpoint>> GetByCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(c => c.CategoryId == categoryId).OrderBy(c => c.Sequence).ToListAsync(ct);

    public Task<Checkpoint?> GetTrackedAsync(int checkpointId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(c => c.CheckpointId == checkpointId, ct);

    /// <summary>Đã có thời gian bấm tại trạm hoặc VĐV bỏ cuộc tại trạm ⇒ không xóa được.</summary>
    public async Task<bool> IsInUseAsync(int checkpointId, CancellationToken ct = default) =>
        await Context.TimingRecords.AnyAsync(t => t.CheckpointId == checkpointId, ct) ||
        await Context.Results.AnyAsync(r => r.DnfCheckpointId == checkpointId, ct);
}
