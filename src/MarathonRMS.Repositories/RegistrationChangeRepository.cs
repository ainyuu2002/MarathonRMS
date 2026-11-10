using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

/// <summary>Phần A – UC-46, UC-47.</summary>
public interface IRegistrationChangeRepository
{
    Task<Registration?> GetRegistrationHeaderAsync(int registrationId, CancellationToken ct = default);
    Task<Registration?> GetRegistrationTrackedAsync(int registrationId, CancellationToken ct = default);
    Task<List<RaceCategory>> GetCategoriesWithTiersAsync(int eventId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default);
    Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, CancellationToken ct = default);
    Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, CancellationToken ct = default);
    Task<int?> GetNextFreeBibAsync(int eventId, int bibFrom, int bibTo, CancellationToken ct = default);
    Task AddRegistrationAsync(Registration registration, CancellationToken ct = default);
    Task AddPaymentAsync(Payment payment, CancellationToken ct = default);
    Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default);
    Task<AppUser?> FindActiveUserByEmailAsync(string email, CancellationToken ct = default);
    Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default);
    Task AddParticipantAsync(Participant participant, CancellationToken ct = default);
    Task<BibTransfer?> GetTrackedAsync(int transferId, CancellationToken ct = default);
    Task<BibTransfer?> GetDetailsAsync(int transferId, CancellationToken ct = default);
    Task AddAsync(BibTransfer transfer, CancellationToken ct = default);
    Task<BibTransfer?> GetPendingForRegistrationAsync(int registrationId, CancellationToken ct = default);
    Task<List<BibTransfer>> GetForUserAsync(string userId, CancellationToken ct = default);
    Task<Dictionary<string, (string Name, string Email)>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default);
}

public class RegistrationChangeRepository : IRegistrationChangeRepository
{
    private readonly RegistrationChangeDAO _dao;
    public RegistrationChangeRepository(RegistrationChangeDAO dao) => _dao = dao;

    public Task<Registration?> GetRegistrationHeaderAsync(int registrationId, CancellationToken ct = default) => _dao.GetRegistrationHeaderAsync(registrationId, ct);
    public Task<Registration?> GetRegistrationTrackedAsync(int registrationId, CancellationToken ct = default) => _dao.GetRegistrationTrackedAsync(registrationId, ct);
    public Task<List<RaceCategory>> GetCategoriesWithTiersAsync(int eventId, CancellationToken ct = default) => _dao.GetCategoriesWithTiersAsync(eventId, ct);
    public Task<Dictionary<int, int>> GetActiveCountsAsync(int eventId, CancellationToken ct = default) => _dao.GetActiveCountsAsync(eventId, ct);
    public Task<bool> ParticipantHasActiveAsync(int eventId, int participantId, CancellationToken ct = default) => _dao.ParticipantHasActiveAsync(eventId, participantId, ct);
    public Task<bool> IdNumberHasActiveAsync(int eventId, IdType idType, string idNumber, CancellationToken ct = default) => _dao.IdNumberHasActiveAsync(eventId, idType, idNumber, ct);
    public Task<int?> GetNextFreeBibAsync(int eventId, int bibFrom, int bibTo, CancellationToken ct = default) => _dao.GetNextFreeBibAsync(eventId, bibFrom, bibTo, ct);
    public Task AddRegistrationAsync(Registration registration, CancellationToken ct = default) => _dao.AddRegistrationAsync(registration, ct);
    public Task AddPaymentAsync(Payment payment, CancellationToken ct = default) => _dao.AddPaymentAsync(payment, ct);
    public Task AddConsentAsync(ConsentRecord record, CancellationToken ct = default) => _dao.AddConsentAsync(record, ct);
    public Task<AppUser?> FindActiveUserByEmailAsync(string email, CancellationToken ct = default) => _dao.FindActiveUserByEmailAsync(email, ct);
    public Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default) => _dao.GetSelfParticipantAsync(userId, ct);
    public Task AddParticipantAsync(Participant participant, CancellationToken ct = default) => _dao.AddParticipantAsync(participant, ct);
    public Task<BibTransfer?> GetTrackedAsync(int transferId, CancellationToken ct = default) => _dao.GetTrackedAsync(transferId, ct);
    public Task<BibTransfer?> GetDetailsAsync(int transferId, CancellationToken ct = default) => _dao.GetDetailsAsync(transferId, ct);
    public Task AddAsync(BibTransfer transfer, CancellationToken ct = default) => _dao.AddAsync(transfer, ct);
    public Task<BibTransfer?> GetPendingForRegistrationAsync(int registrationId, CancellationToken ct = default) => _dao.GetPendingForRegistrationAsync(registrationId, ct);
    public Task<List<BibTransfer>> GetForUserAsync(string userId, CancellationToken ct = default) => _dao.GetForUserAsync(userId, ct);
    public Task<Dictionary<string, (string Name, string Email)>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default) => _dao.GetUserNamesAsync(userIds, ct);
}
