using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

public class ParticipantDAO : BaseDAO<Participant>
{
    public ParticipantDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Hồ sơ chính chủ (có tracking – dùng để sửa).</summary>
    public Task<Participant?> GetSelfAsync(string userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.OwnerUserId == userId && p.Relationship == ParticipantRelationship.Self && !p.IsDeleted, ct);
}

public class ConsentRecordDAO : BaseDAO<ConsentRecord>
{
    public ConsentRecordDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Bản ghi đồng ý mới nhất của từng loại (cấp tài khoản, không gắn đăng ký) – có tracking.</summary>
    public async Task<List<ConsentRecord>> GetLatestPerTypeAsync(string userId, CancellationToken ct = default)
    {
        var records = await Set.Where(c => c.UserId == userId && c.RegistrationId == null)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.ConsentId)
            .ToListAsync(ct);
        return records.GroupBy(c => c.ConsentType).Select(g => g.First()).ToList();
    }
}

public class LegalDocumentDAO : BaseDAO<LegalDocument>
{
    public LegalDocumentDAO(MarathonDbContext context) : base(context) { }

    public Task<LegalDocument?> GetCurrentAsync(LegalDocType type, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(d => d.DocType == type).OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);

    public Task<List<LegalDocument>> GetVersionsAsync(LegalDocType type, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(d => d.DocType == type).OrderByDescending(d => d.Version).ToListAsync(ct);
}

public class SystemSettingDAO : BaseDAO<SystemSetting>
{
    public SystemSettingDAO(MarathonDbContext context) : base(context) { }

    public Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default) =>
        Set.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct);

    public Task<SystemSetting?> GetTrackedAsync(string key, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(s => s.Key == key, ct);
}

public class AuditLogDAO : BaseDAO<AuditLog>
{
    public AuditLogDAO(MarathonDbContext context) : base(context) { }

    // Nhật ký chỉ được thêm (BR-36)
    public override void Update(AuditLog entity) => throw new InvalidOperationException("AuditLogs are insert-only.");
    public override void Remove(AuditLog entity) => throw new InvalidOperationException("AuditLogs are insert-only.");
}
