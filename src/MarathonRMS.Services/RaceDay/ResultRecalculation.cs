using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// Tính lại kết quả của cả giải (UC-59) – dùng chung cho tính kết quả, phạt / thu hồi phạt, ghi nhận / hủy bỏ cuộc.
/// Không kiểm tra quyền, không SaveChanges: nơi gọi kiểm tra quyền, mở transaction và giữ khóa RaceDayLocks.Results.
/// </summary>
public interface IResultRecalculation
{
    /// <summary>Tính lại mọi cự ly bấm giờ và đồng bộ bảng Results với danh sách đăng ký hiện tại.</summary>
    Task<ResultCalculationSummaryDto> RecalculateAsync(Event ev, CancellationToken ct = default);

    /// <summary>Tính lại nếu giải đã từng được tính kết quả (có dòng kết quả ngoài các ca bỏ cuộc). Chặn khi kết quả đã chính thức.</summary>
    Task RecalculateIfCalculatedAsync(int eventId, CancellationToken ct = default);
}

public class ResultRecalculation : IResultRecalculation
{
    private readonly IResultRepository _results;
    private readonly IResultCalculator _calculator;
    private readonly IDateTimeProvider _clock;

    public ResultRecalculation(IResultRepository results, IResultCalculator calculator, IDateTimeProvider clock)
    {
        _results = results;
        _calculator = calculator;
        _clock = clock;
    }

    public async Task RecalculateIfCalculatedAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await _results.GetEventForResultsAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể thay đổi.");
        if (await _results.HasCalculatedResultsAsync(eventId, ct))
            await RecalculateAsync(ev, ct);
    }

    public async Task<ResultCalculationSummaryDto> RecalculateAsync(Event ev, CancellationToken ct = default)
    {
        var registrations = await _results.GetConfirmedWithTimingAsync(ev.EventId, ct);
        var existing = (await _results.GetTrackedByEventAsync(ev.EventId, ct)).ToDictionary(r => r.RegistrationId);
        var touched = new HashSet<int>();
        var now = _clock.Now;
        var warnings = new List<string>();
        int finished = 0, dnf = 0, dns = 0, dsq = 0, onCourse = 0;

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

            var outcomes = _calculator.Calculate(ToCalcCategory(category), policy, runners, now);
            foreach (var outcome in outcomes)
            {
                if (outcome.OnCourse)
                {
                    onCourse++;
                    continue;
                }
                if (!existing.TryGetValue(outcome.RegistrationId, out var result))
                {
                    result = new Result { RegistrationId = outcome.RegistrationId };
                    await _results.AddAsync(result, ct);
                    existing[outcome.RegistrationId] = result;
                }
                touched.Add(outcome.RegistrationId);
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

        if (onCourse > 0)
            warnings.Add($"{onCourse} VĐV còn trên đường chạy (chưa về đích, chưa quá giờ giới hạn) – chưa có kết quả, hãy tính lại sau.");

        // Dòng kết quả không còn ứng với VĐV nào được tính (đăng ký đã hủy / chuyển nhượng, cự ly đổi cấu hình, VĐV còn trên
        // đường chạy): xóa; còn quyết định phạt gắn vào thì giữ dòng nhưng bỏ thời gian và thứ hạng.
        foreach (var stale in existing.Values.Where(r => !touched.Contains(r.RegistrationId)))
        {
            if (stale.Penalties.Count == 0)
            {
                _results.Remove(stale);
                continue;
            }
            stale.Status = ResultStatus.DNS;
            stale.DnfReason = null;
            stale.DnfCheckpointId = null;
            stale.GunTimeMs = stale.ChipTimeMs = stale.FinalTimeMs = null;
            stale.OverallRank = stale.GenderRank = stale.AgeGroupRank = null;
            stale.UpdatedAt = now;
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
}
