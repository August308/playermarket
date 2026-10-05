using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Application.Services;

/// <summary>Chạy đồng bộ định kỳ trong nền (mặc định 24 giờ một lần).</summary>
public class DongBoNenService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DongBoNenService> _log;

    public DongBoNenService(IServiceProvider services, ILogger<DongBoNenService> log)
    {
        _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ 1 phút sau khi khởi động để ứng dụng sẵn sàng.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dongBo = scope.ServiceProvider.GetRequiredService<IDongBoService>();
                var ketQua = await dongBo.DongBoAsync("Định kỳ", stoppingToken);
                _log.LogInformation("Kết quả đồng bộ định kỳ: {ThongDiep}", ketQua.ThongDiep);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Lỗi khi chạy đồng bộ định kỳ.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
