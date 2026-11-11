using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Bộ dữ liệu mẫu lớn để demo / chấm bài (chỉ khi Seed:Enabled và Seed:SampleData – môi trường Development):
/// thêm 4 BTC, ~330 tài khoản VĐV (kèm người phụ thuộc), 17 giải trải đủ mọi trạng thái (nháp → kết quả chính thức,
/// hoãn, hủy + hoàn tiền), ~2.600 đăng ký, bấm giờ, kết quả, phạt, khiếu nại, giải thưởng, mã giảm giá, chuyển nhượng,
/// hồ sơ đăng ký BTC và nhật ký thao tác.
/// Chạy sau DbInitializer và RaceDayDemoSeeder; chỉ nạp một lần (dấu hiệu: BTC <see cref="MarkerOrgSlug"/>), trong một
/// transaction. Ngày tháng tính theo ngày chạy lần đầu nên giải "sắp tới" / "đã qua" luôn đúng nghĩa lúc nạp.
/// Kết quả được tính bằng chính IResultRecalculation của hệ thống nên thứ hạng khớp với khi BTC bấm "Tính kết quả".
/// Mật khẩu mọi tài khoản mẫu = Seed:DemoPassword.
/// </summary>
public static class SampleDataSeeder
{
    public const string MarkerOrgSlug = "danang-coastal-runners";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        if (!config.GetValue<bool>("Seed:Enabled") || !config.GetValue<bool>("Seed:SampleData")) return;
        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarathonDbContext>();
            if (!await db.Organizations.AnyAsync(o => o.Slug == "hanoi-demo-running")) return;
            if (!await db.Organizations.AnyAsync(o => o.Slug == MarkerOrgSlug))
            {
                logger.LogInformation("Đang nạp dữ liệu mẫu (chỉ lần đầu, khoảng 1–2 phút)...");
                var watch = Stopwatch.StartNew();
                var builder = new Builder(scope.ServiceProvider, db, password);
                await using var tx = await db.Database.BeginTransactionAsync();
                await builder.RunAsync();
                await tx.CommitAsync();
                logger.LogInformation("Đã nạp dữ liệu mẫu: {Summary} ({Seconds:F0} giây).", await builder.SummaryAsync(), watch.Elapsed.TotalSeconds);
            }
        }

        // Ảnh chạy mỗi lần khởi động: điền ảnh còn thiếu, chép lại file đã bị xóa khỏi wwwroot/uploads
        await SampleImageSeeder.EnsureAsync(services, logger);
    }

    // ============================ Đặc tả dữ liệu ============================

    private enum Phase { Draft, Published, Open, Closed, Postponed, Cancelled, RaceDone, Provisional, Official }

    private sealed record CpSpec(string Name, decimal Km, int? CutOffAfterStartMinutes = null, StationType Station = StationType.Water);

    private sealed record CatSpec(string Name, decimal Km, int StartHour, int StartMinute, int? CutOff, int MinAge, int BibFrom,
        string Color, decimal Price, double Share, Discipline Discipline = Discipline.Road, CategoryType Type = CategoryType.Standard,
        int? MaxAge = null, int? Elevation = null, byte? Itra = null, CpSpec[]? Checkpoints = null, int? EscortUnder = null)
    {
        public bool Timed => Type != CategoryType.Kids;
    }

    private sealed record CouponSpec(string Code, DiscountType Type, decimal Value, decimal? Max, int MaxUses,
        string? Partner = null, bool EarlyOnly = false, bool Active = true);

    private sealed record EventSpec(string Org, string Name, string Slug, string Venue, int DayOffset, Phase Phase,
        int Registrations, string Theme, CatSpec[] Categories, string Blurb, string[]? Featured = null,
        TransferPolicy Transfer = TransferPolicy.Forbidden, decimal ComplaintFee = 0, CouponSpec[]? Coupons = null,
        string? CancelReason = null);

    private static CatSpec Road(string name, decimal km, int h, int m, int cutOff, int minAge, int bibFrom, string color,
        decimal price, double share) => new(name, km, h, m, cutOff, minAge, bibFrom, color, price, share);

    private static CatSpec Trail(string name, decimal km, int h, int m, int cutOff, int minAge, int bibFrom, string color,
        decimal price, double share, int elevation, byte? itra, params CpSpec[] cps) =>
        new(name, km, h, m, cutOff, minAge, bibFrom, color, price, share, Discipline.Trail, Elevation: elevation, Itra: itra,
            Checkpoints: cps);

    private static CatSpec Kids(string name, decimal km, int h, int m, int bibFrom, decimal price, double share) =>
        new(name, km, h, m, null, 6, bibFrom, "Hồng", price, share, Type: CategoryType.Kids, MaxAge: 12, EscortUnder: 10);

    private static IEnumerable<EventSpec> EventSpecs() => new[]
    {
        // ---------- Đã kết thúc, kết quả chính thức (năm trước – tạo lịch sử thành tích nhiều mùa) ----------
        new EventSpec("hanoi-demo-running", "Hà Nội Marathon Mùa Thu 2025", "ha-noi-marathon-mua-thu-2025", "Hồ Tây", -420,
            Phase.Official, 200, "#C0392B",
            new[]
            {
                Road("42K", 42.2m, 4, 30, 420, 18, 1001, "Đỏ", 900_000, 0.25),
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 650_000, 0.40),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Xanh lá", 450_000, 0.35),
            },
            "Cung đường vòng quanh Hồ Tây lộng gió mùa thu, đi qua đường Thanh Niên, Lạc Long Quân và làng hoa Nhật Tân.",
            Featured: new[] { "member1@mrms.local=42K", "member3@mrms.local=21K" }),
        new EventSpec("danang-coastal-runners", "Đà Nẵng Coastal Half Marathon 2025", "da-nang-coastal-half-marathon-2025",
            "Biển Mỹ Khê", -406, Phase.Official, 150, "#1A73E8",
            new[]
            {
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 600_000, 0.45),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 400_000, 0.35),
                Road("5K", 5, 6, 0, 60, 10, 5001, "Cam", 250_000, 0.20),
            },
            "Chạy dọc bờ biển Mỹ Khê lúc bình minh, qua cầu Rồng và cầu Trần Thị Lý.",
            Featured: new[] { "member2@mrms.local=21K" }),
        new EventSpec("saigon-demo-sports", "Sài Gòn Night Run 2025", "sai-gon-night-run-2025", "Phú Mỹ Hưng", -364,
            Phase.Official, 200, "#8E44AD",
            new[]
            {
                Road("10K", 10, 19, 30, 100, 14, 1001, "Tím", 420_000, 0.55),
                Road("5K", 5, 20, 0, 60, 10, 5001, "Trắng", 280_000, 0.45),
            },
            "Đường chạy đêm rực ánh đèn trong khu đô thị Phú Mỹ Hưng, về đích tại hồ Bán Nguyệt.",
            Featured: new[] { "member1@mrms.local=10K", "member2@mrms.local=5K" }),

        // ---------- Đã kết thúc, kết quả chính thức (năm nay) ----------
        new EventSpec("hanoi-demo-running", "Hà Nội Marathon Mùa Thu 2026", "ha-noi-marathon-mua-thu-2026", "Hồ Tây", -56,
            Phase.Official, 300, "#C0392B",
            new[]
            {
                Road("42K", 42.2m, 4, 30, 420, 18, 1001, "Đỏ", 950_000, 0.25),
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 690_000, 0.35),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Xanh lá", 480_000, 0.25),
                Road("5K", 5, 6, 0, 60, 10, 5001, "Cam", 300_000, 0.15),
            },
            "Mùa giải thứ hai trên cung đường Hồ Tây – Nhật Tân, có thêm cự ly 5K cho người mới chạy.",
            Featured: new[] { "member1@mrms.local=42K", "member2@mrms.local=21K", "member3@mrms.local=10K" },
            Coupons: new[] { new CouponSpec("HNAUTUMN-EARLY", DiscountType.Percent, 15, 150_000, 60, EarlyOnly: true) }),
        new EventSpec("danang-coastal-runners", "Đà Nẵng Coastal Half Marathon 2026", "da-nang-coastal-half-marathon-2026",
            "Biển Mỹ Khê", -42, Phase.Official, 220, "#1A73E8",
            new[]
            {
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 650_000, 0.45),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 420_000, 0.35),
                Road("5K", 5, 6, 0, 60, 10, 5001, "Cam", 260_000, 0.20),
            },
            "Bán marathon ven biển đẹp nhất miền Trung: Mỹ Khê – Sơn Trà – cầu Rồng.",
            Featured: new[] { "member1@mrms.local=21K" }),
        new EventSpec("highland-trail-vn", "Dalat Ultra Trail 2026", "dalat-ultra-trail-2026", "Hồ Xuân Hương", -28,
            Phase.Official, 180, "#27AE60",
            new[]
            {
                Trail("70K Ultra", 70, 3, 0, 1200, 21, 7001, "Đen", 2_400_000, 0.20, 3600, 4,
                    new CpSpec("CP1 – Langbiang", 15, 300, StationType.Full),
                    new CpSpec("CP2 – Thung lũng Vàng", 32, 600, StationType.Full),
                    new CpSpec("CP3 – Hồ Tuyền Lâm", 50, 900, StationType.Full),
                    new CpSpec("CP4 – Đồi chè Cầu Đất", 61, 1070, StationType.Water)),
                Trail("42K Trail", 42.2m, 5, 0, 720, 18, 4001, "Cam", 1_500_000, 0.35, 2100, 3,
                    new CpSpec("CP1 – Đồi Robin", 12, 200),
                    new CpSpec("CP2 – Thác Datanla", 25, 420, StationType.Full),
                    new CpSpec("CP3 – Rừng thông Prenn", 34, 580)),
                Trail("21K Trail", 21.1m, 6, 30, 420, 16, 2001, "Xanh lá", 850_000, 0.45, 900, 1,
                    new CpSpec("CP1 – Đồi Cù", 10, 210, StationType.Full)),
            },
            "Giải chạy địa hình qua rừng thông, đồi chè và đỉnh Langbiang – có điểm ITRA cho cự ly 21K trở lên.",
            Featured: new[] { "member3@mrms.local=42K Trail", "member2@mrms.local=21K Trail" }, ComplaintFee: 200_000),
        new EventSpec("mekong-sports-events", "Mekong Delta Marathon 2026", "mekong-delta-marathon-2026", "Bến Ninh Kiều", -14,
            Phase.Official, 240, "#F39C12",
            new[]
            {
                Road("42K", 42.2m, 4, 30, 420, 18, 1001, "Đỏ", 850_000, 0.20),
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 600_000, 0.35),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 380_000, 0.35),
                Kids("Kids 2K", 2, 7, 30, 9001, 150_000, 0.10),
            },
            "Chạy qua chợ nổi Cái Răng, cầu Cần Thơ và những vườn trái cây miệt vườn – có cự ly Kids cho cả gia đình.",
            Featured: new[] { "member2@mrms.local=10K" }),

        // ---------- Vừa thi xong ----------
        new EventSpec("danang-coastal-runners", "Huế Heritage Run 2026", "hue-heritage-run-2026", "Đại Nội Huế", -2,
            Phase.Provisional, 150, "#9B2335",
            new[]
            {
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Đỏ", 550_000, 0.40),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 380_000, 0.35),
                Road("5K", 5, 6, 0, 60, 10, 5001, "Trắng", 220_000, 0.25),
            },
            "Chạy quanh Kinh thành Huế, dọc sông Hương và cầu Trường Tiền – kết quả đang công bố tạm thời.",
            Featured: new[] { "member1@mrms.local=21K" }, ComplaintFee: 100_000),
        new EventSpec("hanoi-demo-running", "Sa Pa Mountain Trail 2026", "sa-pa-mountain-trail-2026", "Thị xã Sa Pa", -1,
            Phase.RaceDone, 100, "#16A085",
            new[]
            {
                Trail("25K Trail", 25, 6, 0, 540, 18, 2501, "Xanh lá", 1_100_000, 0.55, 1500, 2,
                    new CpSpec("CP1 – Bản Cát Cát", 9, 210),
                    new CpSpec("CP2 – Thác Bạc", 17, 390, StationType.Full)),
                Trail("12K Trail", 12, 7, 0, 270, 14, 1201, "Vàng", 650_000, 0.45, 600, null,
                    new CpSpec("CP1 – Bản Tả Van", 6, 150)),
            },
            "Giải trail ruộng bậc thang Sa Pa – đã có dữ liệu bấm giờ, BTC chưa tính kết quả (thử UC-59).",
            Featured: new[] { "member3@mrms.local=25K Trail" }),

        // ---------- Sắp diễn ra ----------
        new EventSpec("mekong-sports-events", "Cần Thơ Family Run 2026", "can-tho-family-run-2026", "Bến Ninh Kiều", 9,
            Phase.Closed, 80, "#E67E22",
            new[]
            {
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 300_000, 0.40),
                Road("5K", 5, 6, 0, 60, 8, 5001, "Cam", 200_000, 0.40),
                Kids("Kids 2K", 2, 7, 0, 9001, 120_000, 0.20),
            },
            "Ngày hội chạy bộ gia đình bên bến Ninh Kiều – đã đóng đăng ký, đã gán BIB, chuẩn bị phát race kit."),
        new EventSpec("danang-coastal-runners", "Đà Nẵng Bridge Night Run 2026", "da-nang-bridge-night-run-2026", "Cầu Rồng", 20,
            Phase.Cancelled, 120, "#2C3E50",
            new[]
            {
                Road("10K", 10, 19, 0, 100, 14, 1001, "Xanh dương", 400_000, 0.60),
                Road("5K", 5, 19, 30, 60, 10, 5001, "Vàng", 250_000, 0.40),
            },
            "Đường chạy đêm qua 4 cây cầu biểu tượng của Đà Nẵng.",
            Featured: new[] { "member2@mrms.local=10K" },
            CancelReason: "Bão số 13 đổ bộ miền Trung đúng tuần thi đấu, chính quyền thành phố không cấp phép tổ chức. BTC hoàn 100% lệ phí."),
        new EventSpec("hanoi-demo-running", "Hạ Long Bay Marathon 2026", "ha-long-bay-marathon-2026", "Bãi Cháy", 26,
            Phase.Open, 260, "#0E6655",
            new[]
            {
                Road("42K", 42.2m, 4, 30, 420, 18, 1001, "Đỏ", 1_050_000, 0.20),
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 750_000, 0.35),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Xanh lá", 500_000, 0.30),
                Road("5K", 5, 6, 0, 60, 10, 5001, "Cam", 300_000, 0.15),
            },
            "Cung đường ven vịnh Hạ Long, qua cầu Bãi Cháy và đường bao biển Trần Quốc Nghiễn.",
            Featured: new[] { "member1@mrms.local=21K", "member3@mrms.local=42K" }, Transfer: TransferPolicy.Paid,
            Coupons: new[]
            {
                new CouponSpec("HALONG-EARLY", DiscountType.Percent, 15, 150_000, 50, EarlyOnly: true),
                new CouponSpec("HALONG50K", DiscountType.Amount, 50_000, null, 200),
                new CouponSpec("HANOIRUNNERS", DiscountType.Amount, 100_000, null, 30, Partner: "CLB Hanoi Runners"),
                new CouponSpec("HALONG-VIP", DiscountType.Percent, 50, 500_000, 10, Active: false),
            }),
        new EventSpec("hanoi-demo-running", "Hà Nội Heritage 10K 2026", "ha-noi-heritage-10k-2026", "Hồ Hoàn Kiếm", 5,
            Phase.Postponed, 90, "#B7950B",
            new[]
            {
                Road("10K", 10, 6, 0, 100, 14, 1001, "Vàng", 350_000, 0.65),
                Road("5K", 5, 6, 30, 60, 10, 5001, "Trắng", 220_000, 0.35),
            },
            "Chạy qua phố cổ, Nhà hát Lớn và Hoàng thành Thăng Long – đã hoãn 30 ngày do trùng lịch sự kiện của thành phố."),
        new EventSpec("saigon-demo-sports", "Sài Gòn River Run 2026", "sai-gon-river-run-2026", "Bến Bạch Đằng", 40,
            Phase.Open, 200, "#2471A3",
            new[]
            {
                Road("21K", 21.1m, 4, 45, 210, 16, 2001, "Xanh dương", 700_000, 0.40),
                Road("10K", 10, 5, 15, 100, 14, 3001, "Xanh lá", 450_000, 0.35),
                Road("5K", 5, 5, 45, 60, 10, 5001, "Cam", 280_000, 0.25),
            },
            "Chạy dọc sông Sài Gòn, qua cầu Thủ Thiêm và hầm Thủ Thiêm lúc bình minh.",
            Featured: new[] { "member3@mrms.local=21K", "member2@mrms.local=10K" }, Transfer: TransferPolicy.Free,
            Coupons: new[]
            {
                new CouponSpec("SGRIVER10", DiscountType.Percent, 10, 80_000, 100),
                new CouponSpec("SAIGONRUNNERS", DiscountType.Amount, 70_000, null, 40, Partner: "Saigon Runners Club"),
            }),
        new EventSpec("danang-coastal-runners", "Hội An Lantern Night Run 2026", "hoi-an-lantern-night-run-2026", "Phố cổ Hội An", 55,
            Phase.Open, 110, "#D35400",
            new[]
            {
                Road("10K", 10, 18, 30, 100, 14, 1001, "Đỏ", 420_000, 0.55),
                Road("5K", 5, 19, 0, 60, 10, 5001, "Vàng", 260_000, 0.45),
            },
            "Chạy đêm giữa phố cổ lung linh đèn lồng, qua chùa Cầu và bờ sông Hoài.",
            Coupons: new[] { new CouponSpec("HOIAN-LANTERN", DiscountType.Percent, 20, 60_000, 50) }),
        new EventSpec("highland-trail-vn", "Dalat Pine Forest Trail 2027", "dalat-pine-forest-trail-2027", "Hồ Xuân Hương", 95,
            Phase.Published, 0, "#1E8449",
            new[]
            {
                Trail("50K Trail", 50, 4, 0, 840, 21, 5001, "Đen", 1_800_000, 0.30, 2500, 3,
                    new CpSpec("CP1 – Thác Prenn", 12, 240), new CpSpec("CP2 – Hồ Tuyền Lâm", 25, 480, StationType.Full),
                    new CpSpec("CP3 – Đồi Mộng Mơ", 38, 690)),
                Trail("25K Trail", 25, 6, 0, 480, 18, 2501, "Cam", 1_000_000, 0.40, 1100, 2,
                    new CpSpec("CP1 – Rừng thông Prenn", 12, 240, StationType.Full)),
                Trail("12K Trail", 12, 7, 0, 240, 14, 1201, "Xanh lá", 550_000, 0.30, 450, null),
            },
            "Mùa giải 2027 – đã công bố, đăng ký mở sau 10 ngày nữa."),
        new EventSpec("saigon-demo-sports", "Vũng Tàu Beach Run 2027", "vung-tau-beach-run-2027", "Bãi Sau", 120,
            Phase.Draft, 0, "#5DADE2",
            new[]
            {
                Road("21K", 21.1m, 5, 0, 210, 16, 2001, "Xanh dương", 600_000, 0.50),
                Road("10K", 10, 5, 30, 100, 14, 3001, "Vàng", 400_000, 0.50),
            },
            "Bản nháp: chưa chọn cơ sở xếp hạng, cự ly 10K chưa có mức giá – dùng để thử kiểm tra BR-38 khi mở đăng ký."),
    };

    // ============================ Dựng dữ liệu ============================

    private sealed class OrgInfo
    {
        public int Id { get; init; }
        public string Slug { get; init; } = "";
        public string Name { get; init; } = "";
        public string OrganizerId { get; set; } = "";
        public List<string> StaffIds { get; } = new();
    }

    /// <summary>Một người chạy trong "kho" VĐV mẫu (chủ tài khoản hoặc người phụ thuộc).</summary>
    private sealed class Runner
    {
        public required Participant Profile { get; init; }
        public required string OwnerName { get; init; }
        public required string OwnerPhone { get; init; }
        public string? OwnerIdNumber { get; init; }
        public string? Email { get; init; }
        /// <summary>Hệ số thể lực: &lt; 1 nhanh hơn trung bình, giữ nguyên qua mọi giải để thành tích nhất quán.</summary>
        public double Ability { get; init; }
        public bool IsDependent => Profile.Relationship != ParticipantRelationship.Self;
    }

    private sealed record Entry(Registration Reg, Runner Runner, CatSpec Spec, RaceCategory Category);

    private sealed class Builder
    {
        private const int MemberCount = 320;
        private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly MarathonDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly IResultRepository _resultRepo;
        private readonly IResultRecalculation _recalc;
        private readonly string _passwordHash;
        private readonly Random _rng = new(20261111);
        private readonly DateTime _now;
        private readonly DateOnly _today;

        private readonly List<Runner> _adults = new();
        private readonly List<Runner> _kids = new();
        private readonly Dictionary<string, OrgInfo> _orgs = new();
        private readonly Dictionary<string, int> _venues = new();
        private readonly Dictionary<int, int> _couponUses = new();
        private readonly List<Coupon> _platformCoupons = new();
        private string _adminId = "";
        private string _superAdminId = "";
        private int _idSeq = 100_000;

        public Builder(IServiceProvider sp, MarathonDbContext db, string password)
        {
            _db = db;
            _users = sp.GetRequiredService<UserManager<AppUser>>();
            _resultRepo = sp.GetRequiredService<IResultRepository>();
            _recalc = sp.GetRequiredService<IResultRecalculation>();
            _now = sp.GetRequiredService<IDateTimeProvider>().Now;
            _today = DateOnly.FromDateTime(_now);
            _passwordHash = _users.PasswordHasher.HashPassword(new AppUser(), password);
        }

        public async Task RunAsync()
        {
            await LoadExistingAsync();
            await CreateVenuesAsync();
            await CreateOrganizationsAsync();
            await CreateMembersAsync();
            await CreateApplicationsAsync();
            await CreatePlatformCouponsAsync();
            foreach (var spec in EventSpecs())
                await CreateEventAsync(spec);
            foreach (var (couponId, uses) in _couponUses)
                await _db.Coupons.Where(c => c.CouponId == couponId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, uses));
        }

        public async Task<string> SummaryAsync() =>
            $"{await _db.Users.CountAsync()} tài khoản, {await _db.Organizations.CountAsync()} BTC, " +
            $"{await _db.Events.CountAsync()} giải, {await _db.Registrations.CountAsync()} đăng ký, " +
            $"{await _db.TimingRecords.CountAsync()} lượt bấm giờ, {await _db.Results.CountAsync()} kết quả";

        // ---------------------------- Nền ----------------------------

        private async Task LoadExistingAsync()
        {
            var demo = await _db.Users.Where(u => u.Email != null && u.Email.EndsWith("@mrms.local")).ToDictionaryAsync(u => u.Email!);
            _adminId = demo["admin@mrms.local"].Id;
            _superAdminId = demo["superadmin@mrms.local"].Id;

            foreach (var org in await _db.Organizations.ToListAsync())
                _orgs[org.Slug] = new OrgInfo { Id = org.OrganizationId, Slug = org.Slug, Name = org.Name };
            _orgs["hanoi-demo-running"].OrganizerId = demo["organizer.hn@mrms.local"].Id;
            _orgs["hanoi-demo-running"].StaffIds.Add(demo["staff.hn@mrms.local"].Id);
            _orgs["saigon-demo-sports"].OrganizerId = demo["organizer.sg@mrms.local"].Id;
            _orgs["saigon-demo-sports"].StaffIds.Add(demo["staff.sg@mrms.local"].Id);

            foreach (var venue in await _db.Venues.ToListAsync()) _venues[venue.Name] = venue.VenueId;

            // Ba tài khoản member demo: bổ sung hồ sơ để có lịch sử thi đấu nhiều giải (trang "Thành tích của tôi")
            var featured = new (string Email, DateOnly Dob, Gender Gender, string? Club, double Ability, TShirtSize Size)[]
            {
                ("member1@mrms.local", new DateOnly(1990, 3, 12), Gender.M, "Hanoi Runners", 0.82, TShirtSize.L),
                ("member2@mrms.local", new DateOnly(1994, 7, 2), Gender.F, null, 0.93, TShirtSize.S),
                ("member3@mrms.local", new DateOnly(1987, 11, 25), Gender.M, "Long Biên Running Club", 0.76, TShirtSize.M),
            };
            foreach (var (email, dob, gender, club, ability, size) in featured)
            {
                if (!demo.TryGetValue(email, out var user)) continue;
                var self = await _db.Participants.FirstOrDefaultAsync(p =>
                    p.OwnerUserId == user.Id && p.Relationship == ParticipantRelationship.Self && !p.IsDeleted);
                if (self is null) continue;
                if (self.DateOfBirth is null)
                {
                    self.DateOfBirth = dob;
                    self.Gender = gender;
                    self.Nationality = "Việt Nam";
                    self.IdType = IdType.CCCD;
                    self.IdNumber = Cccd("001", gender, dob);
                    self.Phone = Phone();
                    self.TShirtSize = size;
                    self.ClubName = club;
                    self.EmergencyContactName = VietName(Gender.F);
                    self.EmergencyContactPhone = Phone();
                }
                _adults.Add(new Runner
                {
                    Profile = self, OwnerName = user.FullName, OwnerPhone = self.Phone ?? Phone(),
                    OwnerIdNumber = self.IdNumber, Email = email, Ability = ability
                });
            }
            await _db.SaveChangesAsync();
        }

        private async Task CreateVenuesAsync()
        {
            var venues = new Venue[]
            {
                new() { Name = "Hồ Tây", Address = "Đường Thanh Niên, Tây Hồ", City = "Hà Nội", Latitude = 21.0560m, Longitude = 105.8220m },
                new() { Name = "Cầu Rồng", Address = "Bạch Đằng, Hải Châu", City = "Đà Nẵng", Latitude = 16.0610m, Longitude = 108.2270m },
                new() { Name = "Phố cổ Hội An", Address = "Trần Phú, Minh An", City = "Hội An", Latitude = 15.8770m, Longitude = 108.3260m },
                new() { Name = "Hồ Xuân Hương", Address = "Trần Quốc Toản, Phường 1", City = "Đà Lạt", Latitude = 11.9420m, Longitude = 108.4420m },
                new() { Name = "Bến Ninh Kiều", Address = "Hai Bà Trưng, Ninh Kiều", City = "Cần Thơ", Latitude = 10.0340m, Longitude = 105.7880m },
                new() { Name = "Đại Nội Huế", Address = "Đường 23/8, Phú Hậu", City = "Huế", Latitude = 16.4690m, Longitude = 107.5770m },
                new() { Name = "Bãi Sau", Address = "Thùy Vân, Phường 2", City = "Vũng Tàu", Latitude = 10.3400m, Longitude = 107.0960m },
                new() { Name = "Bãi Cháy", Address = "Đường Hạ Long, Bãi Cháy", City = "Hạ Long", Latitude = 20.9550m, Longitude = 107.0440m },
                new() { Name = "Thị xã Sa Pa", Address = "Quảng trường Sa Pa", City = "Lào Cai", Latitude = 22.3360m, Longitude = 103.8440m },
                new() { Name = "Bến Bạch Đằng", Address = "Tôn Đức Thắng, Quận 1", City = "TP. Hồ Chí Minh", Latitude = 10.7740m, Longitude = 106.7060m },
            };
            foreach (var venue in venues.Where(v => !_venues.ContainsKey(v.Name))) _db.Venues.Add(venue);
            await _db.SaveChangesAsync();
            foreach (var venue in await _db.Venues.ToListAsync()) _venues[venue.Name] = venue.VenueId;
        }

        private async Task CreateOrganizationsAsync()
        {
            var specs = new (string Name, string Slug, string Tax, string Phone, string Desc, bool Active, int AgeDays,
                (string Email, string Name, string Role)[] People)[]
            {
                ("Danang Coastal Runners", MarkerOrgSlug, "0400000003", "02363000003",
                    "Đơn vị tổ chức các giải chạy ven biển miền Trung: Đà Nẵng, Hội An, Huế.", true, 520,
                    new[] { ("organizer.dn@mrms.local", "Trương Công Hải", AppRoles.Organizer),
                            ("staff.dn@mrms.local", "Phan Thị Hồng Nhung", AppRoles.Staff),
                            ("staff2.dn@mrms.local", "Lê Đức Trí", AppRoles.Staff) }),
                ("Highland Trail Việt Nam", "highland-trail-vn", "5800000004", "02633000004",
                    "Chuyên tổ chức giải chạy địa hình (trail, ultra trail) tại Tây Nguyên – thành viên ITRA.", true, 300,
                    new[] { ("organizer.dl@mrms.local", "K'Brông Thị Lan", AppRoles.Organizer),
                            ("staff.dl@mrms.local", "Nguyễn Hoàng Lâm", AppRoles.Staff) }),
                ("Mekong Sports Events", "mekong-sports-events", "1800000005", "02923000005",
                    "Sự kiện thể thao cộng đồng tại đồng bằng sông Cửu Long.", true, 260,
                    new[] { ("organizer.ct@mrms.local", "Huỳnh Văn Tâm", AppRoles.Organizer),
                            ("staff.ct@mrms.local", "Lâm Ngọc Hân", AppRoles.Staff) }),
                ("Phan Thiết Beach Run", "phan-thiet-beach-run", "3400000006", "02523000006",
                    "BTC tạm khóa do vi phạm cam kết hoàn tiền mùa giải trước (dùng để thử BTC bị khóa).", false, 400,
                    new[] { ("organizer.pt@mrms.local", "Đoàn Minh Phát", AppRoles.Organizer) }),
            };

            foreach (var spec in specs)
            {
                var createdAt = _now.AddDays(-spec.AgeDays);
                var org = new Organization
                {
                    Name = spec.Name, Slug = spec.Slug, TaxCode = spec.Tax, Description = spec.Desc,
                    ContactEmail = $"contact@{spec.Slug}.local", ContactPhone = spec.Phone, IsActive = spec.Active, CreatedAt = createdAt
                };
                _db.Organizations.Add(org);
                await _db.SaveChangesAsync();
                var info = new OrgInfo { Id = org.OrganizationId, Slug = org.Slug, Name = org.Name };
                _orgs[org.Slug] = info;

                foreach (var (email, name, role) in spec.People)
                {
                    var user = await CreateUserAsync(email, name, new[] { AppRoles.Member, role }, org.OrganizationId, createdAt.AddHours(2));
                    AddMinimalSelfProfile(user, createdAt.AddHours(2));
                    if (role == AppRoles.Organizer) info.OrganizerId = user.Id;
                    else info.StaffIds.Add(user.Id);
                }
                if (info.StaffIds.Count == 0) info.StaffIds.Add(info.OrganizerId);
            }

            // Thêm nhân viên thứ hai cho BTC Hà Nội (giải đông người)
            var hanoi = _orgs["hanoi-demo-running"];
            var staff2 = await CreateUserAsync("staff2.hn@mrms.local", "Ngô Thị Thu Hà", new[] { AppRoles.Member, AppRoles.Staff },
                hanoi.Id, _now.AddDays(-150));
            AddMinimalSelfProfile(staff2, _now.AddDays(-150));
            hanoi.StaffIds.Add(staff2.Id);
            await _db.SaveChangesAsync();
        }

        private async Task CreateMembersAsync()
        {
            for (var i = 1; i <= MemberCount; i++)
            {
                var gender = _rng.NextDouble() < 0.62 ? Gender.M : Gender.F;
                var foreign = _rng.NextDouble() < 0.04 ? Foreigners.Where(f => f.Gender == gender).ElementAt(_rng.Next(5)) : default;
                var name = foreign.Name ?? VietName(gender);
                var email = $"runner{i:D3}@mrms.local";
                var active = i <= MemberCount - 2; // 2 tài khoản cuối bị khóa – thử trang quản lý người dùng
                var createdAt = _now.AddDays(-_rng.Next(30, 900)).AddMinutes(-_rng.Next(0, 1440));
                var user = await CreateUserAsync(email, name, new[] { AppRoles.Member }, null, createdAt, active);

                var profile = NewProfile(user.Id, ParticipantRelationship.Self, name, gender, AdultDob(),
                    foreign.Nationality ?? "Việt Nam", createdAt, adult: true);
                user.PhoneNumber = profile.Phone;
                _db.Participants.Add(profile);
                _db.ConsentRecords.AddRange(
                    Consent(user.Id, ConsentType.Terms, true, createdAt),
                    Consent(user.Id, ConsentType.Privacy, true, createdAt));
                if (_rng.NextDouble() < 0.75) _db.ConsentRecords.Add(Consent(user.Id, ConsentType.PublicListing, true, createdAt));
                if (profile.BloodType is not null || profile.MedicalNotes is not null)
                    _db.ConsentRecords.Add(Consent(user.Id, ConsentType.HealthData, true, createdAt));

                var owner = new Runner
                {
                    Profile = profile, OwnerName = name, OwnerPhone = profile.Phone!, OwnerIdNumber = profile.IdNumber,
                    Email = email, Ability = Ability()
                };
                if (active) _adults.Add(owner);

                // Người phụ thuộc (không có tài khoản): 35 trẻ em, 15 người thân
                if (i > 50 || !active) continue;
                var child = i <= 35;
                var depGender = _rng.NextDouble() < 0.5 ? Gender.M : Gender.F;
                var depName = $"{name.Split(' ')[0]} {MiddleAndGiven(depGender)}";
                var depDob = child
                    ? _today.AddYears(-_rng.Next(6, 13)).AddDays(-_rng.Next(0, 365))
                    : AdultDob();
                var dep = NewProfile(user.Id, child ? ParticipantRelationship.Child : ParticipantRelationship.Relative,
                    depName, depGender, depDob, "Việt Nam", createdAt.AddDays(_rng.Next(1, 20)), adult: !child);
                dep.LinkedUserId = null;
                _db.Participants.Add(dep);
                var depRunner = new Runner
                {
                    Profile = dep, OwnerName = name, OwnerPhone = profile.Phone!, OwnerIdNumber = profile.IdNumber,
                    Ability = Ability()
                };
                (child ? _kids : _adults).Add(depRunner);
            }
            await _db.SaveChangesAsync();
        }

        private async Task CreateApplicationsAsync()
        {
            var organizers = new[] { ("danang-coastal-runners", "Đà Nẵng"), ("highland-trail-vn", "Đà Lạt"),
                ("mekong-sports-events", "Cần Thơ"), ("phan-thiet-beach-run", "Phan Thiết") };
            foreach (var (slug, city) in organizers)
            {
                var org = await _db.Organizations.SingleAsync(o => o.Slug == slug);
                var organizerId = _orgs[slug].OrganizerId;
                var applicant = await _db.Users.SingleAsync(u => u.Id == organizerId);
                _db.OrganizationApplications.Add(new OrganizationApplication
                {
                    ApplicantId = applicant.Id, OrganizationName = org.Name, TaxCode = org.TaxCode,
                    RepresentativeName = applicant.FullName, ContactEmail = org.ContactEmail, ContactPhone = org.ContactPhone ?? Phone(),
                    Address = $"Số {_rng.Next(1, 300)} đường Trần Phú, {city}", Description = org.Description,
                    Status = ApplicationStatus.Approved, ReviewedById = _adminId,
                    CreatedAt = org.CreatedAt.AddDays(-4), ReviewedAt = org.CreatedAt, OrganizationId = org.OrganizationId
                });
                Audit(_adminId, AppRoles.Admin, "OrganizationApplication.Approve", nameof(OrganizationApplication), org.Slug,
                    new { Status = "Pending" }, new { Status = "Approved", org.Name }, org.CreatedAt);
            }

            var members = _adults.Where(r => !r.IsDependent && r.Email!.StartsWith("runner")).ToList();
            var pending = new[]
            {
                ("Quy Nhơn Runners", "4100000101", "Tổ chức giải chạy ven biển Quy Nhơn, dự kiến 2 giải/năm."),
                ("Nha Trang Sea Run", "4200000102", "Công ty sự kiện thể thao tại Khánh Hòa, đã tổ chức 3 giải phong trào."),
                ("Tây Ninh Mountain Race", "3900000103", "Giải leo núi – chạy địa hình núi Bà Đen."),
            };
            for (var i = 0; i < pending.Length; i++)
            {
                var m = members[4 + i * 7];
                _db.OrganizationApplications.Add(new OrganizationApplication
                {
                    ApplicantId = m.Profile.OwnerUserId, OrganizationName = pending[i].Item1, TaxCode = pending[i].Item2,
                    RepresentativeName = m.OwnerName, ContactEmail = m.Email!, ContactPhone = m.OwnerPhone,
                    Address = "Đang cập nhật", Description = pending[i].Item3, Status = ApplicationStatus.Pending,
                    CreatedAt = _now.AddDays(-i - 1).AddHours(-_rng.Next(1, 10))
                });
            }

            var rejected = new[]
            {
                ("Hà Giang Loop Trail", "0500000104", "Mã số thuế không khớp giấy chứng nhận đăng ký doanh nghiệp, vui lòng nộp lại."),
                ("CLB Chạy bộ Ba Đình", "0100000105", "Hồ sơ là câu lạc bộ phong trào, chưa có tư cách pháp nhân để tổ chức giải."),
            };
            for (var i = 0; i < rejected.Length; i++)
            {
                var m = members[40 + i * 5];
                var createdAt = _now.AddDays(-30 - i * 12);
                _db.OrganizationApplications.Add(new OrganizationApplication
                {
                    ApplicantId = m.Profile.OwnerUserId, OrganizationName = rejected[i].Item1, TaxCode = rejected[i].Item2,
                    RepresentativeName = m.OwnerName, ContactEmail = m.Email!, ContactPhone = m.OwnerPhone,
                    Description = "Đăng ký làm ban tổ chức trên MRMS.", Status = ApplicationStatus.Rejected,
                    RejectReason = rejected[i].Item3, ReviewedById = _adminId, CreatedAt = createdAt, ReviewedAt = createdAt.AddDays(2)
                });
                Audit(_adminId, AppRoles.Admin, "OrganizationApplication.Reject", nameof(OrganizationApplication), rejected[i].Item2,
                    new { Status = "Pending" }, new { Status = "Rejected", Reason = rejected[i].Item3 }, createdAt.AddDays(2));
            }
            await _db.SaveChangesAsync();
        }

        private async Task CreatePlatformCouponsAsync()
        {
            var welcome = new Coupon
            {
                Code = "MRMS-WELCOME", DiscountType = DiscountType.Amount, DiscountValue = 30_000, MaxUses = 500,
                ValidFrom = _now.AddDays(-120), ValidTo = _now.AddDays(90), PartnerName = "Nền tảng MRMS",
                CreatedById = _adminId, CreatedAt = _now.AddDays(-121)
            };
            _db.Coupons.Add(welcome);
            _platformCoupons.Add(welcome);

            // Mã cá nhân tặng member1 (OwnerMemberId) – hiện ở "Mã giảm giá của tôi"
            var member1 = _adults.FirstOrDefault(r => r.Email == "member1@mrms.local");
            if (member1 is not null)
                _db.Coupons.Add(new Coupon
                {
                    Code = "MEMBER1-THANKS", DiscountType = DiscountType.Amount, DiscountValue = 100_000, MaxUses = 1,
                    OwnerMemberId = member1.Profile.OwnerUserId, ValidFrom = _now.AddDays(-10), ValidTo = _now.AddDays(80),
                    PartnerName = "Quà tri ân VĐV hoàn thành 5 giải", CreatedById = _adminId, CreatedAt = _now.AddDays(-10)
                });
            await _db.SaveChangesAsync();
        }

        // ---------------------------- Một giải ----------------------------

        private async Task CreateEventAsync(EventSpec spec)
        {
            var org = _orgs[spec.Org];
            var originalDate = _today.AddDays(spec.DayOffset);
            var eventDate = spec.Phase == Phase.Postponed ? originalDate.AddDays(30) : originalDate;
            var (openAt, closeAt) = RegistrationWindow(spec.Phase, originalDate);
            var createdAt = Min(openAt.AddDays(-14), _now.AddDays(-4));
            var cancelAt = _now.AddDays(-3).Date.AddHours(17);

            var ev = new Event
            {
                OrganizationId = org.Id,
                VenueId = _venues[spec.Venue],
                Name = spec.Name,
                Slug = spec.Slug,
                DescriptionHtml = $"<p>{spec.Blurb}</p><p>Giải do <strong>{org.Name}</strong> tổ chức, quản lý trên nền tảng MRMS.</p>",
                ThemeColor = spec.Theme,
                WaiverText = "Tôi tự nguyện tham gia, đã kiểm tra sức khỏe, hiểu rủi ro của môn chạy bộ đường dài và cam kết " +
                             "tuân thủ điều lệ giải cùng hướng dẫn của ban tổ chức, đội y tế.",
                WaiverVersion = 1,
                EventDate = eventDate,
                EndDate = eventDate,
                RegistrationOpenAt = openAt,
                RegistrationCloseAt = closeAt,
                Status = spec.Phase switch
                {
                    Phase.Draft => EventStatus.Draft,
                    Phase.Published => EventStatus.Published,
                    Phase.Open => EventStatus.RegistrationOpen,
                    Phase.Postponed => EventStatus.Postponed,
                    Phase.Cancelled => EventStatus.Cancelled,
                    Phase.Official => EventStatus.Completed,
                    _ => EventStatus.RegistrationClosed
                },
                PostponedFromDate = spec.Phase == Phase.Postponed ? originalDate : null,
                CancelReason = spec.Phase == Phase.Cancelled ? spec.CancelReason : null,
                EmergencyPhone = "0" + _rng.Next(200_000_000, 299_999_999),
                InsuranceNote = "Mỗi VĐV được mua bảo hiểm tai nạn 20.000.000 đ trong ngày thi.",
                CreatedById = org.OrganizerId,
                CreatedAt = createdAt,
                Policy = new EventPolicy
                {
                    GuardianConsentUnderAge = 18,
                    MaxDependentsPerAccount = 5,
                    InfoEditDeadline = closeAt,
                    TransferPolicy = spec.Transfer,
                    TransferFee = spec.Transfer == TransferPolicy.Paid ? 100_000 : 0,
                    MaxTransfers = 1,
                    TransferDeadline = spec.Transfer == TransferPolicy.Forbidden ? null : eventDate.AddDays(-10).ToDateTime(new TimeOnly(23, 59)),
                    CategoryChangeFee = 50_000,
                    MaxCategoryChanges = 1,
                    CategoryChangeDeadline = closeAt,
                    OverallRankingBasis = spec.Phase == Phase.Draft ? null : RankingBasis.GunTime,
                    AgeGroupRankingBasis = spec.Phase == Phase.Draft ? null : RankingBasis.ChipTime,
                    TieBreakRule = TieBreakRule.ChipTime,
                    LateStartToleranceMinutes = 15,
                    ComplaintWindowMinutes = spec.Phase == Phase.Provisional ? 4320 : 1440,
                    ComplaintFee = spec.ComplaintFee,
                    AwardStackingPolicy = AwardStackingPolicy.HighestOnly,
                    ForceMajeurePolicy = spec.Phase == Phase.Cancelled ? ForceMajeurePolicy.Refund : ForceMajeurePolicy.NoRefund
                }
            };

            var categories = new List<(CatSpec Spec, RaceCategory Entity)>();
            for (var i = 0; i < spec.Categories.Length; i++)
            {
                var cs = spec.Categories[i];
                var startAt = eventDate.ToDateTime(new TimeOnly(cs.StartHour, cs.StartMinute));
                var category = new RaceCategory
                {
                    Name = cs.Name, Discipline = cs.Discipline, CategoryType = cs.Type, DistanceKm = cs.Km,
                    ElevationGainM = cs.Elevation, ItraPoints = cs.Itra,
                    Capacity = Math.Max(150, (int)Math.Ceiling(spec.Registrations * cs.Share * 1.6 / 50) * 50),
                    StartAt = startAt, CutOffMinutes = cs.CutOff, MinAge = cs.MinAge, MaxAge = cs.MaxAge,
                    EscortRequiredUnderAge = cs.EscortUnder, IsTimed = cs.Timed, IsRanked = cs.Timed,
                    BibFrom = cs.BibFrom, BibTo = cs.BibFrom + 998, BibColor = cs.Color
                };
                // Giải nháp: cự ly cuối chưa có mức giá (thiếu cấu hình – BR-38 chặn mở đăng ký)
                if (!(spec.Phase == Phase.Draft && i == spec.Categories.Length - 1))
                    AddPriceTiers(category, cs.Price, openAt, closeAt);
                if (cs.Timed) AddCheckpoints(category, cs, startAt);
                ev.Categories.Add(category);
                categories.Add((cs, category));
            }
            foreach (var (name, min, max) in new (string, int, int?)[]
                         { ("18–29", 18, 29), ("30–39", 30, 39), ("40–49", 40, 49), ("50–59", 50, 59), ("60+", 60, null) })
                ev.AgeGroups.Add(new AgeGroup { Name = name, MinAge = min, MaxAge = max });
            foreach (var (cs, category) in categories.Where(c => c.Spec.Timed && c.Spec.MinAge < 18))
                ev.AgeGroups.Add(new AgeGroup { Name = "U18", MinAge = 0, MaxAge = 17, Category = category });
            if (spec.Phase != Phase.Draft) AddContent(ev, spec, eventDate, categories);

            _db.Events.Add(ev);
            await _db.SaveChangesAsync();

            var coupons = await CreateEventCouponsAsync(spec, ev, org, openAt, closeAt);
            var entries = CreateRegistrations(spec, ev, eventDate, categories, openAt, closeAt, cancelAt, coupons);

            if (spec.Phase is Phase.Closed or Phase.RaceDone or Phase.Provisional or Phase.Official)
                AssignBibs(entries);
            if (spec.Phase is Phase.RaceDone or Phase.Provisional or Phase.Official)
                SimulateRaceDay(entries, org, eventDate);
            if (spec.Phase == Phase.Cancelled)
                CancelEvent(entries, org, cancelAt);
            await _db.SaveChangesAsync();

            if (spec.Phase == Phase.Open && spec.Transfer != TransferPolicy.Forbidden)
                AddTransfers(entries, ev);

            var provisionalAt = eventDate.ToDateTime(new TimeOnly(16, 0));
            var officialAt = eventDate.AddDays(2).ToDateTime(new TimeOnly(10, 0));
            AddLifecycleAudit(spec, ev, org, openAt, closeAt, provisionalAt, officialAt, cancelAt, originalDate);
            await _db.SaveChangesAsync();

            if (spec.Phase is Phase.Provisional or Phase.Official)
            {
                var eventId = ev.EventId;
                var categoryRefs = categories.Select(c => (c.Spec, c.Entity.CategoryId)).ToList();
                var ageGroupIds = ev.AgeGroups.Where(g => g.CategoryId == null).Select(g => g.AgeGroupId).ToList();
                await CalculateResultsAsync(eventId);
                await AddPenaltiesAsync(eventId, eventDate, org);
                await CalculateResultsAsync(eventId);
                await AddComplaintsAsync(eventId, spec, org, provisionalAt);
                AddPrizes(categoryRefs, ageGroupIds);

                var tracked = await _db.Events.SingleAsync(e => e.EventId == eventId);
                tracked.ResultsStatus = spec.Phase == Phase.Official ? ResultsStatus.Official : ResultsStatus.Provisional;
                tracked.ResultsProvisionalAt = provisionalAt;
                tracked.ResultsOfficialAt = spec.Phase == Phase.Official ? officialAt : null;
                await _db.SaveChangesAsync();
            }

            _db.ChangeTracker.Clear();
        }

        private (DateTime Open, DateTime Close) RegistrationWindow(Phase phase, DateOnly eventDate)
        {
            DateTime At(DateOnly d, int h, int m = 0) => d.ToDateTime(new TimeOnly(h, m));
            return phase switch
            {
                Phase.Draft => (At(_today.AddDays(30), 8), At(eventDate.AddDays(-10), 22)),
                Phase.Published => (At(_today.AddDays(10), 8), At(eventDate.AddDays(-7), 22)),
                Phase.Open => (At(_today.AddDays(-60), 8), At(eventDate.AddDays(-5), 22)),
                Phase.Closed => (At(_today.AddDays(-70), 8), At(_today.AddDays(-1), 22)),
                Phase.Postponed => (At(_today.AddDays(-80), 8), At(eventDate.AddDays(-3), 22)),
                Phase.Cancelled => (At(_today.AddDays(-60), 8), At(eventDate.AddDays(-5), 22)),
                _ => (At(eventDate.AddDays(-90), 8), At(eventDate.AddDays(-7), 22)),
            };
        }

        private static void AddPriceTiers(RaceCategory category, decimal price, DateTime openAt, DateTime closeAt)
        {
            // Phủ kín thời gian đăng ký, nối liền nhau: Early bird 30 ngày đầu, giá thường, giá muộn 14 ngày cuối
            var earlyEnd = openAt.AddDays(30);
            var lateStart = closeAt.AddDays(-14);
            category.PriceTiers.Add(new PriceTier { Name = "Early bird", Price = Round1000(price * 0.8m), ValidFrom = openAt, ValidTo = earlyEnd });
            category.PriceTiers.Add(new PriceTier { Name = "Giá thường", Price = price, ValidFrom = earlyEnd, ValidTo = lateStart });
            category.PriceTiers.Add(new PriceTier { Name = "Giá muộn", Price = Round1000(price * 1.15m), ValidFrom = lateStart, ValidTo = closeAt });
        }

        private static void AddCheckpoints(RaceCategory category, CatSpec cs, DateTime startAt)
        {
            var seq = 1;
            category.Checkpoints.Add(new Checkpoint { Name = "Xuất phát", DistanceKm = 0, Sequence = seq++, Type = CheckpointType.Start });
            var splits = cs.Checkpoints ?? (cs.Km >= 40
                    ? new[] { new CpSpec("Km 10", 10), new CpSpec("Km 21,1", 21.1m), new CpSpec("Km 30", 30), new CpSpec("Km 40", 40) }
                    : cs.Km >= 21 ? new[] { new CpSpec("Km 5", 5), new CpSpec("Km 10", 10), new CpSpec("Km 15", 15) }
                    : cs.Km >= 10 ? new[] { new CpSpec("Km 5", 5) }
                    : Array.Empty<CpSpec>());
            foreach (var cp in splits)
                category.Checkpoints.Add(new Checkpoint
                {
                    Name = cp.Name, DistanceKm = cp.Km, Sequence = seq++, Type = CheckpointType.Split, IsMandatory = true,
                    StationType = cp.Station,
                    CutOffAt = cp.CutOffAfterStartMinutes is int minutes ? startAt.AddMinutes(minutes) : null
                });
            category.Checkpoints.Add(new Checkpoint
            {
                Name = "Về đích", DistanceKm = cs.Km, Sequence = seq, Type = CheckpointType.Finish, StationType = StationType.Medical
            });
        }

        private void AddContent(Event ev, EventSpec spec, DateOnly eventDate, List<(CatSpec Spec, RaceCategory Entity)> categories)
        {
            var distances = string.Join(", ", categories.Select(c => c.Spec.Name));
            ev.ContentSections.Add(new EventContentSection
            {
                SectionType = SectionType.Intro, Title = "Giới thiệu giải", DisplayOrder = 0,
                BodyHtml = $"<p>{spec.Blurb}</p><p>Các cự ly: <strong>{distances}</strong>. Mọi VĐV hoàn thành nhận huy chương và áo finisher.</p>"
            });
            ev.ContentSections.Add(new EventContentSection
            {
                SectionType = SectionType.Custom, Title = "Race kit gồm những gì?", DisplayOrder = 1,
                BodyHtml = "<ul><li>BIB có gắn chip bấm giờ</li><li>Áo thi đấu</li><li>Túi rút đựng đồ</li><li>Voucher của nhà tài trợ</li></ul>" +
                           "<p>Mang CCCD/hộ chiếu khi nhận. Nhận hộ cần giấy ủy quyền và giấy tờ của người nhận hộ.</p>"
            });
            ev.ContentSections.Add(new EventContentSection
            {
                SectionType = SectionType.Faq, Title = "Câu hỏi thường gặp", DisplayOrder = 2,
                BodyHtml = "<p><strong>Có được chuyển nhượng BIB không?</strong><br>" +
                           (spec.Transfer == TransferPolicy.Forbidden ? "Không, giải không cho phép chuyển nhượng." : "Có, thực hiện trên MRMS trước hạn chuyển nhượng.") +
                           "</p><p><strong>Xếp hạng theo giờ nào?</strong><br>Chung cuộc theo giờ súng, nhóm tuổi theo giờ chip.</p>" +
                           "<p><strong>Khiếu nại kết quả ở đâu?</strong><br>Trong trang kết quả cá nhân, trong thời hạn nhận khiếu nại.</p>"
            });

            var venue = spec.Venue;
            ev.ScheduleItems.Add(new EventScheduleItem
            {
                Title = "Nhận race kit (ngày 1)", Location = $"Nhà văn hóa gần {venue}",
                StartsAt = eventDate.AddDays(-2).ToDateTime(new TimeOnly(9, 0)), EndsAt = eventDate.AddDays(-2).ToDateTime(new TimeOnly(20, 0))
            });
            ev.ScheduleItems.Add(new EventScheduleItem
            {
                Title = "Nhận race kit (ngày 2)", Location = $"Nhà văn hóa gần {venue}",
                StartsAt = eventDate.AddDays(-1).ToDateTime(new TimeOnly(9, 0)), EndsAt = eventDate.AddDays(-1).ToDateTime(new TimeOnly(20, 0)),
                Note = "Không phát race kit vào ngày thi."
            });
            foreach (var (cs, category) in categories)
                ev.ScheduleItems.Add(new EventScheduleItem
                {
                    Title = $"Xuất phát {cs.Name}", Location = venue, StartsAt = category.StartAt,
                    Note = cs.CutOff is int cot ? $"Thời gian giới hạn {cot / 60}h{cot % 60:D2}" : "Không bấm giờ"
                });
            var lastStart = categories.Max(c => c.Entity.StartAt);
            ev.ScheduleItems.Add(new EventScheduleItem
            {
                Title = "Lễ trao giải", Location = $"Sân khấu chính – {venue}",
                StartsAt = lastStart.AddHours(3), EndsAt = lastStart.AddHours(4)
            });
        }

        private async Task<List<Coupon>> CreateEventCouponsAsync(EventSpec spec, Event ev, OrgInfo org, DateTime openAt, DateTime closeAt)
        {
            var list = new List<Coupon>();
            foreach (var c in spec.Coupons ?? Array.Empty<CouponSpec>())
            {
                var coupon = new Coupon
                {
                    OrganizationId = org.Id, EventId = ev.EventId, Code = c.Code, DiscountType = c.Type, DiscountValue = c.Value,
                    MaxDiscountAmount = c.Max, MaxUses = c.MaxUses, PartnerName = c.Partner, IsActive = c.Active,
                    ValidFrom = openAt, ValidTo = c.EarlyOnly ? openAt.AddDays(30) : closeAt,
                    CreatedById = org.OrganizerId, CreatedAt = openAt.AddDays(-2)
                };
                _db.Coupons.Add(coupon);
                list.Add(coupon);
            }
            await _db.SaveChangesAsync();
            if (spec.Phase == Phase.Open) list.AddRange(_platformCoupons);
            return list.Where(c => c.IsActive).ToList();
        }

        // ---------------------------- Đăng ký ----------------------------

        private List<Entry> CreateRegistrations(EventSpec spec, Event ev, DateOnly eventDate,
            List<(CatSpec Spec, RaceCategory Entity)> categories, DateTime openAt, DateTime closeAt, DateTime cancelAt,
            List<Coupon> coupons)
        {
            var entries = new List<Entry>();
            if (spec.Registrations == 0) return entries;

            var featured = (spec.Featured ?? Array.Empty<string>())
                .Select(f => f.Split('=')).ToDictionary(p => p[0], p => p[1]);
            var kidsCategory = categories.FirstOrDefault(c => c.Spec.Type == CategoryType.Kids);
            var kidsCount = kidsCategory.Spec is null ? 0 : (int)Math.Round(spec.Registrations * kidsCategory.Spec.Share);
            var adultCount = spec.Registrations - kidsCount;

            var pool = Shuffle(_adults.ToList());
            pool = pool.Where(r => r.Email is not null && featured.ContainsKey(r.Email))
                .Concat(pool.Where(r => r.Email is null || !featured.ContainsKey(r.Email))).ToList();
            var adultCategories = categories.Where(c => c.Spec.Type != CategoryType.Kids).ToList();

            var regEnd = spec.Phase == Phase.Cancelled ? cancelAt : Min(closeAt, _now.AddMinutes(-20));
            foreach (var runner in pool)
            {
                if (entries.Count >= adultCount) break;
                var age = AgeOn(runner.Profile.DateOfBirth!.Value, eventDate);
                var eligible = adultCategories.Where(c => age >= c.Spec.MinAge && (c.Spec.MaxAge is null || age <= c.Spec.MaxAge)).ToList();
                if (eligible.Count == 0) continue;
                var chosen = runner.Email is not null && featured.TryGetValue(runner.Email, out var wanted)
                    ? eligible.FirstOrDefault(c => c.Spec.Name == wanted)
                    : default;
                if (chosen.Spec is null)
                {
                    chosen = PickWeighted(eligible);
                    // Người chạy chậm ít khi chọn cự ly dài nhất
                    if (chosen.Spec.Km >= 40 && runner.Ability > 1.25) chosen = PickWeighted(eligible);
                }
                entries.Add(NewEntry(spec, ev, eventDate, chosen, runner, openAt, regEnd, coupons));
            }

            if (kidsCategory.Spec is not null)
                foreach (var kid in Shuffle(_kids.ToList())
                             .Where(k => AgeOn(k.Profile.DateOfBirth!.Value, eventDate) is var a && a >= kidsCategory.Spec.MinAge && a <= kidsCategory.Spec.MaxAge)
                             .Take(kidsCount))
                    entries.Add(NewEntry(spec, ev, eventDate, kidsCategory, kid, openAt, regEnd, coupons));
            return entries;
        }

        private Entry NewEntry(EventSpec spec, Event ev, DateOnly eventDate, (CatSpec Spec, RaceCategory Entity) cat, Runner runner,
            DateTime openAt, DateTime regEnd, List<Coupon> coupons)
        {
            var roll = _rng.NextDouble();
            RegistrationStatus status;
            DateTime at;
            if (spec.Phase == Phase.Open && roll > 0.97)
            {
                // Đang chờ thanh toán (giữ chỗ 15 phút – BR-13): sẽ tự hết hạn nếu không thanh toán
                status = RegistrationStatus.PendingPayment;
                at = _now.AddMinutes(-_rng.Next(1, 12));
            }
            else
            {
                at = RandomBetween(openAt, regEnd);
                status = roll < 0.88 ? RegistrationStatus.Confirmed
                    : roll < 0.94 ? RegistrationStatus.Expired
                    : RegistrationStatus.Cancelled;
            }

            var entry = EntryType.Public;
            if (status == RegistrationStatus.Confirmed && !runner.IsDependent)
            {
                var special = _rng.NextDouble();
                if (special < 0.02 && runner.Ability < 0.9 && cat.Spec.Timed) entry = EntryType.Pacer;
                else if (special < 0.03) entry = EntryType.Complimentary;
                else if (special < 0.04) entry = EntryType.Partner;
            }

            var tier = cat.Entity.PriceTiers.Where(t => t.ValidFrom <= at && at < t.ValidTo).OrderBy(t => t.ValidFrom).FirstOrDefault()
                       ?? cat.Entity.PriceTiers.OrderBy(t => t.ValidFrom).Last();
            var price = entry is EntryType.Pacer or EntryType.Complimentary ? 0 : tier.Price;

            Coupon? coupon = null;
            decimal discount = 0;
            if (price > 0 && status is RegistrationStatus.Confirmed or RegistrationStatus.PendingPayment && _rng.NextDouble() < 0.15)
            {
                var usable = coupons.Where(c => c.ValidFrom <= at && at < c.ValidTo &&
                                                _couponUses.GetValueOrDefault(c.CouponId) < c.MaxUses).ToList();
                if (usable.Count > 0)
                {
                    coupon = usable[_rng.Next(usable.Count)];
                    discount = coupon.DiscountType == DiscountType.Amount
                        ? coupon.DiscountValue
                        : Math.Min(Round1000(price * coupon.DiscountValue / 100), coupon.MaxDiscountAmount ?? decimal.MaxValue);
                    discount = Math.Min(discount, price - 10_000);
                    _couponUses[coupon.CouponId] = _couponUses.GetValueOrDefault(coupon.CouponId) + 1;
                }
            }

            var reg = NewRegistration(ev, eventDate, cat.Spec, cat.Entity, runner, at, status, entry, price, coupon, discount);
            return new Entry(reg, runner, cat.Spec, cat.Entity);
        }

        private Registration NewRegistration(Event ev, DateOnly eventDate, CatSpec cs, RaceCategory category, Runner runner,
            DateTime at, RegistrationStatus status, EntryType entry, decimal price, Coupon? coupon, decimal discount)
        {
            var p = runner.Profile;
            var dob = p.DateOfBirth!.Value;
            var age = AgeOn(dob, eventDate);
            var minor = age < 18;
            var escort = cs.EscortUnder is int under && age < under;
            var hide = _rng.NextDouble() < 0.06;
            var reg = new Registration
            {
                EventId = ev.EventId, CategoryId = category.CategoryId, ParticipantId = p.ParticipantId,
                RegisteredByUserId = p.OwnerUserId, EntryType = entry, Status = status,
                OriginalPrice = price, DiscountAmount = discount, FinalPrice = price - discount, CouponId = coupon?.CouponId,
                QrToken = Guid.NewGuid().ToString("N").ToUpperInvariant(), RegisteredAt = at, HideFromPublicList = hide,
                FullName = p.FullName, BibName = BibNameOf(p.FullName), DateOfBirth = dob, Gender = p.Gender!.Value,
                IdType = p.IdType, IdNumber = p.IdNumber, Nationality = p.Nationality ?? "Việt Nam",
                Phone = p.Phone ?? runner.OwnerPhone, Email = runner.IsDependent ? null : runner.Email,
                TShirtSize = p.TShirtSize ?? TShirtSize.M,
                EmergencyContactName = p.EmergencyContactName ?? runner.OwnerName,
                EmergencyContactPhone = p.EmergencyContactPhone ?? runner.OwnerPhone,
                BloodType = p.BloodType, MedicalNotes = p.MedicalNotes, ClubName = p.ClubName,
                GuardianName = minor ? runner.OwnerName : null, GuardianPhone = minor ? runner.OwnerPhone : null,
                GuardianIdNumber = minor ? runner.OwnerIdNumber : null, GuardianConsent = minor,
                EscortName = escort ? runner.OwnerName : null, EscortPhone = escort ? runner.OwnerPhone : null,
                WaiverAccepted = true, HealthDeclarationAccepted = true
            };

            var paid = reg.FinalPrice > 0;
            switch (status)
            {
                case RegistrationStatus.Confirmed:
                    reg.ConfirmedAt = at.AddMinutes(_rng.Next(2, 9));
                    if (paid) reg.Payments.Add(NewPayment(reg.FinalPrice, PaymentStatus.Success, at, reg.ConfirmedAt));
                    break;
                case RegistrationStatus.PendingPayment:
                    reg.ExpiresAt = at.AddMinutes(15);
                    if (paid) reg.Payments.Add(NewPayment(reg.FinalPrice, PaymentStatus.Pending, at, null));
                    break;
                case RegistrationStatus.Expired:
                    reg.ExpiresAt = at.AddMinutes(15);
                    if (paid) reg.Payments.Add(NewPayment(reg.FinalPrice, PaymentStatus.Expired, at, null));
                    break;
                case RegistrationStatus.Cancelled:
                    reg.CancelledAt = at.AddMinutes(_rng.Next(3, 14));
                    if (paid) reg.Payments.Add(NewPayment(reg.FinalPrice, PaymentStatus.Failed, at, null));
                    break;
            }

            _db.Registrations.Add(reg);
            _db.ConsentRecords.Add(new ConsentRecord
            {
                UserId = p.OwnerUserId, Registration = reg, ConsentType = ConsentType.Waiver, DocumentVersion = 1, Granted = true, CreatedAt = at
            });
            _db.ConsentRecords.Add(new ConsentRecord
            {
                UserId = p.OwnerUserId, Registration = reg, ConsentType = ConsentType.PublicListing, DocumentVersion = 1, Granted = !hide, CreatedAt = at
            });
            if (minor)
                _db.ConsentRecords.Add(new ConsentRecord
                {
                    UserId = p.OwnerUserId, Registration = reg, ConsentType = ConsentType.Guardian, DocumentVersion = 1, Granted = true, CreatedAt = at
                });
            return reg;
        }

        private Payment NewPayment(decimal amount, PaymentStatus status, DateTime createdAt, DateTime? paidAt) => new()
        {
            Purpose = PaymentPurpose.Registration, Provider = PaymentProvider.Mock, Amount = amount, Status = status,
            TxnRef = "SMP" + Guid.NewGuid().ToString("N").ToUpperInvariant(), CreatedAt = createdAt, PaidAt = paidAt,
            ResponseCode = status switch { PaymentStatus.Success => "00", PaymentStatus.Failed => "24", _ => null }
        };

        private static void AssignBibs(List<Entry> entries)
        {
            foreach (var group in entries.Where(e => e.Reg.Status == RegistrationStatus.Confirmed).GroupBy(e => e.Category))
            {
                var bib = group.Key.BibFrom;
                foreach (var e in group.OrderBy(e => e.Reg.RegisteredAt)) e.Reg.BibNumber = bib++;
            }
        }

        // ---------------------------- Ngày thi ----------------------------

        private void SimulateRaceDay(List<Entry> entries, OrgInfo org, DateOnly eventDate)
        {
            foreach (var group in entries.Where(e => e.Reg.Status == RegistrationStatus.Confirmed).GroupBy(e => e.Category))
            {
                var category = group.Key;
                var checkpoints = category.Checkpoints.OrderBy(c => c.Sequence).ToList();
                var ordered = group.OrderBy(e => e.Runner.Ability).ToList();
                for (var i = 0; i < ordered.Count; i++)
                    SimulateRunner(ordered[i], checkpoints, (double)i / Math.Max(1, ordered.Count - 1), org, eventDate);
            }
        }

        private void SimulateRunner(Entry e, List<Checkpoint> checkpoints, double position, OrgInfo org, DateOnly eventDate)
        {
            var staffId = org.StaffIds[_rng.Next(org.StaffIds.Count)];
            var roll = _rng.NextDouble();
            if (!e.Spec.Timed)
            {
                if (roll > 0.08) AddKit(e.Reg, staffId, eventDate);
                return;
            }
            if (roll < 0.045)
            {
                // DNS: không xuất phát (một nửa vẫn đã nhận kit)
                if (_rng.NextDouble() < 0.5) AddKit(e.Reg, staffId, eventDate);
                return;
            }
            AddKit(e.Reg, staffId, eventDate);

            var km = (double)e.Spec.Km;
            var age = AgeOn(e.Reg.DateOfBirth, eventDate);
            var secPerKm = e.Spec.Discipline == Discipline.Trail
                ? 480 * (1 + (e.Spec.Elevation ?? 0) / km / 100) * Math.Pow(km / 21, 0.07)
                : 370 * Math.Pow(km / 10, 0.06);
            secPerKm *= e.Runner.Ability * (e.Reg.Gender == Gender.F ? 1.1 : 1) * (1 + Math.Max(0, age - 35) * 0.008)
                        * (1 + Normal() * 0.03);
            var total = secPerKm * km;
            var matOffset = 3 + position * 240 + _rng.NextDouble() * 25;
            if (_rng.NextDouble() < 0.004) matOffset += 1200; // qua thảm muộn 20 phút ⇒ DNS (BR-24)

            var dnfChance = e.Spec.Discipline == Discipline.Trail
                ? km >= 60 ? 0.14 : km >= 40 ? 0.09 : 0.05
                : km >= 40 ? 0.04 : km >= 20 ? 0.02 : 0.008;
            var dnf = _rng.NextDouble() < dnfChance;
            var reached = dnf ? (checkpoints.Count > 2 ? _rng.Next(2, checkpoints.Count) : 1) : checkpoints.Count;
            var skipStart = _rng.NextDouble() < 0.01;                       // mất tín hiệu thảm xuất phát
            int? skipSplit = !dnf && checkpoints.Count > 2 && _rng.NextDouble() < 0.004
                ? _rng.Next(1, checkpoints.Count - 1) : null;                // bỏ checkpoint bắt buộc

            double previous = 0;
            for (var i = 0; i < reached; i++)
            {
                var fraction = (double)checkpoints[i].DistanceKm / km;
                var elapsed = i == 0 ? 0 : Math.Max(previous + 30, total * Math.Pow(fraction, 1.03) * (1 + Normal() * 0.01));
                previous = elapsed;
                if ((i == 0 && skipStart) || i == skipSplit) continue;
                e.Reg.TimingRecords.Add(new TimingRecord
                {
                    CheckpointId = checkpoints[i].CheckpointId,
                    RecordedAt = e.Category.StartAt.AddMilliseconds(Math.Round((matOffset + elapsed) * 1000)),
                    Source = TimingSource.Import,
                    RecordedById = staffId
                });
            }

            // Một phần VĐV bỏ cuộc được ghi nhận tại trạm (UC-58) – lần tính kết quả giữ nguyên lý do
            if (dnf && checkpoints.Count > 1 && _rng.NextDouble() < 0.45)
                e.Reg.Result = new Result
                {
                    Status = ResultStatus.DNF,
                    DnfReason = _rng.NextDouble() < 0.5 ? DnfReason.Medical : DnfReason.Voluntary,
                    DnfCheckpointId = checkpoints[Math.Max(1, reached - 1)].CheckpointId,
                    UpdatedAt = e.Category.StartAt.AddHours(2)
                };
        }

        private void AddKit(Registration reg, string staffId, DateOnly eventDate)
        {
            var proxy = _rng.NextDouble() < 0.08;
            reg.RaceKitPickup = new RaceKitPickup
            {
                PickedUpAt = eventDate.AddDays(-_rng.Next(1, 3)).ToDateTime(new TimeOnly(9, 0)).AddMinutes(_rng.Next(0, 660)),
                StaffId = staffId,
                IdChecked = true,
                IsProxy = proxy,
                ProxyName = proxy ? VietName(_rng.NextDouble() < 0.5 ? Gender.M : Gender.F) : null,
                ProxyIdNumber = proxy ? "0" + _rng.Next(10, 99) + _rng.Next(100_000_000, 999_999_999) : null,
                Note = proxy ? "Nhận hộ – có giấy ủy quyền" : null
            };
        }

        private async Task CalculateResultsAsync(int eventId)
        {
            _db.ChangeTracker.Clear();
            var ev = await _resultRepo.GetEventForResultsAsync(eventId)
                     ?? throw new InvalidOperationException($"Không tìm thấy giải {eventId}.");
            await _recalc.RecalculateAsync(ev);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        private async Task AddPenaltiesAsync(int eventId, DateOnly eventDate, OrgInfo org)
        {
            var finishers = await _db.Results
                .Where(r => r.Registration.EventId == eventId && r.Status == ResultStatus.Finished)
                .OrderBy(r => r.Registration.CategoryId).ThenBy(r => r.OverallRank)
                .Select(r => new { r.ResultId, r.Registration.Category.Discipline })
                .ToListAsync();
            if (finishers.Count < 20) return;

            var issuedAt = eventDate.ToDateTime(new TimeOnly(12, 0));
            var trail = finishers[0].Discipline == Discipline.Trail;
            var picks = new (int Index, string Code, PenaltyAction Action, int? Minutes, string Note, bool Revoked)[]
            {
                (finishers.Count / 3, trail ? "UNAUTHORISED_ASSIST" : "SHORTCUT", PenaltyAction.DSQ, null,
                    trail ? "Nhận tiếp tế ngoài trạm tại km 18 (ảnh của tình nguyện viên)." : "Camera tại km 14 không ghi nhận, cắt ngang qua dải phân cách.", false),
                (finishers.Count / 2, trail ? "MISSING_GEAR" : "LITTERING", PenaltyAction.TimePenalty, trail ? 30 : 15,
                    trail ? "Thiếu áo mưa bắt buộc khi kiểm tra tại CP2." : "Vứt vỏ gel ngoài khu vực thu gom rác.", false),
                (finishers.Count * 2 / 3, "LITTERING", PenaltyAction.TimePenalty, 15, "Xả rác sau trạm nước km 8.", false),
                (finishers.Count / 4, "BIB_NOT_VISIBLE", PenaltyAction.Warning, null, "Đeo BIB sau lưng.", false),
                (finishers.Count / 5, "SHORTCUT", PenaltyAction.DSQ, null, "Báo cáo nhầm VĐV – đã thu hồi sau khi xem lại camera.", true),
            };
            foreach (var pick in picks)
                _db.ResultPenalties.Add(new ResultPenalty
                {
                    ResultId = finishers[pick.Index].ResultId, ReasonCode = pick.Code, Action = pick.Action, Minutes = pick.Minutes,
                    Note = pick.Note, IssuedById = org.OrganizerId, IssuedAt = issuedAt,
                    RevokedAt = pick.Revoked ? issuedAt.AddHours(2) : null
                });
            Audit(org.OrganizerId, "Member,Organizer", "Result.Penalty", nameof(Event), eventId.ToString(), null,
                new { Count = picks.Length }, issuedAt);
            await _db.SaveChangesAsync();
        }

        private async Task AddComplaintsAsync(int eventId, EventSpec spec, OrgInfo org, DateTime provisionalAt)
        {
            var rows = await _db.Results
                .Where(r => r.Registration.EventId == eventId && (r.Status == ResultStatus.Finished || r.Status == ResultStatus.DSQ))
                .OrderBy(r => r.ResultId)
                .Select(r => new { r.ResultId, r.Status, r.Registration.RegisteredByUserId })
                .ToListAsync();
            if (rows.Count < 10) return;
            var dsq = rows.FirstOrDefault(r => r.Status == ResultStatus.DSQ);
            var finishers = rows.Where(r => r.Status == ResultStatus.Finished).ToList();

            var texts = new[]
            {
                "Đồng hồ GPS của tôi ghi thời gian ít hơn kết quả gần 2 phút, nhờ BTC kiểm tra lại thời gian qua thảm xuất phát.",
                "Tôi có qua trạm km 10 nhưng trang kết quả không có thời gian chặng, đề nghị BTC đối chiếu ảnh tại trạm.",
                "Kết quả xếp tôi sai nhóm tuổi, ngày sinh trên CCCD là đúng nhưng hệ thống hiển thị nhóm khác.",
                "Tôi về đích cùng lúc với VĐV phía trước nhưng bị xếp sau, đề nghị xem lại giờ chip.",
                "Đề nghị xem lại thứ hạng nữ: VĐV xếp trên tôi được hỗ trợ dẫn tốc bằng xe đạp.",
            };
            var accepted = "BTC đã đối chiếu dữ liệu chip và camera: chấp nhận khiếu nại, đã điều chỉnh dữ liệu bấm giờ.";
            var rejectedText = "BTC đã đối chiếu dữ liệu chip, ảnh tại trạm và camera: kết quả công bố là chính xác.";

            var plan = new List<(int ResultId, string UserId, string Content, ComplaintStatus Status, string? Response)>();
            if (dsq is not null)
                plan.Add((dsq.ResultId, dsq.RegisteredByUserId, "Tôi không đi tắt, đề nghị BTC xem lại quyết định truất quyền thi đấu.",
                    ComplaintStatus.Rejected, "Camera tại km 14 cho thấy VĐV cắt qua dải phân cách – giữ nguyên quyết định DSQ."));
            var open = spec.Phase == Phase.Provisional;
            var statuses = open
                ? new[] { ComplaintStatus.Open, ComplaintStatus.Open, ComplaintStatus.Open, ComplaintStatus.Accepted, ComplaintStatus.Open }
                : new[] { ComplaintStatus.Accepted, ComplaintStatus.Rejected, ComplaintStatus.Rejected };
            for (var i = 0; i < statuses.Length; i++)
            {
                var row = finishers[(i * 37 + 11) % finishers.Count];
                plan.Add((row.ResultId, row.RegisteredByUserId, texts[i % texts.Length], statuses[i],
                    statuses[i] switch { ComplaintStatus.Accepted => accepted, ComplaintStatus.Rejected => rejectedText, _ => null }));
            }

            var windowEnd = Min(provisionalAt.AddMinutes(open ? 4320 : 1440), _now);
            foreach (var (resultId, userId, content, status, response) in plan.DistinctBy(p => p.ResultId))
            {
                var createdAt = RandomBetween(provisionalAt.AddMinutes(30), windowEnd.AddMinutes(-60));
                _db.ResultComplaints.Add(new ResultComplaint
                {
                    ResultId = resultId, UserId = userId, Content = content, FeeAmount = spec.ComplaintFee, Status = status,
                    Response = response, HandledById = status == ComplaintStatus.Open ? null : org.OrganizerId,
                    CreatedAt = createdAt, HandledAt = status == ComplaintStatus.Open ? null : Min(createdAt.AddHours(3), _now)
                });
                if (status != ComplaintStatus.Open)
                    Audit(org.OrganizerId, "Member,Organizer", "Result.ComplaintResolve", nameof(ResultComplaint), resultId.ToString(),
                        new { Status = "Open" }, new { Status = status.ToString() }, Min(createdAt.AddHours(3), _now));
            }
            await _db.SaveChangesAsync();
        }

        private void AddPrizes(List<(CatSpec Spec, int CategoryId)> categories, List<int> ageGroupIds)
        {
            var overall = new[] { ("Cúp vô địch + giấy chứng nhận", 5_000_000m), ("Cúp + giấy chứng nhận", 3_000_000m), ("Cúp + giấy chứng nhận", 2_000_000m) };
            foreach (var (cs, categoryId) in categories.Where(c => c.Spec.Timed))
            {
                var scale = cs.Km >= 40 ? 1.5m : cs.Km >= 20 ? 1m : cs.Km >= 10 ? 0.5m : 0.3m;
                foreach (var gender in new[] { Gender.M, Gender.F })
                {
                    for (var rank = 1; rank <= overall.Length; rank++)
                        _db.Prizes.Add(new Prize
                        {
                            CategoryId = categoryId, AwardType = AwardType.Overall, Gender = gender, Rank = rank,
                            Reward = overall[rank - 1].Item1, CashAmount = Round1000(overall[rank - 1].Item2 * scale)
                        });
                    if (cs.Km < 20) continue;
                    foreach (var groupId in ageGroupIds)
                        _db.Prizes.Add(new Prize
                        {
                            CategoryId = categoryId, AwardType = AwardType.AgeGroup, Gender = gender, AgeGroupId = groupId,
                            Rank = 1, Reward = "Huy chương nhóm tuổi", CashAmount = 500_000
                        });
                }
            }
        }

        // ---------------------------- Hủy giải, chuyển nhượng ----------------------------

        private void CancelEvent(List<Entry> entries, OrgInfo org, DateTime cancelAt)
        {
            var i = 0;
            foreach (var e in entries.Where(e => e.Reg.Status == RegistrationStatus.Confirmed))
            {
                e.Reg.CancelledAt = cancelAt;
                if (e.Reg.FinalPrice <= 0)
                {
                    e.Reg.Status = RegistrationStatus.Cancelled;
                    continue;
                }
                var done = i++ % 3 == 0; // 1/3 đã hoàn xong, còn lại đang chờ
                e.Reg.Status = done ? RegistrationStatus.Refunded : RegistrationStatus.RefundPending;
                var processedAt = cancelAt.AddDays(2).AddHours(_rng.Next(1, 8));
                _db.Refunds.Add(new Refund
                {
                    Registration = e.Reg, Amount = e.Reg.FinalPrice, Reason = RefundReason.EventCancelled,
                    Status = done ? RefundStatus.Completed : RefundStatus.Pending,
                    BankInfo = done || _rng.NextDouble() < 0.5
                        ? $"{Pick(Banks)} – {_rng.Next(100_000, 999_999)}{_rng.Next(1000, 9999)} – {RemoveDiacritics(e.Runner.OwnerName).ToUpperInvariant()}"
                        : null,
                    ProcessedById = done ? org.OrganizerId : null,
                    ProcessedAt = done ? Min(processedAt, _now) : null,
                    TransactionRef = done ? $"FT{_today:yyMMdd}{_rng.Next(100_000, 999_999)}" : null
                });
            }
        }

        private void AddTransfers(List<Entry> entries, Event ev)
        {
            var inEvent = entries.Select(e => e.Runner.Profile.ParticipantId).ToHashSet();
            var senders = Shuffle(entries.Where(e => e.Reg.Status == RegistrationStatus.Confirmed && !e.Runner.IsDependent &&
                                                     e.Reg.EntryType == EntryType.Public && e.Runner.Email?.StartsWith("runner") == true).ToList())
                .Take(6).ToList();
            var recipients = Shuffle(_adults.Where(r => !r.IsDependent && !inEvent.Contains(r.Profile.ParticipantId)).ToList());
            var fee = ev.Policy.TransferPolicy == TransferPolicy.Paid ? ev.Policy.TransferFee : 0;
            var statuses = new[]
            {
                TransferStatus.Pending, TransferStatus.Pending,
                fee > 0 ? TransferStatus.AwaitingFee : TransferStatus.Pending,
                TransferStatus.Rejected, TransferStatus.Cancelled, TransferStatus.Expired
            };
            for (var i = 0; i < senders.Count && i < recipients.Count; i++)
            {
                var requestedAt = statuses[i] == TransferStatus.Expired ? _now.AddDays(-6) : _now.AddHours(-_rng.Next(2, 72));
                _db.BibTransfers.Add(new BibTransfer
                {
                    RegistrationId = senders[i].Reg.RegistrationId, FromUserId = senders[i].Reg.RegisteredByUserId,
                    ToUserId = recipients[i].Profile.OwnerUserId, ToParticipantId = recipients[i].Profile.ParticipantId,
                    Status = statuses[i], FeeAmount = fee, RequestedAt = requestedAt,
                    RespondedAt = statuses[i] is TransferStatus.Pending ? null : Min(requestedAt.AddHours(_rng.Next(1, 30)), _now)
                });
            }
        }

        private void AddLifecycleAudit(EventSpec spec, Event ev, OrgInfo org, DateTime openAt, DateTime closeAt,
            DateTime provisionalAt, DateTime officialAt, DateTime cancelAt, DateOnly originalDate)
        {
            const string role = "Member,Organizer";
            var id = ev.EventId.ToString();
            Audit(org.OrganizerId, role, "Event.Create", nameof(Event), id, null, new { ev.Name, ev.EventDate }, ev.CreatedAt);
            if (spec.Phase == Phase.Draft) return;
            Audit(org.OrganizerId, role, "Event.StatusChange", nameof(Event), id, new { Status = "Draft" }, new { Status = "Published" }, ev.CreatedAt.AddDays(1));
            if (spec.Phase == Phase.Published) return;
            Audit(org.OrganizerId, role, "Event.StatusChange", nameof(Event), id, new { Status = "Published" }, new { Status = "RegistrationOpen" }, openAt);
            if (spec.Phase == Phase.Cancelled)
            {
                Audit(org.OrganizerId, role, "Event.Cancel", nameof(Event), id, new { Status = "RegistrationOpen" },
                    new { Status = "Cancelled", ev.CancelReason }, cancelAt);
                return;
            }
            if (spec.Phase == Phase.Postponed)
            {
                Audit(org.OrganizerId, role, "Event.Postpone", nameof(Event), id, new { EventDate = originalDate },
                    new { ev.EventDate }, _now.AddDays(-2));
                return;
            }
            if (spec.Phase == Phase.Open) return;
            Audit(org.OrganizerId, role, "Event.StatusChange", nameof(Event), id, new { Status = "RegistrationOpen" }, new { Status = "RegistrationClosed" }, closeAt);
            Audit(org.OrganizerId, role, "Bib.Assign", nameof(Event), id, null, new { Mode = "Auto" }, closeAt.AddHours(10));
            if (spec.Phase is not (Phase.Provisional or Phase.Official)) return;
            Audit(org.OrganizerId, role, "Result.Calculate", nameof(Event), id, null, null, provisionalAt.AddMinutes(-30));
            Audit(org.OrganizerId, role, "Result.PublishProvisional", nameof(Event), id, new { ResultsStatus = "Draft" },
                new { ResultsStatus = "Provisional" }, provisionalAt);
            if (spec.Phase != Phase.Official) return;
            Audit(org.OrganizerId, role, "Result.Finalize", nameof(Event), id, new { ResultsStatus = "Provisional" },
                new { ResultsStatus = "Official" }, officialAt);
            Audit(org.OrganizerId, role, "Event.StatusChange", nameof(Event), id, new { Status = "RegistrationClosed" },
                new { Status = "Completed" }, officialAt.AddHours(1));
        }

        // ---------------------------- Tiện ích ----------------------------

        private async Task<AppUser> CreateUserAsync(string email, string name, string[] roles, int? orgId, DateTime createdAt,
            bool active = true)
        {
            var user = new AppUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = name, OrganizationId = orgId,
                CreatedAt = createdAt, IsActive = active, PasswordHash = _passwordHash
            };
            var created = await _users.CreateAsync(user);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Seed {email}: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            var inRoles = await _users.AddToRolesAsync(user, roles);
            if (!inRoles.Succeeded)
                throw new InvalidOperationException($"Seed {email}: " + string.Join("; ", inRoles.Errors.Select(e => e.Description)));
            if (!active)
                Audit(_adminId, AppRoles.Admin, "User.Lock", nameof(AppUser), user.Id, new { IsActive = true },
                    new { IsActive = false, Reason = "Đăng ký nhiều tài khoản để giữ chỗ" }, _now.AddDays(-_rng.Next(3, 20)));
            return user;
        }

        private void AddMinimalSelfProfile(AppUser user, DateTime createdAt)
        {
            _db.Participants.Add(new Participant
            {
                OwnerUserId = user.Id, LinkedUserId = user.Id, Relationship = ParticipantRelationship.Self,
                FullName = user.FullName, CreatedAt = createdAt
            });
            _db.ConsentRecords.AddRange(
                Consent(user.Id, ConsentType.Terms, true, createdAt),
                Consent(user.Id, ConsentType.Privacy, true, createdAt));
        }

        private Participant NewProfile(string ownerId, ParticipantRelationship relationship, string name, Gender gender, DateOnly dob,
            string nationality, DateTime createdAt, bool adult)
        {
            var vietnamese = nationality == "Việt Nam";
            var (idType, idNumber) = !adult
                ? (IdType.BirthCertificate, $"KS{dob.Year}{_idSeq++:D6}")
                : vietnamese ? (IdType.CCCD, Cccd(Pick(Provinces), gender, dob))
                : (IdType.Passport, $"P{_idSeq++:D8}");
            var health = _rng.NextDouble();
            return new Participant
            {
                OwnerUserId = ownerId,
                LinkedUserId = relationship == ParticipantRelationship.Self ? ownerId : null,
                Relationship = relationship,
                FullName = name,
                DateOfBirth = dob,
                Gender = gender,
                Nationality = nationality,
                IdType = idType,
                IdNumber = idNumber,
                Phone = Phone(),
                TShirtSize = adult
                    ? gender == Gender.M ? Pick(new[] { TShirtSize.M, TShirtSize.L, TShirtSize.L, TShirtSize.XL, TShirtSize.XXL })
                                         : Pick(new[] { TShirtSize.XS, TShirtSize.S, TShirtSize.S, TShirtSize.M })
                    : TShirtSize.XS,
                EmergencyContactName = VietName(_rng.NextDouble() < 0.5 ? Gender.M : Gender.F),
                EmergencyContactPhone = Phone(),
                BloodType = health < 0.4 ? Pick(BloodTypes) : null,
                MedicalNotes = health < 0.05 ? Pick(MedicalNotes) : null,
                ClubName = adult && _rng.NextDouble() < 0.45 ? Pick(Clubs) : null,
                CreatedAt = createdAt
            };
        }

        private string Cccd(string province, Gender gender, DateOnly dob)
        {
            var centuryGender = (dob.Year >= 2000 ? 2 : 0) + (gender == Gender.F ? 1 : 0);
            return $"{province}{centuryGender}{dob.Year % 100:D2}{_idSeq++:D6}";
        }

        private DateOnly AdultDob()
        {
            var r = _rng.NextDouble();
            var age = r < 0.15 ? _rng.Next(18, 25) : r < 0.48 ? _rng.Next(25, 35) : r < 0.78 ? _rng.Next(35, 45)
                : r < 0.93 ? _rng.Next(45, 55) : _rng.Next(55, 71);
            return _today.AddYears(-age).AddDays(-_rng.Next(0, 365));
        }

        private double Ability() => Math.Clamp(1 + Normal() * 0.16, 0.58, 1.55);

        private double Normal()
        {
            var u1 = 1 - _rng.NextDouble();
            var u2 = _rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        private string Phone() => Pick(PhonePrefixes) + _rng.Next(1_000_000, 9_999_999);

        private string VietName(Gender gender) => $"{Pick(Surnames)} {MiddleAndGiven(gender)}";

        private string MiddleAndGiven(Gender gender) => gender == Gender.M
            ? $"{Pick(MaleMiddle)} {Pick(MaleGiven)}"
            : $"{Pick(FemaleMiddle)} {Pick(FemaleGiven)}";

        private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

        private (CatSpec Spec, RaceCategory Entity) PickWeighted(List<(CatSpec Spec, RaceCategory Entity)> items)
        {
            var total = items.Sum(i => i.Spec.Share);
            var roll = _rng.NextDouble() * total;
            foreach (var item in items)
            {
                roll -= item.Spec.Share;
                if (roll <= 0) return item;
            }
            return items[^1];
        }

        private List<T> Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private DateTime RandomBetween(DateTime from, DateTime to) =>
            to <= from ? from : from.AddSeconds(_rng.NextDouble() * (to - from).TotalSeconds);

        private void Audit(string? userId, string role, string action, string entityType, string? entityId, object? oldValues,
            object? newValues, DateTime at)
        {
            if (at > _now) return;
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId, UserRole = role, Action = action, EntityType = entityType, EntityId = entityId,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json),
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json),
                IpAddress = "127.0.0.1", CreatedAt = at
            });
        }

        private static ConsentRecord Consent(string userId, ConsentType type, bool granted, DateTime at) => new()
        {
            UserId = userId, ConsentType = type, DocumentVersion = 1, Granted = granted, CreatedAt = at
        };

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private static decimal Round1000(decimal value) => Math.Round(value / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        private static int AgeOn(DateOnly dob, DateOnly date)
        {
            var age = date.Year - dob.Year;
            return dob.AddYears(age) > date ? age - 1 : age;
        }

        /// <summary>Tên in BIB: hai từ cuối (hoặc tên gọi), tối đa 16 ký tự.</summary>
        private static string BibNameOf(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = parts.Length >= 2 ? $"{parts[^2]} {parts[^1]}" : fullName;
            if (name.Length > 16) name = parts[^1];
            if (name.Length > 16) name = name[..16];
            return name.ToUpperInvariant();
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Replace('Đ', 'D').Replace('đ', 'd').Normalize(System.Text.NormalizationForm.FormD);
            return new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                                                    System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }
    }

    // ============================ Từ điển ============================

    private static readonly string[] Surnames =
    {
        "Nguyễn", "Nguyễn", "Nguyễn", "Trần", "Trần", "Lê", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng",
        "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý", "Đinh", "Trịnh", "Mai", "Cao", "Lâm", "Tô", "Hà", "Tạ", "Lương", "Kiều"
    };
    private static readonly string[] MaleMiddle = { "Văn", "Đức", "Minh", "Quang", "Hữu", "Thành", "Công", "Hoàng", "Tuấn", "Xuân", "Gia", "Trọng", "Anh", "Ngọc", "Quốc", "Việt" };
    private static readonly string[] FemaleMiddle = { "Thị", "Ngọc", "Thu", "Thanh", "Minh", "Phương", "Mai", "Khánh", "Bảo", "Hồng", "Kim", "Diệu", "Hải", "Thùy", "Mỹ" };
    private static readonly string[] MaleGiven =
    {
        "Anh", "Bảo", "Cường", "Dũng", "Duy", "Đạt", "Hải", "Hiếu", "Hoàng", "Hùng", "Huy", "Khánh", "Khoa", "Kiên", "Lâm",
        "Long", "Lộc", "Minh", "Nam", "Nghĩa", "Phong", "Phúc", "Quân", "Sơn", "Tài", "Thắng", "Thành", "Thịnh", "Toàn",
        "Trung", "Tú", "Tuấn", "Việt", "Vinh", "Vũ", "Hưng", "Đông", "Tiến"
    };
    private static readonly string[] FemaleGiven =
    {
        "An", "Anh", "Chi", "Dung", "Giang", "Hà", "Hằng", "Hạnh", "Hiền", "Hoa", "Hương", "Huyền", "Lan", "Linh", "Loan",
        "Ly", "Mai", "My", "Nga", "Ngân", "Ngọc", "Nhung", "Oanh", "Phương", "Quyên", "Quỳnh", "Tâm", "Thảo", "Thư",
        "Trang", "Uyên", "Vân", "Vy", "Yến", "Trâm", "Hồng"
    };
    private static readonly (string Name, Gender Gender, string Nationality)[] Foreigners =
    {
        ("Kenji Tanaka", Gender.M, "Nhật Bản"), ("Kim Min-jun", Gender.M, "Hàn Quốc"), ("Pierre Dubois", Gender.M, "Pháp"),
        ("John Miller", Gender.M, "Hoa Kỳ"), ("Lars Nielsen", Gender.M, "Đan Mạch"),
        ("Yuki Sato", Gender.F, "Nhật Bản"), ("Park Ji-woo", Gender.F, "Hàn Quốc"), ("Sarah Wilson", Gender.F, "Úc"),
        ("Emma Schneider", Gender.F, "Đức"), ("Chloe Martin", Gender.F, "Pháp"),
    };
    private static readonly string[] Clubs =
    {
        "Hanoi Runners", "Long Biên Running Club", "Saigon Runners Club", "Đà Nẵng Runners", "FPT Runners",
        "Viettel Marathon Club", "VnExpress Runners", "Cần Thơ Running", "Dalat Trail Team", "Vietcombank Runners",
        "Techcombank Running Club", "Sunday Runners", "Hải Phòng Runners", "Huế Runners"
    };
    private static readonly string[] Provinces = { "001", "001", "079", "079", "048", "092", "068", "046", "031", "022", "036", "038" };
    private static readonly string[] PhonePrefixes =
        { "090", "091", "093", "094", "096", "097", "098", "086", "088", "089", "070", "077", "079", "081", "083", "085", "032", "035", "038", "039" };
    private static readonly string[] BloodTypes = { "O+", "O+", "A+", "B+", "AB+", "O-" };
    private static readonly string[] MedicalNotes =
    {
        "Hen suyễn nhẹ, luôn mang bình xịt", "Dị ứng penicillin", "Từng phẫu thuật dây chằng gối (2022)",
        "Huyết áp cao, đang dùng thuốc", "Tiểu đường type 2, mang theo kẹo"
    };
    private static readonly string[] Banks = { "Vietcombank", "Techcombank", "BIDV", "VietinBank", "MB Bank", "ACB", "TPBank" };
}
