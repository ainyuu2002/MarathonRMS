using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Organizations;

/// <summary>UC-08 (Member nộp hồ sơ BTC), UC-09 (Admin duyệt / từ chối) – BR-33.</summary>
public interface IOrganizationApplicationService
{
    Task<OrganizationApplicationDto> SubmitAsync(CreateOrganizationApplicationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationApplicationDto>> GetMineAsync(CancellationToken ct = default);
    Task<PagedResult<OrganizationApplicationDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<OrganizationApplicationDto> ApproveAsync(int applicationId, CancellationToken ct = default);
    Task<OrganizationApplicationDto> RejectAsync(int applicationId, RejectApplicationRequest request, CancellationToken ct = default);
}

public class OrganizationApplicationService : IOrganizationApplicationService
{
    private readonly IOrganizationApplicationRepository _applications;
    private readonly IOrganizationRepository _organizations;
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public OrganizationApplicationService(IOrganizationApplicationRepository applications, IOrganizationRepository organizations,
        UserManager<AppUser> users, ICurrentUserService currentUser, IAuditLogger audit, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _applications = applications;
        _organizations = organizations;
        _users = users;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _uow = uow;
    }

    private string UserId => _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");

    public async Task<OrganizationApplicationDto> SubmitAsync(CreateOrganizationApplicationRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(UserId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        if (user.OrganizationId is not null)
            throw new BusinessRuleException("BR-33", "Tài khoản đã thuộc một ban tổ chức nên không nộp hồ sơ mới được.");
        if (await _applications.HasPendingAsync(user.Id, ct))
            throw new BusinessRuleException("BR-33", "Bạn đã có một hồ sơ đang chờ duyệt.");
        var taxCode = request.TaxCode.Trim();
        if (await _organizations.ExistsByTaxCodeAsync(taxCode, ct))
            throw new BusinessRuleException("BR-33", "Mã số thuế này đã thuộc một ban tổ chức trên hệ thống.");
        if (await _applications.HasPendingWithTaxCodeAsync(taxCode, ct))
            throw new BusinessRuleException("BR-33", "Mã số thuế này đang có hồ sơ khác chờ duyệt.");

        var application = new OrganizationApplication
        {
            ApplicantId = user.Id,
            OrganizationName = request.OrganizationName.Trim(),
            TaxCode = taxCode,
            RepresentativeName = request.RepresentativeName.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            Address = request.Address?.Trim(),
            Description = request.Description?.Trim(),
            DocumentUrl = request.DocumentUrl?.Trim(),
            Status = ApplicationStatus.Pending,
            CreatedAt = _clock.Now
        };
        await _applications.AddAsync(application, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(application.ApplicationId, ct);
    }

    public async Task<IReadOnlyList<OrganizationApplicationDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        return await Project(_applications.QueryWithPeople().Where(a => a.ApplicantId == userId))
            .OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }

    public Task<PagedResult<OrganizationApplicationDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _applications.QueryWithPeople();
        if (Enum.TryParse<ApplicationStatus>(status, true, out var s))
            query = query.Where(a => a.Status == s);
        // Hồ sơ chờ duyệt lên trước, cũ nhất trước (xử lý theo thứ tự nộp); hồ sơ đã xử lý mới nhất trước
        var ordered = query
            .OrderBy(a => a.Status == ApplicationStatus.Pending ? 0 : 1)
            .ThenBy(a => a.Status == ApplicationStatus.Pending ? a.CreatedAt : DateTime.MaxValue)
            .ThenByDescending(a => a.ReviewedAt);
        return Project(ordered).ToPagedAsync(page, pageSize, ct);
    }

