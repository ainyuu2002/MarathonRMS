using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

public class OrganizationDAO : BaseDAO<Organization>
{
    public OrganizationDAO(MarathonDbContext context) : base(context) { }

    public Task<Organization?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(o => o.OrganizationId == id, ct);

    public Task<bool> ExistsByTaxCodeAsync(string taxCode, CancellationToken ct = default) =>
        Set.AnyAsync(o => o.TaxCode == taxCode, ct);

    public Task<bool> ExistsByNameAsync(string name, int? exceptId = null, CancellationToken ct = default) =>
        Set.AnyAsync(o => o.Name == name && o.OrganizationId != exceptId, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.AnyAsync(o => o.Slug == slug, ct);

    public Task<int> CountEventsAsync(int organizationId, CancellationToken ct = default) =>
        Context.Events.CountAsync(e => e.OrganizationId == organizationId, ct);
}

public class OrganizationApplicationDAO : BaseDAO<OrganizationApplication>
{
    public OrganizationApplicationDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Truy vấn chỉ đọc kèm người nộp, người duyệt.</summary>
    public IQueryable<OrganizationApplication> QueryWithPeople() =>
        Set.AsNoTracking().Include(a => a.Applicant).Include(a => a.ReviewedBy);

    public Task<OrganizationApplication?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Set.Include(a => a.Applicant).FirstOrDefaultAsync(a => a.ApplicationId == id, ct);

    public Task<bool> HasPendingAsync(string applicantId, CancellationToken ct = default) =>
        Set.AnyAsync(a => a.ApplicantId == applicantId && a.Status == ApplicationStatus.Pending, ct);

    public Task<bool> HasPendingWithTaxCodeAsync(string taxCode, CancellationToken ct = default) =>
        Set.AnyAsync(a => a.TaxCode == taxCode && a.Status == ApplicationStatus.Pending, ct);
}

public class VenueDAO : BaseDAO<Venue>
{
    public VenueDAO(MarathonDbContext context) : base(context) { }

    public Task<Venue?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(v => v.VenueId == id, ct);

    public Task<bool> ExistsAsync(string name, string city, int? exceptId = null, CancellationToken ct = default) =>
        Set.AnyAsync(v => v.Name == name && v.City == city && v.VenueId != exceptId, ct);

    public Task<bool> IsUsedByEventAsync(int venueId, CancellationToken ct = default) =>
        Context.Events.AnyAsync(e => e.VenueId == venueId, ct);

    /// <summary>Danh sách kèm số giải sử dụng.</summary>
    public IQueryable<(Venue Venue, int EventCount)> QueryWithEventCount() =>
        Set.AsNoTracking().Select(v => new ValueTuple<Venue, int>(v, Context.Events.Count(e => e.VenueId == v.VenueId)));
}
