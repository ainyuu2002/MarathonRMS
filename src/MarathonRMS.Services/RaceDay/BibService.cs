using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>UC-49 – gán BIB hàng loạt theo dải số của từng cự ly (BR-19).</summary>
public class BibService : IBibService
{
    private readonly IBibRepository _bibs;
    private readonly IBibAllocator _allocator;
    private readonly IOrganizationScope _scope;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public BibService(IBibRepository bibs, IBibAllocator allocator, IOrganizationScope scope, IAuditLogger audit, IUnitOfWork uow)
    {
        _bibs = bibs;
        _allocator = allocator;
        _scope = scope;
        _audit = audit;
        _uow = uow;
    }

    public async Task<BibOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventAsync(eventId, ct);
        var counts = (await _bibs.GetCategoryCountsAsync(eventId, ct)).ToDictionary(c => c.CategoryId);
        var used = await _bibs.GetUsedBibNumbersAsync(eventId, ct);

        return new BibOverviewDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            EventStatus = ev.Status.ToString(),
            CanAssign = CanAssign(ev),
            Categories = ev.Categories.OrderBy(c => c.BibFrom).Select(c =>
            {
                var count = counts.GetValueOrDefault(c.CategoryId);
                var confirmed = count?.Confirmed ?? 0;
                var assigned = count?.Assigned ?? 0;
                return new BibCategoryStatusDto
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.Name,
                    BibFrom = c.BibFrom,
                    BibTo = c.BibTo,
                    Confirmed = confirmed,
                    Assigned = assigned,
                    Unassigned = confirmed - assigned,
                    FreeNumbers = c.BibTo - c.BibFrom + 1 - used.Count(n => n >= c.BibFrom && n <= c.BibTo)
                };
            }).ToList()
        };
    }

    public async Task<PagedResult<BibListItemDto>> GetAssignmentsAsync(int eventId, int? categoryId, string? keyword, int page,
        int pageSize, CancellationToken ct = default)
    {
        _ = await LoadEventAsync(eventId, ct);
        var query = _bibs.QueryAssigned(eventId);
        if (categoryId is int id) query = query.Where(r => r.CategoryId == id);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = int.TryParse(k, out var bib)
                ? query.Where(r => r.BibNumber == bib)
                : query.Where(r => r.FullName.Contains(k));
        }

        return await query.OrderBy(r => r.BibNumber)
            .Select(r => new BibListItemDto
            {
                RegistrationId = r.RegistrationId,
                BibNumber = r.BibNumber!.Value,
                FullName = r.FullName,
                BibName = r.BibName,
                CategoryId = r.CategoryId,
                CategoryName = r.Category.Name,
                Gender = r.Gender.ToString()
            })
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<BibAssignmentResultDto> AssignAsync(int eventId, AssignBibsRequest request, CancellationToken ct = default)
    {
        var ev = await LoadEventAsync(eventId, ct);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (!CanAssign(ev))
            throw new BusinessRuleException("BR-19",
                "Chỉ gán BIB khi giải đang mở hoặc đã đóng đăng ký và kết quả chưa công bố.");

        var categories = ev.Categories.OrderBy(c => c.BibFrom).ToList();
        if (request.CategoryId is int categoryId)
        {
            categories = categories.Where(c => c.CategoryId == categoryId).ToList();
            if (categories.Count == 0) throw new NotFoundException("Không tìm thấy cự ly trong giải.");
        }

        var counts = (await _bibs.GetCategoryCountsAsync(eventId, ct)).ToDictionary(c => c.CategoryId);
        var used = (await _bibs.GetUsedBibNumbersAsync(eventId, ct)).ToHashSet();
        var warnings = new List<string>();
        var assigned = 0;
        var alreadyAssigned = 0;

        await using var tx = await _uow.BeginTransactionAsync(ct);
        foreach (var category in categories)
        {
            alreadyAssigned += counts.GetValueOrDefault(category.CategoryId)?.Assigned ?? 0;
            var pending = await _bibs.GetUnassignedConfirmedAsync(category.CategoryId, ct);
            if (pending.Count == 0) continue;

            var numbers = _allocator.Allocate(category.BibFrom, category.BibTo, used, pending.Count);
            for (var i = 0; i < numbers.Count; i++)
            {
                pending[i].BibNumber = numbers[i];
                used.Add(numbers[i]);
            }
            assigned += numbers.Count;

            if (numbers.Count < pending.Count)
                warnings.Add($"Cự ly {category.Name}: hết số trong dải BIB {category.BibFrom}–{category.BibTo}, " +
                             $"còn {pending.Count - numbers.Count} đăng ký chưa gán.");
        }

        if (assigned > 0)
        {
            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new BusinessRuleException(ErrorCodes.Conflict,
                    "Số BIB vừa được gán bởi một thao tác khác. Vui lòng tải lại và thử lại.");
            }
            await _audit.LogAsync("Bib.Assign", nameof(Event), eventId.ToString(), null,
                new { request.CategoryId, Assigned = assigned }, ct);
            await _uow.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        return new BibAssignmentResultDto
        {
            EventId = eventId,
            AssignedCount = assigned,
            AlreadyAssignedCount = alreadyAssigned,
            Warnings = warnings
        };
    }

    private async Task<Event> LoadEventAsync(int eventId, CancellationToken ct)
    {
        var ev = await _bibs.GetEventWithCategoriesAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        return ev;
    }

    private static bool CanAssign(Event ev) =>
        ev.Status is EventStatus.RegistrationOpen or EventStatus.RegistrationClosed or EventStatus.Postponed
        && ev.ResultsStatus == ResultsStatus.Draft;
}
