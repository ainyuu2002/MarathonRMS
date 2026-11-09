using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;
using static MarathonRMS.DataAccessObjects.DashboardDAO;

namespace MarathonRMS.Repositories;

/// <summary>Phần A – UC-69, UC-71.</summary>
public interface IDashboardRepository
{
    Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<List<CategoryRow>> GetCategoriesAsync(int eventId, CancellationToken ct = default);
    Task<List<RegistrationRow>> GetRegistrationRowsAsync(int eventId, CancellationToken ct = default);
    Task<List<ResultRow>> GetResultRowsAsync(int eventId, CancellationToken ct = default);
    Task<List<PaymentRow>> GetSuccessfulPaymentsAsync(int? eventId, DateTime? from = null, CancellationToken ct = default);
    Task<decimal> GetRefundedAsync(int? eventId, CancellationToken ct = default);
    Task<(int Total, int Locked)> GetUserCountsAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetUsersByRoleAsync(CancellationToken ct = default);
    Task<(int Active, int Locked, int PendingApplications)> GetOrganizationCountsAsync(CancellationToken ct = default);
    Task<Dictionary<EventStatus, int>> GetEventsByStatusAsync(CancellationToken ct = default);
    Task<int> CountActiveRegistrationsAsync(CancellationToken ct = default);
    Task<List<DateTime>> GetRegistrationDatesAsync(DateTime from, CancellationToken ct = default);
    Task<(int Count, decimal Amount)> GetPendingRefundsAsync(CancellationToken ct = default);
}

public class DashboardRepository : IDashboardRepository
{
    private readonly DashboardDAO _dao;
    public DashboardRepository(DashboardDAO dao) => _dao = dao;

    public Task<Event?> GetEventAsync(int eventId, CancellationToken ct = default) => _dao.GetEventAsync(eventId, ct);
    public Task<List<CategoryRow>> GetCategoriesAsync(int eventId, CancellationToken ct = default) => _dao.GetCategoriesAsync(eventId, ct);
    public Task<List<RegistrationRow>> GetRegistrationRowsAsync(int eventId, CancellationToken ct = default) => _dao.GetRegistrationRowsAsync(eventId, ct);
    public Task<List<ResultRow>> GetResultRowsAsync(int eventId, CancellationToken ct = default) => _dao.GetResultRowsAsync(eventId, ct);
    public Task<List<PaymentRow>> GetSuccessfulPaymentsAsync(int? eventId, DateTime? from = null, CancellationToken ct = default) =>
        _dao.GetSuccessfulPaymentsAsync(eventId, from, ct);
    public Task<decimal> GetRefundedAsync(int? eventId, CancellationToken ct = default) => _dao.GetRefundedAsync(eventId, ct);
    public Task<(int Total, int Locked)> GetUserCountsAsync(CancellationToken ct = default) => _dao.GetUserCountsAsync(ct);
    public Task<Dictionary<string, int>> GetUsersByRoleAsync(CancellationToken ct = default) => _dao.GetUsersByRoleAsync(ct);
    public Task<(int Active, int Locked, int PendingApplications)> GetOrganizationCountsAsync(CancellationToken ct = default) =>
        _dao.GetOrganizationCountsAsync(ct);
    public Task<Dictionary<EventStatus, int>> GetEventsByStatusAsync(CancellationToken ct = default) => _dao.GetEventsByStatusAsync(ct);
    public Task<int> CountActiveRegistrationsAsync(CancellationToken ct = default) => _dao.CountActiveRegistrationsAsync(ct);
    public Task<List<DateTime>> GetRegistrationDatesAsync(DateTime from, CancellationToken ct = default) => _dao.GetRegistrationDatesAsync(from, ct);
    public Task<(int Count, decimal Amount)> GetPendingRefundsAsync(CancellationToken ct = default) => _dao.GetPendingRefundsAsync(ct);
}
