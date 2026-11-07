using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>UC-59 – tính kết quả của giải; Organizer rà soát kết quả trước khi công bố.</summary>
public class ResultService : IResultService
{
    private readonly IResultRepository _results;
    private readonly IResultCalculator _calculator;
    private readonly IOrganizationScope _scope;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public ResultService(IResultRepository results, IResultCalculator calculator, IOrganizationScope scope,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _results = results;
        _calculator = calculator;
        _scope = scope;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<ResultCalculationSummaryDto> CalculateAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventAsync(eventId, ct);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        EnsureCanCalculate(ev);

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var summary = await RecalculateAsync(ev, ct);
        await _audit.LogAsync("Result.Calculate", nameof(Event), eventId.ToString(), null,
            new { summary.Finished, summary.Dnf, summary.Dns, summary.Dsq }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return summary;
    }

    public async Task<ResultsOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventAsync(eventId, ct);
        var counts = await _results.CountByStatusAsync(eventId, ct);
        return new ResultsOverviewDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            EventDate = ev.EventDate,
            EventStatus = ev.Status.ToString(),
            ResultsStatus = ev.ResultsStatus.ToString(),
            ProvisionalAt = ev.ResultsProvisionalAt,
            OfficialAt = ev.ResultsOfficialAt,
            Finished = counts.GetValueOrDefault(ResultStatus.Finished),
            Dnf = counts.GetValueOrDefault(ResultStatus.DNF),
            Dns = counts.GetValueOrDefault(ResultStatus.DNS),
            Dsq = counts.GetValueOrDefault(ResultStatus.DSQ),
            LastUpdatedAt = await _results.GetLastUpdatedAtAsync(eventId, ct),
            CanCalculate = CanCalculate(ev),
            Categories = ev.Categories.OrderBy(c => c.StartAt).ThenBy(c => c.Name)
                .Select(c => new ResultCategoryOptionDto { Id = c.CategoryId, Name = c.Name, IsTimed = c.IsTimed, IsRanked = c.IsRanked })
                .ToList()
        };
    }

    public async Task<PagedResult<LeaderboardRowDto>> GetForOrganizerAsync(int eventId, int? categoryId, string? status,
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        _ = await LoadEventAsync(eventId, ct);
        var query = _results.QueryByEvent(eventId);
        if (categoryId is int id) query = query.Where(r => r.Registration.CategoryId == id);
        if (Enum.TryParse<ResultStatus>(status, ignoreCase: true, out var parsed)) query = query.Where(r => r.Status == parsed);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = int.TryParse(k, out var bib)
                ? query.Where(r => r.Registration.BibNumber == bib)
                : query.Where(r => r.Registration.FullName.Contains(k));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PagingExtensions.MaxPageSize);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.Registration.Category.StartAt).ThenBy(r => r.Registration.CategoryId)
            .ThenBy(r => r.Status == ResultStatus.Finished ? 0 : r.Status == ResultStatus.DNF ? 1 : r.Status == ResultStatus.DSQ ? 2 : 3)
            .ThenBy(r => r.OverallRank ?? int.MaxValue)
            .ThenBy(r => r.Registration.BibNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new LeaderboardRowDto
            {
                ResultId = r.ResultId,
                RegistrationId = r.RegistrationId,
                EventId = r.Registration.EventId,
                CategoryId = r.Registration.CategoryId,
                CategoryName = r.Registration.Category.Name,
                BibNumber = r.Registration.BibNumber,
                DisplayName = r.Registration.FullName,
                Gender = r.Registration.Gender.ToString(),
                AgeGroupId = r.AgeGroupId,
                AgeGroupName = r.AgeGroup != null ? r.AgeGroup.Name : null,
                ClubName = r.Registration.ClubName,
                GunTimeMs = r.GunTimeMs,
                ChipTimeMs = r.ChipTimeMs,
                FinalTimeMs = r.FinalTimeMs,
                PenaltySeconds = r.PenaltySeconds,
                Status = r.Status.ToString(),
                OverallRank = r.OverallRank,
                GenderRank = r.GenderRank,
                AgeGroupRank = r.AgeGroupRank,
                IsProvisional = r.Registration.Event.ResultsStatus == ResultsStatus.Provisional
            })
            .ToListAsync(ct);
        return new PagedResult<LeaderboardRowDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<ResultsStatusDto> PublishProvisionalAsync(int eventId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<ResultsStatusDto> FinalizeAsync(int eventId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public IQueryable<LeaderboardRowDto> QueryLeaderboard() => throw new NotImplementedException();

    public Task<ResultDetailDto> GetDetailAsync(int registrationId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<PenaltyReasonDto>> GetPenaltyReasonsAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PenaltyDto> IssuePenaltyAsync(int resultId, IssuePenaltyRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task RevokePenaltyAsync(int penaltyId, CancellationToken ct = default) => throw new NotImplementedException();

    // ============================ Tính kết quả ============================

    /// <summary>Tính lại mọi cự ly bấm giờ của giải và cập nhật bảng Results (chưa SaveChanges).</summary>
    private async Task<ResultCalculationSummaryDto> RecalculateAsync(Event ev, CancellationToken ct)
    {
        var registrations = await _results.GetConfirmedWithTimingAsync(ev.EventId, ct);
        var existing = (await _results.GetTrackedByEventAsync(ev.EventId, ct)).ToDictionary(r => r.RegistrationId);
        var now = _clock.Now;
        var warnings = new List<string>();
        int finished = 0, dnf = 0, dns = 0, dsq = 0;

        var overallBasis = ev.Policy.OverallRankingBasis;
        var policy = new CalcPolicy(
            overallBasis ?? RankingBasis.GunTime,
            ev.Policy.AgeGroupRankingBasis ?? overallBasis ?? RankingBasis.GunTime,
            ev.Policy.TieBreakRule,
            ev.Policy.LateStartToleranceMinutes);
        if (overallBasis is null)
            warnings.Add("Giải chưa chọn cơ sở xếp hạng – tạm xếp hạng theo giờ súng.");
        if (ev.AgeGroups.Count == 0)
            warnings.Add("Giải chưa có nhóm tuổi – bỏ qua xếp hạng nhóm tuổi.");

        foreach (var category in ev.Categories.OrderBy(c => c.StartAt).ThenBy(c => c.Name))
        {
            if (!category.IsTimed)
            {
                warnings.Add($"Cự ly {category.Name} không bấm giờ – không tính kết quả (BR-26).");
                continue;
            }
            if (!category.Checkpoints.Any(c => c.Type == CheckpointType.Finish))
            {
                warnings.Add($"Cự ly {category.Name} chưa có checkpoint về đích – chưa tính được kết quả.");
                continue;
            }

            var runners = registrations.Where(r => r.CategoryId == category.CategoryId)
                .Select(r => ToRunner(r, ev, category, existing.GetValueOrDefault(r.RegistrationId)))
                .ToList();
            if (runners.Count == 0) continue;

            var outcomes = _calculator.Calculate(ToCalcCategory(category), policy, runners);
            foreach (var outcome in outcomes)
            {
                if (!existing.TryGetValue(outcome.RegistrationId, out var result))
                {
                    result = new Result { RegistrationId = outcome.RegistrationId };
                    await _results.AddAsync(result, ct);
                    existing[outcome.RegistrationId] = result;
                }
                Apply(result, outcome, now);
                switch (outcome.Status)
                {
                    case ResultStatus.Finished: finished++; break;
                    case ResultStatus.DNF: dnf++; break;
                    case ResultStatus.DNS: dns++; break;
                    case ResultStatus.DSQ: dsq++; break;
                }
            }

            var missingMat = outcomes.Count(o => o.MissingStartMat);
            if (missingMat > 0)
                warnings.Add($"Cự ly {category.Name}: {missingMat} VĐV không có thời gian tại thảm xuất phát – giờ chip lấy bằng giờ súng.");
            var lateStart = outcomes.Count(o => o.LateStart);
            if (lateStart > 0)
                warnings.Add($"Cự ly {category.Name}: {lateStart} VĐV qua thảm xuất phát muộn quá {ev.Policy.LateStartToleranceMinutes} phút – ghi nhận DNS (BR-24).");
        }

        return new ResultCalculationSummaryDto
        {
            EventId = ev.EventId,
            Finished = finished,
            Dnf = dnf,
            Dns = dns,
            Dsq = dsq,
            Warnings = warnings
        };
    }

    private static CalcRunner ToRunner(Registration r, Event ev, RaceCategory category, Result? existing)
    {
        var active = existing?.Penalties.Where(p => p.RevokedAt == null).ToList() ?? new List<ResultPenalty>();
        CalcWithdrawal? withdrawal = existing is { Status: ResultStatus.DNF, DnfReason: DnfReason.Voluntary or DnfReason.Medical }
            ? new CalcWithdrawal(existing.DnfReason!.Value, existing.DnfCheckpointId)
            : null;
        var age = RaceDayRules.AgeForEvent(r.DateOfBirth, ev.EventDate, ev.Policy.AgeCalculationMode);

        return new CalcRunner(
            r.RegistrationId,
            r.Gender,
            PickAgeGroup(ev.AgeGroups, category.CategoryId, age)?.AgeGroupId,
            r.TimingRecords.GroupBy(t => t.CheckpointId).ToDictionary(g => g.Key, g => g.First().RecordedAt),
            withdrawal,
            active.Any(p => p.Action == PenaltyAction.DSQ),
            active.Where(p => p.Action == PenaltyAction.TimePenalty).Sum(p => (p.Minutes ?? 0) * 60));
    }

    /// <summary>Nhóm tuổi của VĐV: ưu tiên nhóm riêng của cự ly, rồi nhóm có tuổi tối thiểu cao nhất.</summary>
    private static AgeGroup? PickAgeGroup(IEnumerable<AgeGroup> groups, int categoryId, int age) =>
        groups.Where(g => (g.CategoryId == null || g.CategoryId == categoryId) && age >= g.MinAge && (g.MaxAge == null || age <= g.MaxAge))
            .OrderByDescending(g => g.CategoryId != null)
            .ThenByDescending(g => g.MinAge)
            .FirstOrDefault();

    private static CalcCategory ToCalcCategory(RaceCategory c) => new(
        c.CategoryId, c.Name, c.StartAt, c.CutOffMinutes, c.IsRanked,
        c.Checkpoints.Select(p => new CalcCheckpoint(p.CheckpointId, p.Name, p.Sequence, p.Type, p.IsMandatory, p.CutOffAt)).ToList());

    private static void Apply(Result result, CalcOutcome o, DateTime now)
    {
        result.Status = o.Status;
        result.DnfReason = o.DnfReason;
        result.DnfCheckpointId = o.DnfCheckpointId;
        result.GunTimeMs = o.GunTimeMs;
        result.ChipTimeMs = o.ChipTimeMs;
        result.PenaltySeconds = o.PenaltySeconds;
        result.FinalTimeMs = o.FinalTimeMs;
        result.OverallRank = o.OverallRank;
        result.GenderRank = o.GenderRank;
        result.AgeGroupId = o.AgeGroupId;
        result.AgeGroupRank = o.AgeGroupRank;
        result.UpdatedAt = now;
    }

    // ============================ Kiểm tra ============================

    private async Task<Event> LoadEventAsync(int eventId, CancellationToken ct)
    {
        var ev = await _results.GetEventForResultsAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        return ev;
    }

    private bool CanCalculate(Event ev) =>
        ev.ResultsStatus != ResultsStatus.Official &&
        ev.Status is EventStatus.RegistrationClosed or EventStatus.Completed &&
        _clock.Today >= ev.EventDate;

    private void EnsureCanCalculate(Event ev)
    {
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không tính lại.");
        if (ev.Status is not (EventStatus.RegistrationClosed or EventStatus.Completed))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Chỉ tính kết quả khi giải đã đóng đăng ký hoặc đã kết thúc.");
        if (_clock.Today < ev.EventDate)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Chưa tới ngày thi – chưa thể tính kết quả.");
    }
}
