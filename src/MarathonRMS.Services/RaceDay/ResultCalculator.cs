using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.Services.RaceDay;

public sealed record CalcCheckpoint(int CheckpointId, string Name, int Sequence, CheckpointType Type, bool IsMandatory, DateTime? CutOffAt);

public sealed record CalcCategory(int CategoryId, string Name, DateTime StartAt, int? CutOffMinutes, bool IsRanked,
    IReadOnlyList<CalcCheckpoint> Checkpoints);

public sealed record CalcPolicy(RankingBasis OverallBasis, RankingBasis AgeGroupBasis, TieBreakRule TieBreak, int? LateStartToleranceMinutes);

/// <summary>VĐV đã được ghi nhận bỏ cuộc tại trạm (UC-58) – giữ nguyên DNF khi tính lại.</summary>
public sealed record CalcWithdrawal(DnfReason Reason, int? CheckpointId);

/// <summary>Đầu vào của một VĐV: thời gian theo checkpoint, quyết định phạt còn hiệu lực.</summary>
public sealed record CalcRunner(int RegistrationId, Gender Gender, int? AgeGroupId, IReadOnlyDictionary<int, DateTime> Times,
    CalcWithdrawal? Withdrawal, bool Disqualified, int PenaltySeconds);

public sealed class CalcOutcome
{
    public int RegistrationId { get; init; }
    public int? AgeGroupId { get; init; }
    public Gender Gender { get; init; }
    public ResultStatus Status { get; set; }
    public DnfReason? DnfReason { get; set; }
    public int? DnfCheckpointId { get; set; }
    public long? GunTimeMs { get; set; }
    public long? ChipTimeMs { get; set; }
    public long? FinalTimeMs { get; set; }
    public int PenaltySeconds { get; set; }
    public int? OverallRank { get; set; }
    public int? GenderRank { get; set; }
    public int? AgeGroupRank { get; set; }
    /// <summary>Không có bản ghi tại thảm xuất phát ⇒ giờ chip lấy bằng giờ súng.</summary>
    public bool MissingStartMat { get; set; }
    /// <summary>Qua thảm xuất phát muộn hơn mức cho phép ⇒ DNS.</summary>
    public bool LateStart { get; set; }
}

/// <summary>
/// Tính thành tích và xếp hạng của một cự ly – logic thuần, không truy cập DB.
/// BR-21 (thiếu checkpoint bắt buộc), BR-22 (giờ súng/chip, làm tròn lên giây khi xếp hạng), BR-23 (COT tổng theo giờ súng,
/// cut-off tại trạm), BR-24 (DNS, xuất phát muộn), BR-25 (FinalTime = thời gian theo cơ sở xếp hạng + phạt; hòa), BR-26 (IsRanked).
/// </summary>
public interface IResultCalculator
{
    IReadOnlyList<CalcOutcome> Calculate(CalcCategory category, CalcPolicy policy, IReadOnlyList<CalcRunner> runners);
}

public class ResultCalculator : IResultCalculator
{
    public IReadOnlyList<CalcOutcome> Calculate(CalcCategory category, CalcPolicy policy, IReadOnlyList<CalcRunner> runners)
    {
        var ordered = category.Checkpoints.OrderBy(c => c.Sequence).ToList();
        var start = ordered.FirstOrDefault(c => c.Type == CheckpointType.Start);
        var finish = ordered.FirstOrDefault(c => c.Type == CheckpointType.Finish)
                     ?? throw new InvalidOperationException($"Cự ly {category.Name} chưa có checkpoint về đích.");

        var outcomes = runners.Select(r => Evaluate(r, category, policy, ordered, start, finish)).ToList();
        if (category.IsRanked) Rank(outcomes, policy);
        return outcomes;
    }

    private static CalcOutcome Evaluate(CalcRunner runner, CalcCategory category, CalcPolicy policy,
        IReadOnlyList<CalcCheckpoint> ordered, CalcCheckpoint? start, CalcCheckpoint finish)
    {
        var o = new CalcOutcome
        {
            RegistrationId = runner.RegistrationId,
            AgeGroupId = runner.AgeGroupId,
            Gender = runner.Gender,
            PenaltySeconds = runner.PenaltySeconds
        };

        EvaluateNatural(o, runner, category, policy, ordered, start, finish);

        if (runner.Disqualified)
        {
            o.Status = ResultStatus.DSQ;
            o.DnfReason = null;
            o.DnfCheckpointId = null;
            o.FinalTimeMs = null;
        }
        return o;
    }

