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
/// Mỗi giải nạp trong một transaction (lỗi giữa chừng thì không để lại giải nạp dở).
/// Giải "diễn ra hôm nay" tự dời sang ngày hiện tại mỗi lần chạy API (khi kết quả chưa công bố).
/// Lưu ý cho phần B: seeder ghi thẳng vào Registrations, Payments, PriceTiers, Checkpoints, AgeGroups –
/// đổi entity của các bảng này thì sửa seeder theo.
/// </summary>
public static class RaceDayDemoSeeder
{
    /// <summary>Giải đã đóng đăng ký, đăng ký đã xác nhận nhưng chưa có BIB – thử UC-49, UC-52, UC-53.</summary>
    public const string PrepEventSlug = "demo-ho-guom-night-run-2026";

    /// <summary>Giải diễn ra hôm nay: đã gán BIB, đã phát kit, có sẵn thời gian bấm giờ – thử UC-57 trở đi.</summary>
    public const string RaceEventSlug = "demo-cuc-phuong-trail-2026";

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
            await using var tx = await db.Database.BeginTransactionAsync();
            var count = await SeedPrepEventAsync(db, context, org, now);
            await tx.CommitAsync();
            logger.LogInformation("Đã nạp giải demo {Slug}: {Count} đăng ký đã xác nhận.", PrepEventSlug, count);
        }

        if (!await db.Events.AnyAsync(e => e.Slug == RaceEventSlug))
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var count = await SeedRaceEventAsync(db, context, org, now);
            await tx.CommitAsync();
            logger.LogInformation("Đã nạp giải demo {Slug}: {Count} VĐV có dữ liệu bấm giờ.", RaceEventSlug, count);
        }
        else
        {
            await ShiftRaceEventToTodayAsync(db, now, logger);
        }

        await SeedPrizesAsync(db, logger);
    }

    /// <summary>
    /// Cơ cấu giải thưởng mẫu cho giải demo ngày thi (UC-28): top 3 chung cuộc nam/nữ mỗi cự ly, giải nhất nhóm tuổi của 21K;
    /// mỗi VĐV chỉ nhận giải cao nhất. Chỉ nạp khi giải chưa có giải thưởng nào (DB cũ cũng được bổ sung).
    /// </summary>
    private static async Task SeedPrizesAsync(MarathonDbContext db, ILogger logger)
    {
        var ev = await db.Events.Include(e => e.Policy).Include(e => e.Categories).Include(e => e.AgeGroups)
            .FirstOrDefaultAsync(e => e.Slug == RaceEventSlug);
        if (ev is null || ev.ResultsStatus == ResultsStatus.Official ||
            await db.Prizes.AnyAsync(p => p.Category.EventId == ev.EventId)) return;

        ev.Policy.AwardStackingPolicy = AwardStackingPolicy.HighestOnly;
        var overallRewards = new[] { ("Cúp vô địch + giấy chứng nhận", 5_000_000m), ("Cúp + giấy chứng nhận", 3_000_000m), ("Cúp + giấy chứng nhận", 2_000_000m) };
        foreach (var category in ev.Categories.Where(c => c.IsTimed && c.IsRanked))
        {
            var scale = category.DistanceKm >= 20 ? 1m : 0.5m;
            foreach (var gender in new[] { Gender.M, Gender.F })
            {
                for (var rank = 1; rank <= overallRewards.Length; rank++)
                {
                    var (reward, cash) = overallRewards[rank - 1];
                    db.Prizes.Add(new Prize
                    {
                        CategoryId = category.CategoryId, AwardType = AwardType.Overall, Gender = gender,
                        Rank = rank, Reward = reward, CashAmount = cash * scale
                    });
                }
                if (category.DistanceKm < 20) continue;
                foreach (var group in ev.AgeGroups.Where(g => g.CategoryId == null))
                    db.Prizes.Add(new Prize
                    {
                        CategoryId = category.CategoryId, AwardType = AwardType.AgeGroup, Gender = gender,
                        AgeGroupId = group.AgeGroupId, Rank = 1, Reward = "Huy chương nhóm tuổi", CashAmount = 500_000
                    });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Đã nạp giải thưởng mẫu cho giải demo {Slug}.", RaceEventSlug);
    }

    /// <summary>
    /// Giải demo ngày thi được nạp với ngày thi = ngày chạy lần đầu; sang hôm sau thì dời mọi mốc thời gian (ngày thi, giờ xuất
    /// phát, cut-off trạm, thời gian bấm giờ, phát kit…) thêm đúng số ngày để giải luôn "diễn ra hôm nay".
    /// Chỉ dời khi kết quả còn Draft; kết quả nháp đã tính theo ngày cũ bị xóa để tính lại.
    /// </summary>
    private static async Task ShiftRaceEventToTodayAsync(MarathonDbContext db, DateTime now, ILogger logger)
    {
        var today = DateOnly.FromDateTime(now);
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Slug == RaceEventSlug);
        if (ev is null || ev.EventDate >= today || ev.ResultsStatus != ResultsStatus.Draft) return;

        var days = today.DayNumber - ev.EventDate.DayNumber;
        var eventId = ev.EventId;
        await using var tx = await db.Database.BeginTransactionAsync();

        ev.EventDate = ev.EventDate.AddDays(days);
        ev.EndDate = ev.EndDate.AddDays(days);
        ev.RegistrationOpenAt = ev.RegistrationOpenAt.AddDays(days);
        ev.RegistrationCloseAt = ev.RegistrationCloseAt.AddDays(days);
        await db.SaveChangesAsync();

        await db.RaceCategories.Where(c => c.EventId == eventId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.StartAt, c => c.StartAt.AddDays(days)));
        await db.PriceTiers.Where(p => p.Category.EventId == eventId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.ValidFrom, p => p.ValidFrom.AddDays(days))
                .SetProperty(p => p.ValidTo, p => p.ValidTo.AddDays(days)));
        await db.Checkpoints.Where(c => c.Category.EventId == eventId && c.CutOffAt != null)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.CutOffAt, c => c.CutOffAt!.Value.AddDays(days)));
        await db.TimingRecords.Where(t => t.Registration.EventId == eventId)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.RecordedAt, t => t.RecordedAt.AddDays(days)));
        await db.RaceKitPickups.Where(k => k.Registration.EventId == eventId)
            .ExecuteUpdateAsync(u => u.SetProperty(k => k.PickedUpAt, k => k.PickedUpAt.AddDays(days)));

        await db.ResultComplaints.Where(c => c.Result.Registration.EventId == eventId).ExecuteDeleteAsync();
        await db.ResultPenalties.Where(p => p.Result.Registration.EventId == eventId).ExecuteDeleteAsync();
        await db.Results.Where(r => r.Registration.EventId == eventId).ExecuteDeleteAsync();

        await tx.CommitAsync();
        logger.LogInformation("Đã dời giải demo {Slug} thêm {Days} ngày để ngày thi là hôm nay.", RaceEventSlug, days);
    }

    private static async Task<int> SeedRaceEventAsync(MarathonDbContext db, DemoContext ctx, Organization org, DateTime now)
    {
        var raceDay = DateOnly.FromDateTime(now);
        var dayStart = raceDay.ToDateTime(TimeOnly.MinValue);
        var openAt = now.Date.AddDays(-40);
        var closeAt = now.Date.AddDays(-3).AddHours(22);

        var ev = NewEvent(ctx, org, "Cúc Phương Trail 2026 (demo)", RaceEventSlug, "VQG Cúc Phương", raceDay, openAt, closeAt, now);
        var trail21 = NewCategory(ev, "21K Trail", 21, dayStart.AddHours(5).AddMinutes(30), 300, 18, 2001, 2300, "Xanh lá",
            650_000, openAt, closeAt, Discipline.Trail);
        AddCheckpoint(trail21, "Xuất phát", 0, 1, CheckpointType.Start);
        AddCheckpoint(trail21, "CP1 – Bản Khanh", 7, 2, CheckpointType.Split, cutOffAt: dayStart.AddHours(7).AddMinutes(30),
            station: StationType.Water);
        AddCheckpoint(trail21, "CP2 – Đỉnh Mây Bạc", 14, 3, CheckpointType.Split, cutOffAt: dayStart.AddHours(9),
            station: StationType.Full);
        AddCheckpoint(trail21, "Về đích", 21, 4, CheckpointType.Finish);
        var trail10 = NewCategory(ev, "10K Trail", 10, dayStart.AddHours(6).AddMinutes(30), 180, 15, 3001, 3300, "Vàng",
            450_000, openAt, closeAt, Discipline.Trail);
        AddCheckpoint(trail10, "Xuất phát", 0, 1, CheckpointType.Start);
        AddCheckpoint(trail10, "CP1 – Cây Chò Ngàn Năm", 5, 2, CheckpointType.Split, station: StationType.Water);
        AddCheckpoint(trail10, "Về đích", 10, 3, CheckpointType.Finish);
        foreach (var (name, min, max) in new (string Name, int Min, int? Max)[]
                     { ("18–29", 18, 29), ("30–39", 30, 39), ("40–49", 40, 49), ("50+", 50, null) })
            ev.AgeGroups.Add(new AgeGroup { Name = name, MinAge = min, MaxAge = max });
        ev.AgeGroups.Add(new AgeGroup { Name = "U18", MinAge = 0, MaxAge = 17, Category = trail10 });
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        // Mốc thời gian: số giây tính từ giờ xuất phát của cự ly, theo thứ tự checkpoint; null = không có bản ghi
        var entries21 = new (RunnerSeed Runner, double?[] Splits)[]
        {
            (new("member1@mrms.local", null, "M", new(1990, 3, 12), trail21, "0913000201", TShirtSize.L, "Hanoi Runners"), new double?[] { 12, 3258, 7690, 10905 }),
            (new("member2@mrms.local", null, "F", new(1994, 7, 2), trail21, "0913000202", TShirtSize.S), new double?[] { 25, 3930, 8410, 12020 }),
            (new("member3@mrms.local", null, "M", new(1987, 11, 25), trail21, "0913000203", TShirtSize.M, "Long Biên Running Club"), new double?[] { 9, 2985, 6850, 9640 }),
            (new("staff.hn@mrms.local", null, "F", new(1996, 1, 9), trail21, "0913000204", TShirtSize.S), new double?[] { 31, 4120, 8890, 12950 }),
            (new("organizer.sg@mrms.local", null, "M", new(1985, 5, 30), trail21, "0913000205", TShirtSize.XL), new double?[] { 15, 3405, 7920, 11410 }),
            (new("staff.sg@mrms.local", null, "F", new(1999, 10, 10), trail21, "0913000206", TShirtSize.S), new double?[] { 44, 4410, 9300, 13680 }),
            // Bỏ qua CP2 bắt buộc nhưng vẫn về đích
            (new("member1@mrms.local", "Nguyễn Văn Tuấn", "M", new(1984, 4, 4), trail21, "0913000207", TShirtSize.L), new double?[] { 18, 3300, null, 10120 }),
            // Tới CP1 sau giờ cut-off của trạm (07:30)
            (new("member1@mrms.local", "Trần Thị Mai", "F", new(1991, 6, 19), trail21, "0913000208", TShirtSize.M), new double?[] { 22, 7920, null, null }),
            // Về đích quá COT 300 phút tính theo giờ súng
            (new("member2@mrms.local", "Lê Hoàng Nam", "M", new(1978, 9, 9), trail21, "0913000209", TShirtSize.L), new double?[] { 35, 5600, 11800, 18900 }),
            // Không có thời gian nào
            (new("member2@mrms.local", "Phạm Minh Đức", "M", new(1993, 2, 2), trail21, "0913000210", TShirtSize.M), new double?[] { null, null, null, null }),
            // Qua thảm xuất phát muộn 21 phút (chính sách cho phép 15 phút)
            (new("member2@mrms.local", "Hoàng Thị Lan", "F", new(1988, 12, 12), trail21, "0913000211", TShirtSize.S), new double?[] { 1260, 4900, 9800, 14200 }),
            // Cùng giờ súng khi làm tròn giây, khác giờ chip
            (new("member3@mrms.local", "Vũ Đức Anh", "M", new(1995, 8, 1), trail21, "0913000212", TShirtSize.M), new double?[] { 40, 3150, 6400, 9310.2 }),
            (new("member3@mrms.local", "Đinh Quang Vinh", "M", new(1992, 3, 27), trail21, "0913000213", TShirtSize.L), new double?[] { 8, 3160, 6420, 9310.7 }),
            // Không có bản ghi tại thảm xuất phát
            (new("member3@mrms.local", "Ngô Thanh Hương", "F", new(1983, 10, 5), trail21, "0913000214", TShirtSize.S), new double?[] { null, 3700, 8000, 11850 }),
            (new("organizer.sg@mrms.local", "Bùi Văn Hải", "M", new(1975, 1, 15), trail21, "0913000215", TShirtSize.XL), new double?[] { 20, 3800, 8300, 12400 }),
            (new("organizer.sg@mrms.local", "Đặng Thu Trang", "F", new(1998, 5, 21), trail21, "0913000216", TShirtSize.XS, Hide: true), new double?[] { 28, 3520, 7600, 11200 }),
            (new("staff.sg@mrms.local", "Phan Văn Lộc", "M", new(1969, 7, 30), trail21, "0913000217", TShirtSize.L), new double?[] { 50, 4300, 9050, 13300 }),
            (new("staff.sg@mrms.local", "Lý Thị Hoa", "F", new(1972, 11, 3), trail21, "0913000218", TShirtSize.M), new double?[] { 33, 4000, 8700, 12800 }),
            // Còn trên đường chạy: dùng để thử nhập thời gian về đích / ghi nhận bỏ cuộc
            (new("member1@mrms.local", "Trương Minh Khang", "M", new(2000, 2, 29), trail21, "0913000219", TShirtSize.M), new double?[] { 14, 3350, 7400, null }),
            (new("member2@mrms.local", "Mai Anh Thư", "F", new(1997, 4, 14), trail21, "0913000220", TShirtSize.S), new double?[] { 38, 4200, null, null }),
        };
        var entries10 = new (RunnerSeed Runner, double?[] Splits)[]
        {
            // 16 tuổi: nhóm U18, tên được ẩn trên trang công khai
            (new("member1@mrms.local", "Hoàng Gia Huy", "M", new(2010, 6, 1), trail10, "0913000301", TShirtSize.S, Child: true), new double?[] { 10, 1650, 3600 }),
            (new("member1@mrms.local", "Nguyễn Thảo Vy", "F", new(2001, 9, 9), trail10, "0913000302", TShirtSize.XS), new double?[] { 18, 1820, 3960 }),
            (new("member2@mrms.local", "Trần Quốc Bảo", "M", new(1990, 12, 30), trail10, "0913000303", TShirtSize.L), new double?[] { 6, 1500, 3240 }),
            (new("member2@mrms.local", "Lê Thu Hằng", "F", new(1986, 3, 8), trail10, "0913000304", TShirtSize.M), new double?[] { 25, 2100, 4500 }),
            (new("member3@mrms.local", "Phạm Văn Sơn", "M", new(1965, 10, 20), trail10, "0913000305", TShirtSize.L), new double?[] { 30, 2050, 4410 }),
            (new("member3@mrms.local", "Đỗ Thị Ngọc", "F", new(1979, 1, 1), trail10, "0913000306", TShirtSize.S), new double?[] { 21, 1950, 4200 }),
            (new("organizer.sg@mrms.local", "Vũ Minh Tâm", "M", new(1999, 5, 5), trail10, "0913000307", TShirtSize.M), new double?[] { 12, null, 3500 }),
            (new("organizer.sg@mrms.local", "Hồ Thị Thanh", "F", new(1994, 8, 18), trail10, "0913000308", TShirtSize.S), new double?[] { null, null, null }),
            (new("staff.sg@mrms.local", "Kiều Văn Đạt", "M", new(1989, 4, 25), trail10, "0913000309", TShirtSize.L), new double?[] { 16, 1880, null }),
            (new("staff.sg@mrms.local", "Lâm Quỳnh Như", "F", new(2003, 7, 7), trail10, "0913000310", TShirtSize.XS), new double?[] { 19, 1700, 3700 }),
        };

        var staffId = ctx.Users["staff.hn@mrms.local"].Id;
        var organizerId = ctx.Users["organizer.hn@mrms.local"].Id;
        var registeredAt = now.Date.AddDays(-35).AddHours(8);
        var index = 0;
        foreach (var (category, entries) in new[] { (trail21, entries21), (trail10, entries10) })
        {
            var bib = category.BibFrom;
            var checkpoints = category.Checkpoints.OrderBy(c => c.Sequence).ToList();
            foreach (var (runner, splits) in entries)
            {
                index++;
                // Nhân viên không tự thao tác trên đăng ký của mình (BR-32)
                var operatorId = runner.OwnerEmail == "staff.hn@mrms.local" ? organizerId : staffId;
                var registration = await AddConfirmedRegistrationAsync(db, ctx, ev, runner, registeredAt.AddHours(index * 5), index);
                registration.BibNumber = bib++;
                registration.RaceKitPickup = new RaceKitPickup
                {
                    PickedUpAt = dayStart.AddDays(-1).AddHours(9).AddMinutes(index * 7),
                    StaffId = operatorId,
                    IdChecked = true
                };
                for (var i = 0; i < checkpoints.Count; i++)
                {
                    if (splits[i] is not double seconds) continue;
                    var jitterMs = seconds % 1 == 0 ? index * 37 % 1000 : 0;
                    registration.TimingRecords.Add(new TimingRecord
                    {
                        Checkpoint = checkpoints[i],
                        RecordedAt = category.StartAt.AddMilliseconds(Math.Round(seconds * 1000) + jitterMs),
                        Source = TimingSource.Import,
                        RecordedById = operatorId
                    });
                }
            }
        }
        await db.SaveChangesAsync();
        return index;
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
