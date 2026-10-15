using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IParticipantRepository
{
    Task<Participant?> GetSelfAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Participant participant, CancellationToken ct = default);
}

public class ParticipantRepository : IParticipantRepository
{
    private readonly ParticipantDAO _dao;
    public ParticipantRepository(ParticipantDAO dao) => _dao = dao;

    public Task<Participant?> GetSelfAsync(string userId, CancellationToken ct = default) => _dao.GetSelfAsync(userId, ct);
    public Task AddAsync(Participant participant, CancellationToken ct = default) => _dao.AddAsync(participant, ct);
}

public interface IConsentRecordRepository
{
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
}

public class ConsentRecordRepository : IConsentRecordRepository
{
    private readonly ConsentRecordDAO _dao;
    public ConsentRecordRepository(ConsentRecordDAO dao) => _dao = dao;

    public Task AddAsync(ConsentRecord record, CancellationToken ct = default) => _dao.AddAsync(record, ct);
}

public interface ILegalDocumentRepository
{
    Task<LegalDocument?> GetCurrentAsync(LegalDocType type, CancellationToken ct = default);
}

public class LegalDocumentRepository : ILegalDocumentRepository
{
    private readonly LegalDocumentDAO _dao;
    public LegalDocumentRepository(LegalDocumentDAO dao) => _dao = dao;

    public Task<LegalDocument?> GetCurrentAsync(LegalDocType type, CancellationToken ct = default) => _dao.GetCurrentAsync(type, ct);
}

public interface ISystemSettingRepository
{
    Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default);
}

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly SystemSettingDAO _dao;
    public SystemSettingRepository(SystemSettingDAO dao) => _dao = dao;

    public Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default) => _dao.GetAllAsync(ct);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditLogDAO _dao;
    public AuditLogRepository(AuditLogDAO dao) => _dao = dao;

    public Task AddAsync(AuditLog log, CancellationToken ct = default) => _dao.AddAsync(log, ct);
}

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default);
}

public class OrganizationRepository : IOrganizationRepository
{
    private readonly OrganizationDAO _dao;
    public OrganizationRepository(OrganizationDAO dao) => _dao = dao;

    public async Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default) => await _dao.FindAsync(id);
}
