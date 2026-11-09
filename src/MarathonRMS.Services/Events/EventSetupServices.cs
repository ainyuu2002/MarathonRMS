using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Events;

/// <summary>Kiểm tra dùng chung cho UC-22…24: quyền BTC (BR-30), giải còn sửa được, khóa khi kết quả đã công bố (BR-46c).</summary>
public class EventSetupGuard
{
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;

    public EventSetupGuard(IOrganizationScope scope, ICurrentUserService currentUser)
    {
        _scope = scope;
        _currentUser = currentUser;
    }

    /// <summary>Giải Draft chỉ BTC sở hữu (hoặc Admin) xem được.</summary>
    public void EnsureCanView(Event ev)
    {
        var canManage = _currentUser.IsAdminOrAbove ||
                        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == ev.OrganizationId);
        if (ev.Status == EventStatus.Draft && !canManage) throw new NotFoundException("Không tìm thấy giải.");
    }

    public async Task EnsureCanEditAsync(Event ev, bool lockedWhenResultsPublished, CancellationToken ct)
    {
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw new BusinessRuleException("BR-46", "Giải đã kết thúc hoặc đã hủy – không sửa cấu hình được nữa.");
        if (lockedWhenResultsPublished && ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-46", "Kết quả đã công bố – nhóm tuổi và checkpoint bị khóa.");
    }

    /// <summary>Từ khi mở đăng ký, các mức giá phải luôn phủ kín thời gian đăng ký (BR-47).</summary>
    public static bool RequiresFullPriceCoverage(Event ev) =>
        ev.Status is EventStatus.RegistrationOpen or EventStatus.RegistrationClosed or EventStatus.Postponed;

    /// <summary>BR-47: phủ kín [openAt, closeAt], không chồng lấn, không hở.</summary>
    public static bool PriceTiersCover(IEnumerable<PriceTier> tiers, DateTime openAt, DateTime closeAt)
    {
        var ordered = tiers.OrderBy(t => t.ValidFrom).ToList();
        if (ordered.Count == 0 || ordered[0].ValidFrom != openAt || ordered[^1].ValidTo != closeAt) return false;
        return ordered.Zip(ordered.Skip(1), (current, next) => current.ValidTo == next.ValidFrom).All(x => x);
    }
}

/// <summary>UC-22 – mức giá của cự ly (BR-47 phủ kín, BR-48 giá hợp lệ, BR-49 cảnh báo).</summary>
public class PriceTierService : IPriceTierService
{
    private readonly IPriceTierRepository _tiers;
    private readonly IRaceCategoryRepository _categories;
    private readonly EventSetupGuard _guard;
    private readonly ISystemSettingsProvider _settings;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    private readonly IPriceReferenceService _reference;

    public PriceTierService(IPriceTierRepository tiers, IRaceCategoryRepository categories, EventSetupGuard guard,
        ISystemSettingsProvider settings, IAuditLogger audit, IUnitOfWork uow, IPriceReferenceService reference)
    {
        _reference = reference;
        _tiers = tiers;
        _categories = categories;
        _guard = guard;
        _settings = settings;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<PriceTierDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        _guard.EnsureCanView(category.Event);
        var tiers = await _tiers.GetByCategoryAsync(categoryId, ct);
        return tiers.Select(t => ToDto(t)).ToList();
    }

    public async Task<PriceTierDto> CreateAsync(int categoryId, UpsertPriceTierRequest request, CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(category.EventId), ct);

        var others = await _tiers.GetByCategoryAsync(categoryId, ct);
        var entity = new PriceTier { CategoryId = categoryId };
        Apply(entity, request);
        await ValidateAsync(category, entity, others, ct);

        await _tiers.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("PriceTier.Create", nameof(PriceTier), entity.PriceTierId.ToString(), null, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(entity, await WarningsAsync(category, entity, others, ct));
    }

