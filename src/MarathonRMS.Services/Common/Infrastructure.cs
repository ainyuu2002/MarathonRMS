using System.Security.Claims;
using System.Text.Json;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace MarathonRMS.Services.Common;

/// <summary>Nguồn "giờ hiện tại" duy nhất – giờ Việt Nam. Thay được khi test các BR theo thời gian.</summary>
public interface IDateTimeProvider
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

public class VietnamDateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo VnZone = FindZone();

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);
    public DateOnly Today => DateOnly.FromDateTime(Now);

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }
}

/// <summary>Người dùng đang gọi API (đọc từ JWT).</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    int? OrganizationId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
    bool IsAdminOrAbove { get; }
    string? IpAddress { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public int? OrganizationId => int.TryParse(User?.FindFirstValue(AppClaims.OrganizationId), out var id) ? id : null;
    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();
    public bool IsInRole(string role) => User?.IsInRole(role) == true;
    public bool IsAdminOrAbove => IsInRole(AppRoles.Admin) || IsInRole(AppRoles.SuperAdmin);
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>Ghi nhật ký thao tác nhạy cảm (BR-36). Bản ghi được lưu cùng SaveChanges của nghiệp vụ.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string? entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default);
}

public class AuditLogger : IAuditLogger
{
    // Giữ nguyên tiếng Việt trong nhật ký (không escape thành mã unicode) để SuperAdmin đọc được
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAuditLogRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuditLogger(IAuditLogRepository repository, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task LogAsync(string action, string entityType, string? entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default) =>
        _repository.AddAsync(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserRole = string.Join(",", _currentUser.Roles),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, AuditJson),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, AuditJson),
            IpAddress = _currentUser.IpAddress,
            CreatedAt = _clock.Now
        }, ct);
}

/// <summary>Đọc SystemSettings (có cache). Gọi Invalidate() sau khi SuperAdmin sửa cấu hình.</summary>
public interface ISystemSettingsProvider
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    void Invalidate();
}

public class SystemSettingsProvider : ISystemSettingsProvider
{
    private const string CacheKey = "system-settings";
    private readonly ISystemSettingRepository _repository;
    private readonly IMemoryCache _cache;

    public SystemSettingsProvider(ISystemSettingRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken ct) =>
        (await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return (await _repository.GetAllAsync(ct)).ToDictionary(s => s.Key, s => s.Value);
        }))!;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        (await LoadAsync(ct)).TryGetValue(key, out var v) ? v : null;

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        int.TryParse(await GetAsync(key, ct), out var v) ? v : fallback;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
        decimal.TryParse(await GetAsync(key, ct), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) =>
        bool.TryParse(await GetAsync(key, ct), out var v) ? v : fallback;

    public void Invalidate() => _cache.Remove(CacheKey);
}
