using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Tests;

/// <summary>
/// E11 — dựng toàn bộ máy chủ API trong bộ nhớ để kiểm thử tích hợp.
/// Dùng SQLite in-memory (giữ kết nối mở suốt vòng đời) thay cho tệp thật,
/// và tắt mọi tiến trình nền để mỗi lần chạy đều sạch và nhanh.
/// </summary>
public class MayChuKiemThu : WebApplicationFactory<Program>
{
    private SqliteConnection _ketNoi;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cauHinh) =>
        {
            cauHinh.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:TuDongTao"] = "true",
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                // Không sinh 2.000 cầu thủ trong kiểm thử cho nhanh.
                ["Seed:TongCauThu"] = "0",
                ["ChupAnhGia:Bat"] = "false",
                ["FootballApi:BatDongBoDinhKy"] = "false",
                ["FootballApi:Key"] = "",
                ["Jwt:Key"] = "khoa-kiem-thu-rat-dai-toi-thieu-32-ky-tu-cho-hmacsha256",
                ["BatSwagger"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Gỡ đăng ký DbContext trỏ vào tệp và thay bằng SQLite trong bộ nhớ.
            foreach (var mo in services
                         .Where(x => x.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                                     || x.ServiceType == typeof(ApplicationDbContext))
                         .ToList())
            {
                services.Remove(mo);
            }

            _ketNoi = new SqliteConnection("DataSource=:memory:");
            _ketNoi.Open();

            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_ketNoi));

            // Tiến trình nền không cần thiết khi kiểm thử.
            foreach (var mo in services.Where(x => x.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(mo);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _ketNoi?.Dispose();
            _ketNoi = null;
        }
    }
}
