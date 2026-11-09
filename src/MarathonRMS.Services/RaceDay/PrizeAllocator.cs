using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.Services.RaceDay;

public sealed record AllocPrize(int PrizeId, AwardType AwardType, Gender Gender, int? AgeGroupId, string? Nationality, int Rank);

/// <summary>VĐV về đích của một cự ly kèm thứ hạng đã tính (UC-59).</summary>
public sealed record AllocRunner(int RegistrationId, Gender Gender, int? AgeGroupId, string Nationality,
    int? GenderRank, int? AgeGroupRank, int? BibNumber);

/// <summary>
/// BR-29 – lập danh sách nhận giải của một cự ly từ kết quả (logic thuần, không truy cập DB).
/// Thứ tự xét: giải chung cuộc (theo hạng giới tính) → giải VĐV theo quốc tịch → giải nhóm tuổi (theo hạng nhóm tuổi).
/// HighestOnly: mỗi VĐV chỉ nhận giải cao nhất, người kế tiếp được đôn lên; AllowAll: nhận cộng dồn.
/// Giải hạng k trao cho người thứ k trong danh sách đủ điều kiện (sau khi loại người đã nhận giải nếu HighestOnly).
/// </summary>
public interface IPrizeAllocator
{
    /// <returns>PrizeId → RegistrationId (null = không đủ VĐV để trao).</returns>
    IReadOnlyDictionary<int, int?> Allocate(IReadOnlyList<AllocPrize> prizes, IReadOnlyList<AllocRunner> finishers,
        AwardStackingPolicy stacking);
}

public class PrizeAllocator : IPrizeAllocator
{
    public IReadOnlyDictionary<int, int?> Allocate(IReadOnlyList<AllocPrize> prizes, IReadOnlyList<AllocRunner> finishers,
        AwardStackingPolicy stacking)
    {
        var awarded = new HashSet<int>();
        var result = new Dictionary<int, int?>();

        // Mỗi "bảng giải" (loại + giới tính + nhóm tuổi / quốc tịch) xét chung một danh sách ứng viên
        var boards = prizes
            .GroupBy(p => (p.AwardType, p.Gender, p.AgeGroupId, Nationality: Normalize(p.Nationality)))
            .OrderBy(g => Precedence(g.Key.AwardType))
            .ThenBy(g => g.Key.Gender)
            .ThenBy(g => g.Key.AgeGroupId);

        foreach (var board in boards)
        {
            var (type, gender, ageGroupId, nationality) = board.Key;
            var candidates = finishers.Where(f => f.Gender == gender);
            candidates = type switch
            {
                AwardType.AgeGroup => candidates.Where(f => f.AgeGroupId == ageGroupId)
                    .OrderBy(f => f.AgeGroupRank ?? int.MaxValue).ThenBy(f => f.BibNumber),
                AwardType.Nationality => candidates.Where(f => Normalize(f.Nationality) == nationality)
                    .OrderBy(f => f.GenderRank ?? int.MaxValue).ThenBy(f => f.BibNumber),
                _ => candidates.OrderBy(f => f.GenderRank ?? int.MaxValue).ThenBy(f => f.BibNumber)
            };

            var eligible = candidates
                .Where(f => stacking == AwardStackingPolicy.AllowAll || !awarded.Contains(f.RegistrationId))
                .ToList();
            var winnersOfBoard = new List<int>();
            foreach (var prize in board.OrderBy(p => p.Rank))
            {
                int? winner = prize.Rank <= eligible.Count ? eligible[prize.Rank - 1].RegistrationId : null;
                result[prize.PrizeId] = winner;
                if (winner is int id) winnersOfBoard.Add(id);
            }
            // Loại sau khi xong cả bảng: trong cùng một bảng các hạng không đẩy nhau
            foreach (var id in winnersOfBoard) awarded.Add(id);
        }
        return result;
    }

    private static int Precedence(AwardType type) => type switch
    {
        AwardType.Overall => 0,
        AwardType.Nationality => 1,
        _ => 2
    };

    private static string? Normalize(string? value) => value?.Trim().ToLowerInvariant();
}
