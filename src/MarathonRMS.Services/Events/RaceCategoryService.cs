using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Events;

/// <summary>UC-21 – CRUD cự ly, dải BIB, sức chứa và điều kiện tham gia.</summary>
public class RaceCategoryService : IRaceCategoryService
{
    private readonly IRaceCategoryRepository _categories;
    private readonly IEventRepository _events;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public RaceCategoryService(IRaceCategoryRepository categories, IEventRepository events,
        IOrganizationScope scope, ICurrentUserService currentUser, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _categories = categories;
        _events = events;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<RaceCategoryDto>> GetByEventAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _events.GetDetailedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (ev.Status == EventStatus.Draft && !CanManage(ev.OrganizationId))
            throw new NotFoundException("Không tìm thấy giải.");

        var categories = await _categories.GetByEventAsync(eventId, ct);
        var counts = await _categories.GetActiveCountsAsync(eventId, ct);
        return categories.Select(c => RaceCategoryMappings.ToDto(c, counts.GetValueOrDefault(c.CategoryId), _clock.Now)).ToList();
    }

    public async Task<RaceCategoryDto> CreateAsync(int eventId, UpsertRaceCategoryRequest request,
        CancellationToken ct = default)
    {
        var ev = await LoadEventForWriteAsync(eventId, ct);
        var values = await ValidateAsync(ev, request, null, ct);

        var entity = new RaceCategory { EventId = eventId };
        Apply(entity, request, values.Discipline, values.CategoryType);

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _categories.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("RaceCategory.Create", nameof(RaceCategory), entity.CategoryId.ToString(), null,
            Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return RaceCategoryMappings.ToDto(entity, 0, _clock.Now);
    }

    public async Task<RaceCategoryDto> UpdateAsync(int categoryId, UpsertRaceCategoryRequest request,
        CancellationToken ct = default)
    {
        var entity = await LoadCategoryForWriteAsync(categoryId, ct);
        var rowVersion = ParseRowVersion(request.RowVersion);
        if (!entity.RowVersion.SequenceEqual(rowVersion))
            throw new BusinessRuleException(ErrorCodes.Conflict,
                "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại và thử lại.");
        var values = await ValidateAsync(entity.Event, request, categoryId, ct);
        await EnsureExistingRegistrationsRemainValidAsync(entity, request, ct);

        var old = Snapshot(entity);
        _categories.SetOriginalRowVersion(entity, rowVersion);
        Apply(entity, request, values.Discipline, values.CategoryType);

        await _audit.LogAsync("RaceCategory.Update", nameof(RaceCategory), categoryId.ToString(), old,
            Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);

        var activeCount = await _categories.CountActiveRegistrationsAsync(categoryId, ct);
        return RaceCategoryMappings.ToDto(entity, activeCount, _clock.Now);
    }

    public async Task DeleteAsync(int categoryId, CancellationToken ct = default)
    {
        var entity = await LoadCategoryForWriteAsync(categoryId, ct);
        if (await _categories.HasRegistrationsAsync(categoryId, ct))
            throw new BusinessRuleException("BR-46", "Không thể xóa cự ly đã có đăng ký.");

        await _audit.LogAsync("RaceCategory.Delete", nameof(RaceCategory), categoryId.ToString(), Snapshot(entity), null, ct);
        _categories.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Event> LoadEventForWriteAsync(int eventId, CancellationToken ct)
    {
        var ev = await _events.GetTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        return ev;
    }

    private async Task<RaceCategory> LoadCategoryForWriteAsync(int categoryId, CancellationToken ct)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct)
            ?? throw new NotFoundException("Không tìm thấy cự ly.");
        _scope.EnsureCanManage(category.Event.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(category.Event.OrganizationId, ct);
        return category;
    }

    private bool CanManage(int organizationId) =>
        _currentUser.IsAdminOrAbove ||
        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == organizationId);

