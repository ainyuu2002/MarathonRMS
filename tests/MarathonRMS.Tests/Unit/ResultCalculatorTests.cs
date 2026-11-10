using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.Services.RaceDay;

namespace MarathonRMS.Tests.Unit;

/// <summary>UC-59 – tính thành tích và xếp hạng (BR-21…26), logic thuần.</summary>
public class ResultCalculatorTests
{
    private static readonly DateTime Gun = new(2026, 11, 15, 6, 0, 0);
    private const int StartCp = 1, Cp1 = 2, FinishCp = 3;
    private static readonly DateTime AfterRace = Gun.AddHours(14);

    private static CalcCategory Category(int? cutOffMinutes = 300, bool ranked = true, DateTime? cp1CutOff = null, bool withStart = true)
    {
        var cps = new List<CalcCheckpoint>();
        if (withStart) cps.Add(new CalcCheckpoint(StartCp, "Start", 1, CheckpointType.Start, true, null));
        cps.Add(new CalcCheckpoint(Cp1, "CP1", 2, CheckpointType.Split, true, cp1CutOff ?? Gun.AddHours(2)));
        cps.Add(new CalcCheckpoint(FinishCp, "Finish", 3, CheckpointType.Finish, true, null));
        return new CalcCategory(10, "21K", Gun, cutOffMinutes, ranked, cps);
    }

    private static CalcPolicy Policy(RankingBasis basis = RankingBasis.ChipTime, TieBreakRule tie = TieBreakRule.Shared,
        int? lateTolerance = null, RankingBasis? ageBasis = null) =>
        new(basis, ageBasis ?? basis, tie, lateTolerance);

    private static CalcRunner Runner(int id, Dictionary<int, DateTime> times, Gender g = Gender.M, int? ageGroup = null,
        CalcWithdrawal? withdrawal = null, bool dsq = false, int penalty = 0) =>
        new(id, g, ageGroup, times, withdrawal, dsq, penalty);

    private static Dictionary<int, DateTime> Full(DateTime finish, TimeSpan? startDelay = null) => new()
    {
        [StartCp] = Gun.Add(startDelay ?? TimeSpan.FromMinutes(1)),
        [Cp1] = Gun.AddHours(1),
        [FinishCp] = finish
    };

    private static CalcOutcome One(CalcRunner r, CalcCategory? c = null, CalcPolicy? p = null, DateTime? now = null) =>
        new ResultCalculator().Calculate(c ?? Category(), p ?? Policy(), new[] { r }, now ?? AfterRace).Single();

    private static Dictionary<int, CalcOutcome> Many(CalcPolicy p, params CalcRunner[] runners) =>
        new ResultCalculator().Calculate(Category(), p, runners, AfterRace).ToDictionary(o => o.RegistrationId);

