using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Organization?> GetTrackedAsync(int id, CancellationToken ct = default);
    IQueryable<Organization> Query();
    Task<bool> ExistsByTaxCodeAsync(string taxCode, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, int? exceptId = null, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task<int> CountEventsAsync(int organizationId, CancellationToken ct = default);
    Task AddAsync(Organization organization, CancellationToken ct = default);
}

public class OrganizationRepository : IOrganizationRepository
{
    private readonly OrganizationDAO _dao;
    public OrganizationRepository(OrganizationDAO dao) => _dao = dao;

    public async Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default) => await _dao.FindAsync(id);
    public Task<Organization?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public IQueryable<Organization> Query() => _dao.Query();
    public Task<bool> ExistsByTaxCodeAsync(string taxCode, CancellationToken ct = default) => _dao.ExistsByTaxCodeAsync(taxCode, ct);
    public Task<bool> ExistsByNameAsync(string name, int? exceptId = null, CancellationToken ct = default) => _dao.ExistsByNameAsync(name, exceptId, ct);
    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) => _dao.ExistsBySlugAsync(slug, ct);
    public Task<int> CountEventsAsync(int organizationId, CancellationToken ct = default) => _dao.CountEventsAsync(organizationId, ct);
    public Task AddAsync(Organization organization, CancellationToken ct = default) => _dao.AddAsync(organization, ct);
}

public interface IOrganizationApplicationRepository
{
    IQueryable<OrganizationApplication> QueryWithPeople();
    Task<OrganizationApplication?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task<bool> HasPendingAsync(string applicantId, CancellationToken ct = default);
    Task<bool> HasPendingWithTaxCodeAsync(string taxCode, CancellationToken ct = default);
    Task AddAsync(OrganizationApplication application, CancellationToken ct = default);
}

public class OrganizationApplicationRepository : IOrganizationApplicationRepository
{
    private readonly OrganizationApplicationDAO _dao;
    public OrganizationApplicationRepository(OrganizationApplicationDAO dao) => _dao = dao;

    public IQueryable<OrganizationApplication> QueryWithPeople() => _dao.QueryWithPeople();
    public Task<OrganizationApplication?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public Task<bool> HasPendingAsync(string applicantId, CancellationToken ct = default) => _dao.HasPendingAsync(applicantId, ct);
    public Task<bool> HasPendingWithTaxCodeAsync(string taxCode, CancellationToken ct = default) => _dao.HasPendingWithTaxCodeAsync(taxCode, ct);
    public Task AddAsync(OrganizationApplication application, CancellationToken ct = default) => _dao.AddAsync(application, ct);
}

public interface IVenueRepository
{
    IQueryable<(Venue Venue, int EventCount)> QueryWithEventCount();
    Task<Venue?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string name, string city, int? exceptId = null, CancellationToken ct = default);
    Task<bool> IsUsedByEventAsync(int venueId, CancellationToken ct = default);
    Task AddAsync(Venue venue, CancellationToken ct = default);
    void Remove(Venue venue);
}

public class VenueRepository : IVenueRepository
{
    private readonly VenueDAO _dao;
    public VenueRepository(VenueDAO dao) => _dao = dao;

    public IQueryable<(Venue Venue, int EventCount)> QueryWithEventCount() => _dao.QueryWithEventCount();
    public Task<Venue?> GetTrackedAsync(int id, CancellationToken ct = default) => _dao.GetTrackedAsync(id, ct);
    public Task<bool> ExistsAsync(string name, string city, int? exceptId = null, CancellationToken ct = default) => _dao.ExistsAsync(name, city, exceptId, ct);
    public Task<bool> IsUsedByEventAsync(int venueId, CancellationToken ct = default) => _dao.IsUsedByEventAsync(venueId, ct);
    public Task AddAsync(Venue venue, CancellationToken ct = default) => _dao.AddAsync(venue, ct);
    public void Remove(Venue venue) => _dao.Remove(venue);
}