    private async Task<(Discipline Discipline, CategoryType CategoryType)> ValidateAsync(Event ev,
        UpsertRaceCategoryRequest request, int? categoryId, CancellationToken ct)
    {
        var discipline = Enum.Parse<Discipline>(request.Discipline, true);
        var categoryType = Enum.Parse<CategoryType>(request.CategoryType, true);

        if (request.MaxAge is int maxAge && maxAge < request.MinAge)
            throw new BusinessRuleException("VALIDATION", "Tuổi tối đa phải lớn hơn hoặc bằng tuổi tối thiểu.", 400);
        if (request.BibFrom >= request.BibTo)
            throw new BusinessRuleException("BR-19", "Số đầu dải BIB phải nhỏ hơn số cuối dải BIB.", 400);
        if (!request.IsTimed && request.IsRanked)
            throw new BusinessRuleException("BR-26", "Cự ly không bấm giờ thì không thể xếp hạng.", 400);
        if (request.IsTimed && request.CutOffMinutes is null)
            throw new BusinessRuleException("VALIDATION", "Cự ly có bấm giờ phải có thời gian cut-off.", 400);

        var earliestStart = ev.EventDate.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var latestStart = ev.EndDate.ToDateTime(TimeOnly.MaxValue);
        if (request.StartAt < earliestStart || request.StartAt > latestStart)
            throw new BusinessRuleException("BR-39",
                $"Giờ xuất phát phải nằm từ {earliestStart:dd/MM/yyyy HH:mm} đến {latestStart:dd/MM/yyyy HH:mm}.", 400);

        var name = request.Name.Trim();
        if (await _categories.NameExistsAsync(ev.EventId, name, categoryId, ct))
            throw new BusinessRuleException("VALIDATION", "Tên cự ly đã tồn tại trong giải.", 400);
        if (await _categories.BibRangeOverlapsAsync(ev.EventId, request.BibFrom, request.BibTo, categoryId, ct))
            throw new BusinessRuleException("BR-19", "Dải BIB bị chồng lấn với cự ly khác.");

        return (discipline, categoryType);
    }

    private async Task EnsureExistingRegistrationsRemainValidAsync(RaceCategory entity,
        UpsertRaceCategoryRequest request, CancellationToken ct)
    {
        var activeCount = await _categories.CountActiveRegistrationsAsync(entity.CategoryId, ct);
        if (request.Capacity < activeCount)
            throw new BusinessRuleException("BR-46",
                $"Sức chứa không được nhỏ hơn {activeCount} đăng ký còn hiệu lực.");

        if (await _categories.HasAssignedBibOutsideRangeAsync(entity.CategoryId, request.BibFrom, request.BibTo, ct))
            throw new BusinessRuleException("BR-19", "Dải BIB mới không chứa toàn bộ BIB đã gán.");

        if (activeCount == 0) return;
        var birthDates = await _categories.GetActiveRegistrationBirthDatesAsync(entity.CategoryId, ct);
        var eventDate = entity.Event.EventDate;
        foreach (var dateOfBirth in birthDates)
        {
            var age = entity.Event.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear
                ? eventDate.Year - dateOfBirth.Year
                : AgeCalculator.AgeAt(dateOfBirth, eventDate);
            if (age < request.MinAge || (request.MaxAge is int maxAge && age > maxAge))
                throw new BusinessRuleException("BR-46",
                    "Không được siết điều kiện tuổi làm đăng ký hiện có trở thành không hợp lệ.");
        }
    }

