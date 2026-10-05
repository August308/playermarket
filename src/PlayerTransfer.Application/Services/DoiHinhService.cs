using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

public class DoiHinhService : IDoiHinhService
{
    /// <summary>Số cầu thủ tối đa trong một đội hình (11 chính thức).</summary>
    private const int SoViTriToiDa = 11;

    private readonly ApplicationDbContext _db;

    public DoiHinhService(ApplicationDbContext db) => _db = db;

    private IQueryable<DoiHinhAo> TruyVan() => _db.DoiHinhAos
        .Include(x => x.ViTris).ThenInclude(v => v.CauThu).ThenInclude(c => c.ViTri)
        .Include(x => x.ViTris).ThenInclude(v => v.CauThu).ThenInclude(c => c.LichSuChanThuongs)
        .Include(x => x.ViTris).ThenInclude(v => v.CauThu).ThenInclude(c => c.QuocGia)
        .Include(x => x.ViTris).ThenInclude(v => v.CauThu).ThenInclude(c => c.CauLacBo).ThenInclude(c => c.GiaiDau)
        .Include(x => x.ViTris).ThenInclude(v => v.CauThu).ThenInclude(c => c.ChiSo);

    private static DoiHinhDto ToDto(DoiHinhAo dh)
    {
        var ganKet = GanKetHelper.Tinh(dh.ViTris?.ToList());
        var viTris = (dh.ViTris ?? new List<DoiHinhAoCauThu>()).OrderBy(x => x.ThuTu).Select(v => new ViTriDoiHinhDto
        {
            ViTriTrenSan = v.ViTriTrenSan,
            ThuTu = v.ThuTu,
            CauThu = v.CauThu != null ? MapperHelper.ToTomTat(v.CauThu) : null
        }).ToList();

        var coCauThu = viTris.Where(x => x.CauThu != null).ToList();

        return new DoiHinhDto
        {
            MaDoiHinh = dh.MaDoiHinh,
            TenDoiHinh = dh.TenDoiHinh,
            SoDoChienThuat = dh.SoDoChienThuat,
            NgayTao = dh.NgayTao,
            ViTris = viTris,
            TongGiaTri = coCauThu.Sum(x => x.CauThu.GiaTriHienTai),
            // Bỏ cầu thủ chưa có chỉ số (0) khỏi phép trung bình, nếu không sẽ kéo điểm đội hình xuống oan.
            ChiSoTrungBinh = coCauThu.Any(x => x.CauThu.ChiSoTongHop > 0)
                ? (int)Math.Round(coCauThu.Where(x => x.CauThu.ChiSoTongHop > 0).Average(x => x.CauThu.ChiSoTongHop))
                : 0,
            ChiSoGanKet = ganKet.ChiSoGanKet
        };
    }

    public async Task<List<DoiHinhDto>> LayDanhSachAsync(int maNguoiDung)
    {
        var danhSach = await TruyVan().AsNoTracking()
            .Where(x => x.MaNguoiDung == maNguoiDung)
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync();
        return danhSach.Select(ToDto).ToList();
    }

