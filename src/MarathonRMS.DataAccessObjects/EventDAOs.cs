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

    public Task<Event?> GetTrackedAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(e => e.Organization)
            .Include(e => e.Venue)
            .Include(e => e.Policy)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        Set.AnyAsync(e => e.Slug == slug, ct);

    public Task<bool> HasRegistrationsAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AnyAsync(r => r.EventId == eventId, ct);

}
