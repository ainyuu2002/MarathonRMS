using System.Security.Cryptography;
using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Dữ liệu demo phần C – ngày thi (chỉ khi Seed:Enabled, tức môi trường Development).
/// Chạy sau DbInitializer; mỗi giải demo chỉ nạp một lần theo slug nên chạy lại API không tạo trùng.
/// </summary>
public static class RaceDayDemoSeeder
{
    /// <summary>Giải đã đóng đăng ký, đăng ký đã xác nhận nhưng chưa có BIB – thử UC-49, UC-52, UC-53.</summary>
    public const string PrepEventSlug = "demo-ho-guom-night-run-2026";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        if (!config.GetValue<bool>("Seed:Enabled")) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarathonDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Slug == "hanoi-demo-running");
        if (org is null) return; // chưa có dữ liệu demo nền (tài khoản, BTC)

        var context = await DemoContext.LoadAsync(db);
        if (context is null) return;

        if (!await db.Events.AnyAsync(e => e.Slug == PrepEventSlug))
        {
            var count = await SeedPrepEventAsync(db, context, org, now);
            logger.LogInformation("Đã nạp giải demo {Slug}: {Count} đăng ký đã xác nhận.", PrepEventSlug, count);
        }
    }

    private static async Task<int> SeedPrepEventAsync(MarathonDbContext db, DemoContext ctx, Organization org, DateTime now)
    {
        var eventDate = DateOnly.FromDateTime(now).AddDays(10);
        var openAt = now.Date.AddDays(-30);
        var closeAt = now.Date.AddDays(-1).AddHours(22);

        var ev = NewEvent(ctx, org, "Hồ Gươm Night Run 2026 (demo)", PrepEventSlug, "Hồ Hoàn Kiếm", eventDate, openAt, closeAt, now);
        var run10 = NewCategory(ev, "10K", 10, eventDate.ToDateTime(new TimeOnly(19, 0)), 90, 16, 1001, 1300, "Xanh dương", 350_000, openAt, closeAt);
        AddCheckpoint(run10, "Xuất phát", 0, 1, CheckpointType.Start);
        AddCheckpoint(run10, "Km 5 – Đền Ngọc Sơn", 5, 2, CheckpointType.Split);
        AddCheckpoint(run10, "Về đích", 10, 3, CheckpointType.Finish);
        var run5 = NewCategory(ev, "5K", 5, eventDate.ToDateTime(new TimeOnly(19, 30)), 60, 10, 5001, 5300, "Cam", 250_000, openAt, closeAt);
        AddCheckpoint(run5, "Xuất phát", 0, 1, CheckpointType.Start);
        AddCheckpoint(run5, "Về đích", 5, 2, CheckpointType.Finish);
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var runners = new RunnerSeed[]
        {
            new("member1@mrms.local", null, "M", new(1990, 3, 12), run10, "0912000101", TShirtSize.L, "Hanoi Runners"),
            new("member2@mrms.local", null, "F", new(1994, 7, 2), run10, "0912000102", TShirtSize.S),
            new("member3@mrms.local", null, "M", new(1987, 11, 25), run10, "0912000103", TShirtSize.M, "Long Biên Running Club"),
            // Staff của BTC tự đăng ký chạy – dùng để thử BR-32 (không tự phát kit cho mình)
            new("staff.hn@mrms.local", null, "F", new(1996, 1, 9), run10, "0912000104", TShirtSize.S),
            new("organizer.sg@mrms.local", null, "M", new(1985, 5, 30), run10, "0912000105", TShirtSize.XL),
            new("member1@mrms.local", "Nguyễn Thu Hà", "F", new(1992, 9, 18), run10, "0912000106", TShirtSize.S),
            new("member1@mrms.local", "Phạm Quang Huy", "M", new(1989, 2, 14), run10, "0912000107", TShirtSize.L),
            new("member2@mrms.local", "Bùi Ngọc Ánh", "F", new(2000, 12, 1), run10, "0912000108", TShirtSize.XS, Hide: true),
            new("member2@mrms.local", "Đặng Minh Khoa", "M", new(1998, 4, 22), run10, "0912000109", TShirtSize.M),
            new("member3@mrms.local", "Lý Thanh Tâm", "F", new(1979, 8, 8), run10, "0912000110", TShirtSize.M),
            new("member3@mrms.local", "Trịnh Văn Long", "M", new(1975, 6, 16), run10, "0912000111", TShirtSize.L),
            new("staff.sg@mrms.local", null, "F", new(1999, 10, 10), run5, "0912000112", TShirtSize.S),
            new("member1@mrms.local", "Hoàng Gia Bảo", "M", new(2011, 5, 3), run5, "0912000113", TShirtSize.S, Child: true),
            // Trẻ em chưa có đồng ý của người giám hộ – quầy race kit phải chặn
            new("member2@mrms.local", "Đỗ Minh Anh", "F", new(2012, 2, 27), run5, "0912000114", TShirtSize.XS, Child: true, GuardianConsent: false),
            new("member3@mrms.local", "Vũ Thị Lan", "F", new(1965, 3, 3), run5, "0912000115", TShirtSize.M),
            new("member1@mrms.local", "Ngô Đức Thắng", "M", new(2003, 7, 7), run5, "0912000116", TShirtSize.L, "FPT Runners"),
        };

        var registeredAt = now.Date.AddDays(-25).AddHours(9);
        for (var i = 0; i < runners.Length; i++)
            await AddConfirmedRegistrationAsync(db, ctx, ev, runners[i], registeredAt.AddHours(i * 7), i + 1);
        await db.SaveChangesAsync();
        return runners.Length;
    }

    // ============================ Dựng dữ liệu ============================

    private sealed record RunnerSeed(string OwnerEmail, string? DependentName, string Gender, DateOnly DateOfBirth,
        RaceCategory Category, string Phone, TShirtSize TShirtSize, string? Club = null, bool Hide = false,
        bool Child = false, bool GuardianConsent = true);

    /// <summary>Tài khoản demo nền (DbInitializer) cần cho dữ liệu ngày thi.</summary>
    private sealed class DemoContext
    {
        public Dictionary<string, AppUser> Users { get; private init; } = new();
        public Dictionary<string, Participant> SelfProfiles { get; private init; } = new();
        public List<Venue> Venues { get; private init; } = new();

        public static async Task<DemoContext?> LoadAsync(MarathonDbContext db)
        {
            var users = await db.Users.Where(u => u.Email != null && u.Email.EndsWith("@mrms.local"))
                .ToDictionaryAsync(u => u.Email!);
            if (!users.ContainsKey("organizer.hn@mrms.local")) return null;
            var ids = users.Values.Select(u => u.Id).ToList();
            var selves = await db.Participants
                .Where(p => p.Relationship == ParticipantRelationship.Self && ids.Contains(p.OwnerUserId))
                .ToListAsync();
            return new DemoContext
            {
                Users = users,
                SelfProfiles = selves.GroupBy(p => p.OwnerUserId).ToDictionary(g => g.Key, g => g.First()),
                Venues = await db.Venues.ToListAsync()
            };
        }
    }

    private static Event NewEvent(DemoContext ctx, Organization org, string name, string slug, string venueName,
        DateOnly eventDate, DateTime openAt, DateTime closeAt, DateTime now) => new()
    {
        OrganizationId = org.OrganizationId,
        VenueId = (ctx.Venues.FirstOrDefault(v => v.Name == venueName) ?? ctx.Venues.First()).VenueId,
        Name = name,
        Slug = slug,
        DescriptionHtml = "<p>Giải chạy dùng để thử các chức năng ngày thi: gán BIB, phát race kit, bấm giờ và kết quả.</p>",
        WaiverText = "Tôi tự nguyện tham gia, đã kiểm tra sức khỏe và tuân thủ hướng dẫn của ban tổ chức.",
        WaiverVersion = 1,
        EventDate = eventDate,
        EndDate = eventDate,
        RegistrationOpenAt = openAt,
        RegistrationCloseAt = closeAt,
        Status = EventStatus.RegistrationClosed,
        ResultsStatus = ResultsStatus.Draft,
        EmergencyPhone = "0240000115",
        CreatedById = ctx.Users["organizer.hn@mrms.local"].Id,
        CreatedAt = openAt.AddDays(-7),
        Policy = new EventPolicy
        {
            GuardianConsentUnderAge = 18,
            OverallRankingBasis = RankingBasis.GunTime,
            AgeGroupRankingBasis = RankingBasis.ChipTime,
            TieBreakRule = TieBreakRule.ChipTime,
            LateStartToleranceMinutes = 15,
            ComplaintWindowMinutes = 10
        }
    };

    private static RaceCategory NewCategory(Event ev, string name, decimal distanceKm, DateTime startAt, int cutOffMinutes,
        int minAge, int bibFrom, int bibTo, string bibColor, decimal price, DateTime openAt, DateTime closeAt,
        Discipline discipline = Discipline.Road)
    {
        var category = new RaceCategory
        {
            Name = name,
            Discipline = discipline,
            DistanceKm = distanceKm,
            Capacity = 300,
            StartAt = startAt,
            CutOffMinutes = cutOffMinutes,
            MinAge = minAge,
            IsTimed = true,
            IsRanked = true,
            BibFrom = bibFrom,
            BibTo = bibTo,
            BibColor = bibColor
        };
        category.PriceTiers.Add(new PriceTier { Name = "Giá thường", Price = price, ValidFrom = openAt, ValidTo = closeAt });
        ev.Categories.Add(category);
        return category;
    }

    private static Checkpoint AddCheckpoint(RaceCategory category, string name, decimal distanceKm, int sequence,
        CheckpointType type, bool mandatory = true, DateTime? cutOffAt = null, StationType station = StationType.None)
    {
        var checkpoint = new Checkpoint
        {
            Name = name,
            DistanceKm = distanceKm,
            Sequence = sequence,
            Type = type,
            IsMandatory = mandatory,
            CutOffAt = cutOffAt,
            StationType = station
        };
        category.Checkpoints.Add(checkpoint);
        return checkpoint;
    }

    private static async Task<Registration> AddConfirmedRegistrationAsync(MarathonDbContext db, DemoContext ctx, Event ev,
        RunnerSeed seed, DateTime registeredAt, int index)
    {
        var owner = ctx.Users[seed.OwnerEmail];
        Participant participant;
        if (seed.DependentName is null)
        {
            participant = ctx.SelfProfiles[owner.Id];
        }
        else
        {
            participant = new Participant
            {
                OwnerUserId = owner.Id,
                Relationship = seed.Child ? ParticipantRelationship.Child : ParticipantRelationship.Relative,
                FullName = seed.DependentName,
                DateOfBirth = seed.DateOfBirth,
                Gender = Enum.Parse<Gender>(seed.Gender),
                Nationality = "Việt Nam",
                Phone = seed.Phone,
                TShirtSize = seed.TShirtSize,
                CreatedAt = registeredAt.AddMinutes(-10)
            };
            db.Participants.Add(participant);
            await db.SaveChangesAsync();
        }

        var fullName = seed.DependentName ?? owner.FullName;
        var price = seed.Category.PriceTiers.First().Price;
        var registration = new Registration
        {
            EventId = ev.EventId,
            CategoryId = seed.Category.CategoryId,
            ParticipantId = participant.ParticipantId,
            RegisteredByUserId = owner.Id,
            Status = RegistrationStatus.Confirmed,
            OriginalPrice = price,
            FinalPrice = price,
            QrToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            RegisteredAt = registeredAt,
            ConfirmedAt = registeredAt.AddMinutes(3),
            HideFromPublicList = seed.Hide,
            FullName = fullName,
            BibName = BibNameOf(fullName),
            DateOfBirth = seed.DateOfBirth,
            Gender = Enum.Parse<Gender>(seed.Gender),
            IdType = IdType.CCCD,
            IdNumber = $"001{seed.DateOfBirth.Year % 100:D2}{ev.EventId % 1000:D3}{index:D4}",
            Nationality = "Việt Nam",
            Phone = seed.Phone,
            Email = seed.DependentName is null ? owner.Email : null,
            TShirtSize = seed.TShirtSize,
            EmergencyContactName = seed.DependentName is null ? "Người thân" : owner.FullName,
            EmergencyContactPhone = "0988000" + index.ToString("D3"),
            ClubName = seed.Club,
            GuardianName = seed.Child ? owner.FullName : null,
            GuardianPhone = seed.Child ? "0988000" + index.ToString("D3") : null,
            GuardianConsent = seed.Child && seed.GuardianConsent,
            WaiverAccepted = true,
            HealthDeclarationAccepted = true
        };
        registration.Payments.Add(new Payment
        {
            Purpose = PaymentPurpose.Registration,
            Provider = PaymentProvider.Mock,
            Amount = price,
            Status = PaymentStatus.Success,
            TxnRef = "DEMO-" + Guid.NewGuid().ToString("N"),
            CreatedAt = registeredAt,
            PaidAt = registeredAt.AddMinutes(3)
        });
        db.Registrations.Add(registration);
        return registration;
    }

    /// <summary>Tên in BIB: tên gọi (từ cuối), tối đa 16 ký tự.</summary>
    private static string BibNameOf(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length >= 2 ? $"{parts[^2]} {parts[^1]}" : fullName;
        if (name.Length > 16) name = parts[^1];
        if (name.Length > 16) name = name[..16];
        return name.ToUpperInvariant();
    }
}