    private static void Apply(RaceCategory entity, UpsertRaceCategoryRequest request,
        Discipline discipline, CategoryType categoryType)
    {
        entity.Name = request.Name.Trim();
        entity.Discipline = discipline;
        entity.CategoryType = categoryType;
        entity.DistanceKm = request.DistanceKm;
        entity.ActualDistanceKm = request.ActualDistanceKm;
        entity.ElevationGainM = request.ElevationGainM;
        entity.ItraPoints = request.ItraPoints;
        entity.Capacity = request.Capacity;
        entity.StartAt = request.StartAt;
        entity.CutOffMinutes = request.IsTimed ? request.CutOffMinutes : null;
        entity.MinAge = request.MinAge;
        entity.MaxAge = request.MaxAge;
        entity.EscortRequiredUnderAge = request.EscortRequiredUnderAge;
        entity.IsTimed = request.IsTimed;
        entity.IsRanked = request.IsRanked;
        entity.BibFrom = request.BibFrom;
        entity.BibTo = request.BibTo;
        entity.BibColor = CleanOptional(request.BibColor);
        entity.MedalImageUrl = CleanOptional(request.MedalImageUrl);
    }

    private static byte[] ParseRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException("VALIDATION", "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.", 400);
        try { return Convert.FromBase64String(value); }
        catch (FormatException)
        {
            throw new BusinessRuleException("VALIDATION", "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.", 400);
        }
    }

    private static object Snapshot(RaceCategory c) => new
    {
        c.EventId, c.Name, Discipline = c.Discipline.ToString(), CategoryType = c.CategoryType.ToString(),
        c.DistanceKm, c.ActualDistanceKm, c.ElevationGainM, c.ItraPoints, c.Capacity, c.StartAt,
        c.CutOffMinutes, c.MinAge, c.MaxAge, c.EscortRequiredUnderAge, c.IsTimed, c.IsRanked,
        c.BibFrom, c.BibTo, c.BibColor, c.MedalImageUrl
    };

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class RaceCategoryMappings
{
    public static RaceCategoryDto ToDto(RaceCategory c, int activeRegistrations, DateTime now)
    {
        var currentTier = c.PriceTiers.FirstOrDefault(t => t.ValidFrom <= now && now < t.ValidTo);
        return new RaceCategoryDto
        {
            Id = c.CategoryId,
            EventId = c.EventId,
            Name = c.Name,
            Discipline = c.Discipline.ToString(),
            CategoryType = c.CategoryType.ToString(),
            DistanceKm = c.DistanceKm,
            ActualDistanceKm = c.ActualDistanceKm,
            ElevationGainM = c.ElevationGainM,
            ItraPoints = c.ItraPoints,
            Capacity = c.Capacity,
            RemainingSlots = Math.Max(0, c.Capacity - activeRegistrations),
            StartAt = c.StartAt,
            CutOffMinutes = c.CutOffMinutes,
            MinAge = c.MinAge,
            MaxAge = c.MaxAge,
            EscortRequiredUnderAge = c.EscortRequiredUnderAge,
            IsTimed = c.IsTimed,
            IsRanked = c.IsRanked,
            BibFrom = c.BibFrom,
            BibTo = c.BibTo,
            BibColor = c.BibColor,
            MedalImageUrl = c.MedalImageUrl,
            CurrentPrice = currentTier?.Price,
            CurrentPriceTierName = currentTier?.Name,
            PriceTiers = c.PriceTiers.OrderBy(t => t.ValidFrom).Select(t => new PriceTierDto
            {
                Id = t.PriceTierId, CategoryId = t.CategoryId, Name = t.Name, Price = t.Price,
                ValidFrom = t.ValidFrom, ValidTo = t.ValidTo
            }).ToList(),
            Checkpoints = c.Checkpoints.OrderBy(p => p.Sequence).Select(p => new CheckpointDto
            {
                Id = p.CheckpointId, CategoryId = p.CategoryId, Name = p.Name, DistanceKm = p.DistanceKm,
                Sequence = p.Sequence, Type = p.Type.ToString(), IsMandatory = p.IsMandatory,
                CutOffAt = p.CutOffAt, StationType = p.StationType.ToString(),
                Latitude = p.Latitude, Longitude = p.Longitude
            }).ToList(),
            RowVersion = c.RowVersion.Length == 0 ? null : Convert.ToBase64String(c.RowVersion)
        };
    }
}
