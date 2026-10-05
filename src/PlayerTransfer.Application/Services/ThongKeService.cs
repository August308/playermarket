using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

public class ThongKeService : IThongKeService
{
    // Báo cáo tổng hợp nặng và ít đổi trong ngày nên được lưu đệm 15 phút (yêu cầu NFR-01, AC-09).
    private static readonly TimeSpan ThoiGianDem = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _dem;

    public ThongKeService(ApplicationDbContext db, IMemoryCache dem)
    {
        _db = db; _dem = dem;
    }

    private Task<T> LayTuDemAsync<T>(string khoa, Func<Task<T>> nap) =>
        _dem.GetOrCreateAsync(khoa, muc =>
        {
            muc.AbsoluteExpirationRelativeToNow = ThoiGianDem;
            return nap();
        });

    public Task<List<DiemThongKeDto>> TheoMuaGiaiAsync(int? maGiaiDau, int soMua)
        => LayTuDemAsync($"tk:mua:{maGiaiDau}:{soMua}", async () =>
    {
        var truyVan = _db.LichSuChuyenNhuongs.AsNoTracking().AsQueryable();
        if (maGiaiDau.HasValue)
            truyVan = truyVan.Where(x => x.CLBDen != null && x.CLBDen.MaGiaiDau == maGiaiDau.Value);

        var ketQua = await truyVan
            .GroupBy(x => x.MuaGiai)
            .Select(g => new DiemThongKeDto
            {
                Nhan = g.Key,
                GiaTri = g.Sum(x => x.PhiChuyenNhuong),
                GiaTriPhu = g.Count()
            })
            .OrderByDescending(x => x.Nhan)
            .Take(soMua <= 0 ? 5 : soMua)
            .ToListAsync();

        return ketQua.OrderBy(x => x.Nhan).ToList();
    });

