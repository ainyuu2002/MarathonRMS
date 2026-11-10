using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Events;

/// <summary>UC-22 – CRUD mức giá, BR-47/48 và cảnh báo BR-49.</summary>
public class PriceTierService : IPriceTierService
{
    private readonly IPriceTierRepository _tiers;
    private readonly IRaceCategoryRepository _categories;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;
    private readonly IPriceReferenceService _reference;

    public PriceTierService(IPriceTierRepository tiers, IRaceCategoryRepository categories,
        IOrganizationScope scope, ICurrentUserService currentUser, ISystemSettingsProvider settings,
        IAuditLogger audit, IUnitOfWork uow, IPriceReferenceService reference)
    {
        _reference = reference;
        _tiers = tiers;
        _categories = categories;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<PriceTierDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, requireWrite: false, ct);
        var tiers = await _tiers.GetByCategoryAsync(categoryId, ct);
        var coverageWarning = GetCoverageWarning(category.Event, tiers.Select(ToCandidate).ToList());
        return tiers.Select(t => ToDto(t, coverageWarning is null ? [] : [coverageWarning])).ToList();
    }

    public async Task<PriceTierDto> CreateAsync(int categoryId, UpsertPriceTierRequest request,
        CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, requireWrite: true, ct);
        var candidate = new PriceTierCandidate(null, request.Name.Trim(), request.Price, request.ValidFrom, request.ValidTo);
        var candidates = category.PriceTiers.Select(ToCandidate).Append(candidate).ToList();
        var warnings = await ValidateAsync(category, candidate, candidates, ct);

        var entity = new PriceTier { CategoryId = categoryId };
        Apply(entity, request);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _tiers.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("PriceTier.Create", nameof(PriceTier), entity.PriceTierId.ToString(), null,
            Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity, warnings);
    }

    public async Task<PriceTierDto> UpdateAsync(int priceTierId, UpsertPriceTierRequest request,
        CancellationToken ct = default)
    {
        var entity = await _tiers.GetTrackedAsync(priceTierId, ct) ?? throw new NotFoundException("Không tìm thấy mức giá.");
        await EnsureWriteAsync(entity.Category.Event, ct);
        var candidate = new PriceTierCandidate(priceTierId, request.Name.Trim(), request.Price, request.ValidFrom, request.ValidTo);
        var candidates = entity.Category.PriceTiers.Where(t => t.PriceTierId != priceTierId)
            .Select(ToCandidate).Append(candidate).ToList();
        var warnings = await ValidateAsync(entity.Category, candidate, candidates, ct);

        var old = Snapshot(entity);
        Apply(entity, request);
        await _audit.LogAsync("PriceTier.Update", nameof(PriceTier), priceTierId.ToString(), old, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity, warnings);
    }

    public async Task DeleteAsync(int priceTierId, CancellationToken ct = default)
    {
        var entity = await _tiers.GetTrackedAsync(priceTierId, ct) ?? throw new NotFoundException("Không tìm thấy mức giá.");
        await EnsureWriteAsync(entity.Category.Event, ct);
        var candidates = entity.Category.PriceTiers.Where(t => t.PriceTierId != priceTierId).Select(ToCandidate).ToList();
        EnsureCoverageAllowed(entity.Category.Event, candidates);

        await _audit.LogAsync("PriceTier.Delete", nameof(PriceTier), priceTierId.ToString(), Snapshot(entity), null, ct);
        _tiers.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<RaceCategory> LoadCategoryAsync(int categoryId, bool requireWrite, CancellationToken ct)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        if (requireWrite) await EnsureWriteAsync(category.Event, ct);
        else if (category.Event.Status == EventStatus.Draft && !CanManage(category.Event.OrganizationId))
            throw new NotFoundException("Không tìm thấy cự ly.");
        return category;
    }

    private async Task EnsureWriteAsync(Event ev, CancellationToken ct)
    {
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
    }

    private async Task<List<string>> ValidateAsync(RaceCategory category, PriceTierCandidate candidate,
        List<PriceTierCandidate> candidates, CancellationToken ct)
    {
        if (candidate.ValidFrom >= candidate.ValidTo)
            throw new BusinessRuleException("BR-47", "Thời điểm kết thúc phải sau thời điểm bắt đầu.", 400);
        if (candidate.ValidFrom < category.Event.RegistrationOpenAt || candidate.ValidTo > category.Event.RegistrationCloseAt)
            throw new BusinessRuleException("BR-47", "Mức giá phải nằm trong toàn bộ thời gian đăng ký của giải.", 400);
        if (decimal.Truncate(candidate.Price) != candidate.Price || candidate.Price % 1000 != 0)
            throw new BusinessRuleException("BR-48", "Giá phải là số nguyên và là bội số 1.000đ.", 400);

        var min = await _settings.GetDecimalAsync("Price.MinNonZero", 10_000m, ct);
        var max = await _settings.GetDecimalAsync("Price.Max", 20_000_000m, ct);
        if (candidate.Price != 0 && (candidate.Price < min || candidate.Price > max))
            throw new BusinessRuleException("BR-48", $"Giá phải bằng 0 hoặc nằm trong khoảng {min:N0}đ–{max:N0}đ.", 400);

        var ordered = candidates.OrderBy(t => t.ValidFrom).ToList();
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i - 1].ValidTo > ordered[i].ValidFrom)
                throw new BusinessRuleException("BR-47", "Các mức giá không được chồng lấn thời gian.");

        EnsureCoverageAllowed(category.Event, ordered);
        var warnings = new List<string>();
        var coverageWarning = GetCoverageWarning(category.Event, ordered);
        if (coverageWarning is not null) warnings.Add(coverageWarning);

        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].Price < ordered[i - 1].Price)
            {
                warnings.Add($"BR-49: mức {ordered[i].Name} rẻ hơn mức {ordered[i - 1].Name}.");
                break;
            }

        if (await _tiers.HasShorterCategoryWithHigherOverlappingTierAsync(category.EventId, category.CategoryId,
                category.DistanceKm, candidate.Price, candidate.ValidFrom, candidate.ValidTo, ct))
            warnings.Add("BR-49: cự ly dài đang rẻ hơn một cự ly ngắn hơn trong cùng thời gian bán.");
        // BR-49 (ý 2): lệch khoảng giá tham khảo quá 50% (UC-87, phần A)
        var reference = await _reference.GetAsync(category.Discipline.ToString(), category.DistanceKm, category.EventId, ct);
        if (_reference.Warn(reference, candidate.Price) is { } refWarning) warnings.Add(refWarning);
        return warnings;
    }

    private static void EnsureCoverageAllowed(Event ev, List<PriceTierCandidate> candidates)
    {
        if (ev.Status is EventStatus.Draft or EventStatus.Published) return;
        if (!CoversWindow(ev, candidates))
            throw new BusinessRuleException("BR-47", "Từ khi mở đăng ký, các mức giá phải phủ kín và liền mạch toàn bộ thời gian đăng ký.");
    }

    private static string? GetCoverageWarning(Event ev, List<PriceTierCandidate> candidates) =>
        CoversWindow(ev, candidates) ? null : "BR-47: các mức giá chưa phủ kín, liền mạch toàn bộ thời gian đăng ký; giải chưa thể mở đăng ký.";

    private static bool CoversWindow(Event ev, List<PriceTierCandidate> candidates)
    {
        var ordered = candidates.OrderBy(t => t.ValidFrom).ToList();
        if (ordered.Count == 0 || ordered[0].ValidFrom != ev.RegistrationOpenAt || ordered[^1].ValidTo != ev.RegistrationCloseAt)
            return false;
        return ordered.Zip(ordered.Skip(1), (current, next) => current.ValidTo == next.ValidFrom).All(x => x);
    }

    private bool CanManage(int organizationId) => _currentUser.IsAdminOrAbove ||
        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == organizationId);

    private static void Apply(PriceTier entity, UpsertPriceTierRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Price = request.Price;
        entity.ValidFrom = request.ValidFrom;
        entity.ValidTo = request.ValidTo;
    }

    private static PriceTierCandidate ToCandidate(PriceTier t) =>
        new(t.PriceTierId, t.Name, t.Price, t.ValidFrom, t.ValidTo);

    private static PriceTierDto ToDto(PriceTier t, IReadOnlyList<string>? warnings = null) => new()
    {
        Id = t.PriceTierId, CategoryId = t.CategoryId, Name = t.Name, Price = t.Price,
        ValidFrom = t.ValidFrom, ValidTo = t.ValidTo, Warnings = warnings ?? []
    };

    private static object Snapshot(PriceTier t) => new { t.CategoryId, t.Name, t.Price, t.ValidFrom, t.ValidTo };
    private sealed record PriceTierCandidate(int? Id, string Name, decimal Price, DateTime ValidFrom, DateTime ValidTo);
}

