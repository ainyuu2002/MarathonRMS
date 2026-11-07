using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn bấm giờ (UC-57, UC-58). Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class TimingDAO : BaseDAO<TimingRecord>
{
    public TimingDAO(MarathonDbContext context) : base(context) { }

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Đăng ký đã xác nhận mang số BIB trong giải, kèm cự ly + checkpoint và kết quả (nếu có).</summary>
    public Task<Registration?> GetConfirmedByBibAsync(int eventId, int bibNumber, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking().AsSplitQuery()
            .Include(r => r.Event)
            .Include(r => r.Category).ThenInclude(c => c.Checkpoints)
            .Include(r => r.Participant)
            .Include(r => r.Result)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.BibNumber == bibNumber &&
                                      r.Status == RegistrationStatus.Confirmed, ct);

    public Task<Registration?> GetRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<List<TimingRecord>> GetByRegistrationAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Include(t => t.Checkpoint)
            .Include(t => t.Registration)
            .Where(t => t.RegistrationId == registrationId)
            .OrderBy(t => t.Checkpoint.Sequence)
            .ToListAsync(ct);

    /// <summary>Bản ghi mới nhất của giải (lọc theo checkpoint nếu có).</summary>
    public Task<List<TimingRecord>> GetRecentAsync(int eventId, int? checkpointId, int take, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Include(t => t.Checkpoint)
            .Include(t => t.Registration)
            .Where(t => t.Registration.EventId == eventId && (checkpointId == null || t.CheckpointId == checkpointId))
            .OrderByDescending(t => t.TimingRecordId)
            .Take(take)
            .ToListAsync(ct);

    public Task<TimingRecord?> GetTrackedAsync(int timingRecordId, CancellationToken ct = default) =>
        Set.Include(t => t.Checkpoint)
            .Include(t => t.Registration).ThenInclude(r => r.Event)
            .FirstOrDefaultAsync(t => t.TimingRecordId == timingRecordId, ct);

    // ---------- Bỏ cuộc (UC-58) ----------

    public Task<Result?> GetResultTrackedAsync(int registrationId, CancellationToken ct = default) =>
        Context.Results.Include(r => r.Penalties).FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public async Task AddResultAsync(Result result, CancellationToken ct = default) =>
        await Context.Results.AddAsync(result, ct);

    public void RemoveResult(Result result) => Context.Results.Remove(result);

    /// <summary>Kết quả DNF do VĐV tự bỏ cuộc hoặc rút vì lý do y tế.</summary>
    public Task<List<Result>> GetWithdrawalsAsync(int eventId, CancellationToken ct = default) =>
        Context.Results.AsNoTracking()
            .Include(r => r.Registration).ThenInclude(g => g.Category)
            .Include(r => r.DnfCheckpoint)
            .Where(r => r.Registration.EventId == eventId && r.Status == ResultStatus.DNF &&
                        (r.DnfReason == DnfReason.Voluntary || r.DnfReason == DnfReason.Medical))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);
}
