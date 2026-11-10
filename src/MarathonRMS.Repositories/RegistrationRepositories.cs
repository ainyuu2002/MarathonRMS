using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IRegistrationRepository
{
    Task<RaceCategory?> GetCategoryForRegistrationAsync(int categoryId, CancellationToken ct = default);
    Task<int> CountActiveAsync(int categoryId, CancellationToken ct = default);
    Task<bool> HasActiveForParticipantAsync(int eventId, int participantId, CancellationToken ct = default);
    Task<bool> HasActiveForIdAsync(int eventId, IdType? idType, string? idNumber, CancellationToken ct = default);
    Task<bool> HasActiveForIdExceptAsync(int eventId, IdType? idType, string? idNumber, int exceptRegistrationId,
        CancellationToken ct = default);
    Task<Registration?> GetTrackedWithDetailsAsync(int registrationId, CancellationToken ct = default);
    Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default);
    IQueryable<Registration> QueryWithDetails();
    Task<List<int>> GetOverdueIdsAsync(DateTime now, CancellationToken ct = default);
    Task<List<Registration>> GetByEventTrackedAsync(int eventId, CancellationToken ct = default);
    Task AddAsync(Registration registration, CancellationToken ct = default);
}

public class RegistrationRepository : IRegistrationRepository
{
    private readonly RegistrationDAO _dao;
    public RegistrationRepository(RegistrationDAO dao) => _dao = dao;
    public Task<RaceCategory?> GetCategoryForRegistrationAsync(int id, CancellationToken ct = default) => _dao.GetCategoryForRegistrationAsync(id, ct);
    public Task<int> CountActiveAsync(int id, CancellationToken ct = default) => _dao.CountActiveAsync(id, ct);
    public Task<bool> HasActiveForParticipantAsync(int eventId, int participantId, CancellationToken ct = default) => _dao.HasActiveForParticipantAsync(eventId, participantId, ct);
    public Task<bool> HasActiveForIdAsync(int eventId, IdType? type, string? number, CancellationToken ct = default) => _dao.HasActiveForIdAsync(eventId, type, number, ct);
    public Task<bool> HasActiveForIdExceptAsync(int eventId, IdType? type, string? number, int exceptId,
        CancellationToken ct = default) => _dao.HasActiveForIdExceptAsync(eventId, type, number, exceptId, ct);
    public Task<Registration?> GetTrackedWithDetailsAsync(int id, CancellationToken ct = default) => _dao.GetTrackedWithDetailsAsync(id, ct);
    public Task<List<Registration>> GetMineAsync(string userId, CancellationToken ct = default) => _dao.GetMineAsync(userId, ct);
    public IQueryable<Registration> QueryWithDetails() => _dao.QueryWithDetails();
    public Task<List<int>> GetOverdueIdsAsync(DateTime now, CancellationToken ct = default) => _dao.GetOverdueIdsAsync(now, ct);
    public Task<List<Registration>> GetByEventTrackedAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventTrackedAsync(eventId, ct);
    public Task AddAsync(Registration registration, CancellationToken ct = default) => _dao.AddAsync(registration, ct);
}

public interface IPaymentRepository
{
    Task<bool> HasSuccessfulAsync(int registrationId, PaymentPurpose purpose, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
}

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDAO _dao;
    public PaymentRepository(PaymentDAO dao) => _dao = dao;
    public Task<bool> HasSuccessfulAsync(int id, PaymentPurpose purpose, CancellationToken ct = default) => _dao.HasSuccessfulAsync(id, purpose, ct);
    public Task AddAsync(Payment payment, CancellationToken ct = default) => _dao.AddAsync(payment, ct);
}
