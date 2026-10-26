using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Organizations;

/// <summary>UC-10 (Admin xem, sửa, tạm khóa BTC); UC-13 (Organizer mời / gỡ Staff – BR-34).</summary>
public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>> SearchAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);
    Task<OrganizationDto> GetAsync(int organizationId, CancellationToken ct = default);
    Task<OrganizationDto> UpdateAsync(int organizationId, UpdateOrganizationRequest request, CancellationToken ct = default);
    /// <summary>Tạm khóa / mở BTC. BTC bị khóa: Organizer/Staff không thao tác được trên giải (B, C kiểm tra Organization.IsActive).</summary>
    Task<OrganizationDto> SetActiveAsync(int organizationId, bool isActive, string? reason, CancellationToken ct = default);

    Task<IReadOnlyList<StaffMemberDto>> GetMyStaffAsync(CancellationToken ct = default);
    Task<StaffMemberDto> InviteStaffAsync(InviteStaffRequest request, CancellationToken ct = default);
    Task RemoveStaffAsync(string userId, CancellationToken ct = default);
}

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _userQuery;
    private readonly UserManager<AppUser> _users;
    private readonly IOrganizationScope _scope;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public OrganizationService(IOrganizationRepository organizations, IUserRepository userQuery, UserManager<AppUser> users,
        IOrganizationScope scope, IAuditLogger audit, IUnitOfWork uow)
    {
        _organizations = organizations;
        _userQuery = userQuery;
        _users = users;
        _scope = scope;
        _audit = audit;
        _uow = uow;
    }

    public Task<PagedResult<OrganizationDto>> SearchAsync(string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _organizations.Query();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(o => o.Name.Contains(k) || o.TaxCode.Contains(k) || o.ContactEmail.Contains(k));
        }
        return Project(query).OrderBy(o => o.Name).ToPagedAsync(page, pageSize, ct);
    }

    public async Task<OrganizationDto> GetAsync(int organizationId, CancellationToken ct = default) =>
        await Project(_organizations.Query().Where(o => o.OrganizationId == organizationId)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy ban tổ chức.");

    public async Task<OrganizationDto> UpdateAsync(int organizationId, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        var org = await _organizations.GetTrackedAsync(organizationId, ct) ?? throw new NotFoundException("Không tìm thấy ban tổ chức.");
        var name = request.Name.Trim();
        if (await _organizations.ExistsByNameAsync(name, organizationId, ct))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Tên ban tổ chức đã tồn tại.");
        var old = new { org.Name, org.ContactEmail, org.ContactPhone };
        org.Name = name;
        org.LogoUrl = request.LogoUrl?.Trim();
        org.Description = request.Description?.Trim();
        org.ContactEmail = request.ContactEmail.Trim();
        org.ContactPhone = request.ContactPhone?.Trim();
        await _audit.LogAsync("Organization.Update", nameof(Organization), organizationId.ToString(), old,
            new { org.Name, org.ContactEmail, org.ContactPhone }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(organizationId, ct);
    }

    public async Task<OrganizationDto> SetActiveAsync(int organizationId, bool isActive, string? reason, CancellationToken ct = default)
    {
        var org = await _organizations.GetTrackedAsync(organizationId, ct) ?? throw new NotFoundException("Không tìm thấy ban tổ chức.");
        if (org.IsActive != isActive)
        {
            org.IsActive = isActive;
            await _audit.LogAsync(isActive ? "Organization.Unlock" : "Organization.Lock", nameof(Organization),
                organizationId.ToString(), null, new { reason }, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return await GetAsync(organizationId, ct);
    }

    public async Task<IReadOnlyList<StaffMemberDto>> GetMyStaffAsync(CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        var rows = await _userQuery.QueryWithRoles(u => u.OrganizationId == orgId).ToListAsync(ct);
        return rows.Select(r => new StaffMemberDto
            {
                UserId = r.User.Id,
                FullName = r.User.FullName,
                Email = r.User.Email ?? "",
                Role = r.Roles.Contains(AppRoles.Organizer) ? AppRoles.Organizer : AppRoles.Staff,
                IsActive = r.User.IsActive
            })
            .OrderBy(s => s.Role == AppRoles.Organizer ? 0 : 1).ThenBy(s => s.FullName).ToList();
    }

    /// <summary>BR-34: chỉ mời tài khoản đã tồn tại, đang hoạt động, chưa thuộc BTC nào, không phải Admin.</summary>
    public async Task<StaffMemberDto> InviteStaffAsync(InviteStaffRequest request, CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        _scope.EnsureCanManage(orgId);
        await _scope.EnsureOrganizationActiveAsync(orgId, ct);
        var user = await _users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new BusinessRuleException("BR-34", "Không có tài khoản nào dùng email này. Người được mời cần tự đăng ký tài khoản trước.");
        if (!user.IsActive)
            throw new BusinessRuleException("BR-34", "Tài khoản này đang bị khóa.");
        if (user.OrganizationId is not null)
            throw new BusinessRuleException("BR-34", user.OrganizationId == orgId
                ? "Tài khoản đã là thành viên của ban tổ chức."
                : "Tài khoản đang thuộc một ban tổ chức khác.");
        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.SuperAdmin))
            throw new BusinessRuleException("BR-34", "Không thể mời tài khoản quản trị nền tảng làm Staff.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        user.OrganizationId = orgId;
        await _users.UpdateAsync(user);
        if (!roles.Contains(AppRoles.Staff)) await _users.AddToRoleAsync(user, AppRoles.Staff);
        if (!roles.Contains(AppRoles.Member)) await _users.AddToRoleAsync(user, AppRoles.Member);
        await _audit.LogAsync("Organization.InviteStaff", nameof(AppUser), user.Id, null, new { OrganizationId = orgId }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new StaffMemberDto { UserId = user.Id, FullName = user.FullName, Email = user.Email ?? "", Role = AppRoles.Staff, IsActive = true };
    }

    /// <summary>Gỡ Staff ⇒ bỏ role Staff và OrganizationId, tài khoản vẫn là Member (BR-34). Không gỡ Organizer bằng chức năng này.</summary>
    public async Task RemoveStaffAsync(string userId, CancellationToken ct = default)
    {
        var orgId = _scope.RequireOrganizationId();
        _scope.EnsureCanManage(orgId);
        await _scope.EnsureOrganizationActiveAsync(orgId, ct);
        var user = await _users.FindByIdAsync(userId);
        if (user is null || user.OrganizationId != orgId)
            throw new NotFoundException("Không tìm thấy thành viên trong ban tổ chức.");
        if (await _users.IsInRoleAsync(user, AppRoles.Organizer))
            throw new BusinessRuleException("BR-34", "Không thể gỡ Organizer bằng chức năng này.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        user.OrganizationId = null;
        await _users.UpdateAsync(user);
        await _users.RemoveFromRoleAsync(user, AppRoles.Staff);
        await _audit.LogAsync("Organization.RemoveStaff", nameof(AppUser), user.Id, new { OrganizationId = orgId }, null, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static IQueryable<OrganizationDto> Project(IQueryable<Organization> query) =>
        query.Select(o => new OrganizationDto
        {
            Id = o.OrganizationId,
            Name = o.Name,
            Slug = o.Slug,
            TaxCode = o.TaxCode,
            LogoUrl = o.LogoUrl,
            Description = o.Description,
            ContactEmail = o.ContactEmail,
            ContactPhone = o.ContactPhone,
            IsActive = o.IsActive,
            CreatedAt = o.CreatedAt,
            MemberCount = o.Members.Count,
            EventCount = o.Events.Count
        });
}
