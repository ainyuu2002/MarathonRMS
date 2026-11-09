using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IRegistrationRepository
{
    Task<RaceCategory?> GetCategoryForRegistrationAsync(int categoryId, CancellationToken ct = default);
    Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default);
    Task<int> CountActiveInCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, int? exceptRegistrationId = null, CancellationToken ct = default);
    Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, int? exceptRegistrationId = null, CancellationToken ct = default);
    Task<int> CountActiveDependentsAsync(int eventId, string userId, CancellationToken ct = default);
    Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default);
    Task<Registration?> GetTrackedAsync(int registrationId, CancellationToken ct = default);
    Task<Registration?> GetHeaderAsync(int registrationId, CancellationToken ct = default);
    IQueryable<Registration> QueryAll();
    Task<List<(int RegistrationId, int EventId)>> GetOverdueAsync(DateTime now, int take, CancellationToken ct = default);
    Task AddAsync(Registration entity, CancellationToken ct = default);
    Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default);
}

public class RegistrationRepository : IRegistrationRepository
{
    private readonly RegistrationDAO _dao;
    public RegistrationRepository(RegistrationDAO dao) => _dao = dao;

    public Task<RaceCategory?> GetCategoryForRegistrationAsync(int categoryId, CancellationToken ct = default) =>
        _dao.GetCategoryForRegistrationAsync(categoryId, ct);
    public Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default) => _dao.GetParticipantAsync(participantId, ct);
    public Task<int> CountActiveInCategoryAsync(int categoryId, CancellationToken ct = default) => _dao.CountActiveInCategoryAsync(categoryId, ct);
    public Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, int? exceptRegistrationId = null, CancellationToken ct = default) =>
        _dao.ParticipantHasActiveAsync(eventId, participantId, exceptRegistrationId, ct);
    public Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, int? exceptRegistrationId = null,
        CancellationToken ct = default) => _dao.IdNumberHasActiveAsync(eventId, idType, idNumber, exceptRegistrationId, ct);
    public Task<int> CountActiveDependentsAsync(int eventId, string userId, CancellationToken ct = default) =>
        _dao.CountActiveDependentsAsync(eventId, userId, ct);
    public Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default) => _dao.GetMineAsync(userId, ct);
    public Task<Registration?> GetTrackedAsync(int registrationId, CancellationToken ct = default) => _dao.GetTrackedAsync(registrationId, ct);
    public Task<Registration?> GetHeaderAsync(int registrationId, CancellationToken ct = default) => _dao.GetHeaderAsync(registrationId, ct);
    public IQueryable<Registration> QueryAll() => _dao.QueryAll();
    public Task<List<(int RegistrationId, int EventId)>> GetOverdueAsync(DateTime now, int take, CancellationToken ct = default) => _dao.GetOverdueAsync(now, take, ct);
    public Task AddAsync(Registration entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
    public Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default) => _dao.AddConsentAsync(record, ct);
}

public interface IPaymentRepository
{
    Task<bool> TxnRefExistsAsync(string txnRef, CancellationToken ct = default);
    Task AddAsync(Payment entity, CancellationToken ct = default);
}

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDAO _dao;
    public PaymentRepository(PaymentDAO dao) => _dao = dao;

    public Task<bool> TxnRefExistsAsync(string txnRef, CancellationToken ct = default) => _dao.TxnRefExistsAsync(txnRef, ct);
    public Task AddAsync(Payment entity, CancellationToken ct = default) => _dao.AddAsync(entity, ct);
}
