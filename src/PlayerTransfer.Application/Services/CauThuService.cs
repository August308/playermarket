using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

public class CauThuService : ICauThuService
{
    private readonly ApplicationDbContext _db;

    public CauThuService(ApplicationDbContext db) => _db = db;

    private IQueryable<CauThu> TruyVanCoBan() => _db.CauThus
        .Include(x => x.ViTri)
        .Include(x => x.QuocGia)
        .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
        .Include(x => x.ChiSo)
        .AsNoTracking();

    /// <summary>
    /// D5 — chiếu thẳng sang DTO ngay trong câu truy vấn: cơ sở dữ liệu chỉ trả về
    /// đúng các cột cần hiển thị, không nạp cả thực thể và các bảng liên quan.
    /// </summary>
    private static IQueryable<CauThuChieuDto> Chieu(IQueryable<CauThu> truyVan) =>
        truyVan.Select(x => new CauThuChieuDto
        {
            MaCauThu = x.MaCauThu,
            HoTen = x.HoTen,
            AnhDaiDien = x.AnhDaiDien,
            NgaySinh = x.NgaySinh,
            MaViTri = x.MaViTri,
            ViTri = x.ViTri.TenViTri,
            ViTriVietTat = x.ViTri.VietTat,
            CauLacBo = x.CauLacBo != null ? x.CauLacBo.TenCLB : null,
            GiaiDau = x.CauLacBo != null ? x.CauLacBo.GiaiDau.TenGiaiDau : null,
            QuocTich = x.QuocGia.TenQuocGia,
            MaISO = x.QuocGia.MaISO,
            GiaTriHienTai = x.GiaTriHienTai,
            // Phải khớp đúng công thức ChiSoTongHop của thực thể ChiSoCauThu,
            // nếu không trang danh sách và trang chi tiết sẽ hiện hai con số khác nhau.
            TongChiSo = x.ChiSo == null || x.ChiSo.NguonChiSo == "KhongCoDuLieu"
                ? 0
                : x.ChiSo.NguonChiSo == "TinhTuThongKe"
                    ? x.ChiSo.DiemPhongDo
                    : (int)Math.Round((x.ChiSo.TocDo + x.ChiSo.DutDiem + x.ChiSo.Chuyen
                                       + x.ChiSo.ReDat + x.ChiSo.PhongNgu + x.ChiSo.TheLuc) / 6.0),
            DangChanThuong = x.LichSuChanThuongs.Any(c => c.NgayKetThuc == null)
        });

    /// <summary>Bản chiếu trung gian — chỉ chứa các cột cần cho thẻ cầu thủ.</summary>
    private class CauThuChieuDto
    {
        public int MaCauThu { get; set; }
        public string HoTen { get; set; }
        public string AnhDaiDien { get; set; }
        public DateTime NgaySinh { get; set; }
        public int MaViTri { get; set; }
        public string ViTri { get; set; }
        public string ViTriVietTat { get; set; }
        public string CauLacBo { get; set; }
        public string GiaiDau { get; set; }
        public string QuocTich { get; set; }
        public string MaISO { get; set; }
        public decimal GiaTriHienTai { get; set; }
        public int TongChiSo { get; set; }
        public bool DangChanThuong { get; set; }
    }

    private static CauThuTomTatDto ToTomTat(CauThuChieuDto x) => new()
    {
        MaCauThu = x.MaCauThu,
        HoTen = x.HoTen,
        AnhDaiDien = x.AnhDaiDien,
        Tuoi = MapperHelper.TinhTuoi(x.NgaySinh),
        MaViTri = x.MaViTri,
        ViTri = x.ViTri,
        ViTriVietTat = x.ViTriVietTat,
        CauLacBo = x.CauLacBo ?? "Tự do",
        GiaiDau = x.GiaiDau,
        QuocTich = x.QuocTich,
        MaISO = x.MaISO,
        GiaTriHienTai = x.GiaTriHienTai,
        ChiSoTongHop = x.TongChiSo,
        DangChanThuong = x.DangChanThuong
    };

    public async Task<TrangDuLieu<CauThuTomTatDto>> TimKiemAsync(string tuKhoa, int trang, int kichThuocTrang)
    {
        var boLoc = new BoLocCauThuDto
        {
            TuKhoa = tuKhoa,
            Trang = trang,
            KichThuocTrang = kichThuocTrang,
            SapXep = "giatri_giam"
        };
        return await LocAsync(boLoc);
    }

