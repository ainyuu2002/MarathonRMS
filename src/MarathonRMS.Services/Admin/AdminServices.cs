using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Admin;

/// <summary>
/// UC-11 (Admin khóa/mở tài khoản Member/Organizer/Staff) và UC-14 (SuperAdmin quản lý tài khoản Admin) – BR-35.
/// </summary>
public interface IUserAdminService
{
    /// <summary>Danh sách tài khoản. role = null ⇒ mọi tài khoản; "Admins" ⇒ Admin + SuperAdmin.</summary>
    Task<PagedResult<UserListItemDto>> SearchAsync(string? keyword, string? role, int page, int pageSize, CancellationToken ct = default);
    /// <summary>UC-11: không áp dụng cho Admin/SuperAdmin (dùng SetAdminActiveAsync).</summary>
    Task<UserListItemDto> SetUserActiveAsync(string userId, SetActiveRequest request, CancellationToken ct = default);

    Task<UserListItemDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default);
    Task<UserListItemDto> SetAdminActiveAsync(string userId, SetActiveRequest request, CancellationToken ct = default);
    Task<UserListItemDto> ChangeAdminRoleAsync(string userId, ChangeAdminRoleRequest request, CancellationToken ct = default);
}

public class UserAdminService : IUserAdminService
{
    public const string AdminsFilter = "Admins";

    private readonly IUserRepository _userQuery;
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public UserAdminService(IUserRepository userQuery, UserManager<AppUser> users, ICurrentUserService currentUser,
        IAuditLogger audit, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _userQuery = userQuery;
        _users = users;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _uow = uow;
    }

    public async Task<PagedResult<UserListItemDto>> SearchAsync(string? keyword, string? role, int page, int pageSize, CancellationToken ct = default)
    {
        var k = keyword?.Trim();
        System.Linq.Expressions.Expression<Func<AppUser, bool>>? filter = string.IsNullOrEmpty(k)
            ? null
            : u => u.FullName.Contains(k) || u.Email!.Contains(k);
        string[]? roles = role == AdminsFilter ? new[] { AppRoles.Admin, AppRoles.SuperAdmin }
                        : string.IsNullOrWhiteSpace(role) ? null : new[] { role };

        var paged = await _userQuery.QueryWithRoles(filter, roles).ToPagedAsync(page, pageSize, ct);
        return new PagedResult<UserListItemDto>
        {
            Items = paged.Items.Select(ToDto).ToList(), Page = paged.Page, PageSize = paged.PageSize, TotalCount = paged.TotalCount
        };
    }

