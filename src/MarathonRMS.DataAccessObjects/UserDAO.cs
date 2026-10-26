using System.Linq.Expressions;
using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Một tài khoản kèm tên các role và tên BTC – dùng cho các màn hình quản trị.</summary>
public class UserRow
{
    public AppUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public string? OrganizationName { get; set; }
}

/// <summary>
/// Truy vấn tài khoản cho màn hình quản trị. Thao tác ghi (tạo, đổi role, mật khẩu) đi qua UserManager của Identity ở tầng Service.
/// </summary>
public class UserDAO : BaseDAO<AppUser>
{
    public UserDAO(MarathonDbContext context) : base(context) { }

    /// <summary>
    /// Tài khoản kèm role, sắp theo họ tên. Lọc được áp TRƯỚC khi chiếu (để EF dịch ra SQL):
    /// filter trên AppUser, anyOfRoles = có ít nhất một trong các role.
    /// </summary>
    public IQueryable<UserRow> QueryWithRoles(Expression<Func<AppUser, bool>>? filter = null, IReadOnlyCollection<string>? anyOfRoles = null)
    {
        var users = Set.AsNoTracking();
        if (filter is not null) users = users.Where(filter);
        if (anyOfRoles is { Count: > 0 })
            users = users.Where(u => Context.UserRoles.Any(ur => ur.UserId == u.Id &&
                                     Context.Roles.Any(r => r.Id == ur.RoleId && anyOfRoles.Contains(r.Name!))));

        return users.OrderBy(u => u.FullName).Select(u => new UserRow
        {
            User = u,
            Roles = (from ur in Context.UserRoles
                     join r in Context.Roles on ur.RoleId equals r.Id
                     where ur.UserId == u.Id
                     select r.Name!).ToList(),
            OrganizationName = u.Organization != null ? u.Organization.Name : null
        });
    }

    /// <summary>Số tài khoản đang hoạt động có role (BR-35: còn ít nhất 1 SuperAdmin).</summary>
    public Task<int> CountActiveInRoleAsync(string roleName, CancellationToken ct = default) =>
        (from u in Set
         join ur in Context.UserRoles on u.Id equals ur.UserId
         join r in Context.Roles on ur.RoleId equals r.Id
         where r.Name == roleName && u.IsActive
         select u.Id).Distinct().CountAsync(ct);
}
