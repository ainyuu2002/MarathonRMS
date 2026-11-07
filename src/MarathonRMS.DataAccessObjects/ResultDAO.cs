using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn kết quả (UC-59 → UC-63). Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class ResultDAO : BaseDAO<Result>
{
    public ResultDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Giải kèm chính sách, cự ly + checkpoint và nhóm tuổi – đủ dữ liệu để tính kết quả.</summary>
    public Task<Event?> GetEventForResultsAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().AsSplitQuery()
            .Include(e => e.Policy)
            .Include(e => e.Categories).ThenInclude(c => c.Checkpoints)
            .Include(e => e.AgeGroups)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Đăng ký đã xác nhận của giải kèm mọi thời gian đã ghi.</summary>
    public Task<List<Registration>> GetConfirmedWithTimingAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Include(r => r.TimingRecords)
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
            .ToListAsync(ct);

    /// <summary>Kết quả hiện có của giải kèm quyết định phạt (được theo dõi để cập nhật).</summary>
    public Task<List<Result>> GetTrackedByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(r => r.Penalties)
            .Where(r => r.Registration.EventId == eventId)
            .ToListAsync(ct);

    public IQueryable<Result> QueryByEvent(int eventId) =>
        Set.AsNoTracking().Where(r => r.Registration.EventId == eventId);

    public Task<Dictionary<ResultStatus, int>> CountByStatusAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(r => r.Registration.EventId == eventId)
            .GroupBy(r => r.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task<DateTime?> GetLastUpdatedAtAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(r => r.Registration.EventId == eventId).MaxAsync(r => (DateTime?)r.UpdatedAt, ct);
}
