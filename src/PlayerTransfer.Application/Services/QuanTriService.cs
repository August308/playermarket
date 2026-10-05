using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

public class QuanTriService : IQuanTriService
{
    private readonly ApplicationDbContext _db;
    private readonly ICauThuService _cauThuService;

    public QuanTriService(ApplicationDbContext db, ICauThuService cauThuService)
    {
        _db = db;
        _cauThuService = cauThuService;
    }

    private async Task GhiNhatKyAsync(int maNguoiDung, string hanhDong, string doiTuong, string chiTiet)
    {
        _db.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaNguoiDung = maNguoiDung,
            HanhDong = hanhDong,
            DoiTuong = doiTuong,
            ChiTiet = chiTiet
        });
        await _db.SaveChangesAsync();
    }

    public async Task<KetQua<int>> LuuCauThuAsync(LuuCauThuDto dto, int maNguoiDung)
    {
        if (string.IsNullOrWhiteSpace(dto.HoTen))
            return KetQua<int>.Loi("THIEU_TEN", "Vui lòng nhập họ tên cầu thủ.");
        if (dto.GiaTriHienTai < 0)
            return KetQua<int>.Loi("GIA_TRI_AM", "Giá trị thị trường không được âm.");
        if (MapperHelper.TinhTuoi(dto.NgaySinh) is < 14 or > 50)
            return KetQua<int>.Loi("NGAY_SINH_SAI", "Ngày sinh không hợp lệ (tuổi phải từ 14 đến 50).");

        CauThu ct;
        if (dto.MaCauThu.HasValue)
        {
            ct = await _db.CauThus.Include(x => x.ChiSo).FirstOrDefaultAsync(x => x.MaCauThu == dto.MaCauThu.Value);
            if (ct == null) return KetQua<int>.Loi("KHONG_TIM_THAY", "Không tìm thấy cầu thủ.");
        }
        else
        {
            ct = new CauThu();
            _db.CauThus.Add(ct);
        }

        var giaTriCu = ct.GiaTriHienTai;

        ct.HoTen = dto.HoTen.Trim();
        ct.TenKhongDau = ChuanHoa.BoDau(ct.HoTen);
        ct.NgaySinh = dto.NgaySinh;
        ct.MaQuocGia = dto.MaQuocGia;
        ct.MaCLB = dto.MaCLB;
        ct.MaViTri = dto.MaViTri;
        ct.GiaTriHienTai = dto.GiaTriHienTai;
        ct.GiaTriCaoNhat = Math.Max(ct.GiaTriCaoNhat, dto.GiaTriHienTai);
        ct.ChieuCao = dto.ChieuCao;
        ct.CanNang = dto.CanNang;
        ct.SoAo = dto.SoAo;
        // Biểu mẫu quản trị không có ô ảnh: chỉ ghi đè khi thật sự gửi ảnh mới,
        // nếu không mỗi lần sửa sẽ xóa mất ảnh đại diện đang có.
        if (!string.IsNullOrWhiteSpace(dto.AnhDaiDien)) ct.AnhDaiDien = dto.AnhDaiDien.Trim();
        ct.NgayHetHanHopDong = dto.NgayHetHanHopDong;
        ct.ChinhSuaThuCong = true;   // Đồng bộ từ API sẽ không ghi đè bản ghi này.
        ct.NgayCapNhat = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.ChanThuan) && Enum.TryParse<ChanThuan>(dto.ChanThuan, out var chan))
            ct.ChanThuan = chan;

        await _db.SaveChangesAsync();

        if (giaTriCu != dto.GiaTriHienTai)
        {
            _db.LichSuGiaTris.Add(new LichSuGiaTri
            {
                MaCauThu = ct.MaCauThu,
                NgayGhiNhan = DateTime.UtcNow.Date,
                GiaTri = dto.GiaTriHienTai,
                Nguon = "QuanTriVien"
            });
            await _db.SaveChangesAsync();
        }

        await GhiNhatKyAsync(maNguoiDung, dto.MaCauThu.HasValue ? "Cập nhật cầu thủ" : "Thêm cầu thủ",
            "CauThu", $"{ct.HoTen} (mã {ct.MaCauThu})");

        return KetQua<int>.Ok(ct.MaCauThu, "Đã lưu thông tin cầu thủ.");
    }

    public async Task<LuuCauThuDto> LayDeSuaAsync(int maCauThu)
    {
        var ct = await _db.CauThus.AsNoTracking().FirstOrDefaultAsync(x => x.MaCauThu == maCauThu);
        if (ct == null) return null;

        return new LuuCauThuDto
        {
            MaCauThu = ct.MaCauThu,
            HoTen = ct.HoTen,
            NgaySinh = ct.NgaySinh,
            MaQuocGia = ct.MaQuocGia,
            MaCLB = ct.MaCLB,
            MaViTri = ct.MaViTri,
            GiaTriHienTai = ct.GiaTriHienTai,
            ChieuCao = ct.ChieuCao,
            CanNang = ct.CanNang,
            SoAo = ct.SoAo,
            ChanThuan = ct.ChanThuan?.ToString(),
            NgayHetHanHopDong = ct.NgayHetHanHopDong,
            AnhDaiDien = ct.AnhDaiDien
        };
    }

    public async Task<KetQua> XoaCauThuAsync(int maCauThu, int maNguoiDung)
    {
        var ct = await _db.CauThus.FirstOrDefaultAsync(x => x.MaCauThu == maCauThu);
        if (ct == null) return KetQua.Loi("KHONG_TIM_THAY", "Không tìm thấy cầu thủ.");

        _db.CauThus.Remove(ct);
        await _db.SaveChangesAsync();
        await GhiNhatKyAsync(maNguoiDung, "Xóa cầu thủ", "CauThu", $"{ct.HoTen} (mã {maCauThu})");

        return KetQua.Ok("Đã xóa cầu thủ.");
    }

    public Task<TrangDuLieu<CauThuTomTatDto>> DanhSachCauThuAsync(string tuKhoa, int trang, int kichThuocTrang)
        => _cauThuService.LocAsync(new BoLocCauThuDto
        {
            TuKhoa = tuKhoa,
            Trang = trang,
            KichThuocTrang = kichThuocTrang,
            SapXep = "ten_az"
        });

    public async Task<List<object>> NhatKyDongBoAsync(int soLuong)
    {
        var danhSach = await _db.NhatKyDongBos.AsNoTracking()
            .OrderByDescending(x => x.ThoiDiemBatDau)
            .Take(soLuong <= 0 ? 50 : soLuong)
            .ToListAsync();

        return danhSach.Select(x => (object)new
        {
            x.MaNhatKy,
            x.ThoiDiemBatDau,
            x.ThoiDiemKetThuc,
            x.PhamVi,
            x.SoBanGhiThemMoi,
            x.SoBanGhiCapNhat,
            x.SoBanGhiLoi,
            x.SoLuotGoiApi,
            x.TrangThai,
            x.ThongDiep
        }).ToList();
    }

    public async Task<List<object>> NhatKyThaoTacAsync(int soLuong)
    {
        var danhSach = await _db.NhatKyThaoTacs.AsNoTracking()
            .OrderByDescending(x => x.ThoiDiem)
            .Take(soLuong <= 0 ? 50 : soLuong)
            .ToListAsync();

        return danhSach.Select(x => (object)new
        {
            x.MaNhatKy, x.MaNguoiDung, x.HanhDong, x.DoiTuong, x.ChiTiet, x.ThoiDiem
        }).ToList();
    }

    public async Task<TrangDuLieu<object>> DanhSachNguoiDungAsync(int trang, int kichThuocTrang)
    {
        var truyVan = _db.NguoiDungs.AsNoTracking().OrderBy(x => x.MaNguoiDung);
        var tongSo = await truyVan.CountAsync();
        var soTrang = trang <= 0 ? 1 : trang;
        var kichThuoc = kichThuocTrang is <= 0 or > 100 ? 20 : kichThuocTrang;

        var danhSach = await truyVan.Skip((soTrang - 1) * kichThuoc).Take(kichThuoc).ToListAsync();

        return new TrangDuLieu<object>
        {
            DanhSach = danhSach.Select(x => (object)new
            {
                x.MaNguoiDung,
                x.Email,
                x.HoTen,
                VaiTro = x.VaiTro.ToString(),
                TrangThai = x.TrangThai.ToString(),
                x.NgayTao
            }).ToList(),
            TongSo = tongSo,
            Trang = soTrang,
            KichThuocTrang = kichThuoc
        };
    }

    public async Task<KetQua> DoiTrangThaiNguoiDungAsync(int maNguoiDung, string trangThai)
    {
        var nd = await _db.NguoiDungs.FindAsync(maNguoiDung);
        if (nd == null) return KetQua.Loi("KHONG_TIM_THAY", "Không tìm thấy tài khoản.");

        if (!Enum.TryParse<TrangThaiTaiKhoan>(trangThai, out var tt))
            return KetQua.Loi("TRANG_THAI_SAI", "Trạng thái không hợp lệ.");

        nd.TrangThai = tt;
        if (tt == TrangThaiTaiKhoan.DaKichHoat)
        {
            nd.SoLanDangNhapSai = 0;
            nd.KhoaDenThoiDiem = null;
        }
        await _db.SaveChangesAsync();
        return KetQua.Ok("Đã cập nhật trạng thái tài khoản.");
    }
}
