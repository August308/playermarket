using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Đồng bộ dữ liệu thật từ API-Football vào cơ sở dữ liệu nội bộ.
///
/// Trình tự: giải đấu → câu lạc bộ → cầu thủ và chỉ số → chuyển nhượng → chấn thương.
/// Nguyên tắc:
///  - Bản ghi có cờ ChinhSuaThuCong = true không bao giờ bị ghi đè (NFR-09).
///  - Một bản ghi lỗi chỉ bị bỏ qua và ghi log, không làm dừng cả lượt đồng bộ.
///  - Dừng sớm khi API chạm ngưỡng hạn mức, phần đã đồng bộ vẫn được giữ lại.
/// </summary>
public class DongBoService : IDongBoService
{
    private readonly ApplicationDbContext _db;
    private readonly IFootballApiClient _api;
    private readonly IConfiguration _cauHinh;
    private readonly ILogger<DongBoService> _log;
    private readonly IThongBaoRealtime _realtime;

    private int _themMoi;
    private int _capNhat;
    private int _loi;

    public DongBoService(ApplicationDbContext db, IFootballApiClient api,
        IConfiguration cauHinh, ILogger<DongBoService> log, IThongBaoRealtime realtime)
    {
        _db = db; _api = api; _cauHinh = cauHinh; _log = log; _realtime = realtime;
    }

    private int[] MaGiaiDauCauHinh
    {
        get
        {
            var ds = _cauHinh.GetSection("FootballApi:MaGiaiDau").Get<int[]>();
            // Mặc định: Premier League, La Liga, Serie A, Bundesliga, Ligue 1.
            return ds is { Length: > 0 } ? ds : new[] { 39, 140, 135, 78, 61 };
        }
    }

    private int MuaGiaiCauHinh
    {
        get
        {
            if (int.TryParse(_cauHinh["FootballApi:MuaGiai"], out var mua) && mua > 2000) return mua;
            return DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        }
    }

    private int SoCLBToiDaMoiLuot =>
        int.TryParse(_cauHinh["FootballApi:SoCLBMoiLuot"], out var v) && v > 0 ? v : 4;

