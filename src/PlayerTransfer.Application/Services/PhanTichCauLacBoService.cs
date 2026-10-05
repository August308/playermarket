using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Tính năng 4, 5, 8 — phân tích ở cấp câu lạc bộ:
/// mạng lưới chuyển nhượng, bảng cân đối thu chi, và cơ cấu độ tuổi đội hình.
/// Toàn bộ số liệu lấy từ bảng LichSuChuyenNhuong và CauThu đã nạp từ bộ dữ liệu thật.
/// </summary>
public class PhanTichCauLacBoService : IPhanTichCauLacBoService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _dem;

    public PhanTichCauLacBoService(ApplicationDbContext db, IMemoryCache dem)
    {
        _db = db; _dem = dem;
    }

    // ------------------------------------------ tính năng 4: mạng chuyển nhượng

    public async Task<MangChuyenNhuongDto> MangChuyenNhuongAsync(string muaGiai, int soCungToiDa,
        CancellationToken ct = default)
    {
        var soCung = Math.Clamp(soCungToiDa, 5, 200);
        var khoa = $"mang:{muaGiai}:{soCung}";

        if (_dem.TryGetValue(khoa, out MangChuyenNhuongDto daCo) && daCo != null) return daCo;

        var truyVan = _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.MaCLBDi != null && x.PhiChuyenNhuong > 0);

        if (!string.IsNullOrWhiteSpace(muaGiai))
            truyVan = truyVan.Where(x => x.MuaGiai == muaGiai);

        // Gom theo cặp (CLB đi, CLB đến) ngay trong SQL.
        // Gom theo chính khóa ngoại nullable thay vì .Value — EF Core không phải lúc nào
        // cũng dịch được .Value bên trong biểu thức GroupBy.
        var cungNullable = await truyVan
            .GroupBy(x => new { Di = x.MaCLBDi, Den = x.MaCLBDen })
            .Select(g => new
            {
                g.Key.Di,
                g.Key.Den,
                SoThuongVu = g.Count(),
                TongPhi = g.Sum(x => x.PhiChuyenNhuong)
            })
            .OrderByDescending(x => x.TongPhi)
            .Take(soCung)
            .ToListAsync(ct);

        // Cả MaCLBDi lẫn MaCLBDen đều là khóa ngoại nullable, phải lọc cả hai
        // trước khi bỏ lớp nullable, nếu không kiểu int? sẽ rò ra mọi chỗ bên dưới.
        var cungTho = cungNullable
            .Where(x => x.Di.HasValue && x.Den.HasValue)
            .Select(x => new { Di = x.Di.Value, Den = x.Den.Value, x.SoThuongVu, x.TongPhi })
            .ToList();

        if (cungTho.Count == 0)
        {
            return new MangChuyenNhuongDto { MuaGiai = muaGiai };
        }

        var maCLBs = cungTho.Select(x => x.Di).Concat(cungTho.Select(x => x.Den)).Distinct().ToList();

        var ten = await _db.CauLacBos.AsNoTracking()
            .Where(x => maCLBs.Contains(x.MaCLB))
            .Select(x => new
            {
                x.MaCLB,
                x.TenCLB,
                GiaiDau = x.GiaiDau != null ? x.GiaiDau.TenGiaiDau : null
            })
            .ToDictionaryAsync(x => x.MaCLB, ct);

        string TenCua(int ma) => ten.TryGetValue(ma, out var c) ? c.TenCLB : $"CLB #{ma}";

        var cacCung = cungTho.Select(x => new CungChuyenNhuongDto
        {
            MaCLBDi = x.Di,
            CLBDi = TenCua(x.Di),
            MaCLBDen = x.Den,
            CLBDen = TenCua(x.Den),
            SoThuongVu = x.SoThuongVu,
            TongPhi = Math.Round(x.TongPhi, 2)
        }).ToList();

        var cacNut = maCLBs.Select(ma => new NutCauLacBoDto
        {
            MaCLB = ma,
            TenCLB = TenCua(ma),
            GiaiDau = ten.TryGetValue(ma, out var c) ? c.GiaiDau : null,
            SoBan = cacCung.Where(x => x.MaCLBDi == ma).Sum(x => x.SoThuongVu),
            SoMua = cacCung.Where(x => x.MaCLBDen == ma).Sum(x => x.SoThuongVu),
            TienThu = cacCung.Where(x => x.MaCLBDi == ma).Sum(x => x.TongPhi),
            TienChi = cacCung.Where(x => x.MaCLBDen == ma).Sum(x => x.TongPhi)
        })
        .OrderByDescending(x => x.TienChi + x.TienThu)
        .ToList();

        var ketQua = new MangChuyenNhuongDto
        {
            MuaGiai = muaGiai,
            CacNut = cacNut,
            CacCung = cacCung,
            TongThuongVu = cacCung.Sum(x => x.SoThuongVu),
            TongPhi = Math.Round(cacCung.Sum(x => x.TongPhi), 2)
        };

        _dem.Set(khoa, ketQua, TimeSpan.FromMinutes(20));
        return ketQua;
    }

    // ------------------------------------------ tính năng 5: cân đối câu lạc bộ

    public async Task<KetQua<CanDoiCauLacBoDto>> CanDoiAsync(int maCLB, CancellationToken ct = default)
    {
        var clb = await _db.CauLacBos.AsNoTracking()
            .Where(x => x.MaCLB == maCLB)
            .Select(x => new
            {
                x.MaCLB,
                x.TenCLB,
                x.SanNha,
                GiaiDau = x.GiaiDau != null ? x.GiaiDau.TenGiaiDau : null
            })
            .FirstOrDefaultAsync(ct);

        if (clb == null)
            return KetQua<CanDoiCauLacBoDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy câu lạc bộ.");

        var mocHetHan = DateTime.UtcNow.AddYears(1);

        var doiHinh = await _db.CauThus.AsNoTracking()
            .Where(x => x.MaCLB == maCLB)
            .Select(x => new { x.MaCauThu, x.NgaySinh, x.GiaTriHienTai, x.NgayHetHanHopDong })
            .ToListAsync(ct);

        var ketQua = new CanDoiCauLacBoDto
        {
            MaCLB = clb.MaCLB,
            TenCLB = clb.TenCLB,
            GiaiDau = clb.GiaiDau,
            SanNha = clb.SanNha,
            SoCauThu = doiHinh.Count,
            TongGiaTriDoiHinh = Math.Round(doiHinh.Sum(x => x.GiaTriHienTai), 2),
            TuoiTrungBinh = doiHinh.Count == 0
                ? 0
                : Math.Round(doiHinh.Average(x => MapperHelper.TinhTuoi(x.NgaySinh)), 1),
            SoCauThuSapHetHopDong = doiHinh.Count(x =>
                x.NgayHetHanHopDong != null && x.NgayHetHanHopDong <= mocHetHan)
        };

        // Thu chi theo mùa, gom trong SQL.
        var chiTheoMua = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.MaCLBDen == maCLB && x.PhiChuyenNhuong > 0)
            .GroupBy(x => x.MuaGiai)
            .Select(g => new { Mua = g.Key, Tien = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync(ct);

        var thuTheoMua = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.MaCLBDi == maCLB && x.PhiChuyenNhuong > 0)
            .GroupBy(x => x.MuaGiai)
            .Select(g => new { Mua = g.Key, Tien = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync(ct);

        ketQua.TongChi = Math.Round(chiTheoMua.Sum(x => x.Tien), 2);
        ketQua.TongThu = Math.Round(thuTheoMua.Sum(x => x.Tien), 2);

        var moiMua = chiTheoMua.Select(x => x.Mua).Concat(thuTheoMua.Select(x => x.Mua))
            .Distinct().OrderByDescending(x => x).Take(10).ToList();

        foreach (var mua in moiMua)
        {
            var chi = chiTheoMua.FirstOrDefault(x => x.Mua == mua)?.Tien ?? 0;
            var thu = thuTheoMua.FirstOrDefault(x => x.Mua == mua)?.Tien ?? 0;

            ketQua.TheoMua.Add(new DiemThongKeDto
            {
                Nhan = mua,
                GiaTri = Math.Round(thu - chi, 2),   // số dư
                GiaTriPhu = Math.Round(chi, 2)       // chi tiêu
            });
        }
        ketQua.TheoMua.Reverse();

        ketQua.MuaDatNhat = await TopThuongVuAsync(maCLB, true, ct);
        ketQua.BanDatNhat = await TopThuongVuAsync(maCLB, false, ct);

        var maTop = doiHinh.OrderByDescending(x => x.GiaTriHienTai).Take(8)
            .Select(x => x.MaCauThu).ToList();

        var topCauThu = await _db.CauThus.AsNoTracking()
            .Include(x => x.ViTri).Include(x => x.QuocGia)
            .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
            .Include(x => x.ChiSo)
            .Where(x => maTop.Contains(x.MaCauThu))
            .ToListAsync(ct);

        ketQua.DoiHinhGiaTriNhat = topCauThu
            .OrderByDescending(x => x.GiaTriHienTai)
            .Select(x => MapperHelper.ToTomTat(x))
            .ToList();

        return KetQua<CanDoiCauLacBoDto>.Ok(ketQua);
    }

    private async Task<List<ThuongVuDto>> TopThuongVuAsync(int maCLB, bool laMua, CancellationToken ct)
    {
        var truyVan = laMua
            ? _db.LichSuChuyenNhuongs.AsNoTracking().Where(x => x.MaCLBDen == maCLB)
            : _db.LichSuChuyenNhuongs.AsNoTracking().Where(x => x.MaCLBDi == maCLB);

        return await truyVan
            .Where(x => x.PhiChuyenNhuong > 0)
            .OrderByDescending(x => x.PhiChuyenNhuong)
            .Take(5)
            .Select(x => new ThuongVuDto
            {
                MaCauThu = x.MaCauThu,
                CauThu = x.CauThu.HoTen,
                MuaGiai = x.MuaGiai,
                CLBDi = x.CLBDi != null ? x.CLBDi.TenCLB : null,
                CLBDen = x.CLBDen.TenCLB,
                PhiChuyenNhuong = x.PhiChuyenNhuong,
                GiaTriThiTruongLucDo = x.GiaTriThiTruongLucDo
            })
            .ToListAsync(ct);
    }

    public async Task<List<DiemThongKeDto>> XepHangSoDuAsync(int soLuong, CancellationToken ct = default)
    {
        var soLay = Math.Clamp(soLuong, 5, 50);

        // Gom theo chính khóa nullable ở cả hai vế rồi mới bỏ lớp nullable,
        // để hai danh sách cùng kiểu int và Concat được với nhau.
        var chiTho = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.PhiChuyenNhuong > 0 && x.MaCLBDen != null)
            .GroupBy(x => x.MaCLBDen)
            .Select(g => new { Ma = g.Key, Tien = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync(ct);

        var thuTho = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.PhiChuyenNhuong > 0 && x.MaCLBDi != null)
            .GroupBy(x => x.MaCLBDi)
            .Select(g => new { Ma = g.Key, Tien = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync(ct);

        var chi = chiTho.Where(x => x.Ma.HasValue)
            .Select(x => new { Ma = x.Ma.Value, x.Tien }).ToList();
        var thu = thuTho.Where(x => x.Ma.HasValue)
            .Select(x => new { Ma = x.Ma.Value, x.Tien }).ToList();

        var maCLBs = chi.Select(x => x.Ma).Concat(thu.Select(x => x.Ma)).Distinct().ToList();

        var ten = await _db.CauLacBos.AsNoTracking()
            .Where(x => maCLBs.Contains(x.MaCLB))
            .ToDictionaryAsync(x => x.MaCLB, x => x.TenCLB, ct);

        return maCLBs
            .Select(ma => new DiemThongKeDto
            {
                Ma = ma,
                Nhan = ten.TryGetValue(ma, out var t) ? t : $"CLB #{ma}",
                GiaTri = Math.Round((thu.FirstOrDefault(x => x.Ma == ma)?.Tien ?? 0)
                                    - (chi.FirstOrDefault(x => x.Ma == ma)?.Tien ?? 0), 2),
                GiaTriPhu = Math.Round(chi.FirstOrDefault(x => x.Ma == ma)?.Tien ?? 0, 2)
            })
            .OrderByDescending(x => x.GiaTri)
            .Take(soLay)
            .ToList();
    }

    // --------------------------------------------- tính năng 8: cơ cấu độ tuổi

    private static readonly (string Nhan, int Tu, int Den)[] BacTuoi =
    {
        ("Dưới 21", 0, 20),
        ("21 - 23", 21, 23),
        ("24 - 26", 24, 26),
        ("27 - 29", 27, 29),
        ("30 - 32", 30, 32),
        ("33 trở lên", 33, 99)
    };

    public async Task<KetQua<CoCauTuoiDto>> CoCauTuoiAsync(int maCLB, CancellationToken ct = default)
    {
        var clb = await _db.CauLacBos.AsNoTracking()
            .Where(x => x.MaCLB == maCLB)
            .Select(x => new { x.MaCLB, x.TenCLB })
            .FirstOrDefaultAsync(ct);

        if (clb == null)
            return KetQua<CoCauTuoiDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy câu lạc bộ.");

        var doiHinh = await _db.CauThus.AsNoTracking()
            .Include(x => x.ViTri).Include(x => x.QuocGia)
            .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
            .Include(x => x.ChiSo)
            .Where(x => x.MaCLB == maCLB)
            .ToListAsync(ct);

        if (doiHinh.Count == 0)
            return KetQua<CoCauTuoiDto>.Loi("DOI_HINH_RONG", "Câu lạc bộ này chưa có cầu thủ nào.");

        var tuoiCua = doiHinh.ToDictionary(x => x.MaCauThu, x => MapperHelper.TinhTuoi(x.NgaySinh));

        var ketQua = new CoCauTuoiDto
        {
            MaCLB = clb.MaCLB,
            TenCLB = clb.TenCLB,
            SoCauThu = doiHinh.Count,
            TuoiTrungBinh = Math.Round(doiHinh.Average(x => tuoiCua[x.MaCauThu]), 1)
        };

        // Tuổi trung bình có trọng số theo số phút: phản ánh đội hình THỰC RA ĐANG DÙNG,
        // khác với tuổi trung bình của cả danh sách đăng ký.
        var tongPhut = doiHinh.Sum(x => x.ChiSo?.SoPhutThiDau ?? 0);
        ketQua.TuoiTrungBinhTheoPhut = tongPhut == 0
            ? ketQua.TuoiTrungBinh
            : Math.Round(doiHinh.Sum(x => tuoiCua[x.MaCauThu] * (double)(x.ChiSo?.SoPhutThiDau ?? 0)) / tongPhut, 1);

        foreach (var (nhan, tu, den) in BacTuoi)
        {
            var nhom = doiHinh.Where(x => tuoiCua[x.MaCauThu] >= tu && tuoiCua[x.MaCauThu] <= den).ToList();

            ketQua.ThapTuoi.Add(new BacTuoiDto
            {
                Nhan = nhan,
                TuTuoi = tu,
                DenTuoi = den,
                SoCauThu = nhom.Count,
                TongGiaTri = Math.Round(nhom.Sum(x => x.GiaTriHienTai), 2),
                GiaTriTrungBinh = nhom.Count == 0 ? 0 : Math.Round(nhom.Average(x => x.GiaTriHienTai), 2)
            });
        }

        var mocHetHan = DateTime.UtcNow.AddYears(1);

        ketQua.SapHetHopDong = doiHinh
            .Where(x => x.NgayHetHanHopDong != null && x.NgayHetHanHopDong <= mocHetHan)
            .OrderByDescending(x => x.GiaTriHienTai)
            .Take(10)
            .Select(x => MapperHelper.ToTomTat(x))
            .ToList();

        // Trụ cột lớn tuổi: từ 30 tuổi, thi đấu nhiều, giá trị cao — rủi ro già hóa.
        ketQua.TruCotLonTuoi = doiHinh
            .Where(x => tuoiCua[x.MaCauThu] >= 30 && (x.ChiSo?.SoPhutThiDau ?? 0) >= 900)
            .OrderByDescending(x => x.GiaTriHienTai)
            .Take(10)
            .Select(x => MapperHelper.ToTomTat(x))
            .ToList();

        var tyLeGia = 100.0 * doiHinh.Count(x => tuoiCua[x.MaCauThu] >= 30) / doiHinh.Count;
        var tyLeTre = 100.0 * doiHinh.Count(x => tuoiCua[x.MaCauThu] <= 23) / doiHinh.Count;

        ketQua.CanhBao = (tyLeGia, tyLeTre) switch
        {
            ( > 35, _) => string.Create(DinhDangSo.VN, $"Đội hình đang già hóa: {tyLeGia:N0}% cầu thủ từ 30 tuổi trở lên."),
            (_, > 45) => string.Create(DinhDangSo.VN, $"Đội hình rất trẻ: {tyLeTre:N0}% cầu thủ từ 23 tuổi trở xuống, có thể thiếu kinh nghiệm."),
            ( > 25, < 20) => "Ít cầu thủ trẻ kế cận trong khi nhóm 30+ đã khá đông.",
            _ => "Cơ cấu độ tuổi cân đối."
        };

        if (ketQua.TuoiTrungBinhTheoPhut - ketQua.TuoiTrungBinh > 1.5)
            ketQua.CanhBao += " Đội hình ra sân thường xuyên già hơn đáng kể so với toàn đội.";

        return KetQua<CoCauTuoiDto>.Ok(ketQua);
    }
}
