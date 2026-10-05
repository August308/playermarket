using PlayerTransfer.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// C1, C2, C3, C11 — các chức năng phân tích nâng cao.
/// Toàn bộ tính toán dựa trên dữ liệu đã có trong cơ sở dữ liệu, không gọi thêm API ngoài.
/// </summary>
public class TuyenTrachService : ITuyenTrachService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _dem;

    public TuyenTrachService(ApplicationDbContext db, IMemoryCache dem)
    {
        _db = db; _dem = dem;
    }

    private IQueryable<CauThu> TruyVanDayDu() => _db.CauThus.AsNoTracking()
        .Include(x => x.ViTri)
        .Include(x => x.QuocGia)
        .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
        .Include(x => x.ChiSo);

    // ---------------------------------------------------------------- C2. Tuyển trạch

    public async Task<List<MucTieuDto>> TimMucTieuAsync(TieuChiTuyenTrachDto tieuChi)
    {
        tieuChi ??= new TieuChiTuyenTrachDto();
        if (tieuChi.NganSach <= 0) tieuChi.NganSach = 100;

        var truyVan = TruyVanDayDu().Where(x => x.GiaTriHienTai <= tieuChi.NganSach);

        if (tieuChi.MaViTris.Any())
            truyVan = truyVan.Where(x => tieuChi.MaViTris.Contains(x.MaViTri));

        if (tieuChi.MaGiaiDaus.Any())
            truyVan = truyVan.Where(x => x.CauLacBo != null && tieuChi.MaGiaiDaus.Contains(x.CauLacBo.MaGiaiDau));

        if (tieuChi.TuoiToiDa.HasValue)
            truyVan = truyVan.Where(x => x.NgaySinh >= DateTime.UtcNow.Date.AddYears(-tieuChi.TuoiToiDa.Value - 1));

        if (tieuChi.ChiCauThuKhongChanThuong)
            truyVan = truyVan.Where(x => !x.LichSuChanThuongs.Any(c => c.NgayKetThuc == null));

        // Cầu thủ chưa đủ số liệu thi đấu có toàn bộ chỉ số bằng 0; để lọt vào đây
        // thì họ sẽ chiếm chỗ của ứng viên thật chỉ vì rẻ và trẻ.
        truyVan = truyVan.Where(x => x.ChiSo != null && x.ChiSo.NguonChiSo != "KhongCoDuLieu");

        // Lấy rộng rồi chấm điểm trong bộ nhớ để kết hợp nhiều tiêu chí mềm.
        var ungVien = await truyVan
            .OrderByDescending(x => x.GiaTriHienTai)
            .Take(300)
            .ToListAsync();

        var mocHetHopDong = DateTime.UtcNow.AddYears(1);
        var ketQua = new List<MucTieuDto>();

        foreach (var ct in ungVien)
        {
            var chiSo = ct.ChiSo?.ChiSoTongHop ?? 0;
            if (tieuChi.ChiSoToiThieu.HasValue && chiSo < tieuChi.ChiSoToiThieu.Value) continue;

            var tuoi = MapperHelper.TinhTuoi(ct.NgaySinh);
            var lyDo = new List<string>();

            // Điểm phù hợp: 60% chất lượng, 25% giá trị so với ngân sách, 15% tiềm năng theo tuổi.
            var diemChatLuong = chiSo;
            var tyLeNganSach = tieuChi.NganSach > 0 ? (double)(ct.GiaTriHienTai / tieuChi.NganSach) : 1;
            var diemGia = (int)Math.Round((1 - Math.Min(tyLeNganSach, 1)) * 100);

            var diemTuoi = tuoi switch
            {
                <= 21 => 100,
                <= 24 => 88,
                <= 27 => 72,
                <= 30 => 52,
                _ => 28
            };

            var diem = (int)Math.Round(diemChatLuong * 0.60 + diemGia * 0.25 + diemTuoi * 0.15);

            if (chiSo >= 82) lyDo.Add($"Chỉ số cao ({chiSo})");
            if (tyLeNganSach <= 0.5) lyDo.Add(string.Create(DinhDangSo.VN, $"Chỉ chiếm {tyLeNganSach * 100:N0}% ngân sách"));
            if (tuoi <= 23) lyDo.Add($"Còn trẻ ({tuoi} tuổi), còn tăng giá");

            if (ct.NgayHetHanHopDong.HasValue && ct.NgayHetHanHopDong <= mocHetHopDong)
            {
                lyDo.Add($"Hợp đồng hết hạn {ct.NgayHetHanHopDong:MM/yyyy} — dễ đàm phán");
                if (tieuChi.UuTienSapHetHopDong) diem += 12;
            }

            if (ct.MaCLB == null) { lyDo.Add("Cầu thủ tự do, không mất phí chuyển nhượng"); diem += 10; }

            if (lyDo.Count == 0) lyDo.Add("Phù hợp với tiêu chí đã chọn");

            ketQua.Add(new MucTieuDto
            {
                CauThu = MapperHelper.ToTomTat(ct),
                DiemPhuHop = Math.Min(diem, 100),
                LyDo = lyDo,
                PhanTramNganSach = tieuChi.NganSach > 0
                    ? Math.Round(ct.GiaTriHienTai / tieuChi.NganSach * 100, 1)
                    : 0
            });
        }

        return ketQua
            .OrderByDescending(x => x.DiemPhuHop)
            .ThenByDescending(x => x.CauThu.ChiSoTongHop)
            .Take(tieuChi.SoLuong <= 0 ? 12 : tieuChi.SoLuong)
            .ToList();
    }

    // ---------------------------------------------------------------- C3. Tìm người thay thế

    public async Task<List<NguoiThayTheDto>> TimNguoiThayTheAsync(int maCauThu, decimal? nganSachToiDa, int soLuong)
    {
        var goc = await TruyVanDayDu().FirstOrDefaultAsync(x => x.MaCauThu == maCauThu);

        // Không có chỉ số thì mọi phép so khoảng cách đều vô nghĩa: vector toàn số 0
        // sẽ khớp hoàn hảo với mọi cầu thủ cũng thiếu dữ liệu.
        if (goc?.ChiSo == null || goc.ChiSo.NguonChiSo == "KhongCoDuLieu")
            return new List<NguoiThayTheDto>();

        var nhomViTri = goc.ViTri.NhomViTri;
        var tuoiGoc = MapperHelper.TinhTuoi(goc.NgaySinh);

        var truyVan = TruyVanDayDu()
            .Where(x => x.MaCauThu != maCauThu && x.ChiSo != null && x.ViTri.NhomViTri == nhomViTri);

        if (nganSachToiDa.HasValue)
            truyVan = truyVan.Where(x => x.GiaTriHienTai <= nganSachToiDa.Value);

        var ungVien = await truyVan.Take(500).ToListAsync();

        var tenChiSo = TenChiSo.Theo(goc.ChiSo.NguonChiSo);
        int[] Vector(ChiSoCauThu cs) => new[] { cs.TocDo, cs.DutDiem, cs.Chuyen, cs.ReDat, cs.PhongNgu, cs.TheLuc };

        var vGoc = Vector(goc.ChiSo);
        var ketQua = new List<NguoiThayTheDto>();

        foreach (var ct in ungVien)
        {
            var v = Vector(ct.ChiSo);

            // Khoảng cách Euclid trên 6 chỉ số, quy về thang tương đồng 0–100.
            double tong = 0;
            for (var i = 0; i < v.Length; i++) tong += Math.Pow(v[i] - vGoc[i], 2);

            var khoangCach = Math.Sqrt(tong);
            var tuongDongChiSo = Math.Max(0, 100 - khoangCach * 1.6);

            // Chênh lệch tuổi cũng làm giảm mức tương đồng, nhưng nhẹ hơn.
            var lechTuoi = Math.Abs(MapperHelper.TinhTuoi(ct.NgaySinh) - tuoiGoc);
            var doTuongDong = (int)Math.Round(tuongDongChiSo * (1 - Math.Min(lechTuoi, 10) * 0.025));

            if (doTuongDong < 35) continue;

            var manhHon = new List<string>();
            var yeuHon = new List<string>();

            for (var i = 0; i < v.Length; i++)
            {
                if (v[i] - vGoc[i] >= 5) manhHon.Add($"{tenChiSo[i]} +{v[i] - vGoc[i]}");
                else if (vGoc[i] - v[i] >= 5) yeuHon.Add($"{tenChiSo[i]} −{vGoc[i] - v[i]}");
            }

            ketQua.Add(new NguoiThayTheDto
            {
                CauThu = MapperHelper.ToTomTat(ct),
                DoTuongDong = Math.Min(doTuongDong, 99),
                ChenhLechGia = ct.GiaTriHienTai - goc.GiaTriHienTai,
                DiemManhHon = manhHon.Take(3).ToList(),
                DiemYeuHon = yeuHon.Take(3).ToList()
            });
        }

        return ketQua
            .OrderByDescending(x => x.DoTuongDong)
            .ThenBy(x => x.ChenhLechGia)
            .Take(soLuong <= 0 ? 10 : soLuong)
            .ToList();
    }

    // ---------------------------------------------------------------- C11. Trung bình vị trí

    public Task<TrungBinhViTriDto> TrungBinhViTriAsync(int maViTri)
        => _dem.GetOrCreateAsync($"tbvitri:{maViTri}", async muc =>
        {
            muc.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            var duLieu = await _db.CauThus.AsNoTracking()
                .Where(x => x.MaViTri == maViTri && x.ChiSo != null
                            && x.ChiSo.NguonChiSo != "KhongCoDuLieu")
                .Select(x => new
                {
                    TenViTri = x.ViTri.TenViTri,
                    x.ChiSo.TocDo, x.ChiSo.DutDiem, x.ChiSo.Chuyen,
                    x.ChiSo.ReDat, x.ChiSo.PhongNgu, x.ChiSo.TheLuc
                })
                .ToListAsync();

            if (duLieu.Count == 0)
                return new TrungBinhViTriDto { ViTri = "Không rõ", SoCauThu = 0 };

            return new TrungBinhViTriDto
            {
                ViTri = duLieu[0].TenViTri,
                SoCauThu = duLieu.Count,
                TocDo = (int)Math.Round(duLieu.Average(x => x.TocDo)),
                DutDiem = (int)Math.Round(duLieu.Average(x => x.DutDiem)),
                Chuyen = (int)Math.Round(duLieu.Average(x => x.Chuyen)),
                ReDat = (int)Math.Round(duLieu.Average(x => x.ReDat)),
                PhongNgu = (int)Math.Round(duLieu.Average(x => x.PhongNgu)),
                TheLuc = (int)Math.Round(duLieu.Average(x => x.TheLuc))
            };
        });

    // ---------------------------------------------------------------- C1. Xu hướng

    public async Task<List<XuHuongDto>> XuHuongAsync(int soNgay, int soLuong)
    {
        var moc = DateTime.UtcNow.AddDays(-(soNgay <= 0 ? 7 : soNgay));
        var soLay = soLuong <= 0 ? 10 : soLuong;

        var dem = await _db.LuotXemCauThus.AsNoTracking()
            .Where(x => x.ThoiDiem >= moc)
            .GroupBy(x => x.MaCauThu)
            .Select(g => new { MaCauThu = g.Key, SoLuot = g.Count() })
            .OrderByDescending(x => x.SoLuot)
            .Take(soLay)
            .ToListAsync();

        if (dem.Count == 0) return new List<XuHuongDto>();

        var maDs = dem.Select(x => x.MaCauThu).ToList();
        var cauThus = await TruyVanDayDu().Where(x => maDs.Contains(x.MaCauThu)).ToListAsync();

        var ketQua = new List<XuHuongDto>();
        var hang = 1;

        foreach (var d in dem)
        {
            var ct = cauThus.FirstOrDefault(x => x.MaCauThu == d.MaCauThu);
            if (ct == null) continue;

            ketQua.Add(new XuHuongDto
            {
                CauThu = MapperHelper.ToTomTat(ct),
                LuotXem = d.SoLuot,
                ThuHang = hang++
            });
        }

        return ketQua;
    }

    public async Task GhiLuotXemAsync(int maCauThu, int? maNguoiDung)
    {
        // Không chặn luồng hiển thị hồ sơ nếu ghi nhận thất bại.
        try
        {
            _db.LuotXemCauThus.Add(new LuotXemCauThu
            {
                MaCauThu = maCauThu,
                MaNguoiDung = maNguoiDung,
                ThoiDiem = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Bỏ qua: lượt xem chỉ phục vụ thống kê xu hướng.
        }
    }
}
