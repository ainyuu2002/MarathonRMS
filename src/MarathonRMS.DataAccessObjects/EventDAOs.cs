using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn giải phục vụ UC-18. Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class EventDAO : BaseDAO<Event>
{
    public EventDAO(MarathonDbContext context) : base(context) { }

    public IQueryable<Event> QueryWithReferences() =>
        Set.AsNoTracking().Include(e => e.Organization).Include(e => e.Venue);

    public Task<Event?> GetDetailedAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Include(e => e.Organization)
            .Include(e => e.Venue)
            .Include(e => e.Policy)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<Event?> GetWithCategoriesAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(e => e.Organization)
            .Include(e => e.Venue)
            .Include(e => e.Policy)
            .Include(e => e.Categories).ThenInclude(c => c.PriceTiers)
            .Include(e => e.Categories).ThenInclude(c => c.Checkpoints)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(e => e.Organization)
            .Include(e => e.Venue)
            .Include(e => e.Policy)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<Event?> GetForStatusChangeAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(e => e.Policy)
            .Include(e => e.Categories).ThenInclude(c => c.PriceTiers)
            .Include(e => e.Categories).ThenInclude(c => c.Checkpoints)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        Set.AnyAsync(e => e.Slug == slug, ct);

    public Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.EventId == eventId, ct);

    public Task<Dictionary<int, int>> GetActiveRegistrationCountsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId &&
                        (r.Status == BusinessObjects.Enums.RegistrationStatus.PendingPayment ||
                         r.Status == BusinessObjects.Enums.RegistrationStatus.Confirmed))
            .GroupBy(r => r.CategoryId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

}

/// <summary>Truy vấn cự ly phục vụ UC-21. Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class RaceCategoryDAO : BaseDAO<RaceCategory>
{
    public RaceCategoryDAO(MarathonDbContext context) : base(context) { }

    public Task<List<RaceCategory>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(c => c.PriceTiers)
            .Include(c => c.Checkpoints)
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).ThenBy(c => c.Name)
            .ToListAsync(ct);

    public Task<RaceCategory?> GetTrackedAsync(int categoryId, CancellationToken ct = default) =>
        Set.AsSplitQuery()
            .Include(c => c.Event).ThenInclude(e => e.Policy)
            .Include(c => c.PriceTiers)
            .Include(c => c.Checkpoints)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

    public Task<bool> NameExistsAsync(int eventId, string name, int? exceptCategoryId = null,
        CancellationToken ct = default) =>
        Set.AnyAsync(c => c.EventId == eventId && c.Name == name &&
                          (!exceptCategoryId.HasValue || c.CategoryId != exceptCategoryId.Value), ct);

    public Task<bool> BibRangeOverlapsAsync(int eventId, int bibFrom, int bibTo, int? exceptCategoryId = null,
        CancellationToken ct = default) =>
        Set.AnyAsync(c => c.EventId == eventId && c.BibFrom <= bibTo && bibFrom <= c.BibTo &&
                          (!exceptCategoryId.HasValue || c.CategoryId != exceptCategoryId.Value), ct);

    public Task<int> CountActiveRegistrationsAsync(int categoryId, CancellationToken ct = default) =>
        Context.Registrations.CountAsync(r => r.CategoryId == categoryId &&
            (r.Status == BusinessObjects.Enums.RegistrationStatus.PendingPayment ||
             r.Status == BusinessObjects.Enums.RegistrationStatus.Confirmed), ct);

    public Task<bool> HasRegistrationsAsync(int categoryId, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.CategoryId == categoryId, ct);

    public Task<List<DateOnly>> GetActiveRegistrationBirthDatesAsync(int categoryId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Where(r => r.CategoryId == categoryId &&
                        (r.Status == BusinessObjects.Enums.RegistrationStatus.PendingPayment ||
                         r.Status == BusinessObjects.Enums.RegistrationStatus.Confirmed))
            .Select(r => r.DateOfBirth)
            .ToListAsync(ct);

    public Task<bool> HasAssignedBibOutsideRangeAsync(int categoryId, int bibFrom, int bibTo,
        CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.CategoryId == categoryId && r.BibNumber != null &&
                                             (r.BibNumber < bibFrom || r.BibNumber > bibTo), ct);

    public Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId &&
                        (r.Status == BusinessObjects.Enums.RegistrationStatus.PendingPayment ||
                         r.Status == BusinessObjects.Enums.RegistrationStatus.Confirmed))
            .GroupBy(r => r.CategoryId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public void SetOriginalRowVersion(RaceCategory entity, byte[] rowVersion) =>
        Context.Entry(entity).Property(c => c.RowVersion).OriginalValue = rowVersion;
}