/// <summary>UC-23 – CRUD nhóm tuổi; khóa khi kết quả đã Provisional (BR-46c).</summary>
public class AgeGroupService : IAgeGroupService
{
    private readonly IAgeGroupRepository _groups;
    private readonly IEventRepository _events;
    private readonly IRaceCategoryRepository _categories;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public AgeGroupService(IAgeGroupRepository groups, IEventRepository events, IRaceCategoryRepository categories,
        IOrganizationScope scope, ICurrentUserService currentUser, IAuditLogger audit, IUnitOfWork uow)
    {
        _groups = groups; _events = events; _categories = categories; _scope = scope;
        _currentUser = currentUser; _audit = audit; _uow = uow;
    }

    public async Task<IReadOnlyList<AgeGroupDto>> GetByEventAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _events.GetDetailedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        EnsureReadable(ev);
        return (await _groups.GetByEventAsync(eventId, ct)).Select(ToDto).ToList();
    }

    public async Task<AgeGroupDto> CreateAsync(int eventId, UpsertAgeGroupRequest request, CancellationToken ct = default)
    {
        var ev = await LoadEventForWriteAsync(eventId, ct);
        var existing = await _groups.GetByEventAsync(eventId, ct);
        await ValidateAsync(ev, request, existing, null, ct);
        var entity = new AgeGroup { EventId = eventId };
        Apply(entity, request);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _groups.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("AgeGroup.Create", nameof(AgeGroup), entity.AgeGroupId.ToString(), null, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<AgeGroupDto> UpdateAsync(int ageGroupId, UpsertAgeGroupRequest request, CancellationToken ct = default)
    {
        var entity = await _groups.GetTrackedAsync(ageGroupId, ct) ?? throw new NotFoundException("Không tìm thấy nhóm tuổi.");
        await EnsureWriteAsync(entity.Event, ct);
        var existing = await _groups.GetByEventAsync(entity.EventId, ct);
        await ValidateAsync(entity.Event, request, existing, ageGroupId, ct);
        var old = Snapshot(entity);
        Apply(entity, request);
        await _audit.LogAsync("AgeGroup.Update", nameof(AgeGroup), ageGroupId.ToString(), old, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int ageGroupId, CancellationToken ct = default)
    {
        var entity = await _groups.GetTrackedAsync(ageGroupId, ct) ?? throw new NotFoundException("Không tìm thấy nhóm tuổi.");
        await EnsureWriteAsync(entity.Event, ct);
        if (await _groups.IsReferencedAsync(ageGroupId, ct))
            throw new BusinessRuleException("BR-46", "Không thể xóa nhóm tuổi đã được dùng trong kết quả.");
        await _audit.LogAsync("AgeGroup.Delete", nameof(AgeGroup), ageGroupId.ToString(), Snapshot(entity), null, ct);
        _groups.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Event> LoadEventForWriteAsync(int eventId, CancellationToken ct)
    {
        var ev = await _events.GetTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        await EnsureWriteAsync(ev, ct);
        return ev;
    }

    private async Task EnsureWriteAsync(Event ev, CancellationToken ct)
    {
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-46", "Không được sửa nhóm tuổi sau khi công bố kết quả tạm.");
    }

    private void EnsureReadable(Event ev)
    {
        if (ev.Status == EventStatus.Draft && !CanManage(ev.OrganizationId))
            throw new NotFoundException("Không tìm thấy giải.");
    }

    private async Task ValidateAsync(Event ev, UpsertAgeGroupRequest request, List<AgeGroup> existing,
        int? ageGroupId, CancellationToken ct)
    {
        if (request.MaxAge is int maxAge && maxAge < request.MinAge)
            throw new BusinessRuleException("VALIDATION", "Tuổi tối đa phải lớn hơn hoặc bằng tuổi tối thiểu.", 400);
        if (request.CategoryId is int categoryId)
        {
            var category = await _categories.GetTrackedAsync(categoryId, ct);
            if (category is null || category.EventId != ev.EventId)
                throw new BusinessRuleException("VALIDATION", "Cự ly không thuộc giải này.", 400);
        }

        var peers = existing.Where(g => g.AgeGroupId != ageGroupId && g.CategoryId == request.CategoryId).ToList();
        if (peers.Any(g => string.Equals(g.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("VALIDATION", "Tên nhóm tuổi đã tồn tại trong cùng phạm vi cự ly.", 400);
        var requestedMax = request.MaxAge ?? int.MaxValue;
        if (peers.Any(g => request.MinAge <= (g.MaxAge ?? int.MaxValue) && g.MinAge <= requestedMax))
            throw new BusinessRuleException("VALIDATION", "Khoảng tuổi bị chồng lấn với nhóm khác trong cùng phạm vi.", 400);
    }

    private bool CanManage(int organizationId) => _currentUser.IsAdminOrAbove ||
        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == organizationId);

    private static void Apply(AgeGroup entity, UpsertAgeGroupRequest request)
    {
        entity.CategoryId = request.CategoryId;
        entity.Name = request.Name.Trim();
        entity.MinAge = request.MinAge;
        entity.MaxAge = request.MaxAge;
    }

    private static AgeGroupDto ToDto(AgeGroup g) => new()
    {
        Id = g.AgeGroupId, EventId = g.EventId, CategoryId = g.CategoryId,
        Name = g.Name, MinAge = g.MinAge, MaxAge = g.MaxAge
    };
    private static object Snapshot(AgeGroup g) => new { g.EventId, g.CategoryId, g.Name, g.MinAge, g.MaxAge };
}

/// <summary>UC-24 – CRUD checkpoint và cut-off tại trạm (BR-39).</summary>
public class CheckpointService : ICheckpointService
{
    private readonly ICheckpointRepository _checkpoints;
    private readonly IRaceCategoryRepository _categories;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public CheckpointService(ICheckpointRepository checkpoints, IRaceCategoryRepository categories,
        IOrganizationScope scope, ICurrentUserService currentUser, IAuditLogger audit, IUnitOfWork uow)
    {
        _checkpoints = checkpoints; _categories = categories; _scope = scope;
        _currentUser = currentUser; _audit = audit; _uow = uow;
    }

    public async Task<IReadOnlyList<CheckpointDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, requireWrite: false, ct);
        return (await _checkpoints.GetByCategoryAsync(category.CategoryId, ct)).Select(ToDto).ToList();
    }

    public async Task<CheckpointDto> CreateAsync(int categoryId, UpsertCheckpointRequest request, CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, requireWrite: true, ct);
        var parsed = Parse(request);
        var candidates = category.Checkpoints.Select(ToCandidate)
            .Append(ToCandidate(null, request, parsed.Type)).ToList();
        Validate(category, request, parsed.Type, candidates);
        var entity = new Checkpoint { CategoryId = categoryId };
        Apply(entity, request, parsed.Type, parsed.StationType);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _checkpoints.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("Checkpoint.Create", nameof(Checkpoint), entity.CheckpointId.ToString(), null, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<CheckpointDto> UpdateAsync(int checkpointId, UpsertCheckpointRequest request, CancellationToken ct = default)
    {
        var entity = await _checkpoints.GetTrackedAsync(checkpointId, ct) ?? throw new NotFoundException("Không tìm thấy checkpoint.");
        await EnsureWriteAsync(entity.Category.Event, ct);
        var parsed = Parse(request);
        var candidates = entity.Category.Checkpoints.Where(p => p.CheckpointId != checkpointId).Select(ToCandidate)
            .Append(ToCandidate(checkpointId, request, parsed.Type)).ToList();
        Validate(entity.Category, request, parsed.Type, candidates);
        var old = Snapshot(entity);
        Apply(entity, request, parsed.Type, parsed.StationType);
        await _audit.LogAsync("Checkpoint.Update", nameof(Checkpoint), checkpointId.ToString(), old, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int checkpointId, CancellationToken ct = default)
    {
        var entity = await _checkpoints.GetTrackedAsync(checkpointId, ct) ?? throw new NotFoundException("Không tìm thấy checkpoint.");
        await EnsureWriteAsync(entity.Category.Event, ct);
        if (await _checkpoints.IsReferencedAsync(checkpointId, ct))
            throw new BusinessRuleException("BR-46", "Không thể xóa checkpoint đã có dữ liệu bấm giờ.");
        await _audit.LogAsync("Checkpoint.Delete", nameof(Checkpoint), checkpointId.ToString(), Snapshot(entity), null, ct);
        _checkpoints.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<RaceCategory> LoadCategoryAsync(int categoryId, bool requireWrite, CancellationToken ct)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        if (requireWrite) await EnsureWriteAsync(category.Event, ct);
        else if (category.Event.Status == EventStatus.Draft && !CanManage(category.Event.OrganizationId))
            throw new NotFoundException("Không tìm thấy cự ly.");
        return category;
    }

    private async Task EnsureWriteAsync(Event ev, CancellationToken ct)
    {
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-46", "Không được sửa checkpoint sau khi công bố kết quả tạm.");
    }

    private static (CheckpointType Type, StationType StationType) Parse(UpsertCheckpointRequest request) =>
        (Enum.Parse<CheckpointType>(request.Type, true), Enum.Parse<StationType>(request.StationType, true));

    private static void Validate(RaceCategory category, UpsertCheckpointRequest request, CheckpointType type,
        List<CheckpointCandidate> candidates)
    {
        var courseDistance = category.ActualDistanceKm ?? category.DistanceKm;
        if (request.DistanceKm > courseDistance)
            throw new BusinessRuleException("VALIDATION", $"Km checkpoint không được vượt quá cự ly thực đo {courseDistance:0.##} km.", 400);
        if ((type is CheckpointType.Start or CheckpointType.Finish) && !request.IsMandatory)
            throw new BusinessRuleException("BR-38", "Checkpoint Start và Finish phải là checkpoint bắt buộc.", 400);
        if (type == CheckpointType.Start && request.DistanceKm != 0)
            throw new BusinessRuleException("VALIDATION", "Checkpoint Start phải ở km 0.", 400);
        if (candidates.Count(p => p.Type == CheckpointType.Start) > 1 || candidates.Count(p => p.Type == CheckpointType.Finish) > 1)
            throw new BusinessRuleException("VALIDATION", "Mỗi cự ly chỉ có một checkpoint Start và một checkpoint Finish.", 400);
        if (candidates.GroupBy(p => p.Sequence).Any(g => g.Count() > 1))
            throw new BusinessRuleException("VALIDATION", "Thứ tự checkpoint không được trùng.", 400);

        var ordered = candidates.OrderBy(p => p.Sequence).ToList();
        if (ordered.Zip(ordered.Skip(1), (a, b) => a.DistanceKm <= b.DistanceKm).Any(valid => !valid))
            throw new BusinessRuleException("VALIDATION", "Km checkpoint phải tăng dần theo thứ tự.", 400);
        if (ordered.Any() && ordered.Any(p => p.Type == CheckpointType.Start) && ordered[0].Type != CheckpointType.Start)
            throw new BusinessRuleException("VALIDATION", "Checkpoint Start phải đứng đầu.", 400);
        if (ordered.Any() && ordered.Any(p => p.Type == CheckpointType.Finish) && ordered[^1].Type != CheckpointType.Finish)
            throw new BusinessRuleException("VALIDATION", "Checkpoint Finish phải đứng cuối.", 400);

        DateTime? previousCutOff = null;
        var latest = category.Event.EndDate.ToDateTime(TimeOnly.MaxValue);
        foreach (var point in ordered.Where(p => p.CutOffAt.HasValue))
        {
            if (point.CutOffAt < category.StartAt || point.CutOffAt > latest)
                throw new BusinessRuleException("BR-39", "Giờ cut-off checkpoint phải nằm từ giờ xuất phát đến hết ngày kết thúc giải.", 400);
            if (previousCutOff.HasValue && point.CutOffAt <= previousCutOff)
                throw new BusinessRuleException("BR-39", "Giờ cut-off checkpoint phải tăng dần theo thứ tự.", 400);
            previousCutOff = point.CutOffAt;
        }
    }

    private bool CanManage(int organizationId) => _currentUser.IsAdminOrAbove ||
        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == organizationId);

    private static void Apply(Checkpoint entity, UpsertCheckpointRequest request, CheckpointType type, StationType stationType)
    {
        entity.Name = request.Name.Trim(); entity.DistanceKm = request.DistanceKm; entity.Sequence = request.Sequence;
        entity.Type = type; entity.IsMandatory = request.IsMandatory; entity.CutOffAt = request.CutOffAt;
        entity.StationType = stationType; entity.Latitude = request.Latitude; entity.Longitude = request.Longitude;
    }

    private static CheckpointDto ToDto(Checkpoint p) => new()
    {
        Id = p.CheckpointId, CategoryId = p.CategoryId, Name = p.Name, DistanceKm = p.DistanceKm,
        Sequence = p.Sequence, Type = p.Type.ToString(), IsMandatory = p.IsMandatory,
        CutOffAt = p.CutOffAt, StationType = p.StationType.ToString(), Latitude = p.Latitude, Longitude = p.Longitude
    };
    private static CheckpointCandidate ToCandidate(Checkpoint p) =>
        new(p.CheckpointId, p.Sequence, p.DistanceKm, p.Type, p.CutOffAt);
    private static CheckpointCandidate ToCandidate(int? id, UpsertCheckpointRequest r, CheckpointType type) =>
        new(id, r.Sequence, r.DistanceKm, type, r.CutOffAt);
    private static object Snapshot(Checkpoint p) => new
    {
        p.CategoryId, p.Name, p.DistanceKm, p.Sequence, Type = p.Type.ToString(), p.IsMandatory,
        p.CutOffAt, StationType = p.StationType.ToString(), p.Latitude, p.Longitude
    };
    private sealed record CheckpointCandidate(int? Id, int Sequence, decimal DistanceKm, CheckpointType Type, DateTime? CutOffAt);
}