    private static void EvaluateNatural(CalcOutcome o, CalcRunner runner, CalcCategory category, CalcPolicy policy,
        IReadOnlyList<CalcCheckpoint> ordered, CalcCheckpoint? start, CalcCheckpoint finish)
    {
        if (runner.Withdrawal is { } withdrawal)
        {
            SetDnf(o, withdrawal.Reason, withdrawal.CheckpointId);
            return;
        }

        var times = runner.Times;
        if (times.Count == 0)
        {
            o.Status = ResultStatus.DNS;
            return;
        }

        DateTime? startMat = start is not null && times.TryGetValue(start.CheckpointId, out var s) ? s : null;
        if (startMat is DateTime crossed && policy.LateStartToleranceMinutes is int tolerance &&
            crossed > category.StartAt.AddMinutes(tolerance))
        {
            o.Status = ResultStatus.DNS;
            o.LateStart = true;
            return;
        }

        // Cut-off tại trạm (BR-23): có mặt tại trạm sau giờ đóng trạm
        foreach (var checkpoint in ordered.Where(c => c.Type != CheckpointType.Start && c.CutOffAt is not null))
        {
            if (times.TryGetValue(checkpoint.CheckpointId, out var at) && at > checkpoint.CutOffAt)
            {
                SetDnf(o, DnfReason.CutOff, checkpoint.CheckpointId);
                return;
            }
        }

        if (!times.TryGetValue(finish.CheckpointId, out var finishedAt))
        {
            // Không về đích: dừng ở checkpoint kế tiếp sau checkpoint cuối cùng có thời gian
            var lastSequence = ordered.Where(c => times.ContainsKey(c.CheckpointId)).Select(c => c.Sequence).DefaultIfEmpty(0).Max();
            var next = ordered.FirstOrDefault(c => c.Sequence > lastSequence) ?? finish;
            SetDnf(o, DnfReason.CutOff, next.CheckpointId);
            return;
        }

        var gunMs = (long)(finishedAt - category.StartAt).TotalMilliseconds;
        if (category.CutOffMinutes is int cutOff && gunMs > cutOff * 60_000L)
        {
            SetDnf(o, DnfReason.CutOff, finish.CheckpointId);
            return;
        }

        // BR-21: phải đi qua mọi checkpoint bắt buộc trên đường chạy
        var missed = ordered.FirstOrDefault(c => c.Type == CheckpointType.Split && c.IsMandatory && !times.ContainsKey(c.CheckpointId));
        if (missed is not null)
        {
            SetDnf(o, DnfReason.MissedCheckpoint, missed.CheckpointId);
            return;
        }

        long chipMs;
        if (startMat is DateTime matTime)
        {
            chipMs = Math.Min((long)(finishedAt - matTime).TotalMilliseconds, gunMs);
        }
        else
        {
            chipMs = gunMs;
            o.MissingStartMat = start is not null;
        }

        o.Status = ResultStatus.Finished;
        o.GunTimeMs = gunMs;
        o.ChipTimeMs = chipMs;
        o.FinalTimeMs = BasisTime(o, policy.OverallBasis) + o.PenaltySeconds * 1000L;
    }

    private static void SetDnf(CalcOutcome o, DnfReason reason, int? checkpointId)
    {
        o.Status = ResultStatus.DNF;
        o.DnfReason = reason;
        o.DnfCheckpointId = checkpointId;
        o.GunTimeMs = o.ChipTimeMs = o.FinalTimeMs = null;
    }

    private static long BasisTime(CalcOutcome o, RankingBasis basis) =>
        basis == RankingBasis.ChipTime ? o.ChipTimeMs!.Value : o.GunTimeMs!.Value;

    /// <summary>Làm tròn lên giây (BR-22) – thời gian công bố và so sánh theo giây.</summary>
    public static long CeilingSeconds(long milliseconds) => (milliseconds + 999) / 1000;

    private static void Rank(List<CalcOutcome> outcomes, CalcPolicy policy)
    {
        var finished = outcomes.Where(o => o.Status == ResultStatus.Finished).ToList();
        Func<CalcOutcome, long>? tieBreak = policy.TieBreak == TieBreakRule.ChipTime
            ? o => CeilingSeconds(o.ChipTimeMs!.Value + o.PenaltySeconds * 1000L)
            : null;

        long Overall(CalcOutcome o) => CeilingSeconds(o.FinalTimeMs!.Value);
        long AgeGroup(CalcOutcome o) => CeilingSeconds(BasisTime(o, policy.AgeGroupBasis) + o.PenaltySeconds * 1000L);

        AssignRanks(finished, Overall, tieBreak, (o, rank) => o.OverallRank = rank);
        foreach (var group in finished.GroupBy(o => o.Gender))
            AssignRanks(group.ToList(), Overall, tieBreak, (o, rank) => o.GenderRank = rank);
        foreach (var group in finished.Where(o => o.AgeGroupId is not null).GroupBy(o => o.AgeGroupId))
            AssignRanks(group.ToList(), AgeGroup, tieBreak, (o, rank) => o.AgeGroupRank = rank);
    }

    /// <summary>Xếp hạng kiểu thi đấu (1, 2, 2, 4): bằng nhau theo khóa chính (và khóa phụ nếu có) thì cùng hạng.</summary>
    private static void AssignRanks(List<CalcOutcome> group, Func<CalcOutcome, long> key, Func<CalcOutcome, long>? tieBreak,
        Action<CalcOutcome, int> setRank)
    {
        var sorted = group.OrderBy(key).ThenBy(o => tieBreak?.Invoke(o) ?? 0).ToList();
        var rank = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var sameAsPrevious = i > 0 && key(sorted[i - 1]) == key(sorted[i]) &&
                                 (tieBreak is null || tieBreak(sorted[i - 1]) == tieBreak(sorted[i]));
            if (!sameAsPrevious) rank = i + 1;
            setRank(sorted[i], rank);
        }
    }
}
