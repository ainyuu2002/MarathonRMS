using MarathonRMS.Services.RaceDay;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// UC-91 – chạy nền: mỗi <c>Privacy:PurgeIntervalMinutes</c> phút (mặc định 60) xóa dữ liệu y tế của các giải đã chính thức
/// quá thời hạn giữ (BR-52). Lần đầu chạy ngay sau khi API khởi động. Lỗi một lần chạy chỉ ghi log, lần sau chạy lại.
/// </summary>
public class MedicalDataRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MedicalDataRetentionWorker> _logger;
    private readonly TimeSpan _interval;

    public MedicalDataRetentionWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<MedicalDataRetentionWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Privacy:PurgeIntervalMinutes", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // để API khởi động xong
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var summary = await scope.ServiceProvider.GetRequiredService<IMedicalDataRetentionService>().PurgeDueAsync(stoppingToken);
                foreach (var ev in summary.Events)
                    _logger.LogInformation("BR-52: đã xóa dữ liệu y tế của {Count} đăng ký – giải {EventId} {EventName}.",
                        ev.Registrations, ev.EventId, ev.EventName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Tự xóa dữ liệu y tế (BR-52) thất bại – sẽ chạy lại ở lần sau.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