    /// <summary>BR-33: tạo BTC + thêm role Organizer + gán OrganizationId cho người nộp – trong một transaction.</summary>
    public async Task<OrganizationApplicationDto> ApproveAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await LoadPendingAsync(applicationId, ct);
        var applicant = await _users.FindByIdAsync(app.ApplicantId) ?? throw new NotFoundException("Người nộp không còn tồn tại.");
        if (applicant.OrganizationId is not null)
            throw new BusinessRuleException("BR-33", "Người nộp đã thuộc một ban tổ chức khác.");
        if (await _organizations.ExistsByTaxCodeAsync(app.TaxCode, ct))
            throw new BusinessRuleException("BR-33", "Mã số thuế đã thuộc một ban tổ chức trên hệ thống.");
        if (await _organizations.ExistsByNameAsync(app.OrganizationName, null, ct))
            throw new BusinessRuleException("BR-33", "Tên ban tổ chức đã tồn tại. Hãy từ chối và đề nghị người nộp đổi tên.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var now = _clock.Now;
        var organization = new Organization
        {
            Name = app.OrganizationName,
            Slug = await UniqueSlugAsync(app.OrganizationName, ct),
            TaxCode = app.TaxCode,
            Description = app.Description,
            ContactEmail = app.ContactEmail,
            ContactPhone = app.ContactPhone,
            IsActive = true,
            CreatedAt = now
        };
        await _organizations.AddAsync(organization, ct);
        await _uow.SaveChangesAsync(ct);

        applicant.OrganizationId = organization.OrganizationId;
        var updated = await _users.UpdateAsync(applicant);
        if (!updated.Succeeded) throw new InvalidOperationException(string.Join("; ", updated.Errors.Select(e => e.Description)));
        if (!await _users.IsInRoleAsync(applicant, AppRoles.Organizer))
            await _users.AddToRoleAsync(applicant, AppRoles.Organizer);

        app.Status = ApplicationStatus.Approved;
        app.OrganizationId = organization.OrganizationId;
        app.ReviewedById = UserId;
        app.ReviewedAt = now;
        await _audit.LogAsync("OrganizationApplication.Approve", nameof(OrganizationApplication), app.ApplicationId.ToString(),
            null, new { organization.OrganizationId, organization.Name, ApplicantId = applicant.Id }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(app.ApplicationId, ct);
    }

    public async Task<OrganizationApplicationDto> RejectAsync(int applicationId, RejectApplicationRequest request, CancellationToken ct = default)
    {
        var app = await LoadPendingAsync(applicationId, ct);
        app.Status = ApplicationStatus.Rejected;
        app.RejectReason = request.Reason.Trim();
        app.ReviewedById = UserId;
        app.ReviewedAt = _clock.Now;
        await _audit.LogAsync("OrganizationApplication.Reject", nameof(OrganizationApplication), app.ApplicationId.ToString(),
            null, new { app.RejectReason }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(app.ApplicationId, ct);
    }

    private async Task<OrganizationApplication> LoadPendingAsync(int id, CancellationToken ct)
    {
        var app = await _applications.GetTrackedAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy hồ sơ.");
        if (app.Status != ApplicationStatus.Pending)
            throw new BusinessRuleException("BR-33", "Hồ sơ đã được xử lý trước đó.");
        return app;
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = SlugHelper.ToSlug(name);
        var slug = baseSlug;
        for (var i = 2; await _organizations.ExistsBySlugAsync(slug, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    private async Task<OrganizationApplicationDto> GetAsync(int id, CancellationToken ct) =>
        await Project(_applications.QueryWithPeople().Where(a => a.ApplicationId == id)).FirstAsync(ct);

    private static IQueryable<OrganizationApplicationDto> Project(IQueryable<OrganizationApplication> query) =>
        query.Select(a => new OrganizationApplicationDto
        {
            Id = a.ApplicationId,
            ApplicantId = a.ApplicantId,
            ApplicantName = a.Applicant.FullName,
            ApplicantEmail = a.Applicant.Email ?? "",
            OrganizationName = a.OrganizationName,
            TaxCode = a.TaxCode,
            RepresentativeName = a.RepresentativeName,
            ContactEmail = a.ContactEmail,
            ContactPhone = a.ContactPhone,
            Address = a.Address,
            Description = a.Description,
            DocumentUrl = a.DocumentUrl,
            Status = a.Status.ToString(),
            RejectReason = a.RejectReason,
            CreatedAt = a.CreatedAt,
            ReviewedAt = a.ReviewedAt,
            ReviewedByName = a.ReviewedBy != null ? a.ReviewedBy.FullName : null,
            OrganizationId = a.OrganizationId
        });
}