    public Task<List<DiemThongKeDto>> TheoGiaiDauAsync(string muaGiai)
        => LayTuDemAsync($"tk:giai:{muaGiai}", async () =>
    {
        var truyVan = _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.CLBDen != null);

        if (!string.IsNullOrWhiteSpace(muaGiai))
            truyVan = truyVan.Where(x => x.MuaGiai == muaGiai);

        return await truyVan
            .GroupBy(x => x.CLBDen.GiaiDau.TenGiaiDau)
            .Select(g => new DiemThongKeDto
            {
                Nhan = g.Key,
                GiaTri = g.Sum(x => x.PhiChuyenNhuong),
                GiaTriPhu = g.Count()
            })
            .OrderByDescending(x => x.GiaTri)
            .ToListAsync();
    });

    public async Task<List<ThuongVuDto>> TopThuongVuAsync(string muaGiai, int? maGiaiDau, int soLuong)
    {
        var truyVan = _db.LichSuChuyenNhuongs.AsNoTracking()
            .Include(x => x.CauThu).Include(x => x.CLBDi).Include(x => x.CLBDen)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(muaGiai))
            truyVan = truyVan.Where(x => x.MuaGiai == muaGiai);
        if (maGiaiDau.HasValue)
            truyVan = truyVan.Where(x => x.CLBDen != null && x.CLBDen.MaGiaiDau == maGiaiDau.Value);

        return await truyVan
            .OrderByDescending(x => x.PhiChuyenNhuong)
            .Take(soLuong <= 0 ? 20 : soLuong)
            .Select(x => new ThuongVuDto
            {
                MaCauThu = x.MaCauThu,
                CauThu = x.CauThu.HoTen,
                MuaGiai = x.MuaGiai,
                CLBDi = x.CLBDi != null ? x.CLBDi.TenCLB : (x.TenCLBDiNgoai ?? "CLB ngoài hệ thống"),
                CLBDen = x.CLBDen != null ? x.CLBDen.TenCLB : (x.TenCLBDenNgoai ?? "CLB ngoài hệ thống"),
                PhiChuyenNhuong = x.PhiChuyenNhuong,
                GiaTriThiTruongLucDo = x.GiaTriThiTruongLucDo
            })
            .ToListAsync();
    }

    public async Task<List<DiemThongKeDto>> SoDuCauLacBoAsync(string muaGiai, int? maGiaiDau, int soLuong)
    {
        var truyVan = _db.LichSuChuyenNhuongs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(muaGiai))
            truyVan = truyVan.Where(x => x.MuaGiai == muaGiai);

        var chi = await truyVan.Where(x => x.CLBDen != null
                && (!maGiaiDau.HasValue || x.CLBDen.MaGiaiDau == maGiaiDau.Value))
            .GroupBy(x => x.CLBDen.TenCLB)
            .Select(g => new { Ten = g.Key, Tong = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync();

        var thu = await truyVan.Where(x => x.CLBDi != null
                && (!maGiaiDau.HasValue || x.CLBDi.MaGiaiDau == maGiaiDau.Value))
            .GroupBy(x => x.CLBDi.TenCLB)
            .Select(g => new { Ten = g.Key, Tong = g.Sum(x => x.PhiChuyenNhuong) })
            .ToListAsync();

        var tenCLBs = chi.Select(x => x.Ten).Union(thu.Select(x => x.Ten)).Distinct();

        return tenCLBs.Select(ten => new DiemThongKeDto
        {
            Nhan = ten,
            GiaTri = chi.FirstOrDefault(x => x.Ten == ten)?.Tong ?? 0,
            GiaTriPhu = thu.FirstOrDefault(x => x.Ten == ten)?.Tong ?? 0
        })
        .OrderByDescending(x => x.GiaTri)
        .Take(soLuong <= 0 ? 20 : soLuong)
        .ToList();
    }

    /// <summary>
    /// TK-05 — phân bố giá trị theo vị trí, trả về đủ 5 mốc của biểu đồ hộp:
    /// nhỏ nhất, tứ phân vị 1, trung vị, tứ phân vị 3, lớn nhất.
    /// </summary>
    public Task<List<DiemThongKeDto>> PhanBoTheoViTriAsync(int? maGiaiDau)
        => LayTuDemAsync($"tk:vitri:{maGiaiDau}", async () =>
    {
        var truyVan = _db.CauThus.AsNoTracking().AsQueryable();
        if (maGiaiDau.HasValue)
            truyVan = truyVan.Where(x => x.CauLacBo != null && x.CauLacBo.MaGiaiDau == maGiaiDau.Value);

        var duLieu = await truyVan
            .Select(x => new { Ten = x.ViTri.TenViTri, x.GiaTriHienTai })
            .ToListAsync();

        return duLieu
            .GroupBy(x => x.Ten)
            .Select(g =>
            {
                var giaTris = g.Select(x => x.GiaTriHienTai).OrderBy(x => x).ToList();
                return new DiemThongKeDto
                {
                    Nhan = g.Key,
                    GiaTri = Math.Round(giaTris.Average(), 2),
                    GiaTriPhu = giaTris.Count,
                    NhoNhat = giaTris.First(),
                    TuPhanVi1 = TuPhanVi(giaTris, 0.25),
                    TrungVi = TuPhanVi(giaTris, 0.50),
                    TuPhanVi3 = TuPhanVi(giaTris, 0.75),
                    LonNhat = giaTris.Last()
                };
            })
            .OrderByDescending(x => x.TrungVi)
            .ToList();
    });

    /// <summary>Tứ phân vị theo phương pháp nội suy tuyến tính, danh sách phải đã sắp xếp tăng dần.</summary>
    private static decimal TuPhanVi(List<decimal> daSapXep, double phanVi)
    {
        if (daSapXep.Count == 0) return 0;
        if (daSapXep.Count == 1) return daSapXep[0];

        var viTri = phanVi * (daSapXep.Count - 1);
        var duoi = (int)Math.Floor(viTri);
        var tren = (int)Math.Ceiling(viTri);

        if (duoi == tren) return Math.Round(daSapXep[duoi], 2);

        var ty = (decimal)(viTri - duoi);
        return Math.Round(daSapXep[duoi] + (daSapXep[tren] - daSapXep[duoi]) * ty, 2);
    }

    public Task<List<DiemThongKeDto>> GiaTriTheoDoTuoiAsync(int? maGiaiDau)
        => LayTuDemAsync($"tk:tuoi:{maGiaiDau}", async () =>
    {
        var truyVan = _db.CauThus.AsNoTracking().AsQueryable();
        if (maGiaiDau.HasValue)
            truyVan = truyVan.Where(x => x.CauLacBo != null && x.CauLacBo.MaGiaiDau == maGiaiDau.Value);

        var danhSach = await truyVan
            .Select(x => new { x.NgaySinh, x.GiaTriHienTai })
            .ToListAsync();

        return danhSach
            .GroupBy(x => MapperHelper.TinhTuoi(x.NgaySinh))
            .Where(g => g.Key is >= 16 and <= 42)
            .Select(g => new DiemThongKeDto
            {
                Nhan = g.Key.ToString(),
                GiaTri = Math.Round(g.Average(x => x.GiaTriHienTai), 2),
                GiaTriPhu = g.Count()
            })
            .OrderBy(x => int.Parse(x.Nhan))
            .ToList();
    });

    public Task<List<CauThuTomTatDto>> BangXepHangAsync(string loai, int soLuong)
        => LayTuDemAsync($"tk:bxh:{loai}:{soLuong}", async () =>
    {
        var soLay = soLuong <= 0 ? 10 : soLuong;
        var truyVan = _db.CauThus.AsNoTracking()
            .Include(x => x.ViTri).Include(x => x.QuocGia)
            .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
            .Include(x => x.ChiSo);

        if (loai == "tang_gia" || loai == "giam_gia")
        {
            // Mốc so sánh tính lùi từ NGÀY CÓ DỮ LIỆU MỚI NHẤT chứ không phải từ hôm nay: nguồn
            // Transfermarkt đã ngừng cập nhật, tính từ hôm nay thì mọi cầu thủ đều "không đổi giá"
            // và bảng xếp hạng thành ngẫu nhiên. Cửa sổ 180 ngày ≈ hai đợt định giá.
            var ngayDuLieuMoiNhat = await _db.LichSuGiaTris.AsNoTracking()
                .MaxAsync(x => (DateTime?)x.NgayGhiNhan) ?? DateTime.UtcNow;
            var moc = (ngayDuLieuMoiNhat < DateTime.UtcNow ? ngayDuLieuMoiNhat : DateTime.UtcNow).AddDays(-180);

            // Lấy mốc giá cũ nhất-gần-nhất bằng hai phép gộp chuẩn (Max ngày, rồi Max giá
            // tại ngày đó). Cách cũ dùng GroupBy(...).First() mà EF Core thường KHÔNG dịch
            // được sang SQL — hoặc ném lỗi, hoặc âm thầm kéo cả bảng lịch sử giá về bộ nhớ.
            var ngayMoc = await _db.LichSuGiaTris.AsNoTracking()
                .Where(x => x.NgayGhiNhan <= moc)
                .GroupBy(x => x.MaCauThu)
                .Select(g => new { MaCauThu = g.Key, Ngay = g.Max(x => x.NgayGhiNhan) })
                .ToListAsync();

            var maCoMoc = ngayMoc.Select(x => x.MaCauThu).ToList();

            var giaTheoNgay = await _db.LichSuGiaTris.AsNoTracking()
                .Where(x => maCoMoc.Contains(x.MaCauThu) && x.NgayGhiNhan <= moc)
                .GroupBy(x => new { x.MaCauThu, x.NgayGhiNhan })
                .Select(g => new { g.Key.MaCauThu, g.Key.NgayGhiNhan, Gia = g.Max(x => x.GiaTri) })
                .ToListAsync();

            var giaTriCu = ngayMoc
                .Join(giaTheoNgay,
                    a => new { a.MaCauThu, Ngay = a.Ngay },
                    b => new { b.MaCauThu, Ngay = b.NgayGhiNhan },
                    (a, b) => new { a.MaCauThu, GiaTri = b.Gia })
                .ToDictionary(x => x.MaCauThu, x => x.GiaTri);

            // Chỉ nạp những cầu thủ thật sự có mốc giá cũ, thay vì cả bảng.
            var maCanLay = giaTriCu.Keys.ToList();
            var ungVien = await truyVan.Where(x => maCanLay.Contains(x.MaCauThu)).ToListAsync();

            // Xếp theo mức tăng/giảm TUYỆT ĐỐI (triệu €), không theo phần trăm: theo phần trăm thì
            // cầu thủ vô danh từ 1 lên 8 triệu (+700%) luôn đứng trên một ngôi sao tăng 20 triệu.
            // Bỏ cầu thủ có giá gốc dưới 1 triệu € vì biến động ở mức đó không đáng chú ý.
            var ketHop = ungVien
                .Where(ct => giaTriCu[ct.MaCauThu] >= 1 && ct.GiaTriHienTai != giaTriCu[ct.MaCauThu])
                .Select(ct => new
                {
                    CauThu = ct,
                    ThayDoi = ct.GiaTriHienTai - giaTriCu[ct.MaCauThu]
                })
                .Where(x => loai == "tang_gia" ? x.ThayDoi > 0 : x.ThayDoi < 0);

            var sapXep = loai == "tang_gia"
                ? ketHop.OrderByDescending(x => x.ThayDoi)
                : ketHop.OrderBy(x => x.ThayDoi);

            return sapXep.Take(soLay).Select(x => MapperHelper.ToTomTat(x.CauThu)).ToList();
        }

        var danhSach = loai switch
        {
            "tre_trien_vong" => await truyVan
                .Where(x => x.NgaySinh >= DateTime.UtcNow.AddYears(-22))
                .OrderByDescending(x => x.GiaTriHienTai).Take(soLay).ToListAsync(),
            "sap_het_hop_dong" => await truyVan
                .Where(x => x.NgayHetHanHopDong != null && x.NgayHetHanHopDong <= DateTime.UtcNow.AddYears(1))
                .OrderByDescending(x => x.GiaTriHienTai).Take(soLay).ToListAsync(),
            _ => await truyVan.OrderByDescending(x => x.GiaTriHienTai).Take(soLay).ToListAsync()
        };

        return danhSach.Select(x => MapperHelper.ToTomTat(x)).ToList();
    });

    public Task<List<string>> LayDanhSachMuaGiaiAsync()
        => LayTuDemAsync("tk:muagiai", async () =>
        await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Select(x => x.MuaGiai).Distinct()
            .OrderByDescending(x => x)
            .ToListAsync());
}
