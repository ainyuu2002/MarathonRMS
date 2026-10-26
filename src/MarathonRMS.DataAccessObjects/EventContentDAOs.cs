using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Khối nội dung, lịch trình, màu chủ đạo của trang giới thiệu giải (UC-88).</summary>
public class EventContentDAO : BaseDAO<EventContentSection>
{
    public EventContentDAO(MarathonDbContext context) : base(context) { }

    public Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<List<EventContentSection>> GetSectionsAsync(int eventId, bool tracked = false, CancellationToken ct = default) =>
        (tracked ? Set : Set.AsNoTracking()).Where(s => s.EventId == eventId).OrderBy(s => s.DisplayOrder).ThenBy(s => s.SectionId).ToListAsync(ct);

    public Task<EventContentSection?> GetSectionTrackedAsync(int sectionId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);

    public Task<List<EventScheduleItem>> GetScheduleAsync(int eventId, CancellationToken ct = default) =>
        Context.EventScheduleItems.AsNoTracking().Where(i => i.EventId == eventId).OrderBy(i => i.StartsAt).ToListAsync(ct);

    public Task<EventScheduleItem?> GetScheduleItemTrackedAsync(int itemId, CancellationToken ct = default) =>
        Context.EventScheduleItems.FirstOrDefaultAsync(i => i.ItemId == itemId, ct);

    public async Task AddScheduleItemAsync(EventScheduleItem item, CancellationToken ct = default) =>
        await Context.EventScheduleItems.AddAsync(item, ct);

    public void RemoveScheduleItem(EventScheduleItem item) => Context.EventScheduleItems.Remove(item);
}

/// <summary>Ảnh của giải (UC-89, BR-51).</summary>
public class EventMediaDAO : BaseDAO<EventMedia>
{
    public EventMediaDAO(MarathonDbContext context) : base(context) { }

    public Task<List<EventMedia>> GetByEventAsync(int eventId, MediaKind? kind, bool visibleOnly, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(m => m.EventId == eventId && (kind == null || m.Kind == kind) && (!visibleOnly || m.IsVisible))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.MediaId).ToListAsync(ct);

    public Task<int> CountByEventAsync(int eventId, CancellationToken ct = default) => Set.CountAsync(m => m.EventId == eventId, ct);

    public async Task<int> MaxOrderAsync(int eventId, MediaKind kind, CancellationToken ct = default) =>
        await Set.Where(m => m.EventId == eventId && m.Kind == kind).Select(m => (int?)m.DisplayOrder).MaxAsync(ct) ?? 0;

    public Task<EventMedia?> GetTrackedAsync(int mediaId, CancellationToken ct = default) =>
        Set.Include(m => m.Event).FirstOrDefaultAsync(m => m.MediaId == mediaId, ct);
}
