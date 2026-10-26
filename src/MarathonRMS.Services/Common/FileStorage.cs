using Microsoft.Extensions.Options;

namespace MarathonRMS.Services.Common;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    /// <summary>Thư mục vật lý chứa file tải lên (API cấu hình = wwwroot/uploads).</summary>
    public string RootPath { get; set; } = "";
    /// <summary>Đường dẫn web tương ứng với RootPath, VD "/uploads".</summary>
    public string RequestPath { get; set; } = "/uploads";
    /// <summary>Địa chỉ gốc công khai của API để tạo URL tuyệt đối, VD "https://localhost:7040".</summary>
    public string PublicBaseUrl { get; set; } = "";
}

/// <summary>Lưu file người dùng tải lên. Bản local lưu vào thư mục của API; có thể thay bằng cloud storage mà không đổi Service.</summary>
public interface IFileStorage
{
    /// <summary>Lưu stream vào thư mục con với tên mới (GUID) – không dùng tên gốc (BR-51). Trả đường dẫn tương đối, VD "/uploads/events/3/ab12….jpg".</summary>
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
    Task DeleteAsync(string relativeUrl, CancellationToken ct = default);
    /// <summary>Đường dẫn tương đối ⇒ URL tuyệt đối để WebApp hiển thị.</summary>
    string ToPublicUrl(string relativeUrl);
}

public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    public LocalFileStorage(IOptions<FileStorageOptions> options) => _options = options.Value;

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        var safeFolder = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p is not ("." or "..")));
        var directory = Path.Combine(_options.RootPath, safeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using (var file = File.Create(Path.Combine(directory, fileName)))
            await content.CopyToAsync(file, ct);
        return $"{_options.RequestPath.TrimEnd('/')}/{safeFolder}/{fileName}";
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        var prefix = _options.RequestPath.TrimEnd('/') + "/";
        if (!relativeUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;
        var relative = relativeUrl[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_options.RootPath, relative));
        // Chỉ xóa file nằm trong RootPath
        if (fullPath.StartsWith(Path.GetFullPath(_options.RootPath), StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string ToPublicUrl(string relativeUrl) =>
        relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? relativeUrl : _options.PublicBaseUrl.TrimEnd('/') + relativeUrl;
}
