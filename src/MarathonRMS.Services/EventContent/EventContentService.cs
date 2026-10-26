using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.EventContent;

/// <summary>
/// UC-88 – trang giới thiệu giải theo khối nội dung + lịch trình + màu chủ đạo; UC-89 – album ảnh (BR-50, BR-51).
/// Quản lý: Organizer của BTC sở hữu giải. Công khai: giải không ở trạng thái Draft.
/// </summary>
public interface IEventContentService
{
    /// <summary>Nội dung công khai (B nhúng vào trang chi tiết giải). Giải Draft chỉ BTC sở hữu / Admin xem được.</summary>
    Task<EventContentDto> GetPublicAsync(int eventId, CancellationToken ct = default);
    /// <summary>Toàn bộ nội dung, kể cả khối và ảnh đang ẩn – cho trang quản lý.</summary>
    Task<EventContentDto> GetForManageAsync(int eventId, CancellationToken ct = default);
    Task<EventContentDto> UpdateThemeAsync(int eventId, UpdateThemeRequest request, CancellationToken ct = default);

    Task<ContentSectionDto> AddSectionAsync(int eventId, UpsertContentSectionRequest request, CancellationToken ct = default);
    Task<ContentSectionDto> UpdateSectionAsync(int sectionId, UpsertContentSectionRequest request, CancellationToken ct = default);
    Task DeleteSectionAsync(int sectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ContentSectionDto>> ReorderSectionsAsync(int eventId, ReorderSectionsRequest request, CancellationToken ct = default);

    Task<ScheduleItemDto> AddScheduleItemAsync(int eventId, UpsertScheduleItemRequest request, CancellationToken ct = default);
    Task<ScheduleItemDto> UpdateScheduleItemAsync(int itemId, UpsertScheduleItemRequest request, CancellationToken ct = default);
    Task DeleteScheduleItemAsync(int itemId, CancellationToken ct = default);

    Task<IReadOnlyList<EventMediaDto>> GetMediaAsync(int eventId, string? kind, CancellationToken ct = default);
    Task<EventMediaDto> UploadMediaAsync(int eventId, string kind, Stream content, string fileName, long length, string? caption, CancellationToken ct = default);
    Task<EventMediaDto> UpdateMediaAsync(int mediaId, UpdateMediaRequest request, CancellationToken ct = default);
    Task DeleteMediaAsync(int mediaId, CancellationToken ct = default);
}

public class EventContentService : IEventContentService
{
    private static readonly Dictionary<string, byte[][]> ImageSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".webp"] = new[] { "RIFF"u8.ToArray() },
    };

    private readonly IEventContentRepository _content;
    private readonly IEventMediaRepository _media;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlContentSanitizer _sanitizer;
    private readonly IFileStorage _files;
    private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public EventContentService(IEventContentRepository content, IEventMediaRepository media, IOrganizationScope scope,
        ICurrentUserService currentUser, IHtmlContentSanitizer sanitizer, IFileStorage files, ISystemSettingsProvider settings,
        IDateTimeProvider clock, IUnitOfWork uow)
    {
        _content = content;
        _media = media;
        _scope = scope;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _files = files;
        _settings = settings;
        _clock = clock;
        _uow = uow;
    }

    // ============================ Đọc ============================

    public async Task<EventContentDto> GetPublicAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _content.GetEventTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (ev.Status == EventStatus.Draft && !CanManage(ev))
            throw new NotFoundException("Không tìm thấy giải.");
        return await BuildAsync(ev, visibleOnly: true, ct);
    }

    public async Task<EventContentDto> GetForManageAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await LoadManagedEventAsync(eventId, requireActive: false, ct);
        return await BuildAsync(ev, visibleOnly: false, ct);
    }

    public async Task<EventContentDto> UpdateThemeAsync(int eventId, UpdateThemeRequest request, CancellationToken ct = default)
    {
        var ev = await LoadManagedEventAsync(eventId, requireActive: true, ct);
        ev.ThemeColor = string.IsNullOrWhiteSpace(request.ThemeColor) ? null : request.ThemeColor.ToUpperInvariant();
        await _uow.SaveChangesAsync(ct);
        return await BuildAsync(ev, visibleOnly: false, ct);
    }

    // ============================ Khối nội dung ============================

    public async Task<ContentSectionDto> AddSectionAsync(int eventId, UpsertContentSectionRequest request, CancellationToken ct = default)
    {
        await LoadManagedEventAsync(eventId, requireActive: true, ct);
        var existing = await _content.GetSectionsAsync(eventId, ct: ct);
        var section = new EventContentSection
        {
            EventId = eventId,
            DisplayOrder = existing.Count == 0 ? 0 : existing.Max(s => s.DisplayOrder) + 1
        };
        Apply(section, request);
        await _content.AddSectionAsync(section, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(section);
    }

    public async Task<ContentSectionDto> UpdateSectionAsync(int sectionId, UpsertContentSectionRequest request, CancellationToken ct = default)
    {
        var section = await _content.GetSectionTrackedAsync(sectionId, ct) ?? throw new NotFoundException("Không tìm thấy khối nội dung.");
        await LoadManagedEventAsync(section.EventId, requireActive: true, ct);
        Apply(section, request);
        await _uow.SaveChangesAsync(ct);
        return ToDto(section);
    }

    public async Task DeleteSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _content.GetSectionTrackedAsync(sectionId, ct) ?? throw new NotFoundException("Không tìm thấy khối nội dung.");
        await LoadManagedEventAsync(section.EventId, requireActive: true, ct);
        _content.RemoveSection(section);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContentSectionDto>> ReorderSectionsAsync(int eventId, ReorderSectionsRequest request, CancellationToken ct = default)
    {
        await LoadManagedEventAsync(eventId, requireActive: true, ct);
        var sections = await _content.GetSectionsAsync(eventId, tracked: true, ct);
        if (request.SectionIds.Count != sections.Count || request.SectionIds.Distinct().Count() != sections.Count
            || sections.Any(s => !request.SectionIds.Contains(s.SectionId)))
            throw new BusinessRuleException("VALIDATION", "Danh sách khối không khớp với nội dung của giải.", 400);
        for (var i = 0; i < request.SectionIds.Count; i++)
            sections.First(s => s.SectionId == request.SectionIds[i]).DisplayOrder = i;
        await _uow.SaveChangesAsync(ct);
        return sections.OrderBy(s => s.DisplayOrder).Select(ToDto).ToList();
    }

    // ============================ Lịch trình ============================

    public async Task<ScheduleItemDto> AddScheduleItemAsync(int eventId, UpsertScheduleItemRequest request, CancellationToken ct = default)
    {
        var ev = await LoadManagedEventAsync(eventId, requireActive: true, ct);
        ValidateSchedule(ev, request);
        var item = new EventScheduleItem { EventId = eventId };
        Apply(item, request);
        await _content.AddScheduleItemAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ScheduleItemDto> UpdateScheduleItemAsync(int itemId, UpsertScheduleItemRequest request, CancellationToken ct = default)
    {
        var item = await _content.GetScheduleItemTrackedAsync(itemId, ct) ?? throw new NotFoundException("Không tìm thấy mục lịch trình.");
        var ev = await LoadManagedEventAsync(item.EventId, requireActive: true, ct);
        ValidateSchedule(ev, request);
        Apply(item, request);
        await _uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task DeleteScheduleItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _content.GetScheduleItemTrackedAsync(itemId, ct) ?? throw new NotFoundException("Không tìm thấy mục lịch trình.");
        await LoadManagedEventAsync(item.EventId, requireActive: true, ct);
        _content.RemoveScheduleItem(item);
        await _uow.SaveChangesAsync(ct);
    }

    // ============================ Ảnh ============================

    public async Task<IReadOnlyList<EventMediaDto>> GetMediaAsync(int eventId, string? kind, CancellationToken ct = default)
    {
        var ev = await _content.GetEventTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        var manage = CanManage(ev);
        if (ev.Status == EventStatus.Draft && !manage) throw new NotFoundException("Không tìm thấy giải.");
        var mediaKind = EnumParser.ParseOptional<MediaKind>(kind);
        return (await _media.GetByEventAsync(eventId, mediaKind, visibleOnly: !manage, ct)).Select(ToDto).ToList();
    }

    /// <summary>BR-51: chỉ JPG/PNG/WebP (kiểm tra cả phần mở rộng lẫn chữ ký file), ≤ Media.MaxFileSizeMB, ≤ Media.MaxPerEvent ảnh/giải, đổi tên GUID.</summary>
    public async Task<EventMediaDto> UploadMediaAsync(int eventId, string kind, Stream content, string fileName, long length, string? caption,
        CancellationToken ct = default)
    {
        await LoadManagedEventAsync(eventId, requireActive: true, ct);
        var mediaKind = EnumParser.ParseOptional<MediaKind>(kind) ?? MediaKind.Gallery;

        var extension = Path.GetExtension(fileName);
        if (!ImageSignatures.ContainsKey(extension))
            throw new BusinessRuleException("BR-51", "Chỉ nhận ảnh JPG, PNG hoặc WebP.", 400);
        var maxMb = await _settings.GetIntAsync("Media.MaxFileSizeMB", 5, ct);
        if (length <= 0 || length > maxMb * 1024L * 1024L)
            throw new BusinessRuleException("BR-51", $"Ảnh phải nhỏ hơn {maxMb} MB.", 400);
        var maxPerEvent = await _settings.GetIntAsync("Media.MaxPerEvent", 200, ct);
        if (await _media.CountByEventAsync(eventId, ct) >= maxPerEvent)
            throw new BusinessRuleException("BR-51", $"Mỗi giải tối đa {maxPerEvent} ảnh.");

        // Đọc vào bộ nhớ để kiểm tra chữ ký file trước khi ghi đĩa
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (!HasValidSignature(buffer.GetBuffer(), (int)buffer.Length, extension))
            throw new BusinessRuleException("BR-51", "Nội dung file không phải ảnh hợp lệ.", 400);
        buffer.Position = 0;

        var url = await _files.SaveAsync(buffer, $"events/{eventId}", extension, ct);
        var media = new EventMedia
        {
            EventId = eventId,
            FileUrl = url,
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            Kind = mediaKind,
            DisplayOrder = await _media.MaxOrderAsync(eventId, mediaKind, ct) + 1,
            IsVisible = true,
            UploadedById = _currentUser.UserId!,
            UploadedAt = _clock.Now
        };
        await _media.AddAsync(media, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await _files.DeleteAsync(url, ct); // không để file mồ côi
            throw;
        }
        return ToDto(media);
    }

    public async Task<EventMediaDto> UpdateMediaAsync(int mediaId, UpdateMediaRequest request, CancellationToken ct = default)
    {
        var media = await _media.GetTrackedAsync(mediaId, ct) ?? throw new NotFoundException("Không tìm thấy ảnh.");
        await LoadManagedEventAsync(media.EventId, requireActive: true, ct);
        media.Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim();
        media.IsVisible = request.IsVisible;
        media.DisplayOrder = request.DisplayOrder;
        await _uow.SaveChangesAsync(ct);
        return ToDto(media);
    }

    public async Task DeleteMediaAsync(int mediaId, CancellationToken ct = default)
    {
        var media = await _media.GetTrackedAsync(mediaId, ct) ?? throw new NotFoundException("Không tìm thấy ảnh.");
        await LoadManagedEventAsync(media.EventId, requireActive: true, ct);
        _media.Remove(media);
        await _uow.SaveChangesAsync(ct);
        await _files.DeleteAsync(media.FileUrl, ct);
    }

    // ============================ Hỗ trợ ============================

    private bool CanManage(Event ev) =>
        _currentUser.IsAdminOrAbove || (_currentUser.IsInRole(DTOs.Common.AppRoles.Organizer) && _currentUser.OrganizationId == ev.OrganizationId);

    private async Task<Event> LoadManagedEventAsync(int eventId, bool requireActive, CancellationToken ct)
    {
        var ev = await _content.GetEventTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        if (requireActive) await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        return ev;
    }

    private async Task<EventContentDto> BuildAsync(Event ev, bool visibleOnly, CancellationToken ct)
    {
        var sections = await _content.GetSectionsAsync(ev.EventId, ct: ct);
        var schedule = await _content.GetScheduleAsync(ev.EventId, ct);
        var gallery = await _media.GetByEventAsync(ev.EventId, MediaKind.Gallery, visibleOnly, ct);
        return new EventContentDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            ThemeColor = ev.ThemeColor,
            Sections = sections.Where(s => !visibleOnly || s.IsVisible).Select(ToDto).ToList(),
            Schedule = schedule.Select(ToDto).ToList(),
            Gallery = gallery.Select(ToDto).ToList()
        };
    }

    private void Apply(EventContentSection s, UpsertContentSectionRequest r)
    {
        s.SectionType = Enum.Parse<SectionType>(r.SectionType);
        s.Title = r.Title.Trim();
        s.BodyHtml = _sanitizer.Sanitize(r.BodyHtml);
        s.IsVisible = r.IsVisible;
    }

    private static void ValidateSchedule(Event ev, UpsertScheduleItemRequest r)
    {
        if (r.EndsAt is DateTime end && end <= r.StartsAt)
            throw new BusinessRuleException("VALIDATION", "Giờ kết thúc phải sau giờ bắt đầu.", 400);
        // Lịch trình hợp lý: từ 30 ngày trước ngày thi tới hết ngày kết thúc giải
        var earliest = ev.EventDate.ToDateTime(TimeOnly.MinValue).AddDays(-30);
        var latest = ev.EndDate.ToDateTime(TimeOnly.MaxValue);
        if (r.StartsAt < earliest || r.StartsAt > latest)
            throw new BusinessRuleException("VALIDATION", "Mục lịch trình phải nằm trong khoảng 30 ngày trước ngày thi đến hết ngày kết thúc giải.", 400);
    }

    private static void Apply(EventScheduleItem i, UpsertScheduleItemRequest r)
    {
        i.StartsAt = r.StartsAt;
        i.EndsAt = r.EndsAt;
        i.Title = r.Title.Trim();
        i.Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim();
        i.Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
    }

    private static bool HasValidSignature(byte[] data, int length, string extension)
    {
        foreach (var sig in ImageSignatures[extension])
        {
            if (length < sig.Length || !data.AsSpan(0, sig.Length).SequenceEqual(sig)) continue;
            // WebP: "RIFF" + 4 byte kích thước + "WEBP"
            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                return length >= 12 && data.AsSpan(8, 4).SequenceEqual("WEBP"u8);
            return true;
        }
        return false;
    }

    private static ContentSectionDto ToDto(EventContentSection s) => new()
    {
        Id = s.SectionId, SectionType = s.SectionType.ToString(), Title = s.Title, BodyHtml = s.BodyHtml,
        DisplayOrder = s.DisplayOrder, IsVisible = s.IsVisible
    };

    private static ScheduleItemDto ToDto(EventScheduleItem i) => new()
    {
        Id = i.ItemId, StartsAt = i.StartsAt, EndsAt = i.EndsAt, Title = i.Title, Location = i.Location, Note = i.Note
    };

    private EventMediaDto ToDto(EventMedia m) => new()
    {
        Id = m.MediaId, EventId = m.EventId, FileUrl = _files.ToPublicUrl(m.FileUrl), Caption = m.Caption,
        Kind = m.Kind.ToString(), DisplayOrder = m.DisplayOrder, IsVisible = m.IsVisible, UploadedAt = m.UploadedAt
    };
}
