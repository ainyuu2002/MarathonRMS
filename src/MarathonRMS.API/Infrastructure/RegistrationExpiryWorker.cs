using MarathonRMS.Services.Registrations;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// BR-13 – chạy nền mỗi phút: đăng ký chờ thanh toán quá hạn giữ chỗ ⇒ Expired, trả suất và lượt coupon.
/// Lỗi một lần chạy chỉ ghi log, lần sau chạy lại.
/// </summary>
public class RegistrationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RegistrationExpiryWorker> _logger;

    public RegistrationExpiryWorker(IServiceScopeFactory scopes, ILogger<RegistrationExpiryWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // để API khởi động xong (migrate + seed)
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<IRegistrationService>().ExpireOverdueAsync(stoppingToken);
                if (count > 0) _logger.LogInformation("BR-13: {Count} đăng ký hết hạn giữ chỗ đã chuyển sang Expired.", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Xử lý đăng ký hết hạn (BR-13) thất bại – sẽ chạy lại ở lần sau.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
