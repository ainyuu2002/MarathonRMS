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

    /// <summary>Đăng ký đã xác nhận của giải kèm mọi thời gian đã ghi và việc nhận kit (BR-26).</summary>
    public Task<List<Registration>> GetConfirmedWithTimingAsync(int eventId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking().AsSplitQuery()
            .Include(r => r.TimingRecords)
            .Include(r => r.RaceKitPickup)
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
            .ToListAsync(ct);

    /// <summary>Kết quả hiện có của giải kèm quyết định phạt (được theo dõi để cập nhật).</summary>
    public Task<List<Result>> GetTrackedByEventAsync(int eventId, CancellationToken ct = default) =>
        Set.Include(r => r.Penalties)
            .Where(r => r.Registration.EventId == eventId)
            .ToListAsync(ct);

    /// <summary>Có dòng kết quả nào ngoài các ca bỏ cuộc (ghi nhận từ trạm) – tức giải đã được tính kết quả.</summary>
    public Task<bool> HasCalculatedResultsAsync(int eventId, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.Registration.EventId == eventId &&
                          !(r.Status == ResultStatus.DNF && (r.DnfReason == DnfReason.Voluntary || r.DnfReason == DnfReason.Medical)), ct);

    /// <summary>Kết quả của giải – chỉ đăng ký còn hiệu lực (đã hủy / chuyển nhượng thì không hiện).</summary>
    public IQueryable<Result> QueryByEvent(int eventId) =>
        Set.AsNoTracking().Where(r => r.Registration.EventId == eventId && r.Registration.Status == RegistrationStatus.Confirmed);

    public Task<Dictionary<ResultStatus, int>> CountByStatusAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(r => r.Registration.EventId == eventId && r.Registration.Status == RegistrationStatus.Confirmed)
            .GroupBy(r => r.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task<DateTime?> GetLastUpdatedAtAsync(int eventId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(r => r.Registration.EventId == eventId).MaxAsync(r => (DateTime?)r.UpdatedAt, ct);

    // ---------- Công bố (UC-60) ----------

    public Task<Event?> GetEventTrackedAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.Include(e => e.Policy).FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Kết quả đang tạm thời mà vừa thay đổi ⇒ tính lại cửa sổ khiếu nại từ thời điểm này (cập nhật ngay, trong transaction hiện tại).</summary>
    public Task ReopenComplaintWindowAsync(int eventId, DateTime now, CancellationToken ct = default) =>
        Context.Events.Where(e => e.EventId == eventId && e.ResultsStatus == ResultsStatus.Provisional)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.ResultsProvisionalAt, now), ct);

    public Task<bool> AnyForEventAsync(int eventId, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.Registration.EventId == eventId && r.Registration.Status == RegistrationStatus.Confirmed, ct);

    /// <summary>Khiếu nại kết quả chưa xử lý của giải (BR-27).</summary>
    public Task<int> CountOpenComplaintsAsync(int eventId, CancellationToken ct = default) =>
        Context.ResultComplaints.CountAsync(c => c.Result.Registration.EventId == eventId && c.Status == ComplaintStatus.Open, ct);

    // ---------- Phạt / DSQ (UC-61) ----------

    /// <summary>Kết quả kèm đăng ký, giải, cự ly + checkpoint, nhóm tuổi và mọi quyết định phạt (kèm lý do).</summary>
    public Task<Result?> GetWithPenaltiesAsync(int resultId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Registration).ThenInclude(g => g.Event)
            .Include(r => r.Registration).ThenInclude(g => g.Participant)
            .Include(r => r.Registration).ThenInclude(g => g.Category).ThenInclude(c => c.Checkpoints)
            .Include(r => r.AgeGroup)
            .Include(r => r.Penalties).ThenInclude(p => p.Reason)
            .FirstOrDefaultAsync(r => r.ResultId == resultId, ct);

    public Task<ResultPenalty?> GetPenaltyTrackedAsync(int penaltyId, CancellationToken ct = default) =>
        Context.ResultPenalties
            .Include(p => p.Result).ThenInclude(r => r.Registration).ThenInclude(g => g.Event)
            .Include(p => p.Result).ThenInclude(r => r.Registration).ThenInclude(g => g.Participant)
            .FirstOrDefaultAsync(p => p.PenaltyId == penaltyId, ct);

    public async Task AddPenaltyAsync(ResultPenalty penalty, CancellationToken ct = default) =>
        await Context.ResultPenalties.AddAsync(penalty, ct);

    // ---------- Bảng xếp hạng công khai (UC-62) ----------

    /// <summary>Kết quả của các giải đã công bố (tạm thời hoặc chính thức) – chỉ đăng ký còn hiệu lực.</summary>
    public IQueryable<Result> QueryPublished() =>
        Set.AsNoTracking().Where(r => r.Registration.Status == RegistrationStatus.Confirmed &&
                                      (r.Registration.Event.ResultsStatus == ResultsStatus.Provisional ||
                                       r.Registration.Event.ResultsStatus == ResultsStatus.Official));

    /// <summary>Kết quả cá nhân kèm thời gian từng checkpoint, nhóm tuổi, quyết định phạt (UC-63).</summary>
    public Task<Result?> GetDetailByRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Registration).ThenInclude(g => g.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Registration).ThenInclude(g => g.Category).ThenInclude(c => c.Checkpoints)
            .Include(r => r.Registration).ThenInclude(g => g.TimingRecords)
            .Include(r => r.Registration).ThenInclude(g => g.Participant)
            .Include(r => r.AgeGroup)
            .Include(r => r.Penalties).ThenInclude(p => p.Reason)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<List<Event>> GetPublishedEventsAsync(CancellationToken ct = default) =>
        Context.Events.AsNoTracking().AsSplitQuery()
            .Include(e => e.Organization)
            .Include(e => e.Categories)
            .Include(e => e.AgeGroups)
            .Where(e => e.ResultsStatus == ResultsStatus.Provisional || e.ResultsStatus == ResultsStatus.Official)
            .OrderByDescending(e => e.EventDate).ThenBy(e => e.Name)
            .ToListAsync(ct);
}
