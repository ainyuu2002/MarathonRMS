using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Events;

/// <summary>UC-18 – tạo, xem, sửa và xóa giải Draft của ban tổ chức.</summary>
public class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly IVenueRepository _venues;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IHtmlContentSanitizer _sanitizer;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public EventService(IEventRepository events, IVenueRepository venues, IOrganizationScope scope,
        ICurrentUserService currentUser, ISystemSettingsProvider settings, IHtmlContentSanitizer sanitizer,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _events = events;
        _venues = venues;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _sanitizer = sanitizer;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    // Interface dùng chung với UC-34; UC-18 chưa công khai endpoint OData này.
    public IQueryable<EventSummaryDto> QueryPublic() =>
        ProjectSummary(_events.QueryWithReferences().Where(e => e.Status != EventStatus.Draft));

    public IQueryable<EventSummaryDto> QueryMine()
    {
        var organizationId = _scope.RequireOrganizationId();
        return ProjectSummary(_events.QueryWithReferences().Where(e => e.OrganizationId == organizationId));
    }

    public async Task<EventDetailDto> GetDetailAsync(int eventId, CancellationToken ct = default)
    {
        var entity = await _events.GetDetailedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(entity.OrganizationId);
        return ToDetail(entity);
    }

    public async Task<EventDetailDto> CreateAsync(UpsertEventRequest request, CancellationToken ct = default)
    {
        var organizationId = _scope.RequireOrganizationId();
        _scope.EnsureCanManage(organizationId);
        await _scope.EnsureOrganizationActiveAsync(organizationId, ct);
        Validate(request);

        _ = await _venues.GetTrackedAsync(request.VenueId, ct)
            ?? throw new BusinessRuleException("VALIDATION", "Địa điểm không tồn tại.", 400);

        var entity = new Event
        {
            OrganizationId = organizationId,
            VenueId = request.VenueId,
            Name = request.Name.Trim(),
            Slug = await CreateUniqueSlugAsync(request.Name, ct),
            DescriptionHtml = CleanHtml(request.DescriptionHtml),
            BannerUrl = CleanOptional(request.BannerUrl),
            EventDate = request.EventDate,
            EndDate = request.EndDate,
            RegistrationOpenAt = request.RegistrationOpenAt,
            RegistrationCloseAt = request.RegistrationCloseAt,
            Status = EventStatus.Draft,
            ResultsStatus = ResultsStatus.Draft,
            EmergencyPhone = CleanOptional(request.EmergencyPhone),
            InsuranceNote = CleanOptional(request.InsuranceNote),
            // UC-19 sẽ cho phép Organizer nhập và quản lý phiên bản văn bản miễn trừ.
            WaiverText = "",
            WaiverVersion = 1,
            CreatedById = _currentUser.UserId!,
            CreatedAt = _clock.Now,
            Policy = await CreateDefaultPolicyAsync(ct)
        };

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _events.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("Event.Create", nameof(Event), entity.EventId.ToString(), null,
            new { entity.Name, entity.VenueId, entity.EventDate }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetDetailAsync(entity.EventId, ct);
    }

    public async Task<EventDetailDto> UpdateAsync(int eventId, UpsertEventRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var entity = await LoadForWriteAsync(eventId, ct);
        _ = await _venues.GetTrackedAsync(request.VenueId, ct)
            ?? throw new BusinessRuleException("VALIDATION", "Địa điểm không tồn tại.", 400);

        var old = new
        {
            entity.Name, entity.VenueId, entity.EventDate, entity.EndDate,
            entity.RegistrationOpenAt, entity.RegistrationCloseAt
        };

        entity.Name = request.Name.Trim();
        entity.VenueId = request.VenueId;
        entity.DescriptionHtml = CleanHtml(request.DescriptionHtml);
        entity.BannerUrl = CleanOptional(request.BannerUrl);
        entity.EventDate = request.EventDate;
        entity.EndDate = request.EndDate;
        entity.RegistrationOpenAt = request.RegistrationOpenAt;
        entity.RegistrationCloseAt = request.RegistrationCloseAt;
        entity.EmergencyPhone = CleanOptional(request.EmergencyPhone);
        entity.InsuranceNote = CleanOptional(request.InsuranceNote);

        await _audit.LogAsync("Event.Update", nameof(Event), eventId.ToString(), old,
            new
            {
                entity.Name, entity.VenueId, entity.EventDate, entity.EndDate,
                entity.RegistrationOpenAt, entity.RegistrationCloseAt
            }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetDetailAsync(eventId, ct);
    }

    public async Task DeleteAsync(int eventId, CancellationToken ct = default)
    {
        var entity = await LoadForWriteAsync(eventId, ct);
        if (entity.Status != EventStatus.Draft)
            throw new BusinessRuleException("BR-31", "Chỉ được xóa giải đang ở trạng thái bản nháp.");
        if (await _events.HasRegistrationsAsync(eventId, ct))
            throw new BusinessRuleException("BR-31", "Không thể xóa giải đã có đăng ký.");

        await _audit.LogAsync("Event.Delete", nameof(Event), eventId.ToString(),
            new { entity.Name, entity.VenueId, entity.EventDate }, null, ct);
        _events.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public Task ChangeStatusAsync(int eventId, ChangeEventStatusRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Chuyển trạng thái giải thuộc UC-20 và chưa được triển khai.");

    private async Task<Event> LoadForWriteAsync(int eventId, CancellationToken ct)
    {
        var entity = await _events.GetTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(entity.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(entity.OrganizationId, ct);
        return entity;
    }

    private async Task<string> CreateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var root = SlugHelper.ToSlug(name);
        if (string.IsNullOrWhiteSpace(root)) root = "giai-chay";
        var slug = root;
        var suffix = 2;
        while (await _events.SlugExistsAsync(slug, ct)) slug = $"{root}-{suffix++}";
        return slug;
    }

    private async Task<EventPolicy> CreateDefaultPolicyAsync(CancellationToken ct)
    {
        var transferValue = await _settings.GetAsync("PolicyDefault.TransferPolicy", ct);
        var transferPolicy = Enum.TryParse<TransferPolicy>(transferValue, true, out var parsed)
            ? parsed
            : TransferPolicy.Forbidden;
        return new EventPolicy
        {
            GuardianConsentUnderAge = await _settings.GetIntAsync("PolicyDefault.GuardianConsentUnderAge", 18, ct),
            MaxDependentsPerAccount = await _settings.GetIntAsync("PolicyDefault.MaxDependentsPerAccount", 5, ct),
            ComplaintWindowMinutes = await _settings.GetIntAsync("PolicyDefault.ComplaintWindowMinutes", 1440, ct),
            TransferPolicy = transferPolicy
        };
    }

    private static void Validate(UpsertEventRequest request)
    {
        if (request.EndDate < request.EventDate)
            throw new BusinessRuleException("VALIDATION", "Ngày kết thúc phải từ ngày tổ chức trở đi.", 400);
        if (request.RegistrationOpenAt >= request.RegistrationCloseAt)
            throw new BusinessRuleException("VALIDATION", "Thời gian đóng đăng ký phải sau thời gian mở đăng ký.", 400);
        if (DateOnly.FromDateTime(request.RegistrationCloseAt) > request.EventDate)
            throw new BusinessRuleException("VALIDATION", "Đăng ký phải đóng chậm nhất vào ngày tổ chức.", 400);
    }

    private string? CleanHtml(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : _sanitizer.Sanitize(value);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IQueryable<EventSummaryDto> ProjectSummary(IQueryable<Event> query) =>
        query.Select(e => new EventSummaryDto
        {
            Id = e.EventId,
            Name = e.Name,
            Slug = e.Slug,
            OrganizationId = e.OrganizationId,
            OrganizationName = e.Organization.Name,
            VenueName = e.Venue.Name,
            City = e.Venue.City,
            EventDate = e.EventDate,
            EndDate = e.EndDate,
            RegistrationOpenAt = e.RegistrationOpenAt,
            RegistrationCloseAt = e.RegistrationCloseAt,
            Status = e.Status.ToString(),
            BannerUrl = e.BannerUrl,
            Disciplines = e.Categories.Any(c => c.Discipline == Discipline.Road) && e.Categories.Any(c => c.Discipline == Discipline.Trail)
                ? "Road,Trail"
                : e.Categories.Any(c => c.Discipline == Discipline.Road) ? "Road"
                : e.Categories.Any(c => c.Discipline == Discipline.Trail) ? "Trail" : "",
            MinDistanceKm = e.Categories.Select(c => (decimal?)c.DistanceKm).Min() ?? 0,
            MaxDistanceKm = e.Categories.Select(c => (decimal?)c.DistanceKm).Max() ?? 0
        });

    private static EventDetailDto ToDetail(Event e) => new()
    {
        Id = e.EventId,
        Name = e.Name,
        Slug = e.Slug,
        OrganizationId = e.OrganizationId,
        OrganizationName = e.Organization.Name,
        VenueId = e.VenueId,
        VenueName = e.Venue.Name,
        VenueAddress = e.Venue.Address,
        City = e.Venue.City,
        DescriptionHtml = e.DescriptionHtml,
        BannerUrl = e.BannerUrl,
        EventDate = e.EventDate,
        EndDate = e.EndDate,
        RegistrationOpenAt = e.RegistrationOpenAt,
        RegistrationCloseAt = e.RegistrationCloseAt,
        Status = e.Status.ToString(),
        ResultsStatus = e.ResultsStatus.ToString(),
        EmergencyPhone = e.EmergencyPhone,
        InsuranceNote = e.InsuranceNote,
        WaiverVersion = e.WaiverVersion,
        Policy = ToPolicyDto(e.Policy)
    };

    private static EventPolicyDto ToPolicyDto(EventPolicy p) => new()
    {
        AgeCalculationMode = p.AgeCalculationMode.ToString(),
        GuardianConsentUnderAge = p.GuardianConsentUnderAge,
        MaxDependentsPerAccount = p.MaxDependentsPerAccount,
        InfoEditDeadline = p.InfoEditDeadline,
        TransferPolicy = p.TransferPolicy.ToString(),
        TransferFee = p.TransferFee,
        MaxTransfers = p.MaxTransfers,
        TransferDeadline = p.TransferDeadline,
        CategoryChangeFee = p.CategoryChangeFee,
        MaxCategoryChanges = p.MaxCategoryChanges,
        CategoryChangeDeadline = p.CategoryChangeDeadline,
        LockSpecialEntries = p.LockSpecialEntries,
        OverallRankingBasis = p.OverallRankingBasis?.ToString(),
        AgeGroupRankingBasis = p.AgeGroupRankingBasis?.ToString(),
        TieBreakRule = p.TieBreakRule.ToString(),
        LateStartToleranceMinutes = p.LateStartToleranceMinutes,
        ComplaintWindowMinutes = p.ComplaintWindowMinutes,
        ComplaintFee = p.ComplaintFee,
        AwardStackingPolicy = p.AwardStackingPolicy.ToString(),
        ForceMajeurePolicy = p.ForceMajeurePolicy.ToString()
    };
}
