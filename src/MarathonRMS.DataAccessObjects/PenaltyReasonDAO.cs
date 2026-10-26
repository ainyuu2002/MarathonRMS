using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Danh mục lý do phạt / DSQ (UC-85, BR-28).</summary>
public class PenaltyReasonDAO : BaseDAO<PenaltyReason>
{
    public PenaltyReasonDAO(MarathonDbContext context) : base(context) { }

    public Task<PenaltyReason?> GetTrackedAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(r => r.Code == code, ct);

    public Task<bool> ExistsAsync(string code, CancellationToken ct = default) => Set.AnyAsync(r => r.Code == code, ct);

    /// <summary>Số lần lý do đã được dùng trong các quyết định phạt.</summary>
    public Task<int> CountUsageAsync(string code, CancellationToken ct = default) =>
        Context.ResultPenalties.CountAsync(p => p.ReasonCode == code, ct);

    public Task<Dictionary<string, int>> GetUsageCountsAsync(CancellationToken ct = default) =>
        Context.ResultPenalties.GroupBy(p => p.ReasonCode).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
