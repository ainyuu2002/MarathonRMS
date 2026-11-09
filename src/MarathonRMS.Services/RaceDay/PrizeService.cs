using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// UC-28 – cơ cấu giải thưởng theo cự ly (Organizer, BR-30); UC-65 – danh sách nhận giải / Vinh danh (BR-29, BR-37).
/// Danh sách nhận giải không lưu riêng: tính từ kết quả mỗi lần xem nên kết quả sửa thì danh sách tự cập nhật.
/// </summary>
public interface IPrizeService
{
    Task<EventPrizesDto> GetForEventAsync(int eventId, CancellationToken ct = default);
    Task<PrizeDto> CreateAsync(int categoryId, SavePrizeRequest request, CancellationToken ct = default);
    Task<PrizeDto> UpdateAsync(int prizeId, SavePrizeRequest request, CancellationToken ct = default);
    Task DeleteAsync(int prizeId, CancellationToken ct = default);

    /// <summary>Công khai khi kết quả chính thức; Organizer/Staff của BTC (và Admin) xem trước khi kết quả tạm thời / nháp.</summary>
    Task<EventWinnersDto> GetWinnersAsync(int eventId, CancellationToken ct = default);
}

public class PrizeService : IPrizeService
{
    private readonly IPrizeRepository _prizes;
    private readonly IPrizeAllocator _allocator;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivacyFilter _privacy;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public PrizeService(IPrizeRepository prizes, IPrizeAllocator allocator, IOrganizationScope scope,
        ICurrentUserService currentUser, IPrivacyFilter privacy, IAuditLogger audit, IUnitOfWork uow)
    {
        _prizes = prizes;
        _allocator = allocator;
        _scope = scope;
        _currentUser = currentUser;
        _privacy = privacy;
        _audit = audit;
        _uow = uow;
    }

    public async Task<EventPrizesDto> GetForEventAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _prizes.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        var prizes = await _prizes.GetByEventAsync(eventId, ct);