    public async Task<KetQua<string>> DongBoAsync(string phamVi, CancellationToken ct = default)
    {
        await DongCacLuotBiTreoAsync(ct);

        var nhatKy = new NhatKyDongBo
        {
            ThoiDiemBatDau = DateTime.UtcNow,
            PhamVi = string.IsNullOrWhiteSpace(phamVi) ? "Toàn bộ" : phamVi,
            TrangThai = "DangChay"
        };
        _db.NhatKyDongBos.Add(nhatKy);
        await _db.SaveChangesAsync(ct);

        if (!_api.DaCauHinh)
        {
            nhatKy.TrangThai = "BoQua";
            nhatKy.ThoiDiemKetThuc = DateTime.UtcNow;
            nhatKy.ThongDiep = "Chưa cấu hình khóa API bóng đá (FootballApi:Key). Hệ thống đang dùng dữ liệu mẫu.";
            await _db.SaveChangesAsync(ct);
            return KetQua<string>.Loi("CHUA_CAU_HINH", nhatKy.ThongDiep);
        }

        _themMoi = _capNhat = _loi = 0;
        var mua = MuaGiaiCauHinh;

        try
        {
            foreach (var maGiaiNgoai in MaGiaiDauCauHinh)
            {
                if (ct.IsCancellationRequested || _api.HetHanMuc || _api.BiChanBoiGoiDichVu) break;

                var giaiDau = await DongBoGiaiDauAsync(maGiaiNgoai, mua, ct);
                if (giaiDau == null) continue;

                var dsCLB = await DongBoCauLacBoAsync(giaiDau, maGiaiNgoai, mua, ct);

                // Hạn mức gói miễn phí rất thấp, nên mỗi lượt chỉ đi sâu vào một số câu lạc bộ,
                // ưu tiên câu lạc bộ lâu chưa cập nhật nhất — các lượt sau sẽ cuốn chiếu tiếp.
                var maCLBs = dsCLB.Select(x => x.MaCLB).ToList();

                var lanCapNhat = await _db.CauThus.AsNoTracking()
                    .Where(x => x.MaCLB != null && maCLBs.Contains(x.MaCLB.Value))
                    .GroupBy(x => x.MaCLB.Value)
                    .Select(g => new { MaCLB = g.Key, GanNhat = g.Max(x => x.NgayCapNhat) })
                    .ToDictionaryAsync(x => x.MaCLB, x => x.GanNhat, ct);

                var uuTien = dsCLB
                    .OrderBy(x => lanCapNhat.TryGetValue(x.MaCLB, out var moc) ? moc : DateTime.MinValue)
                    .Take(SoCLBToiDaMoiLuot)
                    .ToList();

                foreach (var clb in uuTien)
                {
                    if (ct.IsCancellationRequested || _api.HetHanMuc || _api.BiChanBoiGoiDichVu) break;

                    await DongBoCauThuAsync(clb, giaiDau, mua, ct);
                    await DongBoChuyenNhuongAsync(clb, ct);
                }

                if (!_api.HetHanMuc && !_api.BiChanBoiGoiDichVu)
                    await DongBoChanThuongAsync(maGiaiNgoai, mua, ct);
            }

            // Lỗi do API trả về (HTTP 200 nhưng có trường "errors") trước đây bị nuốt mất,
            // nên nhật ký luôn ghi "ThanhCong — lỗi 0" dù không lấy được gì. Giờ cộng vào số lỗi
            // và chọn trạng thái theo kết quả thật.
            _loi += _api.SoLuotLoi;
            var khongLayDuocGi = _api.SoLuotDaGoi > 0 && _api.SoLuotLoi >= _api.SoLuotDaGoi;

            if (_api.BiChanBoiGoiDichVu || khongLayDuocGi)
            {
                nhatKy.TrangThai = "ThatBai";
                nhatKy.ThongDiep = GiaiThichLoi(_api.LoiGanNhat, mua);
            }
            else if (_api.HetHanMuc)
            {
                nhatKy.TrangThai = "HetHanMuc";
                nhatKy.ThongDiep = "Đã đồng bộ tới khi chạm ngưỡng hạn mức API, phần còn lại sẽ tiếp tục ở lượt sau.";
            }
            else if (_api.SoLuotLoi > 0)
            {
                nhatKy.TrangThai = "MotPhan";
                nhatKy.ThongDiep = $"Đồng bộ xong nhưng {_api.SoLuotLoi}/{_api.SoLuotDaGoi} lượt gọi API bị lỗi. Lỗi gần nhất: {_api.LoiGanNhat}";
            }
            else
            {
                nhatKy.TrangThai = "ThanhCong";
                nhatKy.ThongDiep = "Đồng bộ hoàn tất.";
            }
        }
        catch (Exception ex)
        {
            nhatKy.TrangThai = "ThatBai";
            nhatKy.ThongDiep = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            _log.LogError(ex, "Đồng bộ thất bại.");
        }
        finally
        {
            nhatKy.ThoiDiemKetThuc = DateTime.UtcNow;
            nhatKy.SoBanGhiThemMoi = _themMoi;
            nhatKy.SoBanGhiCapNhat = _capNhat;
            nhatKy.SoBanGhiLoi = _loi;
            nhatKy.SoLuotGoiApi = _api.SoLuotDaGoi;
            await _db.SaveChangesAsync(ct);
        }

        var thongDiep = nhatKy.TrangThai == "ThatBai"
            ? nhatKy.ThongDiep
            : $"Thêm mới {_themMoi}, cập nhật {_capNhat}, lỗi {_loi}, đã dùng {_api.SoLuotDaGoi} lượt gọi API.";

        // D1 — báo cho mọi trình duyệt đang mở biết dữ liệu vừa đổi.
        await _realtime.PhatChungAsync("dong-bo-xong", new
        {
            trangThai = nhatKy.TrangThai,
            themMoi = _themMoi,
            capNhat = _capNhat,
            loi = _loi,
            thongDiep
        }, ct);

        return nhatKy.TrangThai == "ThatBai"
            ? KetQua<string>.Loi("THAT_BAI", nhatKy.ThongDiep)
            : KetQua<string>.Ok(thongDiep, thongDiep);
    }

