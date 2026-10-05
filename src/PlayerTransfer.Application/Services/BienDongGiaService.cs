using PlayerTransfer.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Tính năng 9 — tổng hợp biến động giá theo kỳ.
///
/// Khác với cảnh báo theo ngưỡng đã có (chỉ báo khi một cầu thủ vượt mốc người dùng
/// đặt), phần này trả lời câu hỏi "tuần này thị trường có gì đáng chú ý" bằng cách
/// so mốc giá mới nhất với mốc gần nhất trước thời điểm cắt.
/// </summary>
public class BienDongGiaService : IBienDongGiaService
{
    private readonly ApplicationDbContext _db;

    public BienDongGiaService(ApplicationDbContext db) => _db = db;

    public async Task<TongHopBienDongDto> TongHopAsync(int soNgay, int soLuong, CancellationToken ct = default)
    {
        var ngay = Math.Clamp(soNgay, 1, 365);
        var soLay = Math.Clamp(soLuong, 1, 50);

        // Khi nguồn đã ngừng cập nhật, kỳ "N ngày gần nhất" tính từ hôm nay sẽ luôn trống.
        // Neo kỳ vào ngày có dữ liệu mới nhất và báo rõ cho người xem.
        var homNay = DateTime.UtcNow.Date;
        var ngayDuLieuMoiNhat = (await _db.LichSuGiaTris.AsNoTracking()
            .MaxAsync(x => (DateTime?)x.NgayGhiNhan, ct))?.Date ?? homNay;
        var denNgay = ngayDuLieuMoiNhat < homNay ? ngayDuLieuMoiNhat : homNay;
        var tuNgay = denNgay.AddDays(-ngay);

        var ketQua = new TongHopBienDongDto
        {
            SoNgay = ngay, TuNgay = tuNgay, DenNgay = denNgay,
            NeoTheoMocDuLieu = denNgay < homNay.AddDays(-1)
        };

        // Lấy toàn bộ mốc giá trong cửa sổ cộng thêm một khoảng đệm để có mốc "trước".
        var moc = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => x.NgayGhiNhan >= tuNgay.AddDays(-ngay) && x.NgayGhiNhan <= denNgay)
            .Select(x => new { x.MaCauThu, x.NgayGhiNhan, x.GiaTri })
            .ToListAsync(ct);

        if (moc.Count == 0) return ketQua;

        var bienDongs = new List<BienDongDto>();

        foreach (var nhom in moc.GroupBy(x => x.MaCauThu))
        {
            var sapXep = nhom.OrderBy(x => x.NgayGhiNhan).ToList();

            var moiNhat = sapXep[^1];
            // Mốc gần nhất nằm TRƯỚC thời điểm cắt; không có thì lấy mốc đầu tiên.
            var truoc = sapXep.LastOrDefault(x => x.NgayGhiNhan <= tuNgay) ?? sapXep[0];

            if (truoc.NgayGhiNhan >= moiNhat.NgayGhiNhan) continue;
            if (truoc.GiaTri <= 0 || truoc.GiaTri == moiNhat.GiaTri) continue;

            bienDongs.Add(new BienDongDto
            {
                MaCauThu = nhom.Key,
                GiaCu = truoc.GiaTri,
                GiaMoi = moiNhat.GiaTri,
                NgayCu = truoc.NgayGhiNhan,
                NgayMoi = moiNhat.NgayGhiNhan,
                PhanTram = Math.Round(100 * (moiNhat.GiaTri - truoc.GiaTri) / truoc.GiaTri, 1)
            });
        }

        ketQua.SoCauThuCoBienDong = bienDongs.Count;
        if (bienDongs.Count == 0) return ketQua;

        var tang = bienDongs.OrderByDescending(x => x.PhanTram).Take(soLay).ToList();
        var giam = bienDongs.OrderBy(x => x.PhanTram).Take(soLay).ToList();

        await GanThongTinAsync(tang.Concat(giam).ToList(), ct);

        ketQua.TangManhNhat = tang.Where(x => x.PhanTram > 0).ToList();
        ketQua.GiamManhNhat = giam.Where(x => x.PhanTram < 0).ToList();

