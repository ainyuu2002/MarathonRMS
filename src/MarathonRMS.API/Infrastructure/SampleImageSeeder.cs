using System.Text.Json;
using System.Text.RegularExpressions;
using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Ảnh cho dữ liệu mẫu: banner giải + ảnh album (ảnh thật Wikimedia Commons, nguồn ở SampleData/images/CREDITS.md),
/// logo BTC (tự vẽ). Chạy mỗi lần khởi động khi bật Seed:SampleData:
/// <list type="bullet">
/// <item>điền BannerUrl / LogoUrl còn trống và tạo album cho giải đã thi chưa có ảnh (BTC tự đổi ảnh thì giữ nguyên);</item>
/// <item>đồng bộ file mẫu trong wwwroot/uploads với SampleData/images: file thiếu hoặc khác bản gốc thì chép đè
///   (DB tạo từ bản ảnh cũ cũng được cập nhật, thư mục uploads bị xóa cũng được khôi phục);</item>
/// <item>ảnh album mẫu (tên "sample-…") trỏ về đúng file hiện có theo loại ảnh; loại không còn ảnh thì gỡ khỏi album.</item>
/// </list>
/// Banner/logo lưu URL tuyệt đối (WebApp hiển thị thẳng); ảnh album lưu đường dẫn tương đối như ảnh BTC tải lên (UC-89).
/// </summary>
public static class SampleImageSeeder
{
    private const string SampleFolder = "sample";
    private const string MediaPrefix = "sample-";

    private static readonly Dictionary<string, string> Captions = new()
    {
        ["start"] = "Vạch xuất phát", ["warmup"] = "Khởi động trước giờ xuất phát", ["water"] = "Trạm tiếp nước",
        ["finish"] = "Khoảnh khắc về đích", ["medal"] = "Huy chương finisher", ["podium"] = "Lễ trao giải",
        ["trail"] = "Đường trail xuyên rừng", ["summit"] = "Vượt dốc lên đỉnh", ["night"] = "Đường chạy đêm",
        ["coast"] = "Cung đường ven biển", ["kit"] = "Phát race kit", ["kids"] = "Đường chạy Kids",
    };

    public static async Task EnsureAsync(IServiceProvider services, ILogger logger)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var source = Path.Combine(env.ContentRootPath, "SampleData", "images");
        if (!Directory.Exists(source)) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarathonDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
        var today = DateOnly.FromDateTime(now);
        var uploads = options.RootPath;
        var request = options.RequestPath.TrimEnd('/');
        var updatedFiles = 0;

        // 1. Banner + logo: uploads/sample/{banners,logos} luôn khớp bản gốc
        updatedFiles += SyncFolder(Path.Combine(source, "banners"), Path.Combine(uploads, SampleFolder, "banners"));
        updatedFiles += SyncFolder(Path.Combine(source, "logos"), Path.Combine(uploads, SampleFolder, "logos"));

        var banners = 0;
        foreach (var ev in await db.Events.Where(e => e.BannerUrl == null).ToListAsync())
        {
            if (!File.Exists(Path.Combine(source, "banners", ev.Slug + ".jpg"))) continue;
            ev.BannerUrl = storage.ToPublicUrl($"{request}/{SampleFolder}/banners/{ev.Slug}.jpg");
            banners++;
        }
        foreach (var org in await db.Organizations.Where(o => o.LogoUrl == null).ToListAsync())
        {
            if (File.Exists(Path.Combine(source, "logos", org.Slug + ".png")))
                org.LogoUrl = storage.ToPublicUrl($"{request}/{SampleFolder}/logos/{org.Slug}.png");
        }

        // 2. Album cho giải đã thi chưa có ảnh nào
        var pool = Directory.GetFiles(Path.Combine(source, "gallery"), "*.jpg")
            .ToDictionary(f => KindOf(Path.GetFileNameWithoutExtension(f)), Path.GetFileName);
        var credits = LoadCredits(Path.Combine(source, "credits.json"));
        var albums = 0;
        var pastEvents = await db.Events
            .Where(e => e.EventDate < today && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Postponed &&
                        !db.EventMedia.Any(m => m.EventId == e.EventId))
            .Select(e => new { e.EventId, e.Name, e.Slug, e.EventDate, e.CreatedById,
                Trail = db.RaceCategories.Any(c => c.EventId == e.EventId && c.Discipline == Discipline.Trail),
                Kids = db.RaceCategories.Any(c => c.EventId == e.EventId && c.CategoryType == CategoryType.Kids) })
            .ToListAsync();
        foreach (var ev in pastEvents)
        {
            var kinds = new List<string> { "start", "warmup", "water", "finish", "medal", "podium" };
            if (ev.Trail) kinds.InsertRange(1, new[] { "trail", "summit" });
            else if (ev.Name.Contains("Night")) kinds.Insert(1, "night");
            else if (ev.Slug.Contains("coastal") || ev.Slug.Contains("beach")) kinds.Insert(1, "coast");
            else kinds.Insert(1, "kit");
            if (ev.Kids) kinds.Add("kids");

            var uploadedAt = ev.EventDate.AddDays(1).ToDateTime(new TimeOnly(10, 0));
            if (uploadedAt > now) uploadedAt = now;
            var order = 1;
            foreach (var kind in kinds.Where(pool.ContainsKey))
                db.EventMedia.Add(new EventMedia
                {
                    EventId = ev.EventId, FileUrl = $"{request}/events/{ev.EventId}/{MediaPrefix}{pool[kind]}",
                    Caption = CaptionFor(kind, ev.Name, credits, pool), Kind = MediaKind.Gallery, DisplayOrder = order++,
                    IsVisible = true, UploadedById = ev.CreatedById, UploadedAt = uploadedAt.AddMinutes(order)
                });
            albums++;
        }
        await db.SaveChangesAsync();