    public async Task<PriceTierDto> UpdateAsync(int priceTierId, UpsertPriceTierRequest request, CancellationToken ct = default)
    {
        var entity = await _tiers.GetTrackedAsync(priceTierId, ct) ?? throw new NotFoundException("Không tìm thấy mức giá.");
        var category = await LoadCategoryAsync(entity.CategoryId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(category.EventId), ct);

        var others = (await _tiers.GetByCategoryAsync(entity.CategoryId, ct)).Where(t => t.PriceTierId != priceTierId).ToList();
        var old = Snapshot(entity);
        Apply(entity, request);
        await ValidateAsync(category, entity, others, ct);

        await _audit.LogAsync("PriceTier.Update", nameof(PriceTier), priceTierId.ToString(), old, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(entity, await WarningsAsync(category, entity, others, ct));
    }

    public async Task DeleteAsync(int priceTierId, CancellationToken ct = default)
    {
        var entity = await _tiers.GetTrackedAsync(priceTierId, ct) ?? throw new NotFoundException("Không tìm thấy mức giá.");
        var category = await LoadCategoryAsync(entity.CategoryId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(category.EventId), ct);

        var ev = category.Event;
        if (EventSetupGuard.RequiresFullPriceCoverage(ev))
        {
            var remaining = (await _tiers.GetByCategoryAsync(entity.CategoryId, ct)).Where(t => t.PriceTierId != priceTierId);
            if (!EventSetupGuard.PriceTiersCover(remaining, ev.RegistrationOpenAt, ev.RegistrationCloseAt))
                throw new BusinessRuleException("BR-47",
                    "Giải đã mở đăng ký – xóa mức giá này sẽ để hở thời gian đăng ký. Hãy nới mức giá liền kề trước.");
        }

        await _audit.LogAsync("PriceTier.Delete", nameof(PriceTier), priceTierId.ToString(), Snapshot(entity), null, ct);
        _tiers.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<RaceCategory> LoadCategoryAsync(int categoryId, CancellationToken ct)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        await _guard.EnsureCanEditAsync(category.Event, lockedWhenResultsPublished: false, ct);
        return category;
    }

    private async Task ValidateAsync(RaceCategory category, PriceTier tier, IReadOnlyList<PriceTier> others, CancellationToken ct)
    {
        var ev = category.Event;
        if (string.IsNullOrWhiteSpace(tier.Name))
            throw new BusinessRuleException(ErrorCodes.Validation, "Vui lòng nhập tên mức giá.", 400);

        // BR-48: số nguyên, bội số 1.000đ, = 0 hoặc trong [MinNonZero, Max]
        if (tier.Price != decimal.Truncate(tier.Price) || tier.Price % 1000 != 0)
            throw new BusinessRuleException("BR-48", "Giá phải là bội số của 1.000đ.", 400);
        var min = await _settings.GetDecimalAsync("Price.MinNonZero", 10_000, ct);
        var max = await _settings.GetDecimalAsync("Price.Max", 20_000_000, ct);
        if (tier.Price != 0 && (tier.Price < min || tier.Price > max))
            throw new BusinessRuleException("BR-48", $"Giá phải bằng 0 hoặc nằm trong khoảng {min:N0}đ – {max:N0}đ.", 400);

        if (tier.ValidFrom >= tier.ValidTo)
            throw new BusinessRuleException(ErrorCodes.Validation, "Thời điểm kết thúc phải sau thời điểm bắt đầu.", 400);
        if (tier.ValidFrom < ev.RegistrationOpenAt || tier.ValidTo > ev.RegistrationCloseAt)
            throw new BusinessRuleException("BR-47",
                $"Mức giá phải nằm trong thời gian đăng ký {ev.RegistrationOpenAt:dd/MM/yyyy HH:mm} – {ev.RegistrationCloseAt:dd/MM/yyyy HH:mm}.", 400);

        var overlap = others.FirstOrDefault(o => o.ValidFrom < tier.ValidTo && tier.ValidFrom < o.ValidTo);
        if (overlap is not null)
            throw new BusinessRuleException("BR-47",
                $"Bị chồng lấn với mức giá \"{overlap.Name}\" ({overlap.ValidFrom:dd/MM/yyyy HH:mm} – {overlap.ValidTo:dd/MM/yyyy HH:mm}).");

        if (EventSetupGuard.RequiresFullPriceCoverage(ev) &&
            !EventSetupGuard.PriceTiersCover(others.Append(tier), ev.RegistrationOpenAt, ev.RegistrationCloseAt))
            throw new BusinessRuleException("BR-47",
                "Giải đã mở đăng ký – các mức giá phải luôn phủ kín thời gian đăng ký, không được để hở.");
    }

    /// <summary>BR-49 – cảnh báo, không chặn.</summary>
    private async Task<IReadOnlyList<string>> WarningsAsync(RaceCategory category, PriceTier tier,
        IReadOnlyList<PriceTier> others, CancellationToken ct)
    {
        var warnings = new List<string>();
        var previous = others.Where(o => o.ValidTo <= tier.ValidFrom).OrderByDescending(o => o.ValidTo).FirstOrDefault();
        if (previous is not null && tier.Price < previous.Price)
            warnings.Add($"Mức giá này rẻ hơn mức giá trước (\"{previous.Name}\" {previous.Price:N0}đ).");
        var next = others.Where(o => o.ValidFrom >= tier.ValidTo).OrderBy(o => o.ValidFrom).FirstOrDefault();
        if (next is not null && next.Price < tier.Price)
            warnings.Add($"Mức giá sau (\"{next.Name}\" {next.Price:N0}đ) rẻ hơn mức giá này.");

        var eventTiers = await _tiers.GetByEventAsync(category.EventId, ct);
        foreach (var other in eventTiers.Where(t => t.CategoryId != category.CategoryId &&
                                                   t.ValidFrom < tier.ValidTo && tier.ValidFrom < t.ValidTo))
        {
            if (other.Category.DistanceKm < category.DistanceKm && other.Price > tier.Price)
                warnings.Add($"Cự ly {other.Category.Name} ngắn hơn nhưng giá cao hơn ({other.Price:N0}đ) trong cùng thời gian.");
            else if (other.Category.DistanceKm > category.DistanceKm && other.Price < tier.Price)
                warnings.Add($"Cự ly {other.Category.Name} dài hơn nhưng giá thấp hơn ({other.Price:N0}đ) trong cùng thời gian.");
        }
        // BR-49 (ý 2): lệch khoảng giá tham khảo quá 50% (UC-87)
        var reference = await _reference.GetAsync(category.Discipline.ToString(), category.DistanceKm, category.EventId, ct);
        if (_reference.Warn(reference, tier.Price) is { } refWarning) warnings.Add(refWarning);
        return warnings.Distinct().ToList();
    }

    private static void Apply(PriceTier entity, UpsertPriceTierRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Price = request.Price;
        entity.ValidFrom = request.ValidFrom;
        entity.ValidTo = request.ValidTo;
    }

    private static object Snapshot(PriceTier t) => new { t.CategoryId, t.Name, t.Price, t.ValidFrom, t.ValidTo };

    private static PriceTierDto ToDto(PriceTier t, IReadOnlyList<string>? warnings = null) => new()
    {
        Id = t.PriceTierId, CategoryId = t.CategoryId, Name = t.Name, Price = t.Price,
        ValidFrom = t.ValidFrom, ValidTo = t.ValidTo, Warnings = warnings ?? Array.Empty<string>()
    };
}

/// <summary>UC-23 – nhóm tuổi của giải (khóa khi kết quả Provisional – BR-46c).</summary>
public class AgeGroupService : IAgeGroupService
{
    private const int NoUpperAge = 200;
    private readonly IAgeGroupRepository _groups;
    private readonly IEventRepository _events;
    private readonly IRaceCategoryRepository _categories;
    private readonly EventSetupGuard _guard;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public AgeGroupService(IAgeGroupRepository groups, IEventRepository events, IRaceCategoryRepository categories,
        EventSetupGuard guard, IAuditLogger audit, IUnitOfWork uow)
    {
        _groups = groups;
        _events = events;
        _categories = categories;
        _guard = guard;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<AgeGroupDto>> GetByEventAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _events.GetDetailedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _guard.EnsureCanView(ev);
        return (await _groups.GetByEventAsync(eventId, ct)).Select(ToDto).ToList();
    }

