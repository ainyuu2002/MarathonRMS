using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn hồ sơ thành tích và e-certificate (UC-64). Không chứa quy tắc nghiệp vụ, không tự SaveChanges.</summary>
public class AchievementDAO : BaseDAO<Result>
{
    public AchievementDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Hồ sơ người chạy chính chủ của tài khoản.</summary>
    public Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default) =>
        Context.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LinkedUserId == userId && !p.IsDeleted, ct);

    public Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default) =>
        Context.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ParticipantId == participantId && !p.IsDeleted, ct);

    /// <summary>Kết quả đã công bố (tạm thời / chính thức) của người chạy, đăng ký còn hiệu lực.</summary>
    public Task<List<Result>> GetPublishedByParticipantAsync(int participantId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Registration).ThenInclude(g => g.Event)
            .Include(r => r.Registration).ThenInclude(g => g.Category)
            .Include(r => r.AgeGroup)
            .Where(r => r.Registration.ParticipantId == participantId &&
                        r.Registration.Status == RegistrationStatus.Confirmed &&
                        (r.Registration.Event.ResultsStatus == ResultsStatus.Provisional ||
                         r.Registration.Event.ResultsStatus == ResultsStatus.Official))
            .OrderByDescending(r => r.Registration.Event.EventDate)
            .ToListAsync(ct);

    public Task<Result?> GetForCertificateAsync(int registrationId, CancellationToken ct = default) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(r => r.Registration).ThenInclude(g => g.Event).ThenInclude(e => e.Organization)
            .Include(r => r.Registration).ThenInclude(g => g.Event).ThenInclude(e => e.Venue)
            .Include(r => r.Registration).ThenInclude(g => g.Category)
            .Include(r => r.Registration).ThenInclude(g => g.Participant)
            .Include(r => r.AgeGroup)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<int> CountFinishersAsync(int categoryId, CancellationToken ct = default) =>
        Set.CountAsync(r => r.Registration.CategoryId == categoryId && r.Status == ResultStatus.Finished &&
                            r.Registration.Status == RegistrationStatus.Confirmed, ct);
}