    /// <summary>
    /// Lượt đồng bộ bị ngắt giữa chừng (tắt máy chủ, lỗi nặng) để lại bản ghi "DangChay" mãi mãi.
    /// Đóng chúng lại trước mỗi lượt mới để nhật ký phản ánh đúng.
    /// </summary>
    private async Task DongCacLuotBiTreoAsync(CancellationToken ct)
    {
        var moc = DateTime.UtcNow.AddHours(-2);
        var biTreo = await _db.NhatKyDongBos
            .Where(x => x.TrangThai == "DangChay" && x.ThoiDiemBatDau < moc)
            .ToListAsync(ct);

        if (biTreo.Count == 0) return;

        foreach (var nk in biTreo)
        {
            nk.TrangThai = "BiGianDoan";
            nk.ThoiDiemKetThuc ??= nk.ThoiDiemBatDau;
            nk.ThongDiep ??= "Lượt đồng bộ bị ngắt giữa chừng (máy chủ dừng trước khi chạy xong).";
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Đổi thông điệp lỗi thô của API thành hướng dẫn xử lý dễ hiểu.</summary>
    private static string GiaiThichLoi(string loiApi, int mua)
    {
        if (string.IsNullOrWhiteSpace(loiApi))
            return "Không lấy được dữ liệu nào từ API bóng đá.";

        if (loiApi.Contains("Free plans do not have access to this season", StringComparison.OrdinalIgnoreCase))
            return $"Gói API miễn phí không cho truy cập mùa {mua}/{mua + 1} (chỉ mở các mùa cũ). " +
                   "Dữ liệu Transfermarkt hiện có mới hơn nên hệ thống không lùi về mùa cũ để tránh ghi đè. " +
                   "Hãy nâng gói API, hoặc tắt FootballApi:BatDongBoDinhKy. Chi tiết từ API: " + loiApi;

        return "API bóng đá từ chối yêu cầu: " + loiApi;
    }

    // ---------------------------------------------------------------- Giải đấu

    private async Task<GiaiDau> DongBoGiaiDauAsync(int maNgoai, int mua, CancellationToken ct)
    {
        var duLieu = await _api.LayAsync($"leagues?id={maNgoai}&season={mua}", ct);
        if (duLieu.Count == 0) return null;

        var banGhi = duLieu[0];
        var ten = banGhi.Chuoi("league", "name");
        if (string.IsNullOrWhiteSpace(ten)) return null;

        var maChuoi = maNgoai.ToString();
        var giaiDau = await _db.GiaiDaus.FirstOrDefaultAsync(x => x.MaNgoaiApi == maChuoi, ct)
                      ?? await _db.GiaiDaus.FirstOrDefaultAsync(x => x.TenGiaiDau == ten, ct);

        if (giaiDau == null)
        {
            giaiDau = new GiaiDau { TenGiaiDau = ten };
            _db.GiaiDaus.Add(giaiDau);
            _themMoi++;
        }
        else
        {
            _capNhat++;
        }

        giaiDau.MaNgoaiApi = maChuoi;
        giaiDau.Logo = banGhi.Chuoi("league", "logo");

        var tenQuocGia = banGhi.Chuoi("country", "name");
        if (!string.IsNullOrWhiteSpace(tenQuocGia))
        {
            var qg = await LayHoacTaoQuocGiaAsync(tenQuocGia, banGhi.Chuoi("country", "code"), ct);
            giaiDau.MaQuocGia = qg.MaQuocGia;
        }

        await _db.SaveChangesAsync(ct);
        return giaiDau;
    }

    // ---------------------------------------------------------------- Câu lạc bộ

    private async Task<List<CauLacBo>> DongBoCauLacBoAsync(GiaiDau giaiDau, int maGiaiNgoai, int mua, CancellationToken ct)
    {
        var ketQua = new List<CauLacBo>();
        var duLieu = await _api.LayAsync($"teams?league={maGiaiNgoai}&season={mua}", ct);

        foreach (var banGhi in duLieu)
        {
            try
            {
                var maNgoai = banGhi.So("team", "id")?.ToString();
                var ten = banGhi.Chuoi("team", "name");
                if (string.IsNullOrWhiteSpace(maNgoai) || string.IsNullOrWhiteSpace(ten)) { _loi++; continue; }

                var clb = await _db.CauLacBos.FirstOrDefaultAsync(x => x.MaNgoaiApi == maNgoai, ct)
                          ?? await _db.CauLacBos.FirstOrDefaultAsync(x => x.TenCLB == ten, ct);

                if (clb == null)
                {
                    clb = new CauLacBo { TenCLB = ten };
                    _db.CauLacBos.Add(clb);
                    _themMoi++;
                }
                else
                {
                    _capNhat++;
                }

                clb.MaNgoaiApi = maNgoai;
                clb.TenCLB = ten;
                clb.Logo = banGhi.Chuoi("team", "logo");
                clb.NamThanhLap = banGhi.So("team", "founded");
                clb.SanNha = banGhi.Chuoi("venue", "name");
                clb.MaGiaiDau = giaiDau.MaGiaiDau;

                ketQua.Add(clb);
            }
            catch (Exception ex)
            {
                _loi++;
                _log.LogWarning(ex, "Bỏ qua một câu lạc bộ lỗi khi đồng bộ.");
            }
        }

        await _db.SaveChangesAsync(ct);
        return ketQua;
    }

    // ---------------------------------------------------------------- Cầu thủ và chỉ số

    private async Task DongBoCauThuAsync(CauLacBo clb, GiaiDau giaiDau, int mua, CancellationToken ct)
    {
        var trang = 1;
        var tongTrang = 1;

        while (trang <= tongTrang && !_api.HetHanMuc && !ct.IsCancellationRequested)
        {
            var duLieu = await _api.LayAsync(
                $"players?team={clb.MaNgoaiApi}&season={mua}&page={trang}", ct);

            if (duLieu.Count == 0) break;

            foreach (var banGhi in duLieu)
            {
                try
                {
                    await LuuMotCauThuAsync(banGhi, clb, giaiDau, mua, ct);
                }
                catch (Exception ex)
                {
                    _loi++;
                    _log.LogWarning(ex, "Bỏ qua một cầu thủ lỗi khi đồng bộ.");
                }
            }

            await _db.SaveChangesAsync(ct);

            // API trả tối đa 20 bản ghi mỗi trang; chỉ đi tiếp khi còn dữ liệu.
            tongTrang = duLieu.Count >= 20 ? trang + 1 : trang;
            trang++;
        }
    }

    private async Task LuuMotCauThuAsync(JsonElement banGhi, CauLacBo clb, GiaiDau giaiDau, int mua, CancellationToken ct)
    {
        var maNgoai = banGhi.So("player", "id")?.ToString();
        var hoTen = banGhi.Chuoi("player", "name");
        if (string.IsNullOrWhiteSpace(maNgoai) || string.IsNullOrWhiteSpace(hoTen)) { _loi++; return; }

        var cauThu = await _db.CauThus.Include(x => x.ChiSo)
            .FirstOrDefaultAsync(x => x.MaNgoaiApi == maNgoai, ct);

        // Khớp thêm theo tên đã bỏ dấu để không tạo trùng với dữ liệu sẵn có.
        var tenKhongDau = ChuanHoa.BoDau(hoTen);
        cauThu ??= await _db.CauThus.Include(x => x.ChiSo)
            .FirstOrDefaultAsync(x => x.TenKhongDau == tenKhongDau, ct);

        if (cauThu != null && cauThu.ChinhSuaThuCong)
            return;   // Tôn trọng dữ liệu quản trị viên đã sửa tay.

        // Không để số liệu của một mùa CŨ (ví dụ gói API miễn phí chỉ cho mùa 2024) ghi đè lên
        // cầu thủ đã có số liệu mùa mới hơn: chỉ ghi lại mã liên kết rồi bỏ qua.
        var muaChuoi = $"{mua}/{mua + 1}";
        if (cauThu?.ChiSo != null && string.CompareOrdinal(cauThu.ChiSo.MuaGiai, muaChuoi) > 0)
        {
            cauThu.MaNgoaiApi ??= maNgoai;
            return;
        }

        // Cầu thủ đến từ bộ dữ liệu Transfermarkt có giá trị thị trường THẬT.
        // API-Football không cung cấp giá trị thị trường, giá ở đây là số ước lượng,
        // nên tuyệt đối không được ghi đè lên số liệu thật.
        var giuGiaTriThat = cauThu != null && cauThu.NguonDuLieu == "Transfermarkt";

        var thongKe = banGhi.Mang("statistics").FirstOrDefault();

        var viTriChuoi = thongKe.ValueKind == JsonValueKind.Object
            ? thongKe.Chuoi("games", "position")
            : null;
        var nhom = UocLuongHelper.NhomTuChuoi(viTriChuoi);
        var viTri = await LayViTriAsync(nhom, ct);

        var tenQuocGia = banGhi.Chuoi("player", "nationality") ?? "Không rõ";
        var quocGia = await LayHoacTaoQuocGiaAsync(tenQuocGia, null, ct);

        var ngaySinh = banGhi.Ngay("player", "birth", "date")
                       ?? DateTime.UtcNow.Date.AddYears(-(banGhi.So("player", "age") ?? 24));

        var moi = cauThu == null;
        if (moi)
        {
            cauThu = new CauThu();
            _db.CauThus.Add(cauThu);
            _themMoi++;
        }
        else
        {
            _capNhat++;
        }

        cauThu.MaNgoaiApi = maNgoai;
        cauThu.HoTen = hoTen;
        cauThu.TenKhongDau = ChuanHoa.BoDau(hoTen);
        cauThu.AnhDaiDien = banGhi.Chuoi("player", "photo");
        cauThu.NgaySinh = ngaySinh;
        cauThu.ChieuCao = banGhi.So("player", "height");
        cauThu.CanNang = banGhi.So("player", "weight");
        cauThu.MaQuocGia = quocGia.MaQuocGia;
        cauThu.MaCLB = clb.MaCLB;
        cauThu.MaViTri = viTri.MaViTri;
        cauThu.SoAo = thongKe.ValueKind == JsonValueKind.Object ? thongKe.So("games", "number") : null;
        cauThu.NgayCapNhat = DateTime.UtcNow;

        // --- Chỉ số suy ra từ số liệu thi đấu ---
        if (thongKe.ValueKind == JsonValueKind.Object)
        {
            var chiSoMoi = UocLuongHelper.TinhChiSo(
                nhom,
                thongKe.SoThuc("games", "rating"),
                thongKe.So("games", "appearences") ?? 0,
                thongKe.So("games", "minutes") ?? 0,
                thongKe.So("goals", "total") ?? 0,
                thongKe.So("goals", "assists") ?? 0,
                thongKe.So("passes", "accuracy"),
                thongKe.So("duels", "won") ?? 0,
                thongKe.So("duels", "total") ?? 0,
                thongKe.So("tackles", "total") ?? 0,
                $"{mua}/{mua + 1}");

            chiSoMoi.TheVang = thongKe.So("cards", "yellow") ?? 0;
            chiSoMoi.TheDo = thongKe.So("cards", "red") ?? 0;

            if (cauThu.ChiSo == null)
            {
                cauThu.ChiSo = chiSoMoi;
            }
            else
            {
                var cu = cauThu.ChiSo;

                // Số liệu thi đấu của API là số thật, luôn cập nhật.
                cu.MuaGiai = chiSoMoi.MuaGiai; cu.SoTran = chiSoMoi.SoTran;
                cu.SoPhutThiDau = chiSoMoi.SoPhutThiDau; cu.BanThang = chiSoMoi.BanThang;
                cu.KienTao = chiSoMoi.KienTao; cu.TheVang = chiSoMoi.TheVang; cu.TheDo = chiSoMoi.TheDo;

                // Sáu chỉ số kiểu game là số ước lượng. Chỉ ghi đè khi bản ghi hiện tại
                // KHÔNG phải chỉ số tính từ thống kê thật của bộ dữ liệu.
                if (cu.NguonChiSo != "TinhTuThongKe")
                {
                    cu.TocDo = chiSoMoi.TocDo; cu.DutDiem = chiSoMoi.DutDiem; cu.Chuyen = chiSoMoi.Chuyen;
                    cu.ReDat = chiSoMoi.ReDat; cu.PhongNgu = chiSoMoi.PhongNgu; cu.TheLuc = chiSoMoi.TheLuc;
                    cu.NguonChiSo = "UocLuongTuGia";
                    cu.DiemPhongDo = 0;
                }
            }

            // --- Giá trị thị trường ước lượng ---
            var tuoi = MapperHelper.TinhTuoi(cauThu.NgaySinh);
            var giaTriMoi = UocLuongHelper.UocLuongGiaTri(
                cauThu.ChiSo.ChiSoTongHop, tuoi, giaiDau.TenGiaiDau, cauThu.ChiSo.SoPhutThiDau);

            if (!giuGiaTriThat && cauThu.GiaTriHienTai != giaTriMoi)
            {
                cauThu.GiaTriHienTai = giaTriMoi;
                cauThu.GiaTriCaoNhat = Math.Max(cauThu.GiaTriCaoNhat, giaTriMoi);

                await _db.SaveChangesAsync(ct);   // cần MaCauThu trước khi ghi lịch sử

                var homNay = DateTime.UtcNow.Date;
                var daCo = await _db.LichSuGiaTris
                    .AnyAsync(x => x.MaCauThu == cauThu.MaCauThu && x.NgayGhiNhan == homNay, ct);

                if (!daCo)
                {
                    _db.LichSuGiaTris.Add(new LichSuGiaTri
                    {
                        MaCauThu = cauThu.MaCauThu,
                        NgayGhiNhan = homNay,
                        GiaTri = giaTriMoi,
                        Nguon = "UocLuong"
                    });
                }
            }
        }
    }

    // ---------------------------------------------------------------- Chuyển nhượng

    private async Task DongBoChuyenNhuongAsync(CauLacBo clb, CancellationToken ct)
    {
        var duLieu = await _api.LayAsync($"transfers?team={clb.MaNgoaiApi}", ct);

        foreach (var banGhi in duLieu)
        {
            try
            {
                var maCauThuNgoai = banGhi.So("player", "id")?.ToString();
                if (string.IsNullOrWhiteSpace(maCauThuNgoai)) continue;

                var cauThu = await _db.CauThus.FirstOrDefaultAsync(x => x.MaNgoaiApi == maCauThuNgoai, ct);
                if (cauThu == null) continue;   // chỉ ghi thương vụ của cầu thủ đã có trong hệ thống

                foreach (var tv in banGhi.Mang("transfers"))
                {
                    var ngay = tv.Ngay("date");
                    if (ngay == null) continue;

                    var (loai, phi) = UocLuongHelper.DocLoaiChuyenNhuong(tv.Chuoi("type"));

                    var maDen = tv.So("teams", "in", "id")?.ToString();
                    var maDi = tv.So("teams", "out", "id")?.ToString();

                    var clbDen = await LayCLBTheoMaNgoaiAsync(maDen, tv.Chuoi("teams", "in", "name"), clb.MaGiaiDau, ct);
                    var clbDi = await LayCLBTheoMaNgoaiAsync(maDi, tv.Chuoi("teams", "out", "name"), clb.MaGiaiDau, ct);

                    var daCo = await _db.LichSuChuyenNhuongs.AnyAsync(x =>
                        x.MaCauThu == cauThu.MaCauThu &&
                        x.NgayChuyenNhuong == ngay.Value &&
                        x.MaCLBDen == (clbDen != null ? clbDen.MaCLB : (int?)null), ct);

                    if (daCo) continue;

                    _db.LichSuChuyenNhuongs.Add(new LichSuChuyenNhuong
                    {
                        MaCauThu = cauThu.MaCauThu,
                        MaCLBDi = clbDi?.MaCLB,
                        MaCLBDen = clbDen?.MaCLB,
                        NgayChuyenNhuong = ngay.Value,
                        MuaGiai = MapperHelper.MuaGiaiCuaNgay(ngay.Value),
                        PhiChuyenNhuong = phi,
                        GiaTriThiTruongLucDo = cauThu.GiaTriHienTai,
                        LoaiChuyenNhuong = loai
                    });

                    _themMoi++;
                }
            }
            catch (Exception ex)
            {
                _loi++;
                _log.LogWarning(ex, "Bỏ qua một thương vụ lỗi khi đồng bộ.");
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Chấn thương

    private async Task DongBoChanThuongAsync(int maGiaiNgoai, int mua, CancellationToken ct)
    {
        var duLieu = await _api.LayAsync($"injuries?league={maGiaiNgoai}&season={mua}", ct);

        foreach (var banGhi in duLieu)
        {
            try
            {
                var maCauThuNgoai = banGhi.So("player", "id")?.ToString();
                if (string.IsNullOrWhiteSpace(maCauThuNgoai)) continue;

                var cauThu = await _db.CauThus.FirstOrDefaultAsync(x => x.MaNgoaiApi == maCauThuNgoai, ct);
                if (cauThu == null) continue;

                var ngay = banGhi.Ngay("fixture", "date") ?? DateTime.UtcNow.Date;
                var loai = banGhi.Chuoi("player", "type") ?? banGhi.Chuoi("player", "reason") ?? "Không rõ";

                var daCo = await _db.LichSuChanThuongs.AnyAsync(x =>
                    x.MaCauThu == cauThu.MaCauThu && x.NgayBatDau == ngay, ct);

                if (daCo) continue;

                // API chỉ cho biết cầu thủ vắng mặt ở một trận cụ thể, không có ngày hồi phục.
                // Trận đã qua hơn hai tuần thì coi như đã kết thúc tại ngày đó, nếu không mọi
                // bản ghi của mùa cũ sẽ hiện "Đang điều trị" mãi mãi.
                var daQua = ngay < DateTime.UtcNow.Date.AddDays(-14);

                _db.LichSuChanThuongs.Add(new LichSuChanThuong
                {
                    MaCauThu = cauThu.MaCauThu,
                    LoaiChanThuong = loai.Length > 150 ? loai[..150] : loai,
                    NgayBatDau = ngay,
                    NgayKetThuc = daQua ? ngay : null,
                    SoTranVang = 1,
                    MuaGiai = $"{mua}/{mua + 1}"
                });

                _themMoi++;
            }
            catch (Exception ex)
            {
                _loi++;
                _log.LogWarning(ex, "Bỏ qua một bản ghi chấn thương lỗi khi đồng bộ.");
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Tiện ích

    private async Task<QuocGia> LayHoacTaoQuocGiaAsync(string ten, string maISO, CancellationToken ct)
    {
        var quocGia = await _db.QuocGias.FirstOrDefaultAsync(x => x.TenQuocGia == ten, ct);
        if (quocGia != null)
        {
            if (string.IsNullOrWhiteSpace(quocGia.MaISO) && !string.IsNullOrWhiteSpace(maISO))
                quocGia.MaISO = maISO;
            return quocGia;
        }

        quocGia = new QuocGia
        {
            TenQuocGia = ten,
            MaISO = !string.IsNullOrWhiteSpace(maISO)
                ? maISO
                : new string(ten.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant()
        };

        _db.QuocGias.Add(quocGia);
        await _db.SaveChangesAsync(ct);
        return quocGia;
    }

    private async Task<ViTriThiDau> LayViTriAsync(NhomViTri nhom, CancellationToken ct)
    {
        var vietTat = UocLuongHelper.VietTatMacDinh(nhom);

        var viTri = await _db.ViTriThiDaus.FirstOrDefaultAsync(x => x.VietTat == vietTat, ct);
        if (viTri != null) return viTri;

        viTri = new ViTriThiDau
        {
            VietTat = vietTat,
            TenViTri = nhom switch
            {
                NhomViTri.ThuMon => "Thủ môn",
                NhomViTri.HauVe => "Trung vệ",
                NhomViTri.TienVe => "Tiền vệ trung tâm",
                _ => "Trung phong"
            },
            NhomViTri = nhom
        };

        _db.ViTriThiDaus.Add(viTri);
        await _db.SaveChangesAsync(ct);
        return viTri;
    }

    private async Task<CauLacBo> LayCLBTheoMaNgoaiAsync(string maNgoai, string ten, int maGiaiDauMacDinh, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(maNgoai) && string.IsNullOrWhiteSpace(ten)) return null;

        CauLacBo clb = null;

        if (!string.IsNullOrWhiteSpace(maNgoai))
            clb = await _db.CauLacBos.FirstOrDefaultAsync(x => x.MaNgoaiApi == maNgoai, ct);

        if (clb == null && !string.IsNullOrWhiteSpace(ten))
            clb = await _db.CauLacBos.FirstOrDefaultAsync(x => x.TenCLB == ten, ct);

        if (clb != null || string.IsNullOrWhiteSpace(ten)) return clb;

        // Câu lạc bộ ngoài phạm vi đồng bộ (ví dụ đội ở giải khác) — vẫn tạo để giữ đúng lịch sử.
        clb = new CauLacBo { TenCLB = ten, MaNgoaiApi = maNgoai, MaGiaiDau = maGiaiDauMacDinh };
        _db.CauLacBos.Add(clb);
        await _db.SaveChangesAsync(ct);
        _themMoi++;

        return clb;
    }
}
