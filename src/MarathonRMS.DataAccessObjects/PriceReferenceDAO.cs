using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Phần A – UC-87: đọc mức giá của các giải đã công bố để tính khoảng giá tham khảo. Chỉ đọc.</summary>
public class PriceReferenceDAO : BaseDAO<PriceTier>
{
    public PriceReferenceDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Giá "Regular" của mỗi cự ly cùng loại trong nhóm cự ly = mức giá có khung thời gian dài nhất.</summary>
    /// <param name="minKm">Cận dưới, không gồm.</param>
    /// <param name="maxKm">Cận trên, gồm.</param>
    public async Task<List<decimal>> GetRegularPricesAsync(Discipline discipline, decimal minKm, decimal maxKm, int? excludeEventId,
        CancellationToken ct = default)
    {
        var tiers = await Set.AsNoTracking()
            .Where(t => t.Category.Discipline == discipline && t.Category.DistanceKm > minKm && t.Category.DistanceKm <= maxKm &&
                        t.Category.Event.Status != EventStatus.Draft && t.Category.Event.Status != EventStatus.Cancelled &&
                        (excludeEventId == null || t.Category.EventId != excludeEventId))
            .Select(t => new { t.CategoryId, t.Price, t.ValidFrom, t.ValidTo })
            .ToListAsync(ct);
        return tiers.GroupBy(t => t.CategoryId)
            .Select(g => g.OrderByDescending(t => t.ValidTo - t.ValidFrom).First().Price)
            .ToList();
    }
}