        return ketQua;
    }

    /// <summary>Bổ sung tên, câu lạc bộ, ảnh cho danh sách biến động bằng MỘT truy vấn.</summary>
    private async Task GanThongTinAsync(List<BienDongDto> danhSach, CancellationToken ct)
    {
        var ma = danhSach.Select(x => x.MaCauThu).Distinct().ToList();
        if (ma.Count == 0) return;

        var thongTin = await _db.CauThus.AsNoTracking()
            .Where(x => ma.Contains(x.MaCauThu))
            .Select(x => new
            {
                x.MaCauThu,
                x.HoTen,
                x.AnhDaiDien,
                CauLacBo = x.CauLacBo != null ? x.CauLacBo.TenCLB : null
            })
            .ToDictionaryAsync(x => x.MaCauThu, ct);

        foreach (var bd in danhSach)
        {
            if (!thongTin.TryGetValue(bd.MaCauThu, out var t)) continue;
            bd.HoTen = t.HoTen;
            bd.AnhDaiDien = t.AnhDaiDien;
            bd.CauLacBo = t.CauLacBo;
        }
    }

    public string DungThuHtml(TongHopBienDongDto tongHop, string tenNguoiNhan, string diaChiWeb)
    {
        var html = new System.Text.StringBuilder();
        html.Append($"<p>Xin chào {System.Net.WebUtility.HtmlEncode(tenNguoiNhan)},</p>");
        html.Append($"<p>Tổng hợp biến động giá trị cầu thủ trong {tongHop.SoNgay} ngày qua " +
                    $"({tongHop.TuNgay:dd/MM/yyyy} - {tongHop.DenNgay:dd/MM/yyyy}).</p>");

        void Bang(string tieuDe, List<BienDongDto> danhSach, string mau)
        {
            if (danhSach.Count == 0) return;

            html.Append($"<h3 style=\"margin:18px 0 6px\">{tieuDe}</h3>");
            html.Append("<table cellpadding=\"6\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;width:100%\">");
            html.Append("<tr style=\"background:#e2e8f0;text-align:left\">" +
                        "<th>Cầu thủ</th><th>Câu lạc bộ</th><th align=\"right\">Trước</th>" +
                        "<th align=\"right\">Nay</th><th align=\"right\">Thay đổi</th></tr>");

            foreach (var x in danhSach.Take(10))
            {
                var ten = System.Net.WebUtility.HtmlEncode(x.HoTen ?? "—");
                var clb = System.Net.WebUtility.HtmlEncode(x.CauLacBo ?? "—");

                html.Append("<tr style=\"border-bottom:1px solid #e2e8f0\">");
                html.Append($"<td><a href=\"{diaChiWeb}/Players/ChiTiet/{x.MaCauThu}\">{ten}</a></td>");
                html.Append($"<td>{clb}</td>");
                html.Append(string.Create(DinhDangSo.VN, $"<td align=\"right\">{x.GiaCu:N1}</td>"));
                html.Append(string.Create(DinhDangSo.VN, $"<td align=\"right\">{x.GiaMoi:N1}</td>"));
                html.Append(string.Create(DinhDangSo.VN, $"<td align=\"right\" style=\"color:{mau};font-weight:bold\">{x.PhanTram:+0.0;-0.0}%</td>"));
                html.Append("</tr>");
            }
            html.Append("</table>");
        }

        Bang("Tăng mạnh nhất", tongHop.TangManhNhat, "#166534");
        Bang("Giảm mạnh nhất", tongHop.GiamManhNhat, "#b91c1c");

        html.Append($"<p style=\"margin-top:18px;color:#64748b;font-size:13px\">" +
                    string.Create(DinhDangSo.VN, $"Có {tongHop.SoCauThuCoBienDong:N0} cầu thủ thay đổi giá trong kỳ. ") +
                    $"Thư này gửi tự động từ PlayerMarket.</p>");

        return html.ToString();
    }
}

/// <summary>
/// Tính năng 9 — tiến trình nền gửi thư tổng hợp biến động mỗi tuần cho những
/// người dùng đang theo dõi ít nhất một cầu thủ.
/// </summary>
public class ThuTuanNenService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _cauHinh;
    private readonly ILogger<ThuTuanNenService> _log;

    public ThuTuanNenService(IServiceProvider services,
        Microsoft.Extensions.Configuration.IConfiguration cauHinh, ILogger<ThuTuanNenService> log)
    {
        _services = services; _cauHinh = cauHinh; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GuiMotLuotAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Lỗi khi gửi thư tổng hợp biến động tuần.");
            }

            await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
        }
    }

    private async Task GuiMotLuotAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var bienDong = scope.ServiceProvider.GetRequiredService<IBienDongGiaService>();

        if (!email.DaCauHinh)
        {
            _log.LogInformation("Chưa cấu hình máy chủ thư, bỏ qua thư tổng hợp tuần.");
            return;
        }

        var tongHop = await bienDong.TongHopAsync(7, 10, ct);

        // Dữ liệu đã cũ (nguồn ngừng cập nhật) thì tuần này không có biến động mới nào để báo;
        // gửi lại biến động của kỳ cũ sẽ khiến người nhận hiểu nhầm là tin mới.
        if (tongHop.NeoTheoMocDuLieu || tongHop.SoCauThuCoBienDong == 0) return;

        var diaChiWeb = (_cauHinh["Web:BaseUrl"] ?? "https://localhost:7211").TrimEnd('/');

        // Chỉ gửi cho người đang thực sự dùng chức năng theo dõi.
        var nguoiNhan = await db.TheoDoiCauThus.AsNoTracking()
            .Select(x => x.NguoiDung)
            .Where(x => x.Email != null)
            .Distinct()
            .ToListAsync(ct);

        var daGui = 0;
        foreach (var nd in nguoiNhan)
        {
            if (ct.IsCancellationRequested) break;

            var noiDung = bienDong.DungThuHtml(tongHop, nd.HoTen, diaChiWeb);
            if (await email.GuiAsync(nd.Email, "PlayerMarket — biến động giá tuần này", noiDung, ct))
                daGui++;
        }

        _log.LogInformation("Đã gửi {So} thư tổng hợp biến động tuần.", daGui);
    }
}
