using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Áp migration và nạp dữ liệu demo khi chạy Development (cấu hình "Seed").
/// Mật khẩu demo đọc từ cấu hình Seed:DemoPassword – chỉ dùng cho máy local.
/// Dữ liệu giải mẫu (Events, cự ly, đăng ký…) được bổ sung ở Giai đoạn 1 cùng các tính năng tương ứng.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarathonDbContext>();
        await db.Database.MigrateAsync();

        if (!config.GetValue<bool>("Seed:Enabled") || await db.Users.AnyAsync())
            return;

        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword chưa cấu hình – bỏ qua nạp tài khoản demo.");
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;

        var hanoi = new Organization
        {
            Name = "Hanoi Demo Running", Slug = "hanoi-demo-running", TaxCode = "0100000001",
            ContactEmail = "contact@hanoi-demo.local", ContactPhone = "0240000001", CreatedAt = now
        };
        var saigon = new Organization
        {
            Name = "Saigon Demo Sports", Slug = "saigon-demo-sports", TaxCode = "0300000002",
            ContactEmail = "contact@saigon-demo.local", ContactPhone = "0280000002", CreatedAt = now
        };
        db.Organizations.AddRange(hanoi, saigon);
        db.Venues.AddRange(
            new Venue { Name = "Hồ Hoàn Kiếm", Address = "Đinh Tiên Hoàng, Hoàn Kiếm", City = "Hà Nội", Latitude = 21.0285m, Longitude = 105.8524m },
            new Venue { Name = "Phú Mỹ Hưng", Address = "Nguyễn Văn Linh, Quận 7", City = "TP. Hồ Chí Minh", Latitude = 10.7290m, Longitude = 106.7180m },
            new Venue { Name = "Biển Mỹ Khê", Address = "Võ Nguyên Giáp, Sơn Trà", City = "Đà Nẵng", Latitude = 16.0544m, Longitude = 108.2470m },
            new Venue { Name = "VQG Cúc Phương", Address = "Nho Quan", City = "Ninh Bình", Latitude = 20.3170m, Longitude = 105.6000m });
        await db.SaveChangesAsync();

        var accounts = new (string Email, string Name, string[] Roles, Organization? Org)[]
        {
            ("superadmin@mrms.local", "Super Admin", new[] { AppRoles.SuperAdmin }, null),
            ("admin@mrms.local", "Platform Admin", new[] { AppRoles.Admin }, null),
            ("organizer.hn@mrms.local", "Nguyễn Văn Tổ Chức", new[] { AppRoles.Member, AppRoles.Organizer }, hanoi),
            ("staff.hn@mrms.local", "Trần Thị Nhân Viên", new[] { AppRoles.Member, AppRoles.Staff }, hanoi),
            ("organizer.sg@mrms.local", "Lê Minh Ban Tổ Chức", new[] { AppRoles.Member, AppRoles.Organizer }, saigon),
            ("staff.sg@mrms.local", "Phạm Thu Tình Nguyện", new[] { AppRoles.Member, AppRoles.Staff }, saigon),
            ("member1@mrms.local", "Hoàng Văn Chạy", new[] { AppRoles.Member }, null),
            ("member2@mrms.local", "Đỗ Thị Bền Bỉ", new[] { AppRoles.Member }, null),
            ("member3@mrms.local", "Vũ Quốc Tốc Độ", new[] { AppRoles.Member }, null),
        };

        foreach (var (email, name, roles, org) in accounts)
        {
            var user = new AppUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = name,
                OrganizationId = org?.OrganizationId, CreatedAt = now, IsActive = true
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seed {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
            await users.AddToRolesAsync(user, roles);

            if (roles.Contains(AppRoles.Member))
            {
                db.Participants.Add(new Participant
                {
                    OwnerUserId = user.Id, LinkedUserId = user.Id, Relationship = ParticipantRelationship.Self,
                    FullName = name, CreatedAt = now
                });
                db.ConsentRecords.AddRange(
                    new ConsentRecord { UserId = user.Id, ConsentType = ConsentType.Terms, DocumentVersion = 1, Granted = true, CreatedAt = now },
                    new ConsentRecord { UserId = user.Id, ConsentType = ConsentType.Privacy, DocumentVersion = 1, Granted = true, CreatedAt = now });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Đã nạp dữ liệu demo: {Count} tài khoản, 2 BTC, 4 địa điểm.", accounts.Length);
    }
}
