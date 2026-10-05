using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// C10 — trò chơi đoán giá trị cầu thủ.
/// Máy chủ chọn ngẫu nhiên các cầu thủ, gửi kèm giá trị thật để chấm điểm ngay trên trình duyệt;
/// điểm cuối lượt được gửi ngược lên để lưu bảng xếp hạng.
/// </summary>
public class TroChoiService : ITroChoiService
{
    private readonly ApplicationDbContext _db;

    public TroChoiService(ApplicationDbContext db) => _db = db;

    public async Task<List<CauHoiDoanGiaDto>> TaoCauHoiAsync(int soCau, decimal? giaTriToiThieu)
    {
        var soLay = soCau is <= 0 or > 20 ? 5 : soCau;
        var nguong = giaTriToiThieu ?? 15;

        var truyVan = _db.CauThus.AsNoTracking()
            // Chỉ số tổng hợp là manh mối chính của trò chơi; cầu thủ chưa có dữ liệu
            // sẽ hiện "0" và người chơi không còn căn cứ nào để đoán.
            .Where(x => x.GiaTriHienTai >= nguong && x.ChiSo != null
                        && x.ChiSo.NguonChiSo != "KhongCoDuLieu");

        var tongSo = await truyVan.CountAsync();
        if (tongSo == 0) return new List<CauHoiDoanGiaDto>();

        // Lấy ngẫu nhiên bằng cách bỏ qua một số lượng ngẫu nhiên, tránh ORDER BY NEWID() nặng.
        var rnd = new Random();
        var daChon = new HashSet<int>();
        var ketQua = new List<CauHoiDoanGiaDto>();

        for (var i = 0; i < soLay * 4 && ketQua.Count < soLay; i++)
        {
            var viTri = rnd.Next(tongSo);

            var ct = await truyVan
                .OrderBy(x => x.MaCauThu)
                .Skip(viTri).Take(1)
                .Include(x => x.ViTri)
                .Include(x => x.QuocGia)
                .Include(x => x.CauLacBo).ThenInclude(c => c.GiaiDau)
                .Include(x => x.ChiSo)
                .FirstOrDefaultAsync();

            if (ct == null || !daChon.Add(ct.MaCauThu)) continue;

            ketQua.Add(new CauHoiDoanGiaDto
            {
                MaCauThu = ct.MaCauThu,
                HoTen = ct.HoTen,
                AnhDaiDien = ct.AnhDaiDien,
                Tuoi = MapperHelper.TinhTuoi(ct.NgaySinh),
                ViTri = ct.ViTri?.TenViTri,
                CauLacBo = ct.CauLacBo?.TenCLB ?? "Tự do",
                GiaiDau = ct.CauLacBo?.GiaiDau?.TenGiaiDau,
                QuocTich = ct.QuocGia?.TenQuocGia,
                MaISO = ct.QuocGia?.MaISO,
                ChiSoTongHop = ct.ChiSo?.ChiSoTongHop ?? 0,
                GiaTriThat = ct.GiaTriHienTai
            });
        }

        return ketQua;
    }

    public async Task<KetQua> LuuKetQuaAsync(int maNguoiDung, KetQuaChoiDto ketQua)
    {
        if (ketQua == null || ketQua.TongSoCau <= 0)
            return KetQua.Loi("DU_LIEU_SAI", "Kết quả lượt chơi không hợp lệ.");

        if (ketQua.SoCauDung < 0 || ketQua.SoCauDung > ketQua.TongSoCau)
            return KetQua.Loi("DU_LIEU_SAI", "Số câu đúng không hợp lệ.");

        // Chặn điểm bịa: điểm tối đa mỗi câu là 100.
        var diemToiDa = ketQua.TongSoCau * 100;
        var diem = Math.Clamp(ketQua.Diem, 0, diemToiDa);

        _db.LuotChoiDoanGias.Add(new LuotChoiDoanGia
        {
            MaNguoiDung = maNguoiDung,
            SoCauDung = ketQua.SoCauDung,
            TongSoCau = ketQua.TongSoCau,
            Diem = diem,
            DoLechTrungBinh = Math.Clamp(ketQua.DoLechTrungBinh, 0, 100),
            ThoiDiem = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return KetQua.Ok($"Đã lưu kết quả: {diem} điểm.");
    }

    public async Task<List<HangNguoiChoiDto>> BangXepHangAsync(int soLuong)
    {
        var soLay = soLuong <= 0 ? 10 : soLuong;

        // Mỗi người dùng chỉ lấy lượt chơi có điểm cao nhất. Gộp ngay trong SQL —
        // bảng lượt chơi chỉ tăng theo thời gian nên không được nạp toàn bộ về bộ nhớ.
        var diemCaoNhat = await _db.LuotChoiDoanGias.AsNoTracking()
            .GroupBy(x => x.MaNguoiDung)
            .Select(g => new { MaNguoiDung = g.Key, Diem = g.Max(x => x.Diem) })
            .OrderByDescending(x => x.Diem)
            .Take(soLay * 3)
            .ToListAsync();

        var maNguoiDungs = diemCaoNhat.Select(x => x.MaNguoiDung).ToList();
        var diemToiDa = diemCaoNhat.Count == 0 ? 0 : diemCaoNhat.Min(x => x.Diem);

        var ungVien = await _db.LuotChoiDoanGias.AsNoTracking()
            .Include(x => x.NguoiDung)
            .Where(x => maNguoiDungs.Contains(x.MaNguoiDung) && x.Diem >= diemToiDa)
            .ToListAsync();

        return ungVien
            .GroupBy(x => x.MaNguoiDung)
            .Select(g => g.OrderByDescending(x => x.Diem)
                          .ThenBy(x => x.DoLechTrungBinh).First())
            .OrderByDescending(x => x.Diem)
            .ThenBy(x => x.DoLechTrungBinh)
            .Take(soLay)
            .Select((x, i) => new HangNguoiChoiDto
            {
                ThuHang = i + 1,
                HoTen = x.NguoiDung?.HoTen ?? "Ẩn danh",
                Diem = x.Diem,
                SoCauDung = x.SoCauDung,
                TongSoCau = x.TongSoCau,
                DoLechTrungBinh = x.DoLechTrungBinh,
                ThoiDiem = x.ThoiDiem
            })
            .ToList();
    }
}
