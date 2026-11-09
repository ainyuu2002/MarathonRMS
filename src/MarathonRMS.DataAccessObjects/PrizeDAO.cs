using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn giải thưởng (UC-28) và danh sách nhận giải (UC-65). Không chứa quy tắc nghiệp vụ, không tự SaveChanges.</summary>
public class PrizeDAO : BaseDAO<Prize>
{
    public PrizeDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Giải kèm chính sách, BTC, cự ly và nhóm tuổi.</summary>
    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().AsSplitQuery()
            .Include(e => e.Policy)
            .Include(e => e.Organization)
            .Include(e => e.Categories)
            .Include(e => e.AgeGroups)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<RaceCategory?> GetCategoryAsync(int categoryId, CancellationToken ct = default) =>
        Context.RaceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

    public Task<List<Prize>> GetByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.AgeGroup)
            .Where(p => p.Category.EventId == eventId)
            .OrderBy(p => p.Category.StartAt).ThenBy(p => p.CategoryId)
            .ThenBy(p => p.AwardType).ThenBy(p => p.Gender).ThenBy(p => p.AgeGroupId).ThenBy(p => p.Rank)
            .ToListAsync(ct);

    public Task<Prize?> GetTrackedAsync(int prizeId, CancellationToken ct = default) =>
        Set.Include(p => p.Category).FirstOrDefaultAsync(p => p.PrizeId == prizeId, ct);

    /// <summary>Đã có giải cùng cự ly + loại + giới tính + nhóm tuổi + hạng (trừ chính nó khi sửa).</summary>
    public Task<bool> ExistsAsync(int categoryId, AwardType type, Gender gender, int? ageGroupId, int rank, int? exceptId,
        CancellationToken ct = default) =>
        Set.AnyAsync(p => p.CategoryId == categoryId && p.AwardType == type && p.Gender == gender &&
                          p.AgeGroupId == ageGroupId && p.Rank == rank && p.PrizeId != (exceptId ?? 0), ct);

    /// <summary>Kết quả về đích (Finished) của đăng ký còn hiệu lực, kèm thông tin để trao giải.</summary>
    public Task<List<Result>> GetFinishersAsync(int eventId, CancellationToken ct = default) =>
        Context.Results.AsNoTracking()
            .Include(r => r.Registration)
            .Where(r => r.Registration.EventId == eventId && r.Registration.Status == RegistrationStatus.Confirmed &&
                        r.Status == ResultStatus.Finished)
            .ToListAsync(ct);
}
