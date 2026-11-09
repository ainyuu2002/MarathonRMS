using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Số đăng ký đã xác nhận và đã có BIB của một cự ly.</summary>
public class BibCategoryCount
{
    public int CategoryId { get; set; }
    public int Confirmed { get; set; }
    public int Assigned { get; set; }
}

/// <summary>Truy vấn phục vụ gán BIB (UC-49). Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class BibDAO : BaseDAO<Registration>
{
    public BibDAO(MarathonDbContext context) : base(context) { }

    public Task<Event?> GetEventWithCategoriesAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking()
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<List<BibCategoryCount>> GetCategoryCountsAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
            .GroupBy(r => r.CategoryId)
            .Select(g => new BibCategoryCount
            {
                CategoryId = g.Key,
                Confirmed = g.Count(),
                Assigned = g.Count(r => r.BibNumber != null)
            })
            .ToListAsync(ct);

    /// <summary>Mọi số BIB đang dùng trong giải (BIB duy nhất theo giải – BR-19).</summary>
    public Task<List<int>> GetUsedBibNumbersAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(r => r.EventId == eventId && r.BibNumber != null)
            .Select(r => r.BibNumber!.Value)
            .ToListAsync(ct);

    /// <summary>Đăng ký đã xác nhận chưa có BIB, theo thứ tự xác nhận. Bản ghi được theo dõi để gán số.</summary>
    public Task<List<Registration>> GetUnassignedConfirmedAsync(int categoryId, CancellationToken ct = default) =>
        Set.Where(r => r.CategoryId == categoryId && r.Status == RegistrationStatus.Confirmed && r.BibNumber == null)
            .OrderBy(r => r.ConfirmedAt ?? r.RegisteredAt).ThenBy(r => r.RegistrationId)
            .ToListAsync(ct);

    public IQueryable<Registration> QueryAssigned(int eventId) =>
        Set.AsNoTracking().Where(r => r.EventId == eventId && r.BibNumber != null);
}
