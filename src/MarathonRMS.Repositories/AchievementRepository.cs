using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IAchievementRepository
{
    Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default);
    Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default);
    Task<List<Result>> GetPublishedByParticipantAsync(int participantId, CancellationToken ct = default);
    Task<Result?> GetForCertificateAsync(int registrationId, CancellationToken ct = default);
    Task<int> CountFinishersAsync(int categoryId, CancellationToken ct = default);
}

public class AchievementRepository : IAchievementRepository
{
    private readonly AchievementDAO _dao;
    public AchievementRepository(AchievementDAO dao) => _dao = dao;

    public Task<Participant?> GetSelfParticipantAsync(string userId, CancellationToken ct = default) =>
        _dao.GetSelfParticipantAsync(userId, ct);
    public Task<Participant?> GetParticipantAsync(int participantId, CancellationToken ct = default) =>
        _dao.GetParticipantAsync(participantId, ct);
    public Task<List<Result>> GetPublishedByParticipantAsync(int participantId, CancellationToken ct = default) =>
        _dao.GetPublishedByParticipantAsync(participantId, ct);
    public Task<Result?> GetForCertificateAsync(int registrationId, CancellationToken ct = default) =>
        _dao.GetForCertificateAsync(registrationId, ct);
    public Task<int> CountFinishersAsync(int categoryId, CancellationToken ct = default) =>
        _dao.CountFinishersAsync(categoryId, ct);
}