    public async Task<UserListItemDto> SetUserActiveAsync(string userId, SetActiveRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        EnsureNotSelf(user);
        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.SuperAdmin))
            throw new ForbiddenException("Chỉ SuperAdmin được khóa/mở tài khoản quản trị.", "BR-35");
        return await ApplyActiveAsync(user, request, ct);
    }

    public async Task<UserListItemDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
        if (await _users.FindByEmailAsync(request.Email) is not null)
            throw new BusinessRuleException(ErrorCodes.EmailTaken, "Email đã được sử dụng.");

        var user = new AppUser
        {
            UserName = request.Email, Email = request.Email, EmailConfirmed = true,
            FullName = request.FullName.Trim(), IsActive = true, CreatedAt = _clock.Now
        };
        await using var tx = await _uow.BeginTransactionAsync(ct);
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(ErrorCodes.Validation, string.Join(" ", created.Errors.Select(e => e.Description)), 400);
        await _users.AddToRoleAsync(user, AppRoles.Admin);
        await _audit.LogAsync("Admin.Create", nameof(AppUser), user.Id, null, new { user.Email }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserListItemDto> SetAdminActiveAsync(string userId, SetActiveRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        EnsureNotSelf(user);
        var roles = await _users.GetRolesAsync(user);
        if (!roles.Contains(AppRoles.Admin) && !roles.Contains(AppRoles.SuperAdmin))
            throw new BusinessRuleException(ErrorCodes.Validation, "Tài khoản này không phải tài khoản quản trị.", 400);
        // BR-35: luôn còn ít nhất 1 SuperAdmin hoạt động
        if (!request.IsActive && roles.Contains(AppRoles.SuperAdmin) && await _userQuery.CountActiveInRoleAsync(AppRoles.SuperAdmin, ct) <= 1)
            throw new BusinessRuleException("BR-35", "Không thể khóa SuperAdmin cuối cùng.");
        return await ApplyActiveAsync(user, request, ct);
    }

    public async Task<UserListItemDto> ChangeAdminRoleAsync(string userId, ChangeAdminRoleRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        EnsureNotSelf(user);
        var roles = await _users.GetRolesAsync(user);
        var current = roles.Contains(AppRoles.SuperAdmin) ? AppRoles.SuperAdmin
                    : roles.Contains(AppRoles.Admin) ? AppRoles.Admin
                    : throw new BusinessRuleException(ErrorCodes.Validation, "Tài khoản này không phải tài khoản quản trị.", 400);
        if (current == request.Role) return await GetAsync(userId, ct);
        if (current == AppRoles.SuperAdmin && user.IsActive && await _userQuery.CountActiveInRoleAsync(AppRoles.SuperAdmin, ct) <= 1)
            throw new BusinessRuleException("BR-35", "Không thể hạ quyền SuperAdmin cuối cùng.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _users.RemoveFromRoleAsync(user, current);
        await _users.AddToRoleAsync(user, request.Role);
        await _users.UpdateSecurityStampAsync(user); // buộc đăng nhập lại để nhận quyền mới
        await _audit.LogAsync("Admin.ChangeRole", nameof(AppUser), user.Id, new { Role = current }, new { request.Role }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(userId, ct);
    }

    private async Task<UserListItemDto> ApplyActiveAsync(AppUser user, SetActiveRequest request, CancellationToken ct)
    {
        if (user.IsActive != request.IsActive)
        {
            user.IsActive = request.IsActive;
            await _users.UpdateAsync(user);
            await _users.UpdateSecurityStampAsync(user);
            await _audit.LogAsync(request.IsActive ? "User.Unlock" : "User.Lock", nameof(AppUser), user.Id, null, new { request.Reason }, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return await GetAsync(user.Id, ct);
    }

    private void EnsureNotSelf(AppUser user)
    {
        if (user.Id == _currentUser.UserId)
            throw new BusinessRuleException("BR-35", "Không thể thao tác trên chính tài khoản của mình.");
    }

    private async Task<AppUser> FindAsync(string userId) =>
        await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");

    private async Task<UserListItemDto> GetAsync(string userId, CancellationToken ct) =>
        ToDto(await _userQuery.QueryWithRoles(u => u.Id == userId).FirstAsync(ct));

    private static UserListItemDto ToDto(UserRow r) => new()
    {
        Id = r.User.Id,
        Email = r.User.Email ?? "",
        FullName = r.User.FullName,
        Roles = r.Roles,
        OrganizationId = r.User.OrganizationId,
        OrganizationName = r.OrganizationName,
        IsActive = r.User.IsActive,
        CreatedAt = r.User.CreatedAt
    };
}

/// <summary>UC-12 – địa điểm dùng chung giữa các giải (Admin, Organizer). B dùng GetAllAsync cho dropdown khi tạo giải.</summary>
public interface IVenueService
{
    Task<IReadOnlyList<VenueDto>> GetAllAsync(string? city = null, CancellationToken ct = default);
    Task<VenueDto> CreateAsync(UpsertVenueRequest request, CancellationToken ct = default);
    Task<VenueDto> UpdateAsync(int venueId, UpsertVenueRequest request, CancellationToken ct = default);
    /// <summary>BR-31: không xóa địa điểm đang được giải sử dụng.</summary>
    Task DeleteAsync(int venueId, CancellationToken ct = default);
}

public class VenueService : IVenueService
{
    private readonly IVenueRepository _venues;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public VenueService(IVenueRepository venues, IAuditLogger audit, IUnitOfWork uow)
    {
        _venues = venues;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<VenueDto>> GetAllAsync(string? city = null, CancellationToken ct = default)
    {
        var rows = await _venues.QueryWithEventCount().ToListAsync(ct);
        return rows
            .Where(r => string.IsNullOrWhiteSpace(city) || r.Venue.City.Contains(city.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Venue.City).ThenBy(r => r.Venue.Name)
            .Select(r => ToDto(r.Venue, r.EventCount)).ToList();
    }

    public async Task<VenueDto> CreateAsync(UpsertVenueRequest request, CancellationToken ct = default)
    {
        var (name, city) = (request.Name.Trim(), request.City.Trim());
        if (await _venues.ExistsAsync(name, city, null, ct))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Địa điểm này đã có trong hệ thống.");
        var venue = new Venue { Name = name, Address = request.Address.Trim(), City = city, Latitude = request.Latitude, Longitude = request.Longitude };
        await _venues.AddAsync(venue, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(venue, 0);
    }

    public async Task<VenueDto> UpdateAsync(int venueId, UpsertVenueRequest request, CancellationToken ct = default)
    {
        var venue = await _venues.GetTrackedAsync(venueId, ct) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        var (name, city) = (request.Name.Trim(), request.City.Trim());
        if (await _venues.ExistsAsync(name, city, venueId, ct))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Địa điểm này đã có trong hệ thống.");
        venue.Name = name;
        venue.Address = request.Address.Trim();
        venue.City = city;
        venue.Latitude = request.Latitude;
        venue.Longitude = request.Longitude;
        await _uow.SaveChangesAsync(ct);
        return ToDto(venue, 0);
    }

    public async Task DeleteAsync(int venueId, CancellationToken ct = default)
    {
        var venue = await _venues.GetTrackedAsync(venueId, ct) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        if (await _venues.IsUsedByEventAsync(venueId, ct))
            throw new BusinessRuleException("BR-31", "Không thể xóa địa điểm đang được giải chạy sử dụng.");
        _venues.Remove(venue);
        await _audit.LogAsync("Venue.Delete", nameof(Venue), venueId.ToString(), new { venue.Name, venue.City }, null, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static VenueDto ToDto(Venue v, int eventCount) => new()
    {
        Id = v.VenueId, Name = v.Name, Address = v.Address, City = v.City,
        Latitude = v.Latitude, Longitude = v.Longitude, EventCount = eventCount
    };
}