        // 3. Đồng bộ ảnh album mẫu (tên "sample-<số>-<loại>.jpg") với bản gốc hiện tại
        var names = await db.Events.ToDictionaryAsync(e => e.EventId, e => e.Name);
        var removed = 0;
        foreach (var media in await db.EventMedia.Where(m => m.FileUrl.Contains("/" + MediaPrefix)).ToListAsync())
        {
            var fileName = Path.GetFileName(media.FileUrl);
            if (!fileName.StartsWith(MediaPrefix) || !media.FileUrl.StartsWith(request + "/", StringComparison.OrdinalIgnoreCase))
                continue;
            var kind = KindOf(Path.GetFileNameWithoutExtension(fileName[MediaPrefix.Length..]));
            var oldPath = PhysicalPath(uploads, request, media.FileUrl);
            if (!pool.TryGetValue(kind, out var current))
            {
                // Loại ảnh không còn trong bộ ảnh mẫu (VD lễ trao giải) ⇒ gỡ khỏi album
                if (File.Exists(oldPath)) File.Delete(oldPath);
                db.EventMedia.Remove(media);
                removed++;
                continue;
            }
            var expected = $"{request}/events/{media.EventId}/{MediaPrefix}{current}";
            var moved = !string.Equals(media.FileUrl, expected, StringComparison.Ordinal);
            if (moved)
            {
                if (File.Exists(oldPath)) File.Delete(oldPath);
                media.FileUrl = expected;
            }
            // Chú thích chỉ viết lại khi đổi ảnh hoặc còn thiếu nguồn ảnh – BTC tự sửa chú thích thì giữ nguyên
            if (moved || media.Caption?.Contains(" · Ảnh: ") != true)
                media.Caption = CaptionFor(kind, names.GetValueOrDefault(media.EventId, ""), credits, pool);
            if (CopyIfDifferent(Path.Combine(source, "gallery", current), PhysicalPath(uploads, request, expected)))
                updatedFiles++;
        }
        await db.SaveChangesAsync();

        if (banners + albums + updatedFiles + removed > 0)
            logger.LogInformation("Ảnh mẫu: gắn {Banners} banner, tạo {Albums} album, cập nhật {Files} file, gỡ {Removed} ảnh cũ.",
                banners, albums, updatedFiles, removed);
    }

    private static string CaptionFor(string kind, string eventName, Dictionary<string, string> credits,
        Dictionary<string, string> pool)
    {
        var caption = $"{Captions.GetValueOrDefault(kind, "Ảnh giải")} – {eventName}";
        // Ảnh Wikimedia Commons (CC BY / BY-SA): ghi tác giả + giấy phép ngay trong chú thích
        if (pool.TryGetValue(kind, out var file) && credits.TryGetValue("gallery/" + file, out var credit))
            caption += $" · Ảnh: {credit}";
        return caption.Length > 300 ? caption[..300] : caption;
    }

    /// <summary>credits.json (do tools/sample-images/build_photos.py sinh) ⇒ "gallery/01-start.jpg" → "Tác giả (CC BY 4.0), Wikimedia Commons".</summary>
    private static Dictionary<string, string> LoadCredits(string path)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(path)) return result;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in doc.RootElement.EnumerateObject())
        {
            var artist = item.Value.TryGetProperty("artist", out var a) ? a.GetString() ?? "" : "";
            var license = item.Value.TryGetProperty("license", out var l) ? l.GetString() ?? "" : "";
            artist = Regex.Replace(artist, @"\s+", " ").Trim();
            if (artist.Length > 60) artist = artist[..60] + "…";
            result[item.Name] = $"{(artist.Length > 0 ? artist : "không rõ tác giả")} ({license}), Wikimedia Commons";
        }
        return result;
    }

    /// <summary>"03-water" ⇒ "water".</summary>
    private static string KindOf(string name) => name.Contains('-') ? name[(name.IndexOf('-') + 1)..] : name;

    private static string PhysicalPath(string uploads, string request, string url) =>
        Path.Combine(uploads, url[(request.Length + 1)..].Replace('/', Path.DirectorySeparatorChar));

    private static int SyncFolder(string from, string to)
    {
        if (!Directory.Exists(from)) return 0;
        return Directory.GetFiles(from).Count(file => CopyIfDifferent(file, Path.Combine(to, Path.GetFileName(file))));
    }

    /// <summary>Chép khi file đích chưa có hoặc khác bản gốc (so kích thước rồi nội dung). Trả true nếu đã chép.</summary>
    private static bool CopyIfDifferent(string from, string to)
    {
        if (!File.Exists(from)) return false;
        if (File.Exists(to))
        {
            var a = new FileInfo(from);
            var b = new FileInfo(to);
            if (a.Length == b.Length && File.ReadAllBytes(from).AsSpan().SequenceEqual(File.ReadAllBytes(to))) return false;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to, overwrite: true);
        return true;
    }
}