    public async Task<AgeGroupDto> CreateAsync(int eventId, UpsertAgeGroupRequest request, CancellationToken ct = default)
    {
        var ev = await _events.GetTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        await _guard.EnsureCanEditAsync(ev, lockedWhenResultsPublished: true, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(eventId), ct);

        var entity = new AgeGroup { EventId = eventId };
        Apply(entity, request);
        await ValidateAsync(entity, await _groups.GetByEventAsync(eventId, ct), ct);

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
        var ev = await _events.GetTrackedAsync(entity.EventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        await _guard.EnsureCanEditAsync(ev, lockedWhenResultsPublished: true, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(entity.EventId), ct);

        var old = Snapshot(entity);
        Apply(entity, request);
        var others = (await _groups.GetByEventAsync(entity.EventId, ct)).Where(g => g.AgeGroupId != ageGroupId).ToList();
        await ValidateAsync(entity, others, ct);

        await _audit.LogAsync("AgeGroup.Update", nameof(AgeGroup), ageGroupId.ToString(), old, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int ageGroupId, CancellationToken ct = default)
    {
        var entity = await _groups.GetTrackedAsync(ageGroupId, ct) ?? throw new NotFoundException("Không tìm thấy nhóm tuổi.");
        var ev = await _events.GetTrackedAsync(entity.EventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        await _guard.EnsureCanEditAsync(ev, lockedWhenResultsPublished: true, ct);
        if (await _groups.IsInUseAsync(ageGroupId, ct))
            throw new BusinessRuleException("BR-46", "Nhóm tuổi đã được dùng trong kết quả hoặc cơ cấu giải thưởng – không xóa được.");

        await _audit.LogAsync("AgeGroup.Delete", nameof(AgeGroup), ageGroupId.ToString(), Snapshot(entity), null, ct);
        _groups.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task ValidateAsync(AgeGroup group, IReadOnlyList<AgeGroup> others, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            throw new BusinessRuleException(ErrorCodes.Validation, "Vui lòng nhập tên nhóm tuổi.", 400);
        if (group.MaxAge is int maxAge && maxAge < group.MinAge)
            throw new BusinessRuleException(ErrorCodes.Validation, "Tuổi tối đa phải lớn hơn hoặc bằng tuổi tối thiểu.", 400);
        if (group.CategoryId is int categoryId &&
            !(await _categories.GetByEventAsync(group.EventId, ct)).Any(c => c.CategoryId == categoryId))
            throw new BusinessRuleException(ErrorCodes.Validation, "Cự ly không thuộc giải này.", 400);

        // Cùng phạm vi (cùng cự ly, hoặc cùng "mọi cự ly"): không trùng tên, không chồng lấn khoảng tuổi
        var sameScope = others.Where(o => o.CategoryId == group.CategoryId).ToList();
        if (sameScope.Any(o => string.Equals(o.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException(ErrorCodes.Validation, "Tên nhóm tuổi đã tồn tại.", 400);
        var overlap = sameScope.FirstOrDefault(o =>
            o.MinAge <= (group.MaxAge ?? NoUpperAge) && group.MinAge <= (o.MaxAge ?? NoUpperAge));
        if (overlap is not null)
            throw new BusinessRuleException("BR-46", $"Khoảng tuổi bị chồng lấn với nhóm \"{overlap.Name}\".", 400);
    }

    private static void Apply(AgeGroup entity, UpsertAgeGroupRequest request)
    {
        entity.CategoryId = request.CategoryId;
        entity.Name = request.Name.Trim();
        entity.MinAge = request.MinAge;
        entity.MaxAge = request.MaxAge;
    }

    private static object Snapshot(AgeGroup g) => new { g.EventId, g.CategoryId, g.Name, g.MinAge, g.MaxAge };

    private static AgeGroupDto ToDto(AgeGroup g) => new()
    {
        Id = g.AgeGroupId, EventId = g.EventId, CategoryId = g.CategoryId, Name = g.Name, MinAge = g.MinAge, MaxAge = g.MaxAge
    };
}

/// <summary>UC-24 – checkpoint, cut-off tại trạm (BR-39), khóa khi kết quả Provisional (BR-46c).</summary>
public class CheckpointService : ICheckpointService
{
    private readonly ICheckpointRepository _checkpoints;
    private readonly IRaceCategoryRepository _categories;
    private readonly EventSetupGuard _guard;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public CheckpointService(ICheckpointRepository checkpoints, IRaceCategoryRepository categories, EventSetupGuard guard,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _checkpoints = checkpoints;
        _categories = categories;
        _guard = guard;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<CheckpointDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        _guard.EnsureCanView(category.Event);
        return (await _checkpoints.GetByCategoryAsync(categoryId, ct)).Select(ToDto).ToList();
    }

    public async Task<CheckpointDto> CreateAsync(int categoryId, UpsertCheckpointRequest request, CancellationToken ct = default)
    {
        var category = await LoadCategoryAsync(categoryId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(category.EventId), ct);

        var entity = new Checkpoint { CategoryId = categoryId };
        Apply(entity, request);
        Validate(category, entity, await _checkpoints.GetByCategoryAsync(categoryId, ct));

        await _checkpoints.AddAsync(entity, ct);
        await SaveAsync(ct);
        await _audit.LogAsync("Checkpoint.Create", nameof(Checkpoint), entity.CheckpointId.ToString(), null, Snapshot(entity), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<CheckpointDto> UpdateAsync(int checkpointId, UpsertCheckpointRequest request, CancellationToken ct = default)
    {
        var entity = await _checkpoints.GetTrackedAsync(checkpointId, ct) ?? throw new NotFoundException("Không tìm thấy checkpoint.");
        var category = await LoadCategoryAsync(entity.CategoryId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.EventSetup(category.EventId), ct);

        var oldType = entity.Type;
        var old = Snapshot(entity);
        Apply(entity, request);
        if (oldType != entity.Type && await _checkpoints.IsInUseAsync(checkpointId, ct))
            throw new BusinessRuleException("BR-46", "Checkpoint đã có thời gian bấm – không đổi loại được.");
        var others = (await _checkpoints.GetByCategoryAsync(entity.CategoryId, ct)).Where(c => c.CheckpointId != checkpointId).ToList();
        Validate(category, entity, others);

        await _audit.LogAsync("Checkpoint.Update", nameof(Checkpoint), checkpointId.ToString(), old, Snapshot(entity), ct);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int checkpointId, CancellationToken ct = default)
    {
        var entity = await _checkpoints.GetTrackedAsync(checkpointId, ct) ?? throw new NotFoundException("Không tìm thấy checkpoint.");
        var category = await LoadCategoryAsync(entity.CategoryId, ct);
        if (await _checkpoints.IsInUseAsync(checkpointId, ct))
            throw new BusinessRuleException("BR-46", "Checkpoint đã có thời gian bấm hoặc VĐV bỏ cuộc tại đây – không xóa được.");
        if (entity.Type != CheckpointType.Split && category.IsTimed &&
            category.Event.Status is EventStatus.RegistrationOpen or EventStatus.RegistrationClosed or EventStatus.Postponed)
            throw new BusinessRuleException("BR-38", "Giải đã mở đăng ký – cự ly bấm giờ phải luôn có checkpoint Start và Finish.");

        await _audit.LogAsync("Checkpoint.Delete", nameof(Checkpoint), checkpointId.ToString(), Snapshot(entity), null, ct);
        _checkpoints.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<RaceCategory> LoadCategoryAsync(int categoryId, CancellationToken ct)
    {
        var category = await _categories.GetTrackedAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        await _guard.EnsureCanEditAsync(category.Event, lockedWhenResultsPublished: true, ct);
        return category;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw DbErrors.ToBusinessError(ex, "Thứ tự checkpoint đã tồn tại trong cự ly."); }
    }

    private static void Validate(RaceCategory category, Checkpoint cp, IReadOnlyList<Checkpoint> others)
    {
        if (string.IsNullOrWhiteSpace(cp.Name))
            throw new BusinessRuleException(ErrorCodes.Validation, "Vui lòng nhập tên checkpoint.", 400);
        if (others.Any(o => o.Sequence == cp.Sequence))
            throw new BusinessRuleException(ErrorCodes.Validation, $"Thứ tự {cp.Sequence} đã có checkpoint khác.", 400);
        if ((cp.Latitude is null) != (cp.Longitude is null))
            throw new BusinessRuleException(ErrorCodes.Validation, "Nhập đủ cả vĩ độ và kinh độ, hoặc bỏ trống cả hai.", 400);

        var courseKm = Math.Max(category.DistanceKm, category.ActualDistanceKm ?? 0);
        if (cp.DistanceKm > courseKm)
            throw new BusinessRuleException(ErrorCodes.Validation, $"Km của checkpoint vượt quá chiều dài cự ly ({courseKm:0.##} km).", 400);

        // Start đứng đầu ở km 0, Finish đứng cuối; mỗi loại tối đa một
        if (cp.Type == CheckpointType.Start)
        {
            if (others.Any(o => o.Type == CheckpointType.Start))
                throw new BusinessRuleException(ErrorCodes.Validation, "Cự ly đã có checkpoint Start.", 400);
            if (cp.DistanceKm != 0) throw new BusinessRuleException(ErrorCodes.Validation, "Checkpoint Start phải ở km 0.", 400);
            if (others.Any(o => o.Sequence < cp.Sequence))
                throw new BusinessRuleException(ErrorCodes.Validation, "Checkpoint Start phải có thứ tự nhỏ nhất.", 400);
        }
        if (cp.Type == CheckpointType.Finish)
        {
            if (others.Any(o => o.Type == CheckpointType.Finish))
                throw new BusinessRuleException(ErrorCodes.Validation, "Cự ly đã có checkpoint Finish.", 400);
            if (others.Any(o => o.Sequence > cp.Sequence))
                throw new BusinessRuleException(ErrorCodes.Validation, "Checkpoint Finish phải có thứ tự lớn nhất.", 400);
        }
        if (others.Any(o => (o.Type == CheckpointType.Start && o.Sequence > cp.Sequence) ||
                            (o.Type == CheckpointType.Finish && o.Sequence < cp.Sequence)))
            throw new BusinessRuleException(ErrorCodes.Validation, "Checkpoint phải nằm giữa Start và Finish.", 400);

        // Km tăng dần theo thứ tự
        if (others.Any(o => (o.Sequence < cp.Sequence && o.DistanceKm >= cp.DistanceKm) ||
                            (o.Sequence > cp.Sequence && o.DistanceKm <= cp.DistanceKm)))
            throw new BusinessRuleException(ErrorCodes.Validation, "Km của các checkpoint phải tăng dần theo thứ tự.", 400);

        // BR-39: cut-off tại trạm sau giờ xuất phát, không quá COT cự ly, tăng dần theo thứ tự
        if (cp.CutOffAt is DateTime cutOff)
        {
            if (cutOff <= category.StartAt)
                throw new BusinessRuleException("BR-39", "Giờ cut-off tại trạm phải sau giờ xuất phát của cự ly.", 400);
            if (category.CutOffMinutes is int cot && cutOff > category.StartAt.AddMinutes(cot))
                throw new BusinessRuleException("BR-39",
                    $"Giờ cut-off tại trạm không được sau giờ đóng đường ({category.StartAt.AddMinutes(cot):dd/MM HH:mm}).", 400);
            if (others.Any(o => o.CutOffAt is DateTime other &&
                                ((o.Sequence < cp.Sequence && other >= cutOff) || (o.Sequence > cp.Sequence && other <= cutOff))))
                throw new BusinessRuleException("BR-39", "Giờ cut-off của các checkpoint phải tăng dần theo thứ tự.", 400);
        }
    }

    private static void Apply(Checkpoint entity, UpsertCheckpointRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.DistanceKm = request.DistanceKm;
        entity.Sequence = request.Sequence;
        entity.Type = EnumParser.ParseOptional<CheckpointType>(request.Type) ?? CheckpointType.Split;
        entity.IsMandatory = entity.Type != CheckpointType.Split || request.IsMandatory;
        entity.CutOffAt = request.CutOffAt;
        entity.StationType = EnumParser.ParseOptional<StationType>(request.StationType) ?? StationType.None;
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
    }

    private static object Snapshot(Checkpoint c) => new
    {
        c.CategoryId, c.Name, c.DistanceKm, c.Sequence, Type = c.Type.ToString(), c.IsMandatory, c.CutOffAt,
        StationType = c.StationType.ToString(), c.Latitude, c.Longitude
    };

    private static CheckpointDto ToDto(Checkpoint p) => new()
    {
        Id = p.CheckpointId, CategoryId = p.CategoryId, Name = p.Name, DistanceKm = p.DistanceKm, Sequence = p.Sequence,
        Type = p.Type.ToString(), IsMandatory = p.IsMandatory, CutOffAt = p.CutOffAt, StationType = p.StationType.ToString(),
        Latitude = p.Latitude, Longitude = p.Longitude
    };
}