    public async Task<DoiHinhDto> LayChiTietAsync(int maNguoiDung, int maDoiHinh)
    {
        var dh = await TruyVan().AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaDoiHinh == maDoiHinh && x.MaNguoiDung == maNguoiDung);
        return dh == null ? null : ToDto(dh);
    }

    public async Task<KetQua<DoiHinhDto>> LuuAsync(int maNguoiDung, LuuDoiHinhDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TenDoiHinh))
            return KetQua<DoiHinhDto>.Loi("THIEU_TEN", "Vui lòng nhập tên đội hình.");

        if (dto.ViTris.Count > SoViTriToiDa)
            return KetQua<DoiHinhDto>.Loi("VUOT_GIOI_HAN", $"Đội hình chỉ có tối đa {SoViTriToiDa} cầu thủ.");

        if (dto.ViTris.Select(x => x.MaCauThu).Distinct().Count() != dto.ViTris.Count)
            return KetQua<DoiHinhDto>.Loi("TRUNG_CAU_THU", "Một cầu thủ chỉ được xếp vào một vị trí.");

        DoiHinhAo doiHinh;
        if (dto.MaDoiHinh.HasValue)
        {
            doiHinh = await _db.DoiHinhAos.Include(x => x.ViTris)
                .FirstOrDefaultAsync(x => x.MaDoiHinh == dto.MaDoiHinh.Value && x.MaNguoiDung == maNguoiDung);
            if (doiHinh == null) return KetQua<DoiHinhDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy đội hình.");
            _db.DoiHinhAoCauThus.RemoveRange(doiHinh.ViTris);
        }
        else
        {
            doiHinh = new DoiHinhAo { MaNguoiDung = maNguoiDung };
            _db.DoiHinhAos.Add(doiHinh);
        }

        doiHinh.TenDoiHinh = dto.TenDoiHinh.Trim();
        doiHinh.SoDoChienThuat = string.IsNullOrWhiteSpace(dto.SoDoChienThuat) ? "4-3-3" : dto.SoDoChienThuat;
        await _db.SaveChangesAsync();

        foreach (var vt in dto.ViTris)
        {
            _db.DoiHinhAoCauThus.Add(new DoiHinhAoCauThu
            {
                MaDoiHinh = doiHinh.MaDoiHinh,
                MaCauThu = vt.MaCauThu,
                ViTriTrenSan = vt.ViTriTrenSan,
                ThuTu = vt.ThuTu
            });
        }
        await _db.SaveChangesAsync();

        return KetQua<DoiHinhDto>.Ok(await LayChiTietAsync(maNguoiDung, doiHinh.MaDoiHinh), "Đã lưu đội hình.");
    }

    /// <summary>C6 — so sánh hai đội hình của cùng một người dùng.</summary>
    public async Task<KetQua<SoSanhDoiHinhDto>> SoSanhAsync(int maNguoiDung, int maDoiHinhA, int maDoiHinhB)
    {
        if (maDoiHinhA == maDoiHinhB)
            return KetQua<SoSanhDoiHinhDto>.Loi("TRUNG_DOI_HINH", "Hãy chọn hai đội hình khác nhau.");

        var a = await TruyVan().AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaDoiHinh == maDoiHinhA && x.MaNguoiDung == maNguoiDung);
        var b = await TruyVan().AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaDoiHinh == maDoiHinhB && x.MaNguoiDung == maNguoiDung);

        if (a == null || b == null)
            return KetQua<SoSanhDoiHinhDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy một trong hai đội hình.");

        var dtoA = ToDto(a);
        var dtoB = ToDto(b);
        var ganKetA = GanKetHelper.Tinh(a.ViTris.ToList());
        var ganKetB = GanKetHelper.Tinh(b.ViTris.ToList());

        var ketQua = new SoSanhDoiHinhDto
        {
            DoiA = dtoA, DoiB = dtoB,
            GanKetA = ganKetA, GanKetB = ganKetB
        };

        void ThemHang(string khoa, string nhan, decimal giaTriA, decimal giaTriB,
            string hienThiA, string hienThiB, bool caoHonLaTot = true)
        {
            var hang = new HangSoSanhDto
            {
                Khoa = khoa,
                Nhan = nhan,
                CaoHonLaTot = caoHonLaTot,
                GiaTriSo = new List<decimal?> { giaTriA, giaTriB },
                GiaTriHienThi = new List<string> { hienThiA, hienThiB }
            };

            if (giaTriA != giaTriB)
                hang.ViTriTotNhat = caoHonLaTot
                    ? (giaTriA > giaTriB ? 0 : 1)
                    : (giaTriA < giaTriB ? 0 : 1);

            ketQua.CacHang.Add(hang);
        }

        var soA = dtoA.ViTris.Count(x => x.CauThu != null);
        var soB = dtoB.ViTris.Count(x => x.CauThu != null);

        ThemHang("sodo", "Sơ đồ chiến thuật", 0, 0, dtoA.SoDoChienThuat, dtoB.SoDoChienThuat);
        ThemHang("socauthu", "Số cầu thủ đã xếp", soA, soB, soA + "/11", soB + "/11");
        ThemHang("chiso", "Chỉ số trung bình", dtoA.ChiSoTrungBinh, dtoB.ChiSoTrungBinh,
            dtoA.ChiSoTrungBinh.ToString(), dtoB.ChiSoTrungBinh.ToString());
        ThemHang("ganket", "Độ gắn kết", ganKetA.ChiSoGanKet, ganKetB.ChiSoGanKet,
            ganKetA.ChiSoGanKet + "%", ganKetB.ChiSoGanKet + "%");
        ThemHang("giatri", "Tổng giá trị (tr €)", dtoA.TongGiaTri, dtoB.TongGiaTri,
            dtoA.TongGiaTri.ToString("N1", DinhDangSo.VN), dtoB.TongGiaTri.ToString("N1", DinhDangSo.VN));

        var tuoiA = dtoA.ViTris.Where(x => x.CauThu != null).Select(x => x.CauThu.Tuoi).DefaultIfEmpty(0).Average();
        var tuoiB = dtoB.ViTris.Where(x => x.CauThu != null).Select(x => x.CauThu.Tuoi).DefaultIfEmpty(0).Average();
        ThemHang("tuoi", "Tuổi trung bình", (decimal)tuoiA, (decimal)tuoiB,
            tuoiA.ToString("N1", DinhDangSo.VN), tuoiB.ToString("N1", DinhDangSo.VN), caoHonLaTot: false);

        // Kết luận: ưu tiên chỉ số, sau đó gắn kết, cuối cùng là chi phí.
        var diemA = dtoA.ChiSoTrungBinh * 2 + ganKetA.ChiSoGanKet;
        var diemB = dtoB.ChiSoTrungBinh * 2 + ganKetB.ChiSoGanKet;

        ketQua.KetLuan = diemA == diemB
            ? "Hai đội hình ngang nhau về chất lượng và độ gắn kết."
            : diemA > diemB
                ? $"\"{dtoA.TenDoiHinh}\" nhỉnh hơn về tổng hòa chỉ số và độ gắn kết."
                : $"\"{dtoB.TenDoiHinh}\" nhỉnh hơn về tổng hòa chỉ số và độ gắn kết.";

        return KetQua<SoSanhDoiHinhDto>.Ok(ketQua);
    }

    public async Task<KetQua> XoaAsync(int maNguoiDung, int maDoiHinh)
    {
        var dh = await _db.DoiHinhAos
            .FirstOrDefaultAsync(x => x.MaDoiHinh == maDoiHinh && x.MaNguoiDung == maNguoiDung);
        if (dh == null) return KetQua.Loi("KHONG_TIM_THAY", "Không tìm thấy đội hình.");

        _db.DoiHinhAos.Remove(dh);
        await _db.SaveChangesAsync();
        return KetQua.Ok("Đã xóa đội hình.");
    }
}
