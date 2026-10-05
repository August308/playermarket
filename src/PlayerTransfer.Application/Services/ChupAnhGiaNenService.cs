using PlayerTransfer.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// E2 + E3 — mỗi ngày chụp lại giá trị của toàn bộ cầu thủ vào lịch sử giá,
/// sau đó kiểm tra ngưỡng cảnh báo của người dùng và gửi email khi chạm ngưỡng.
/// </summary>
public class ChupAnhGiaNenService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ChupAnhGiaNenService> _log;

    public ChupAnhGiaNenService(IServiceProvider services, ILogger<ChupAnhGiaNenService> log)
    {
        _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ ứng dụng khởi động xong rồi mới chạy lần đầu.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var realtime = scope.ServiceProvider.GetRequiredService<IThongBaoRealtime>();

                var soMoi = await ChupAnhAsync(db, stoppingToken);
                var soThu = await GuiCanhBaoAsync(db, email, realtime, stoppingToken);

                _log.LogInformation("Chụp ảnh giá: thêm {SoMoi} mốc giá, gửi {SoThu} thư cảnh báo.", soMoi, soThu);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Lỗi khi chụp ảnh giá hằng ngày.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    /// <summary>
    /// Ghi một mốc giá cho cầu thủ nào có giá KHÁC với mốc gần nhất đã lưu (mỗi ngày tối đa một mốc).
    /// Trước đây mốc được ghi cho mọi cầu thủ mỗi ngày dù giá không đổi, sinh hàng nghìn điểm phẳng
    /// làm méo biểu đồ và kéo phần trăm biến động về 0.
    /// </summary>
    public static async Task<int> ChupAnhAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var homNay = DateTime.UtcNow.Date;

        // Mốc gần nhất của từng cầu thủ: lấy ngày lớn nhất, rồi giá tại đúng ngày đó
        // (hai phép gộp chuẩn để EF dịch được sang SQL).
        var ngayGanNhat = await db.LichSuGiaTris.AsNoTracking()
            .GroupBy(x => x.MaCauThu)
            .Select(g => new { MaCauThu = g.Key, Ngay = g.Max(x => x.NgayGhiNhan) })
            .ToListAsync(ct);

        var giaTaiNgay = await db.LichSuGiaTris.AsNoTracking()
            .GroupBy(x => new { x.MaCauThu, x.NgayGhiNhan })
            .Select(g => new { g.Key.MaCauThu, g.Key.NgayGhiNhan, Gia = g.Max(x => x.GiaTri) })
            .ToListAsync(ct);

        var mocCuoi = ngayGanNhat
            .Join(giaTaiNgay,
                a => new { a.MaCauThu, Ngay = a.Ngay },
                b => new { b.MaCauThu, Ngay = b.NgayGhiNhan },
                (a, b) => new { a.MaCauThu, a.Ngay, b.Gia })
            .ToDictionary(x => x.MaCauThu);

        var cauThus = await db.CauThus.AsNoTracking()
            .Select(x => new { x.MaCauThu, x.GiaTriHienTai })
            .ToListAsync(ct);

        var them = cauThus
            .Where(x => !mocCuoi.TryGetValue(x.MaCauThu, out var cuoi)
                        || (cuoi.Ngay < homNay && cuoi.Gia != x.GiaTriHienTai))
            .Select(x => new LichSuGiaTri
            {
                MaCauThu = x.MaCauThu,
                NgayGhiNhan = homNay,
                GiaTri = x.GiaTriHienTai,
                Nguon = "ChupAnhNgay"
            })
            .ToList();

        if (them.Count == 0) return 0;

        db.LichSuGiaTris.AddRange(them);
        await db.SaveChangesAsync(ct);

        return them.Count;
    }

    /// <summary>Gửi email cho người theo dõi khi giá trị chạm ngưỡng họ đặt.</summary>
    public static async Task<int> GuiCanhBaoAsync(ApplicationDbContext db, IEmailService email,
        IThongBaoRealtime realtime, CancellationToken ct)
    {

        // Theo dõi thay đổi (không AsNoTracking) để ghi lại cờ đã cảnh báo.
        var canhBaos = await db.TheoDoiCauThus
            .Include(x => x.NguoiDung)
            .Include(x => x.CauThu)
            .Where(x => x.NguongCanhBaoTren != null || x.NguongCanhBaoDuoi != null)
            .ToListAsync(ct);

        var daGui = 0;

        foreach (var cb in canhBaos)
        {
            if (ct.IsCancellationRequested) break;
            if (cb.CauThu == null || cb.NguoiDung == null) continue;

            var gia = cb.CauThu.GiaTriHienTai;
            string lyDo = null;

            if (cb.NguongCanhBaoTren.HasValue && gia >= cb.NguongCanhBaoTren.Value)
                lyDo = string.Create(DinhDangSo.VN, $"đã vượt ngưỡng trên {cb.NguongCanhBaoTren.Value:N1} triệu €");
            else if (cb.NguongCanhBaoDuoi.HasValue && gia <= cb.NguongCanhBaoDuoi.Value)
                lyDo = string.Create(DinhDangSo.VN, $"đã xuống dưới ngưỡng {cb.NguongCanhBaoDuoi.Value:N1} triệu €");

            if (lyDo == null)
            {
                // Giá đã quay về trong khoảng ngưỡng: lần chạm ngưỡng sau sẽ được báo lại.
                cb.DaCanhBaoLuc = null;
                continue;
            }

            // Đã báo cho lần chạm ngưỡng này rồi — không gửi lại mỗi ngày.
            if (cb.DaCanhBaoLuc != null) continue;

            cb.DaCanhBaoLuc = DateTime.UtcNow;

            // D1 — đẩy ngay xuống trình duyệt nếu người dùng đang mở web.
            await realtime.GuiNguoiDungAsync(cb.MaNguoiDung, "canhbao-gia", new
            {
                maCauThu = cb.MaCauThu,
                hoTen = cb.CauThu.HoTen,
                giaTri = gia,
                lyDo
            }, ct);

            if (!email.DaCauHinh) continue;

            var gui = await email.GuiAsync(cb.NguoiDung.Email,
                $"Cảnh báo giá: {cb.CauThu.HoTen}",
                $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(cb.NguoiDung.HoTen)},</p>" +
                $"<p>Giá trị của <strong>{System.Net.WebUtility.HtmlEncode(cb.CauThu.HoTen)}</strong> " +
                string.Create(DinhDangSo.VN, $"hiện là <strong>{gia:N1} triệu €</strong> — {lyDo}.</p>") +
                $"<p>Bạn có thể tắt cảnh báo này trong mục Theo dõi trên PlayerMarket.</p>", ct);

            if (gui) daGui++;
        }

        await db.SaveChangesAsync(ct);
        return daGui;
    }
}
