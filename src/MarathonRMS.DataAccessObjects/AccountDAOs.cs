using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

public class ParticipantDAO : BaseDAO<Participant>
{
    public ParticipantDAO(MarathonDbContext context) : base(context) { }

    public Task<Participant?> GetSelfAsync(string userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.OwnerUserId == userId && p.Relationship == ParticipantRelationship.Self && !p.IsDeleted, ct);
}

public class ConsentRecordDAO : BaseDAO<ConsentRecord>
{
    public ConsentRecordDAO(MarathonDbContext context) : base(context) { }
}

public class LegalDocumentDAO : BaseDAO<LegalDocument>
{
    public LegalDocumentDAO(MarathonDbContext context) : base(context) { }

    public Task<LegalDocument?> GetCurrentAsync(LegalDocType type, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(d => d.DocType == type).OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);
}

public class SystemSettingDAO : BaseDAO<SystemSetting>
{
    public SystemSettingDAO(MarathonDbContext context) : base(context) { }

    public Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default) => Set.AsNoTracking().ToListAsync(ct);
}

public class AuditLogDAO : BaseDAO<AuditLog>
{
    public AuditLogDAO(MarathonDbContext context) : base(context) { }

    // Nhật ký chỉ được thêm (BR-36)
    public override void Update(AuditLog entity) => throw new InvalidOperationException("AuditLogs are insert-only.");
    public override void Remove(AuditLog entity) => throw new InvalidOperationException("AuditLogs are insert-only.");
}

public class OrganizationDAO : BaseDAO<Organization>
{
    public OrganizationDAO(MarathonDbContext context) : base(context) { }
}
