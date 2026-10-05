using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

public class TheoDoiService : ITheoDoiService
{
    private readonly ApplicationDbContext _db;

    public TheoDoiService(ApplicationDbContext db) => _db = db;

    public async Task<List<TheoDoiDto>> LayDanhSachAsync(int maNguoiDung)
    {
        var danhSach = await _db.TheoDoiCauThus.AsNoTracking()
            .Include(x => x.CauThu).ThenInclude(c => c.ViTri)
            .Include(x => x.CauThu).ThenInclude(c => c.QuocGia)
            .Include(x => x.CauThu).ThenInclude(c => c.CauLacBo).ThenInclude(c => c.GiaiDau)
            .Include(x => x.CauThu).ThenInclude(c => c.ChiSo)
            .Where(x => x.MaNguoiDung == maNguoiDung)
            .OrderByDescending(x => x.NgayThem)
            .ToListAsync();

        return danhSach.Select(x => new TheoDoiDto
        {
            MaTheoDoi = x.MaTheoDoi,
            CauThu = MapperHelper.ToTomTat(x.CauThu),
            NguongCanhBaoTren = x.NguongCanhBaoTren,
            NguongCanhBaoDuoi = x.NguongCanhBaoDuoi,
            GhiChu = x.GhiChu,
            NgayThem = x.NgayThem,
            DaVuotNguong = (x.NguongCanhBaoTren.HasValue && x.CauThu.GiaTriHienTai >= x.NguongCanhBaoTren.Value)
                           || (x.NguongCanhBaoDuoi.HasValue && x.CauThu.GiaTriHienTai <= x.NguongCanhBaoDuoi.Value)
        }).ToList();
    }

    public async Task<KetQua> ThemAsync(int maNguoiDung, int maCauThu)
    {
        if (!await _db.CauThus.AnyAsync(x => x.MaCauThu == maCauThu))
            return KetQua.Loi("KHONG_TIM_THAY", "Không tìm thấy cầu thủ.");

        if (await _db.TheoDoiCauThus.AnyAsync(x => x.MaNguoiDung == maNguoiDung && x.MaCauThu == maCauThu))
            return KetQua.Loi("DA_TON_TAI", "Cầu thủ đã có trong danh sách theo dõi.");

        _db.TheoDoiCauThus.Add(new TheoDoiCauThu { MaNguoiDung = maNguoiDung, MaCauThu = maCauThu });
        await _db.SaveChangesAsync();
        return KetQua.Ok("Đã thêm vào danh sách theo dõi.");
    }

    public async Task<KetQua> XoaAsync(int maNguoiDung, int maCauThu)
    {
        var banGhi = await _db.TheoDoiCauThus
            .FirstOrDefaultAsync(x => x.MaNguoiDung == maNguoiDung && x.MaCauThu == maCauThu);
        if (banGhi == null) return KetQua.Loi("KHONG_TIM_THAY", "Cầu thủ không có trong danh sách theo dõi.");

        _db.TheoDoiCauThus.Remove(banGhi);
        await _db.SaveChangesAsync();
        return KetQua.Ok("Đã bỏ theo dõi.");
    }

    public async Task<KetQua> CapNhatCanhBaoAsync(int maNguoiDung, int maCauThu,
        decimal? tren, decimal? duoi, string ghiChu)
    {
        var banGhi = await _db.TheoDoiCauThus
            .FirstOrDefaultAsync(x => x.MaNguoiDung == maNguoiDung && x.MaCauThu == maCauThu);
        if (banGhi == null) return KetQua.Loi("KHONG_TIM_THAY", "Cầu thủ không có trong danh sách theo dõi.");

        if (tren.HasValue && duoi.HasValue && duoi.Value > tren.Value)
            return KetQua.Loi("NGUONG_SAI", "Ngưỡng dưới phải nhỏ hơn hoặc bằng ngưỡng trên.");

        // Đổi ngưỡng thì bắt đầu lại: lần chạm ngưỡng mới sẽ được báo.
        if (banGhi.NguongCanhBaoTren != tren || banGhi.NguongCanhBaoDuoi != duoi)
            banGhi.DaCanhBaoLuc = null;

        banGhi.NguongCanhBaoTren = tren;
        banGhi.NguongCanhBaoDuoi = duoi;
        banGhi.GhiChu = ghiChu;
        await _db.SaveChangesAsync();
        return KetQua.Ok("Đã cập nhật cảnh báo giá.");
    }
}