        return new EventPrizesDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            ResultsStatus = ev.ResultsStatus.ToString(),
            AwardStackingPolicy = ev.Policy.AwardStackingPolicy.ToString(),
            CanEdit = ev.ResultsStatus == ResultsStatus.Draft && ev.Status != EventStatus.Cancelled,
            Categories = ev.Categories.OrderBy(c => c.StartAt).ThenBy(c => c.Name)
                .Select(c => new PrizeCategoryOptionDto { Id = c.CategoryId, Name = c.Name, IsRanked = c.IsTimed && c.IsRanked })
                .ToList(),
            AgeGroups = ev.AgeGroups.OrderBy(g => g.CategoryId != null).ThenBy(g => g.MinAge)
                .Select(g => new AgeGroupOptionDto { Id = g.AgeGroupId, Name = g.Name, CategoryId = g.CategoryId })
                .ToList(),
            Prizes = prizes.Select(ToDto).ToList()
        };
    }

    public async Task<PrizeDto> CreateAsync(int categoryId, SavePrizeRequest request, CancellationToken ct = default)
    {
        var category = await _prizes.GetCategoryAsync(categoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly.");
        var ev = await LoadEventForWriteAsync(category.EventId, ct);
        var prize = new Prize { CategoryId = categoryId };
        await ApplyAsync(prize, ev, ev.Categories.First(c => c.CategoryId == categoryId), request, null, ct);

        await _prizes.AddAsync(prize, ct);
        await SaveAsync(ct);
        await _audit.LogAsync("Prize.Create", nameof(Prize), prize.PrizeId.ToString(), null, Snapshot(prize), ct);
        await _uow.SaveChangesAsync(ct);
        return await ReloadDtoAsync(ev.EventId, prize.PrizeId, ct);
    }

    public async Task<PrizeDto> UpdateAsync(int prizeId, SavePrizeRequest request, CancellationToken ct = default)
    {
        var prize = await _prizes.GetTrackedAsync(prizeId, ct) ?? throw new NotFoundException("Không tìm thấy giải thưởng.");
        var ev = await LoadEventForWriteAsync(prize.Category.EventId, ct);
        var old = Snapshot(prize);
        await ApplyAsync(prize, ev, ev.Categories.First(c => c.CategoryId == prize.CategoryId), request, prizeId, ct);

        await SaveAsync(ct);
        await _audit.LogAsync("Prize.Update", nameof(Prize), prizeId.ToString(), old, Snapshot(prize), ct);
        await _uow.SaveChangesAsync(ct);
        return await ReloadDtoAsync(ev.EventId, prizeId, ct);
    }

    public async Task DeleteAsync(int prizeId, CancellationToken ct = default)
    {
        var prize = await _prizes.GetTrackedAsync(prizeId, ct) ?? throw new NotFoundException("Không tìm thấy giải thưởng.");
        _ = await LoadEventForWriteAsync(prize.Category.EventId, ct);
        await _audit.LogAsync("Prize.Delete", nameof(Prize), prizeId.ToString(), Snapshot(prize), null, ct);
        _prizes.Remove(prize);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<EventWinnersDto> GetWinnersAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _prizes.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        var privileged = IsOperatorOf(ev.OrganizationId);
        var official = ev.ResultsStatus == ResultsStatus.Official;
        if (!official && !privileged)
            throw new NotFoundException("Giải chưa có danh sách vinh danh – kết quả chưa chính thức.");

        var prizes = await _prizes.GetByEventAsync(eventId, ct);
        var finishers = await _prizes.GetFinishersAsync(eventId, ct);
        var stacking = ev.Policy.AwardStackingPolicy;
        var categories = new List<CategoryWinnersDto>();

        foreach (var category in ev.Categories.Where(c => c.IsTimed && c.IsRanked).OrderBy(c => c.StartAt).ThenBy(c => c.Name))
        {
            var categoryPrizes = prizes.Where(p => p.CategoryId == category.CategoryId).ToList();
            if (categoryPrizes.Count == 0) continue;
            var runners = finishers.Where(r => r.Registration.CategoryId == category.CategoryId).ToDictionary(r => r.RegistrationId);

            var allocation = _allocator.Allocate(
                categoryPrizes.Select(p => new AllocPrize(p.PrizeId, p.AwardType, p.Gender, p.AgeGroupId, p.Nationality, p.Rank)).ToList(),
                runners.Values.Select(r => new AllocRunner(r.RegistrationId, r.Registration.Gender, r.AgeGroupId,
                    r.Registration.Nationality, r.GenderRank, r.AgeGroupRank, r.Registration.BibNumber)).ToList(),
                stacking);

            categories.Add(new CategoryWinnersDto
            {
                CategoryId = category.CategoryId,
                CategoryName = category.Name,
                Awards = categoryPrizes.Select(p => new AwardDto
                {
                    Prize = ToDto(p),
                    Winner = allocation.GetValueOrDefault(p.PrizeId) is int id && runners.TryGetValue(id, out var r)
                        ? ToWinner(r, ev.EventDate, privileged)
                        : null
                }).ToList()
            });
        }

        return new EventWinnersDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            OrganizationName = ev.Organization.Name,
            EventDate = ev.EventDate,
            ResultsStatus = ev.ResultsStatus.ToString(),
            IsOfficial = official,
            AwardStackingPolicy = stacking.ToString(),
            Categories = categories
        };
    }

    // ============================ Kiểm tra & ánh xạ ============================

    private async Task<Event> LoadEventForWriteAsync(int eventId, CancellationToken ct)
    {
        var ev = await _prizes.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        // BR-46c: từ khi kết quả tạm thời, cơ cấu giải thưởng bị khóa (VĐV đã thấy hạng, không đổi luật sau đó)
        if (ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-46", "Kết quả đã công bố – cơ cấu giải thưởng bị khóa.");
        if (ev.Status == EventStatus.Cancelled)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Giải đã hủy.");
        return ev;
    }

    private async Task ApplyAsync(Prize prize, Event ev, RaceCategory category, SavePrizeRequest request, int? exceptId,
        CancellationToken ct)
    {
        if (!category.IsTimed || !category.IsRanked)
            throw new BusinessRuleException("BR-26", $"Cự ly {category.Name} không xếp hạng – không đặt giải thưởng theo hạng.", 400);

        var type = Enum.Parse<AwardType>(request.AwardType, ignoreCase: true);
        var gender = Enum.Parse<Gender>(request.Gender, ignoreCase: true);
        int? ageGroupId = null;
        string? nationality = null;
        if (type == AwardType.AgeGroup)
        {
            var group = ev.AgeGroups.FirstOrDefault(g => g.AgeGroupId == request.AgeGroupId)
                        ?? throw new BusinessRuleException(ErrorCodes.Validation, "Chọn nhóm tuổi của giải.", 400);
            if (group.CategoryId is int only && only != category.CategoryId)
                throw new BusinessRuleException(ErrorCodes.Validation, $"Nhóm tuổi {group.Name} không thuộc cự ly {category.Name}.", 400);
            ageGroupId = group.AgeGroupId;
        }
        if (type == AwardType.Nationality)
        {
            nationality = string.IsNullOrWhiteSpace(request.Nationality)
                ? throw new BusinessRuleException(ErrorCodes.Validation, "Nhập quốc tịch áp dụng (VD Việt Nam).", 400)
                : request.Nationality.Trim();
        }
        if (request.CashAmount is decimal cash && (cash < 0 || cash % 1000 != 0))
            throw new BusinessRuleException("BR-48", "Tiền thưởng là bội số 1.000đ.", 400);
        if (await _prizes.ExistsAsync(category.CategoryId, type, gender, ageGroupId, request.Rank, exceptId, ct))
            throw new BusinessRuleException("BR-29", "Cự ly đã có giải thưởng cùng loại, giới tính, nhóm tuổi và hạng này.");

        prize.AwardType = type;
        prize.Gender = gender;
        prize.AgeGroupId = ageGroupId;
        prize.Nationality = nationality;
        prize.Rank = request.Rank;
        prize.Reward = request.Reward.Trim();
        prize.CashAmount = request.CashAmount;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw DbErrors.ToBusinessError(ex, "Cự ly đã có giải thưởng cùng loại, giới tính, nhóm tuổi và hạng này.", "BR-29");
        }
    }

    private async Task<PrizeDto> ReloadDtoAsync(int eventId, int prizeId, CancellationToken ct) =>
        ToDto((await _prizes.GetByEventAsync(eventId, ct)).First(p => p.PrizeId == prizeId));

    private bool IsOperatorOf(int organizationId) =>
        _currentUser.IsAdminOrAbove ||
        ((_currentUser.IsInRole(AppRoles.Organizer) || _currentUser.IsInRole(AppRoles.Staff)) &&
         _currentUser.OrganizationId == organizationId);

    private WinnerDto ToWinner(Result r, DateOnly eventDate, bool privileged)
    {
        var g = r.Registration;
        var name = privileged
            ? g.FullName
            : _privacy.PublicDisplayName(g.FullName, g.HideFromPublicList, g.DateOfBirth, eventDate, g.BibNumber);
        return new WinnerDto
        {
            RegistrationId = r.RegistrationId,
            BibNumber = g.BibNumber,
            DisplayName = name,
            ClubName = name == g.FullName ? g.ClubName : null,
            FinalTimeMs = r.FinalTimeMs
        };
    }

    private static object Snapshot(Prize p) => new
    {
        p.CategoryId,
        AwardType = p.AwardType.ToString(),
        Gender = p.Gender.ToString(),
        p.AgeGroupId,
        p.Nationality,
        p.Rank,
        p.Reward,
        p.CashAmount
    };

    private static PrizeDto ToDto(Prize p) => new()
    {
        Id = p.PrizeId,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name ?? "",
        AwardType = p.AwardType.ToString(),
        Gender = p.Gender.ToString(),
        AgeGroupId = p.AgeGroupId,
        AgeGroupName = p.AgeGroup?.Name,
        Nationality = p.Nationality,
        Rank = p.Rank,
        Reward = p.Reward,
        CashAmount = p.CashAmount
    };
}
