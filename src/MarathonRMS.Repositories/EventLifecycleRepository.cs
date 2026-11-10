using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

/// <summary>Phần A – UC-25, UC-50.</summary>
public interface IEventLifecycleRepository
{
    Task<Event?> GetEventHeaderAsync(int eventId, CancellationToken ct = default);
    Task<Event?> GetEventForUpdateAsync(int eventId, CancellationToken ct = default);
    Task<List<Registration>> GetActiveRegistrationsAsync(int eventId, CancellationToken ct = default);
    Task<List<int>> GetPendingPaymentIdsAsync(int eventId, CancellationToken ct = default);
    Task<List<BibTransfer>> GetPendingTransfersAsync(int eventId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetTransferLinksAsync(int eventId, CancellationToken ct = default);
    Task<List<(int RegistrationId, decimal Amount)>> GetCategoryChangePaymentsAsync(int eventId, CancellationToken ct = default);
    Task<List<Refund>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<List<Refund>> GetForUserAsync(string userId, CancellationToken ct = default);
    Task<Refund?> GetTrackedAsync(int refundId, CancellationToken ct = default);
    Task AddAsync(Refund refund, CancellationToken ct = default);
}

public class EventLifecycleRepository : IEventLifecycleRepository
{
    private readonly EventLifecycleDAO _dao;
    public EventLifecycleRepository(EventLifecycleDAO dao) => _dao = dao;

    public Task<Event?> GetEventHeaderAsync(int eventId, CancellationToken ct = default) => _dao.GetEventHeaderAsync(eventId, ct);
    public Task<Event?> GetEventForUpdateAsync(int eventId, CancellationToken ct = default) => _dao.GetEventForUpdateAsync(eventId, ct);
    public Task<List<Registration>> GetActiveRegistrationsAsync(int eventId, CancellationToken ct = default) => _dao.GetActiveRegistrationsAsync(eventId, ct);
    public Task<List<int>> GetPendingPaymentIdsAsync(int eventId, CancellationToken ct = default) => _dao.GetPendingPaymentIdsAsync(eventId, ct);
    public Task<List<BibTransfer>> GetPendingTransfersAsync(int eventId, CancellationToken ct = default) => _dao.GetPendingTransfersAsync(eventId, ct);
    public Task<Dictionary<int, int>> GetTransferLinksAsync(int eventId, CancellationToken ct = default) => _dao.GetTransferLinksAsync(eventId, ct);
    public Task<List<(int RegistrationId, decimal Amount)>> GetCategoryChangePaymentsAsync(int eventId, CancellationToken ct = default) =>
        _dao.GetCategoryChangePaymentsAsync(eventId, ct);
    public Task<List<Refund>> GetByEventAsync(int eventId, CancellationToken ct = default) => _dao.GetByEventAsync(eventId, ct);
    public Task<List<Refund>> GetForUserAsync(string userId, CancellationToken ct = default) => _dao.GetForUserAsync(userId, ct);
    public Task<Refund?> GetTrackedAsync(int refundId, CancellationToken ct = default) => _dao.GetTrackedAsync(refundId, ct);
    public Task AddAsync(Refund refund, CancellationToken ct = default) => _dao.AddAsync(refund, ct);
}
