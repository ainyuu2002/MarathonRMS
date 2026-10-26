using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IEventContentRepository
{
    Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default);
    Task<List<EventContentSection>> GetSectionsAsync(int eventId, bool tracked = false, CancellationToken ct = default);
    Task<EventContentSection?> GetSectionTrackedAsync(int sectionId, CancellationToken ct = default);
    Task AddSectionAsync(EventContentSection section, CancellationToken ct = default);
    void RemoveSection(EventContentSection section);
    Task<List<EventScheduleItem>> GetScheduleAsync(int eventId, CancellationToken ct = default);
    Task<EventScheduleItem?> GetScheduleItemTrackedAsync(int itemId, CancellationToken ct = default);
    Task AddScheduleItemAsync(EventScheduleItem item, CancellationToken ct = default);
    void RemoveScheduleItem(EventScheduleItem item);
}

public class EventContentRepository : IEventContentRepository
{
    private readonly EventContentDAO _dao;
    public EventContentRepository(EventContentDAO dao) => _dao = dao;

    public Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default) => _dao.GetEventTrackedAsync(eventId, ct);
    public Task<List<EventContentSection>> GetSectionsAsync(int eventId, bool tracked = false, CancellationToken ct = default) => _dao.GetSectionsAsync(eventId, tracked, ct);
    public Task<EventContentSection?> GetSectionTrackedAsync(int sectionId, CancellationToken ct = default) => _dao.GetSectionTrackedAsync(sectionId, ct);
    public Task AddSectionAsync(EventContentSection section, CancellationToken ct = default) => _dao.AddAsync(section, ct);
    public void RemoveSection(EventContentSection section) => _dao.Remove(section);
    public Task<List<EventScheduleItem>> GetScheduleAsync(int eventId, CancellationToken ct = default) => _dao.GetScheduleAsync(eventId, ct);
    public Task<EventScheduleItem?> GetScheduleItemTrackedAsync(int itemId, CancellationToken ct = default) => _dao.GetScheduleItemTrackedAsync(itemId, ct);
    public Task AddScheduleItemAsync(EventScheduleItem item, CancellationToken ct = default) => _dao.AddScheduleItemAsync(item, ct);
    public void RemoveScheduleItem(EventScheduleItem item) => _dao.RemoveScheduleItem(item);
}

public interface IEventMediaRepository
{
    Task<List<EventMedia>> GetByEventAsync(int eventId, MediaKind? kind, bool visibleOnly, CancellationToken ct = default);
    Task<int> CountByEventAsync(int eventId, CancellationToken ct = default);
    Task<int> MaxOrderAsync(int eventId, MediaKind kind, CancellationToken ct = default);
    Task<EventMedia?> GetTrackedAsync(int mediaId, CancellationToken ct = default);
    Task AddAsync(EventMedia media, CancellationToken ct = default);
    void Remove(EventMedia media);
}

public class EventMediaRepository : IEventMediaRepository
{
    private readonly EventMediaDAO _dao;
    public EventMediaRepository(EventMediaDAO dao) => _dao = dao;

    public Task<List<EventMedia>> GetByEventAsync(int eventId, MediaKind? kind, bool visibleOnly, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, kind, visibleOnly, ct);
    public Task<int> CountByEventAsync(int eventId, CancellationToken ct = default) => _dao.CountByEventAsync(eventId, ct);
    public Task<int> MaxOrderAsync(int eventId, MediaKind kind, CancellationToken ct = default) => _dao.MaxOrderAsync(eventId, kind, ct);
    public Task<EventMedia?> GetTrackedAsync(int mediaId, CancellationToken ct = default) => _dao.GetTrackedAsync(mediaId, ct);
    public Task AddAsync(EventMedia media, CancellationToken ct = default) => _dao.AddAsync(media, ct);
    public void Remove(EventMedia media) => _dao.Remove(media);
}
