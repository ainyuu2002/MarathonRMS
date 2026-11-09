using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn khiếu nại kết quả (UC-66). Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class ComplaintDAO : BaseDAO<ResultComplaint>
{
    public ComplaintDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Kết quả kèm đăng ký, người chạy, cự ly và giải + chính sách – đủ để kiểm tra quyền khiếu nại.</summary>
    public Task<Result?> GetResultForComplaintAsync(int resultId, CancellationToken ct = default) =>
        Context.Results.AsNoTracking().AsSplitQuery()
            .Include(r => r.Registration).ThenInclude(g => g.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Registration).ThenInclude(g => g.Participant)
            .Include(r => r.Registration).ThenInclude(g => g.Category)
            .FirstOrDefaultAsync(r => r.ResultId == resultId, ct);

    public Task<bool> HasOpenAsync(int resultId, string userId, CancellationToken ct = default) =>
        Set.AnyAsync(c => c.ResultId == resultId && c.UserId == userId && c.Status == ComplaintStatus.Open, ct);

    /// <summary>Khiếu nại kèm kết quả → đăng ký → giải, người chạy, cự ly (được theo dõi để xử lý).</summary>
    public Task<ResultComplaint?> GetTrackedAsync(int complaintId, CancellationToken ct = default) =>
        Set.AsSplitQuery()
            .Include(c => c.Result).ThenInclude(r => r.Registration).ThenInclude(g => g.Event)
            .Include(c => c.Result).ThenInclude(r => r.Registration).ThenInclude(g => g.Participant)
            .Include(c => c.Result).ThenInclude(r => r.Registration).ThenInclude(g => g.Category)
            .FirstOrDefaultAsync(c => c.ComplaintId == complaintId, ct);

    /// <summary>Đọc lại trạng thái mới nhất từ DB (sau khi đã giữ khóa).</summary>
    public Task ReloadAsync(ResultComplaint complaint, CancellationToken ct = default) => Context.Entry(complaint).ReloadAsync(ct);

    public IQueryable<ResultComplaint> QueryByUser(string userId) =>
        Set.AsNoTracking().Where(c => c.UserId == userId);

    public IQueryable<ResultComplaint> QueryByEvent(int eventId) =>
        Set.AsNoTracking().Where(c => c.Result.Registration.EventId == eventId);

    public Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().Include(e => e.Policy).FirstOrDefaultAsync(e => e.EventId == eventId, ct);
}
