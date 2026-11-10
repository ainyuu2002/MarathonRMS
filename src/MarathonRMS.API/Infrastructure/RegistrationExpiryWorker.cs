using MarathonRMS.Services.Registrations;

namespace MarathonRMS.API.Infrastructure;

/// <summary>BR-13: định kỳ hết hạn các đăng ký chưa thanh toán và trả lượt coupon.</summary>
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
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<IRegistrationService>().ExpireOverdueAsync(stoppingToken);
                if (count > 0) _logger.LogInformation("BR-13: đã hết hạn {Count} đăng ký chờ thanh toán.", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Xử lý đăng ký hết hạn thất bại; sẽ thử lại sau.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