    /// <summary>
    /// D3 — gợi ý có chịu lỗi chính tả: lọc thô ở cơ sở dữ liệu theo ký tự đầu,
    /// sau đó chấm điểm độ liên quan bằng khoảng cách Levenshtein trong bộ nhớ.
    /// </summary>
    public async Task<List<CauThuTomTatDto>> GoiYAsync(string tuKhoa, int soLuong)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa)) return new List<CauThuTomTatDto>();

        var khoa = ChuanHoa.BoDau(tuKhoa);
        var soLay = soLuong <= 0 ? 5 : soLuong;

        // 1) Khớp trực tiếp — nhanh và đủ dùng trong phần lớn trường hợp.
        var khopThang = await Chieu(_db.CauThus.AsNoTracking()
                .Where(x => x.TenKhongDau.Contains(khoa))
                .OrderByDescending(x => x.GiaTriHienTai)
                .Take(soLay * 3))
            .ToListAsync();

        var ungVien = khopThang;

        // 2) Gõ sai chính tả: nới ra theo vài ký tự đầu rồi chấm điểm trong bộ nhớ.
        if (ungVien.Count < soLay && khoa.Length >= 3)
        {
            var doanDau = TimKiemHelper.DoanDauDeLoc(khoa);

            var moRong = await Chieu(_db.CauThus.AsNoTracking()
                    .Where(x => x.TenKhongDau.StartsWith(doanDau)
                                || x.TenKhongDau.Contains(doanDau))
                    .OrderByDescending(x => x.GiaTriHienTai)
                    .Take(400))
                .ToListAsync();

            var daCo = ungVien.Select(x => x.MaCauThu).ToHashSet();
            ungVien = ungVien.Concat(moRong.Where(x => !daCo.Contains(x.MaCauThu))).ToList();
        }

        return ungVien
            .Select(x => new { CauThu = x, Diem = TimKiemHelper.ChamDiem(khoa, ChuanHoa.BoDau(x.HoTen)) })
            .Where(x => x.Diem > 0)
            .OrderByDescending(x => x.Diem)
            .ThenByDescending(x => x.CauThu.GiaTriHienTai)
            .Take(soLay)
            .Select(x => ToTomTat(x.CauThu))
            .ToList();
    }

    public async Task<TrangDuLieu<CauThuTomTatDto>> LocAsync(BoLocCauThuDto boLoc)
    {
        boLoc ??= new BoLocCauThuDto();
        var truyVan = _db.CauThus.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(boLoc.TuKhoa))
        {
            var khoa = ChuanHoa.BoDau(boLoc.TuKhoa);
            truyVan = truyVan.Where(x => x.TenKhongDau.Contains(khoa)
                                      || (x.CauLacBo != null && x.CauLacBo.TenCLB.Contains(boLoc.TuKhoa)));
        }

        if (boLoc.MaGiaiDaus.Any())
            truyVan = truyVan.Where(x => x.CauLacBo != null && boLoc.MaGiaiDaus.Contains(x.CauLacBo.MaGiaiDau));

        if (boLoc.MaCLBs.Any())
            truyVan = truyVan.Where(x => x.MaCLB.HasValue && boLoc.MaCLBs.Contains(x.MaCLB.Value));

        if (boLoc.MaViTris.Any())
            truyVan = truyVan.Where(x => boLoc.MaViTris.Contains(x.MaViTri));

        if (boLoc.MaQuocGias.Any())
            truyVan = truyVan.Where(x => boLoc.MaQuocGias.Contains(x.MaQuocGia));

        if (boLoc.GiaTriTu.HasValue)
            truyVan = truyVan.Where(x => x.GiaTriHienTai >= boLoc.GiaTriTu.Value);

        if (boLoc.GiaTriDen.HasValue)
            truyVan = truyVan.Where(x => x.GiaTriHienTai <= boLoc.GiaTriDen.Value);

        if (boLoc.ChieuCaoTu.HasValue)
            truyVan = truyVan.Where(x => x.ChieuCao >= boLoc.ChieuCaoTu.Value);

        if (!string.IsNullOrWhiteSpace(boLoc.ChanThuan)
            && Enum.TryParse<ChanThuan>(boLoc.ChanThuan, out var chan))
            truyVan = truyVan.Where(x => x.ChanThuan == chan);

        if (boLoc.CauThuTuDo == true)
            truyVan = truyVan.Where(x => x.MaCLB == null);

        if (boLoc.SapHetHopDong == true)
        {
            var moc = DateTime.UtcNow.AddYears(1);
            truyVan = truyVan.Where(x => x.NgayHetHanHopDong != null && x.NgayHetHanHopDong <= moc);
        }

        // Lọc theo tuổi quy đổi thành khoảng ngày sinh để truy vấn chạy được trên cơ sở dữ liệu.
        var homNay = DateTime.UtcNow.Date;
        if (boLoc.TuoiTu.HasValue)
            truyVan = truyVan.Where(x => x.NgaySinh <= homNay.AddYears(-boLoc.TuoiTu.Value));
        if (boLoc.TuoiDen.HasValue)
            truyVan = truyVan.Where(x => x.NgaySinh >= homNay.AddYears(-boLoc.TuoiDen.Value - 1));

        if (boLoc.DangChanThuong.HasValue)
        {
            var dangChanThuong = _db.LichSuChanThuongs.Where(x => x.NgayKetThuc == null).Select(x => x.MaCauThu);
            truyVan = boLoc.DangChanThuong.Value
                ? truyVan.Where(x => dangChanThuong.Contains(x.MaCauThu))
                : truyVan.Where(x => !dangChanThuong.Contains(x.MaCauThu));
        }

        truyVan = boLoc.SapXep switch
        {
            "giatri_tang" => truyVan.OrderBy(x => x.GiaTriHienTai),
            "tuoi_tang" => truyVan.OrderByDescending(x => x.NgaySinh),
            "tuoi_giam" => truyVan.OrderBy(x => x.NgaySinh),
            "ten_az" => truyVan.OrderBy(x => x.HoTen),
            // Sắp xếp phải dùng đúng chỉ số đang hiển thị; cầu thủ chưa có dữ liệu
            // được quy về 0 để không bị NULL đẩy lên đầu bảng.
            "chiso_giam" => truyVan.OrderByDescending(x =>
                x.ChiSo == null || x.ChiSo.NguonChiSo == "KhongCoDuLieu" ? 0
                : x.ChiSo.NguonChiSo == "TinhTuThongKe" ? x.ChiSo.DiemPhongDo
                : (x.ChiSo.TocDo + x.ChiSo.DutDiem + x.ChiSo.Chuyen
                   + x.ChiSo.ReDat + x.ChiSo.PhongNgu + x.ChiSo.TheLuc) / 6),
            _ => truyVan.OrderByDescending(x => x.GiaTriHienTai)
        };

        var tongSo = await truyVan.CountAsync();
        var trang = boLoc.Trang <= 0 ? 1 : boLoc.Trang;
        var kichThuoc = boLoc.KichThuocTrang is <= 0 or > 100 ? 20 : boLoc.KichThuocTrang;

        var danhSach = await Chieu(truyVan.Skip((trang - 1) * kichThuoc).Take(kichThuoc)).ToListAsync();

        return new TrangDuLieu<CauThuTomTatDto>
        {
            DanhSach = danhSach.Select(ToTomTat).ToList(),
            TongSo = tongSo,
            Trang = trang,
            KichThuocTrang = kichThuoc
        };
    }

    public async Task<CauThuChiTietDto> LayChiTietAsync(int maCauThu)
    {
        var ct = await TruyVanCoBan().FirstOrDefaultAsync(x => x.MaCauThu == maCauThu);
        if (ct == null) return null;

        var dangChanThuong = await _db.LichSuChanThuongs
            .AnyAsync(x => x.MaCauThu == maCauThu && x.NgayKetThuc == null);

        var chuyenNhuongs = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu).ToListAsync();

        var mocBaMua = DateTime.UtcNow.AddYears(-3);
        var chanThuongs = await _db.LichSuChanThuongs.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu && x.NgayBatDau >= mocBaMua).ToListAsync();

        var coDuLieuChanThuong = await CoDuLieuChanThuongAsync(ct.MaNgoaiApi, chanThuongs.Count > 0);

        var mocMotThang = DateTime.UtcNow.AddDays(-35);
        var giaTriCu = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu && x.NgayGhiNhan <= mocMotThang)
            .OrderByDescending(x => x.NgayGhiNhan)
            .Select(x => (decimal?)x.GiaTri)
            .FirstOrDefaultAsync();

        // Lần định giá trước: mốc gần nhất (tính lùi) có giá khác giá hiện tại.
        var mocTruoc = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu && x.GiaTri != ct.GiaTriHienTai)
            .OrderByDescending(x => x.NgayGhiNhan)
            .Select(x => new { x.NgayGhiNhan, x.GiaTri })
            .FirstOrDefaultAsync();

        var tomTat = MapperHelper.ToTomTat(ct, dangChanThuong);
        var chiTiet = new CauThuChiTietDto
        {
            MaCauThu = tomTat.MaCauThu,
            HoTen = tomTat.HoTen,
            AnhDaiDien = tomTat.AnhDaiDien,
            Tuoi = tomTat.Tuoi,
            MaViTri = tomTat.MaViTri,
            ViTri = tomTat.ViTri,
            ViTriVietTat = tomTat.ViTriVietTat,
            CauLacBo = tomTat.CauLacBo,
            GiaiDau = tomTat.GiaiDau,
            QuocTich = tomTat.QuocTich,
            MaISO = tomTat.MaISO,
            GiaTriHienTai = tomTat.GiaTriHienTai,
            ChiSoTongHop = tomTat.ChiSoTongHop,
            DangChanThuong = tomTat.DangChanThuong,
            NgaySinh = ct.NgaySinh,
            ChieuCao = ct.ChieuCao,
            CanNang = ct.CanNang,
            ChanThuan = MapperHelper.TenChanThuan(ct.ChanThuan),
            SoAo = ct.SoAo,
            NgayHetHanHopDong = ct.NgayHetHanHopDong,
            GiaTriCaoNhat = ct.GiaTriCaoNhat,
            ChiSo = MapperHelper.ToChiSo(ct.ChiSo),
            TongSoThuongVu = chuyenNhuongs.Count,
            TongPhiChuyenNhuong = chuyenNhuongs.Sum(x => x.PhiChuyenNhuong),
            SoNgayNghiChanThuong3Mua = chanThuongs
                .Sum(x => (int)((x.NgayKetThuc ?? DateTime.UtcNow) - x.NgayBatDau).TotalDays),
            CoDuLieuChanThuong = coDuLieuChanThuong,
            GiaTriMocTruoc = mocTruoc?.GiaTri,
            NgayMocTruoc = mocTruoc?.NgayGhiNhan,
            PhanTramSoVoiMocTruoc = mocTruoc is { GiaTri: > 0 }
                ? Math.Round((ct.GiaTriHienTai - mocTruoc.GiaTri) / mocTruoc.GiaTri * 100, 1)
                : null,
            PhanTramThayDoi30Ngay = giaTriCu is > 0
                ? Math.Round((ct.GiaTriHienTai - giaTriCu.Value) / giaTriCu.Value * 100, 1)
                : 0
        };

        return chiTiet;
    }

    public async Task<List<ChuyenNhuongDto>> LayLichSuChuyenNhuongAsync(int maCauThu)
    {
        return await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Include(x => x.CLBDi).Include(x => x.CLBDen)
            .Where(x => x.MaCauThu == maCauThu)
            .OrderByDescending(x => x.NgayChuyenNhuong)
            .Select(x => new ChuyenNhuongDto
            {
                MaChuyenNhuong = x.MaChuyenNhuong,
                MuaGiai = x.MuaGiai,
                NgayChuyenNhuong = x.NgayChuyenNhuong,
                // Không có CLB trong hệ thống KHÔNG có nghĩa là "tự do": thường là CLB ngoài 5 giải
                // hoặc đội trẻ. Dùng tên lưu kèm nếu có, không thì nói rõ là ngoài hệ thống.
                CLBDi = x.CLBDi != null ? x.CLBDi.TenCLB : (x.TenCLBDiNgoai ?? "CLB ngoài hệ thống"),
                CLBDen = x.CLBDen != null ? x.CLBDen.TenCLB : (x.TenCLBDenNgoai ?? "CLB ngoài hệ thống"),
                MaLoai = (int)x.LoaiChuyenNhuong,
                LoaiChuyenNhuong = x.LoaiChuyenNhuong == LoaiChuyenNhuong.KhongRo ? "Chưa công bố phí"
                    : x.LoaiChuyenNhuong == LoaiChuyenNhuong.MuaDut ? "Mua đứt"
                    : x.LoaiChuyenNhuong == LoaiChuyenNhuong.ChoMuon ? "Cho mượn"
                    : x.LoaiChuyenNhuong == LoaiChuyenNhuong.TuDo ? "Không mất phí"
                    : x.LoaiChuyenNhuong == LoaiChuyenNhuong.HetHan ? "Hết hạn hợp đồng"
                    : "Kết thúc cho mượn",
                PhiChuyenNhuong = x.PhiChuyenNhuong,
                GiaTriThiTruongLucDo = x.GiaTriThiTruongLucDo
            })
            .ToListAsync();
    }

    public async Task<List<ChanThuongDto>> LayLichSuChanThuongAsync(int maCauThu)
    {
        var danhSach = await _db.LichSuChanThuongs.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu)
            .OrderByDescending(x => x.NgayBatDau)
            .ToListAsync();

        return danhSach.Select(x => new ChanThuongDto
        {
            MaChanThuong = x.MaChanThuong,
            MuaGiai = x.MuaGiai,
            LoaiChanThuong = x.LoaiChanThuong,
            NgayBatDau = x.NgayBatDau,
            NgayKetThuc = x.NgayKetThuc,
            SoNgayNghi = x.SoNgayNghi,
            SoTranVang = x.SoTranVang,
            DangDieuTri = x.DangDieuTri
        }).ToList();
    }

    public async Task<List<DiemGiaTriDto>> LayLichSuGiaTriAsync(int maCauThu)
    {
        return await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu)
            .OrderBy(x => x.NgayGhiNhan)
            .Select(x => new DiemGiaTriDto { Ngay = x.NgayGhiNhan, GiaTri = x.GiaTri })
            .ToListAsync();
    }

    public async Task<List<CauThuTomTatDto>> LayCauThuTuongTuAsync(int maCauThu, int soLuong)
    {
        var goc = await _db.CauThus.AsNoTracking().FirstOrDefaultAsync(x => x.MaCauThu == maCauThu);
        if (goc == null) return new List<CauThuTomTatDto>();

        var can = goc.GiaTriHienTai;
        var namSinh = goc.NgaySinh.Year;

        var danhSach = await Chieu(_db.CauThus.AsNoTracking()
                .Where(x => x.MaCauThu != maCauThu && x.MaViTri == goc.MaViTri
                            && x.NgaySinh.Year >= namSinh - 3 && x.NgaySinh.Year <= namSinh + 3)
                .OrderBy(x => x.GiaTriHienTai > can ? x.GiaTriHienTai - can : can - x.GiaTriHienTai)
                .Take(soLuong <= 0 ? 6 : soLuong))
            .ToListAsync();

        return danhSach.Select(ToTomTat).ToList();
    }

    public async Task<KetQua<BangSoSanhDto>> SoSanhAsync(IEnumerable<int> maCauThus)
    {
        var ds = maCauThus?.Distinct().ToList() ?? new List<int>();
        if (ds.Count < 2)
            return KetQua<BangSoSanhDto>.Loi("THIEU_CAU_THU", "Cần chọn tối thiểu 2 cầu thủ để so sánh.");
        if (ds.Count > 4)
            return KetQua<BangSoSanhDto>.Loi("VUOT_GIOI_HAN", "Chỉ so sánh được tối đa 4 cầu thủ.");

        var danhSach = await LayNhieuChiTietAsync(ds);

        if (danhSach.Count < 2)
            return KetQua<BangSoSanhDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy đủ cầu thủ để so sánh.");

        var bang = new BangSoSanhDto
        {
            CauThus = danhSach.Cast<CauThuTomTatDto>().ToList()
        };

        void ThemHang(string khoa, string nhan, Func<CauThuChiTietDto, decimal?> laySo,
            Func<CauThuChiTietDto, string> hienThi, bool caoHonLaTot = true)
        {
            var hang = new HangSoSanhDto { Khoa = khoa, Nhan = nhan, CaoHonLaTot = caoHonLaTot };
            foreach (var ct in danhSach)
            {
                hang.GiaTriSo.Add(laySo(ct));
                hang.GiaTriHienThi.Add(hienThi(ct));
            }

            var coSo = hang.GiaTriSo.Where(x => x.HasValue).ToList();
            if (coSo.Count > 0)
            {
                var tot = caoHonLaTot ? coSo.Max() : coSo.Min();
                hang.ViTriTotNhat = hang.GiaTriSo.FindIndex(x => x.HasValue && x.Value == tot.Value);
            }
            bang.CacHang.Add(hang);
        }

        ThemHang("tuoi", "Tuổi", x => x.Tuoi, x => x.Tuoi.ToString(), false);
        ThemHang("quoctich", "Quốc tịch", x => null, x => x.QuocTich);
        ThemHang("vitri", "Vị trí", x => null, x => x.ViTri);
        ThemHang("clb", "Câu lạc bộ", x => null, x => x.CauLacBo);
        ThemHang("chieucao", "Chiều cao (cm)", x => x.ChieuCao, x => x.ChieuCao?.ToString() ?? "—");
        ThemHang("cannang", "Cân nặng (kg)", x => x.CanNang, x => x.CanNang?.ToString() ?? "—");
        ThemHang("chanthuan", "Chân thuận", x => null, x => x.ChanThuan);
        ThemHang("giatri", "Giá trị hiện tại (tr €)", x => x.GiaTriHienTai, x => DinhDangSo.SoGon(x.GiaTriHienTai, 2));
        ThemHang("giatricaonhat", "Giá trị cao nhất (tr €)", x => x.GiaTriCaoNhat, x => DinhDangSo.SoGon(x.GiaTriCaoNhat, 2));
        // So với LẦN ĐỊNH GIÁ TRƯỚC, giống trang hồ sơ. Trước đây là "Thay đổi 30 ngày": nguồn dữ liệu
        // đã ngừng cập nhật nên mọi cầu thủ đều ra 0,0% — một hàng vô nghĩa trong bảng so sánh.
        ThemHang("thaydoi", "Thay đổi so với lần định giá trước", x => x.PhanTramSoVoiMocTruoc,
            x => x.PhanTramSoVoiMocTruoc is decimal pt && x.NgayMocTruoc is DateTime ngay
                ? $"{DinhDangSo.PhanTramCoDau(pt)} (từ {ngay:MM/yyyy})"
                : "—");
        ThemHang("tongphi", "Tổng phí chuyển nhượng (tr €)", x => x.TongPhiChuyenNhuong,
            x => DinhDangSo.SoGon(x.TongPhiChuyenNhuong, 2));
        ThemHang("sothuongvu", "Số lần chuyển nhượng", x => x.TongSoThuongVu, x => x.TongSoThuongVu.ToString());
        // Cầu thủ chưa đủ số liệu thi đấu có bản ghi chỉ số toàn số 0: hiển thị "—" và KHÔNG đưa vào
        // so sánh, nếu không cột của họ luôn "thua" và cột đối thủ được tô đậm oan.
        static ChiSoDto ChiSoThat(CauThuChiTietDto x) => x.ChiSo is { ThieuDuLieu: false } ? x.ChiSo : null;

        ThemHang("chisotong", "Chỉ số tổng hợp", x => x.ChiSoTongHop > 0 ? x.ChiSoTongHop : null,
            x => x.ChiSoTongHop > 0 ? x.ChiSoTongHop.ToString() : "Chưa đủ số liệu");
        // Tên sáu chỉ số theo nguồn dữ liệu (xem TenChiSo): số liệu thống kê không đo được "tốc độ".
        var ten = TenChiSo.TheoNhieuNguon(danhSach.Select(x => x.ChiSo?.NguonChiSo));
        ThemHang("tocdo", ten[0], x => ChiSoThat(x)?.TocDo, x => ChiSoThat(x)?.TocDo.ToString() ?? "—");
        ThemHang("dutdiem", ten[1], x => ChiSoThat(x)?.DutDiem, x => ChiSoThat(x)?.DutDiem.ToString() ?? "—");
        ThemHang("chuyen", ten[2], x => ChiSoThat(x)?.Chuyen, x => ChiSoThat(x)?.Chuyen.ToString() ?? "—");
        ThemHang("redat", ten[3], x => ChiSoThat(x)?.ReDat, x => ChiSoThat(x)?.ReDat.ToString() ?? "—");
        ThemHang("phongngu", ten[4], x => ChiSoThat(x)?.PhongNgu, x => ChiSoThat(x)?.PhongNgu.ToString() ?? "—");
        ThemHang("theluc", ten[5], x => ChiSoThat(x)?.TheLuc, x => ChiSoThat(x)?.TheLuc.ToString() ?? "—");
        ThemHang("banthang", "Bàn thắng mùa gần nhất", x => x.ChiSo?.BanThang, x => x.ChiSo?.BanThang.ToString() ?? "—");
        ThemHang("kientao", "Kiến tạo mùa gần nhất", x => x.ChiSo?.KienTao, x => x.ChiSo?.KienTao.ToString() ?? "—");
        ThemHang("sotran", "Số trận mùa gần nhất", x => x.ChiSo?.SoTran, x => x.ChiSo?.SoTran.ToString() ?? "—");
        ThemHang("ngaynghi", "Số ngày nghỉ vì chấn thương (3 mùa)",
            x => x.CoDuLieuChanThuong ? x.SoNgayNghiChanThuong3Mua : null,
            x => x.CoDuLieuChanThuong ? x.SoNgayNghiChanThuong3Mua.ToString() : "Chưa có dữ liệu", false);
        ThemHang("hopdong", "Hạn hợp đồng", x => null,
            x => x.NgayHetHanHopDong?.ToString("dd/MM/yyyy") ?? "—");

        return KetQua<BangSoSanhDto>.Ok(bang);
    }

    /// <summary>
    /// Lấy chi tiết nhiều cầu thủ bằng 4 truy vấn gộp thay vì gọi lặp từng cầu thủ.
    /// Giữ nguyên thứ tự người dùng đã chọn.
    /// </summary>
    private async Task<List<CauThuChiTietDto>> LayNhieuChiTietAsync(List<int> maCauThus)
    {
        var cauThus = await TruyVanCoBan()
            .Where(x => maCauThus.Contains(x.MaCauThu))
            .ToListAsync();

        if (cauThus.Count == 0) return new List<CauThuChiTietDto>();

        var mocBaMua = DateTime.UtcNow.AddYears(-3);
        var mocMotThang = DateTime.UtcNow.AddDays(-35);

        var thongKeChuyenNhuong = await _db.LichSuChuyenNhuongs.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu))
            .GroupBy(x => x.MaCauThu)
            .Select(g => new { MaCauThu = g.Key, SoLan = g.Count(), TongPhi = g.Sum(x => x.PhiChuyenNhuong) })
            .ToDictionaryAsync(x => x.MaCauThu, x => x);

        var chanThuongs = await _db.LichSuChanThuongs.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu) && x.NgayBatDau >= mocBaMua)
            .Select(x => new { x.MaCauThu, x.NgayBatDau, x.NgayKetThuc })
            .ToListAsync();

        var dangChanThuong = await _db.LichSuChanThuongs.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu) && x.NgayKetThuc == null)
            .Select(x => x.MaCauThu).Distinct().ToListAsync();

        // EF Core không dịch được OrderByDescending(...).FirstOrDefault() bên trong GroupBy.
        // Dùng hai phép gộp chuẩn: lấy ngày gần nhất, rồi lấy giá tại đúng ngày đó.
        var ngayGanNhat = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu) && x.NgayGhiNhan <= mocMotThang)
            .GroupBy(x => x.MaCauThu)
            .Select(g => new { MaCauThu = g.Key, Ngay = g.Max(x => x.NgayGhiNhan) })
            .ToListAsync();

        var giaTaiNgay = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu) && x.NgayGhiNhan <= mocMotThang)
            .GroupBy(x => new { x.MaCauThu, x.NgayGhiNhan })
            .Select(g => new { g.Key.MaCauThu, g.Key.NgayGhiNhan, Gia = g.Max(x => x.GiaTri) })
            .ToListAsync();

        var giaTriCu = ngayGanNhat
            .Join(giaTaiNgay,
                a => new { a.MaCauThu, Ngay = a.Ngay },
                b => new { b.MaCauThu, Ngay = b.NgayGhiNhan },
                (a, b) => new { a.MaCauThu, b.Gia })
            .ToDictionary(x => x.MaCauThu, x => x.Gia);

        // Lần định giá trước của từng cầu thủ: mốc gần nhất có giá KHÁC giá hiện tại.
        // Danh sách so sánh tối đa 4 cầu thủ nên nạp lịch sử giá của họ về rồi lọc trong bộ nhớ.
        var lichSuGia = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu))
            .Select(x => new { x.MaCauThu, x.NgayGhiNhan, x.GiaTri })
            .ToListAsync();

        // Chỉ khi đã có ít nhất một lượt đồng bộ chấn thương thì cầu thủ có mã API mới được coi là có nguồn.
        var heThongCoChanThuong = await _db.LichSuChanThuongs.AnyAsync();
        var ketQua = new List<CauThuChiTietDto>();

        foreach (var ma in maCauThus)
        {
            var ct = cauThus.FirstOrDefault(x => x.MaCauThu == ma);
            if (ct == null) continue;

            var soNgayNghi = chanThuongs
                .Where(x => x.MaCauThu == ma)
                .Sum(x => (int)((x.NgayKetThuc ?? DateTime.UtcNow) - x.NgayBatDau).TotalDays);

            thongKeChuyenNhuong.TryGetValue(ma, out var tk);
            giaTriCu.TryGetValue(ma, out var giaCu);

            var chiTiet = TaoChiTiet(ct, dangChanThuong.Contains(ma),
                tk?.SoLan ?? 0, tk?.TongPhi ?? 0, soNgayNghi, giaCu);
            chiTiet.CoDuLieuChanThuong = chanThuongs.Any(x => x.MaCauThu == ma)
                                         || (heThongCoChanThuong && !string.IsNullOrEmpty(ct.MaNgoaiApi));

            var mocTruoc = lichSuGia
                .Where(x => x.MaCauThu == ma && x.GiaTri != ct.GiaTriHienTai)
                .OrderByDescending(x => x.NgayGhiNhan)
                .FirstOrDefault();
            if (mocTruoc != null)
            {
                chiTiet.GiaTriMocTruoc = mocTruoc.GiaTri;
                chiTiet.NgayMocTruoc = mocTruoc.NgayGhiNhan;
                chiTiet.PhanTramSoVoiMocTruoc = mocTruoc.GiaTri > 0
                    ? Math.Round((ct.GiaTriHienTai - mocTruoc.GiaTri) / mocTruoc.GiaTri * 100, 1)
                    : null;
            }

            ketQua.Add(chiTiet);
        }

        return ketQua;
    }

    /// <summary>
    /// Dữ liệu chấn thương chỉ đến từ đồng bộ API-Football. Một cầu thủ được coi là "có nguồn"
    /// khi đã có bản ghi chấn thương, hoặc khi cầu thủ đã liên kết với API và hệ thống từng
    /// đồng bộ được chấn thương. Ngoài ra thì "không có bản ghi" nghĩa là "chưa biết".
    /// </summary>
    private async Task<bool> CoDuLieuChanThuongAsync(string maNgoaiApi, bool daCoBanGhi)
    {
        if (daCoBanGhi) return true;
        if (string.IsNullOrEmpty(maNgoaiApi)) return false;
        return await _db.LichSuChanThuongs.AnyAsync();
    }

    /// <summary>Dựng DTO chi tiết từ thực thể và các số liệu tổng hợp đã tính sẵn.</summary>
    private static CauThuChiTietDto TaoChiTiet(CauThu ct, bool dangChanThuong,
        int soThuongVu, decimal tongPhi, int soNgayNghi, decimal giaTriCu)
    {
        var tomTat = MapperHelper.ToTomTat(ct, dangChanThuong);

        return new CauThuChiTietDto
        {
            MaCauThu = tomTat.MaCauThu,
            HoTen = tomTat.HoTen,
            AnhDaiDien = tomTat.AnhDaiDien,
            Tuoi = tomTat.Tuoi,
            MaViTri = tomTat.MaViTri,
            ViTri = tomTat.ViTri,
            ViTriVietTat = tomTat.ViTriVietTat,
            CauLacBo = tomTat.CauLacBo,
            GiaiDau = tomTat.GiaiDau,
            QuocTich = tomTat.QuocTich,
            MaISO = tomTat.MaISO,
            GiaTriHienTai = tomTat.GiaTriHienTai,
            ChiSoTongHop = tomTat.ChiSoTongHop,
            DangChanThuong = tomTat.DangChanThuong,
            NgaySinh = ct.NgaySinh,
            ChieuCao = ct.ChieuCao,
            CanNang = ct.CanNang,
            ChanThuan = MapperHelper.TenChanThuan(ct.ChanThuan),
            SoAo = ct.SoAo,
            NgayHetHanHopDong = ct.NgayHetHanHopDong,
            GiaTriCaoNhat = ct.GiaTriCaoNhat,
            ChiSo = MapperHelper.ToChiSo(ct.ChiSo),
            TongSoThuongVu = soThuongVu,
            TongPhiChuyenNhuong = tongPhi,
            SoNgayNghiChanThuong3Mua = soNgayNghi,
            PhanTramThayDoi30Ngay = giaTriCu > 0
                ? Math.Round((ct.GiaTriHienTai - giaTriCu) / giaTriCu * 100, 1)
                : 0
        };
    }
}
