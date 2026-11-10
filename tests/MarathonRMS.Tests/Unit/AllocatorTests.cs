using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.Services.RaceDay;

namespace MarathonRMS.Tests.Unit;

/// <summary>UC-28/65 – lập danh sách nhận giải (BR-29).</summary>
public class PrizeAllocatorTests
{
    private static AllocRunner R(int id, Gender g, int genderRank, int? ag = null, int? agRank = null, string nat = "VN") =>
        new(id, g, ag, nat, genderRank, agRank, 100 + id);

    private static readonly AllocRunner[] Men =
    {
        R(1, Gender.M, 1, ag: 5, agRank: 1),
        R(2, Gender.M, 2, ag: 5, agRank: 2),
        R(3, Gender.M, 3, ag: 6, agRank: 1),
        R(4, Gender.M, 4, ag: 5, agRank: 3),
    };

    private static readonly AllocPrize[] Prizes =
    {
        new(10, AwardType.Overall, Gender.M, null, null, 1),
        new(11, AwardType.Overall, Gender.M, null, null, 2),
        new(20, AwardType.AgeGroup, Gender.M, 5, null, 1),
        new(21, AwardType.AgeGroup, Gender.M, 5, null, 2),
    };

    [Fact]
    public void HighestOnly_moves_next_runner_up_in_age_group_br29()
    {
        var res = new PrizeAllocator().Allocate(Prizes, Men, AwardStackingPolicy.HighestOnly);
        Assert.Equal(1, res[10]);
        Assert.Equal(2, res[11]);
        Assert.Equal(4, res[20]); // 1 và 2 đã nhận giải chung cuộc
        Assert.Null(res[21]);     // nhóm 5 không còn ai
    }

    [Fact]
    public void AllowAll_lets_runner_stack_awards()
    {
        var res = new PrizeAllocator().Allocate(Prizes, Men, AwardStackingPolicy.AllowAll);
        Assert.Equal(1, res[20]);
        Assert.Equal(2, res[21]);
    }

    [Fact]
    public void Prize_for_other_gender_is_not_given_to_men()
    {
        var res = new PrizeAllocator().Allocate(new[] { new AllocPrize(30, AwardType.Overall, Gender.F, null, null, 1) }, Men,
            AwardStackingPolicy.HighestOnly);
        Assert.Null(res[30]);
    }

    [Fact]
    public void Nationality_board_matches_case_and_space_insensitive()
    {
        var runners = new[] { R(1, Gender.M, 1, nat: "VN"), R(2, Gender.M, 2, nat: "KE"), R(3, Gender.M, 3, nat: "vn ") };
        var prizes = new[]
        {
            new AllocPrize(1, AwardType.Overall, Gender.M, null, null, 1),
            new AllocPrize(2, AwardType.Nationality, Gender.M, null, " vn", 1)
        };
        var res = new PrizeAllocator().Allocate(prizes, runners, AwardStackingPolicy.HighestOnly);
        Assert.Equal(1, res[1]);
        Assert.Equal(3, res[2]);
    }

    [Fact]
    public void Runners_without_rank_are_last()
    {
        var runners = new[] { new AllocRunner(1, Gender.M, null, "VN", null, null, 1), R(2, Gender.M, 1) };
        var res = new PrizeAllocator().Allocate(new[] { new AllocPrize(1, AwardType.Overall, Gender.M, null, null, 1) }, runners,
            AwardStackingPolicy.AllowAll);
        Assert.Equal(2, res[1]);
    }
}

/// <summary>UC-52 – chọn số BIB còn trống trong dải (BR-19).</summary>
public class BibAllocatorTests
{
    [Fact]
    public void Picks_smallest_free_numbers_in_range_br19()
    {
        var res = new BibAllocator().Allocate(1000, 1010, new HashSet<int> { 1000, 1002 }, 3);
        Assert.Equal(new[] { 1001, 1003, 1004 }, res);
    }

    [Fact]
    public void Returns_fewer_when_range_exhausted()
    {
        var res = new BibAllocator().Allocate(1, 3, new HashSet<int> { 2 }, 5);
        Assert.Equal(new[] { 1, 3 }, res);
    }

    [Fact]
    public void Count_zero_returns_empty() =>
        Assert.Empty(new BibAllocator().Allocate(1, 3, new HashSet<int>(), 0));
}