    [Fact]
    public void Finisher_gets_gun_and_chip_time_br22()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3).AddMilliseconds(500))));
        Assert.Equal(ResultStatus.Finished, o.Status);
        Assert.Equal(3 * 3_600_000L + 500, o.GunTimeMs);
        Assert.Equal(3 * 3_600_000L + 500 - 60_000, o.ChipTimeMs);
        Assert.Equal(o.ChipTimeMs, o.FinalTimeMs);
        Assert.Equal(1, o.OverallRank);
    }

    [Fact]
    public void Gun_basis_uses_gun_time_as_final()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3))), p: Policy(RankingBasis.GunTime));
        Assert.Equal(o.GunTimeMs, o.FinalTimeMs);
    }

    [Fact]
    public void No_record_is_dns_br24()
    {
        var o = One(Runner(1, new Dictionary<int, DateTime>()));
        Assert.Equal(ResultStatus.DNS, o.Status);
        Assert.Null(o.OverallRank);
    }

    [Fact]
    public void Late_start_beyond_tolerance_is_dns_br24()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3), TimeSpan.FromMinutes(15))), p: Policy(lateTolerance: 10));
        Assert.Equal(ResultStatus.DNS, o.Status);
        Assert.True(o.LateStart);
    }

    [Fact]
    public void Start_exactly_at_tolerance_is_not_late()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3), TimeSpan.FromMinutes(10))), p: Policy(lateTolerance: 10));
        Assert.Equal(ResultStatus.Finished, o.Status);
    }

    [Fact]
    public void Passing_station_after_cutoff_is_dnf_at_station_br23()
    {
        var times = Full(Gun.AddHours(4));
        times[Cp1] = Gun.AddHours(2).AddSeconds(1);
        var o = One(Runner(1, times));
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(DnfReason.CutOff, o.DnfReason);
        Assert.Equal(Cp1, o.DnfCheckpointId);
        Assert.Null(o.FinalTimeMs);
    }

    [Fact]
    public void Passing_station_exactly_at_cutoff_is_allowed()
    {
        var times = Full(Gun.AddHours(4));
        times[Cp1] = Gun.AddHours(2);
        Assert.Equal(ResultStatus.Finished, One(Runner(1, times)).Status);
    }

    [Fact]
    public void Finishing_after_course_cutoff_by_gun_time_is_dnf_br23()
    {
        // Chip time < 300 phút nhưng gun time > 300 phút ⇒ vẫn DNF (cut-off tính theo gun time)
        var o = One(Runner(1, Full(Gun.AddMinutes(300).AddSeconds(1), TimeSpan.FromMinutes(5))));
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(DnfReason.CutOff, o.DnfReason);
        Assert.Equal(FinishCp, o.DnfCheckpointId);
    }

    [Fact]
    public void Missing_mandatory_checkpoint_is_dnf_missed_br21()
    {
        var times = Full(Gun.AddHours(3));
        times.Remove(Cp1);
        var o = One(Runner(1, times));
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(DnfReason.MissedCheckpoint, o.DnfReason);
        Assert.Equal(Cp1, o.DnfCheckpointId);
    }

    [Fact]
    public void Missing_start_mat_uses_gun_time_as_chip_br22()
    {
        var times = Full(Gun.AddHours(3));
        times.Remove(StartCp);
        var o = One(Runner(1, times));
        Assert.Equal(ResultStatus.Finished, o.Status);
        Assert.Equal(o.GunTimeMs, o.ChipTimeMs);
        Assert.True(o.MissingStartMat);
    }

    [Fact]
    public void Category_without_start_checkpoint_does_not_flag_missing_mat()
    {
        var times = Full(Gun.AddHours(3));
        times.Remove(StartCp);
        var o = One(Runner(1, times), c: Category(withStart: false));
        Assert.False(o.MissingStartMat);
        Assert.Equal(o.GunTimeMs, o.ChipTimeMs);
    }

    [Fact]
    public void Start_mat_before_gun_does_not_make_chip_longer_than_gun()
    {
        var times = Full(Gun.AddHours(3));
        times[StartCp] = Gun.AddSeconds(-30);
        var o = One(Runner(1, times));
        Assert.Equal(o.GunTimeMs, o.ChipTimeMs);
    }

    [Theory]
    [InlineData(DnfReason.Medical)]
    [InlineData(DnfReason.Voluntary)]
    public void Recorded_withdrawal_stays_dnf_br23(DnfReason reason)
    {
        var o = One(Runner(1, Full(Gun.AddHours(3)), withdrawal: new CalcWithdrawal(reason, Cp1)));
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(reason, o.DnfReason);
        Assert.Equal(Cp1, o.DnfCheckpointId);
    }

    [Fact]
    public void Disqualified_overrides_finish()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3)), dsq: true));
        Assert.Equal(ResultStatus.DSQ, o.Status);
        Assert.Null(o.FinalTimeMs);
        Assert.Null(o.OverallRank);
    }

    [Fact]
    public void Penalty_seconds_are_added_to_final_time_br25()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3)), penalty: 120));
        Assert.Equal(o.ChipTimeMs + 120_000, o.FinalTimeMs);
    }

    [Fact]
    public void Runner_still_on_course_has_no_result_yet()
    {
        var times = new Dictionary<int, DateTime> { [StartCp] = Gun.AddMinutes(1) };
        var o = One(Runner(1, times), now: Gun.AddMinutes(90));
        Assert.True(o.OnCourse);
        Assert.Null(o.OverallRank);
    }

    [Fact]
    public void Station_closed_ahead_marks_dnf_at_that_station()
    {
        var times = new Dictionary<int, DateTime> { [StartCp] = Gun.AddMinutes(1) };
        var o = One(Runner(1, times), now: Gun.AddHours(2).AddMinutes(1));
        Assert.False(o.OnCourse);
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(DnfReason.CutOff, o.DnfReason);
        Assert.Equal(Cp1, o.DnfCheckpointId);
    }

    [Fact]
    public void Without_course_cutoff_runner_not_reaching_next_station_by_next_day_is_missed()
    {
        var cat = new CalcCategory(10, "Trail", Gun, null, true, new List<CalcCheckpoint>
        {
            new(StartCp, "Start", 1, CheckpointType.Start, true, null),
            new(Cp1, "CP1", 2, CheckpointType.Split, true, null),
            new(FinishCp, "Finish", 3, CheckpointType.Finish, true, null)
        });
        var times = new Dictionary<int, DateTime> { [StartCp] = Gun.AddMinutes(1) };
        var o = One(Runner(1, times), c: cat, now: Gun.Date.AddDays(1).AddMinutes(1));
        Assert.Equal(ResultStatus.DNF, o.Status);
        Assert.Equal(DnfReason.MissedCheckpoint, o.DnfReason);
        Assert.Equal(Cp1, o.DnfCheckpointId);
    }

    [Fact]
    public void Category_without_finish_checkpoint_throws()
    {
        var cat = new CalcCategory(10, "X", Gun, null, true, new List<CalcCheckpoint>
        {
            new(StartCp, "Start", 1, CheckpointType.Start, true, null)
        });
        Assert.Throws<InvalidOperationException>(() => new ResultCalculator().Calculate(cat, Policy(), Array.Empty<CalcRunner>(), AfterRace));
    }

    [Fact]
    public void Ties_within_same_rounded_second_share_rank_br25()
    {
        var res = Many(Policy(),
            Runner(1, Full(Gun.AddHours(3).AddMilliseconds(100))),
            Runner(2, Full(Gun.AddHours(3).AddMilliseconds(900))),
            Runner(3, Full(Gun.AddHours(3).AddSeconds(5))));
        Assert.Equal(1, res[1].OverallRank);
        Assert.Equal(1, res[2].OverallRank);
        Assert.Equal(3, res[3].OverallRank);
    }

    [Fact]
    public void Tie_break_by_chip_time_splits_equal_gun_times_br25()
    {
        var res = Many(Policy(RankingBasis.GunTime, TieBreakRule.ChipTime),
            Runner(1, Full(Gun.AddHours(3), TimeSpan.FromSeconds(10))),
            Runner(2, Full(Gun.AddHours(3), TimeSpan.FromSeconds(30))));
        Assert.Equal(2, res[1].OverallRank);
        Assert.Equal(1, res[2].OverallRank);
    }

    [Fact]
    public void Gender_and_age_group_ranks_are_separate()
    {
        var res = Many(Policy(),
            Runner(1, Full(Gun.AddHours(3)), Gender.M, ageGroup: 7),
            Runner(2, Full(Gun.AddHours(3).AddMinutes(1)), Gender.F, ageGroup: 7),
            Runner(3, Full(Gun.AddHours(3).AddMinutes(2)), Gender.M, ageGroup: 8),
            Runner(4, Full(Gun.AddHours(3).AddMinutes(3)), Gender.F, ageGroup: null));
        Assert.Equal(new int?[] { 1, 2, 3, 4 }, new[] { res[1].OverallRank, res[2].OverallRank, res[3].OverallRank, res[4].OverallRank });
        Assert.Equal(1, res[2].GenderRank);
        Assert.Equal(2, res[3].GenderRank);
        Assert.Equal(2, res[4].GenderRank);
        Assert.Equal(1, res[1].AgeGroupRank);
        Assert.Equal(2, res[2].AgeGroupRank);
        Assert.Equal(1, res[3].AgeGroupRank);
        Assert.Null(res[4].AgeGroupRank);
    }

    [Fact]
    public void Age_group_rank_can_use_different_basis()
    {
        // Chung cuộc theo gun, nhóm tuổi theo chip: runner 2 xuất phát muộn nên chip nhanh hơn
        var res = Many(Policy(RankingBasis.GunTime, ageBasis: RankingBasis.ChipTime),
            Runner(1, Full(Gun.AddHours(3), TimeSpan.FromSeconds(5)), ageGroup: 1),
            Runner(2, Full(Gun.AddHours(3).AddSeconds(30), TimeSpan.FromMinutes(2)), ageGroup: 1));
        Assert.Equal(1, res[1].OverallRank);
        Assert.Equal(1, res[2].AgeGroupRank);
    }

    [Fact]
    public void Unranked_category_keeps_times_but_no_rank_br26()
    {
        var o = One(Runner(1, Full(Gun.AddHours(3))), c: Category(ranked: false));
        Assert.Equal(ResultStatus.Finished, o.Status);
        Assert.NotNull(o.FinalTimeMs);
        Assert.Null(o.OverallRank);
        Assert.Null(o.GenderRank);
    }

    [Fact]
    public void Non_finishers_are_not_ranked()
    {
        var times = Full(Gun.AddHours(3));
        times.Remove(Cp1);
        var res = Many(Policy(), Runner(1, times), Runner(2, Full(Gun.AddHours(4))));
        Assert.Null(res[1].OverallRank);
        Assert.Equal(1, res[2].OverallRank);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    public void Ceiling_seconds_rounds_up_br22(long ms, long expected) =>
        Assert.Equal(expected, ResultCalculator.CeilingSeconds(ms));
}
