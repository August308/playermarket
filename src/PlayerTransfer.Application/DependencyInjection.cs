using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Application.Services;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application;

public static class DependencyInjection
{
    /// <summary>Đăng ký cơ sở dữ liệu và toàn bộ dịch vụ nghiệp vụ.</summary>
    public static IServiceCollection ThemDichVuUngDung(this IServiceCollection services, IConfiguration cauHinh)
    {
        var provider = cauHinh["Database:Provider"] ?? "SqlServer";
        var chuoiKetNoi = cauHinh.GetConnectionString("Default")
                          ?? "Server=(localdb)\\MSSQLLocalDB;Database=QLCauThu;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(chuoiKetNoi);
            else
                options.UseSqlServer(chuoiKetNoi);
        });

        services.AddMemoryCache();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IXacThucService, XacThucService>();
        services.AddScoped<ICauThuService, CauThuService>();
        services.AddScoped<IThongKeService, ThongKeService>();
        services.AddScoped<ITheoDoiService, TheoDoiService>();
        services.AddScoped<IDoiHinhService, DoiHinhService>();
        services.AddScoped<IDanhMucService, DanhMucService>();
        services.AddScoped<IQuanTriService, QuanTriService>();
        services.AddScoped<ITuyenTrachService, TuyenTrachService>();
        services.AddScoped<ITroChoiService, TroChoiService>();
        services.AddScoped<IDongBoService, DongBoService>();
        services.AddScoped<IBaoCaoService, BaoCaoService>();
        services.AddScoped<INhapCsvService, NhapCsvService>();

        // 10 tính năng phân tích mở rộng.
        services.AddScoped<IDuDoanGiaService, DuDoanGiaService>();
        services.AddScoped<IPhanTichCauLacBoService, PhanTichCauLacBoService>();
        services.AddScoped<IHieuCauHoiService, HieuCauHoiService>();
        services.AddScoped<IBienDongGiaService, BienDongGiaService>();
        services.AddScoped<IKiemChungDuLieuService, KiemChungDuLieuService>();

        // D1 — nếu tầng trình bày không đăng ký kênh SignalR thì dùng bản rỗng
        // để các dịch vụ nghiệp vụ vẫn chạy được (ví dụ trong unit test).
        services.AddSingleton<IThongBaoRealtime, ThongBaoRong>();

        // Chưa có khóa API thì dùng bản giả lập để hệ thống vẫn chạy với dữ liệu mẫu.
        if (string.IsNullOrWhiteSpace(cauHinh["FootballApi:Key"]))
        {
            services.AddScoped<IFootballApiClient, MockFootballApiClient>();
        }
        else
        {
            services.AddHttpClient<IFootballApiClient, FootballApiClient>();
        }

        return services;
    }
}
