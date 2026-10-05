using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Infrastructure.Data;

public static partial class SeedData
{
    private static readonly string[] LoaiChanThuongMau =
    {
        "Căng cơ đùi sau", "Chấn thương đầu gối", "Bong gân mắt cá", "Chấn thương cơ khép",
        "Gãy xương bàn chân", "Chấn thương vai", "Đau lưng", "Chấn thương gân kheo"
    };

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.CauThus.AnyAsync()) return;

        var rnd = new Random(20260921);

        // 1. Quốc gia
        var quocGias = new Dictionary<string, QuocGia>();
        foreach (var dong in QuocGiaRaw)
        {
            var p = dong.Split('|');
            var qg = new QuocGia { TenQuocGia = p[0], MaISO = p[1] };
            quocGias[p[0]] = qg;
        }
        db.QuocGias.AddRange(quocGias.Values);
        await db.SaveChangesAsync();

        // 2. Giải đấu
        var giaiDaus = new Dictionary<string, GiaiDau>();
        foreach (var dong in GiaiDauRaw)
        {
            var p = dong.Split('|');
            var gd = new GiaiDau
            {
                TenGiaiDau = p[0],
                MaQuocGia = quocGias.TryGetValue(p[1], out var q) ? q.MaQuocGia : (int?)null
            };
            giaiDaus[p[0]] = gd;
        }
        db.GiaiDaus.AddRange(giaiDaus.Values);
        await db.SaveChangesAsync();

        // 3. Câu lạc bộ
        var cauLacBos = new Dictionary<string, CauLacBo>();
        foreach (var dong in CauLacBoRaw)
        {
            var p = dong.Split('|');
            var clb = new CauLacBo { TenCLB = p[0], MaGiaiDau = giaiDaus[p[1]].MaGiaiDau };
            cauLacBos[p[0]] = clb;
        }
        db.CauLacBos.AddRange(cauLacBos.Values);
        await db.SaveChangesAsync();

        // 4. Vị trí thi đấu
        var viTris = new Dictionary<string, ViTriThiDau>();
        foreach (var dong in ViTriRaw)
        {
            var p = dong.Split('|');
            var vt = new ViTriThiDau
            {
                VietTat = p[0],
                TenViTri = p[1],
                NhomViTri = Enum.Parse<NhomViTri>(p[2])
            };
            viTris[p[0]] = vt;
        }
        db.ViTriThiDaus.AddRange(viTris.Values);
        await db.SaveChangesAsync();

        // 5. Cầu thủ + chỉ số
        var cauThus = new List<CauThu>();
        foreach (var dong in CauThuRaw)
        {
            var p = dong.Split('|');
            var giaTri = decimal.Parse(p[5]);
            var viTri = viTris[p[2]];
            var ct = new CauThu
            {
                HoTen = p[0],
                TenKhongDau = ChuanHoa.BoDau(p[0]),
                MaCLB = cauLacBos[p[1]].MaCLB,
                MaViTri = viTri.MaViTri,
                MaQuocGia = quocGias[p[3]].MaQuocGia,
                NgaySinh = DateTime.Parse(p[4]),
                GiaTriHienTai = giaTri,
                GiaTriCaoNhat = giaTri,
                ChanThuan = Enum.Parse<ChanThuan>(p[6]),
                ChieuCao = int.Parse(p[7]),
                CanNang = 62 + (int.Parse(p[7]) - 165) * 3 / 4,
                SoAo = int.Parse(p[8]),
                NgayHetHanHopDong = new DateTime(2026 + rnd.Next(0, 4), 6, 30),
                NgayCapNhat = DateTime.UtcNow
            };
            ct.ChiSo = TaoChiSo(ct, viTri.NhomViTri, giaTri, rnd);
            cauThus.Add(ct);
        }
        db.CauThus.AddRange(cauThus);
        await db.SaveChangesAsync();

        // 6. Lịch sử giá trị (24 tháng gần nhất)
        var lichSuGiaTris = new List<LichSuGiaTri>();
        foreach (var ct in cauThus)
        {
            var giaTri = ct.GiaTriHienTai;
            var moc = DateTime.UtcNow.Date;
            var danhSach = new List<LichSuGiaTri>();
            for (var i = 0; i < 8; i++)
            {
                danhSach.Add(new LichSuGiaTri
                {
                    MaCauThu = ct.MaCauThu,
                    NgayGhiNhan = moc.AddMonths(-3 * i),
                    GiaTri = Math.Round(giaTri, 2),
                    Nguon = "DuLieuMau"
                });
                var bienDong = 1m + (decimal)(rnd.NextDouble() * 0.30 - 0.12);
                giaTri = Math.Max(0.5m, Math.Round(giaTri / bienDong, 2));
            }
            ct.GiaTriCaoNhat = danhSach.Max(x => x.GiaTri);
            lichSuGiaTris.AddRange(danhSach);
        }
        db.LichSuGiaTris.AddRange(lichSuGiaTris);
        await db.SaveChangesAsync();

        // 7. Lịch sử chuyển nhượng
        var danhSachCLB = cauLacBos.Values.ToList();
        var chuyenNhuongs = new List<LichSuChuyenNhuong>();
        foreach (var ct in cauThus)
        {
            var soThuongVu = rnd.Next(1, 5);
            var namHienTai = DateTime.UtcNow.Year;
            var clbTruoc = (int?)null;
            for (var i = soThuongVu; i >= 1; i--)
            {
                var nam = namHienTai - i;
                if (nam - ct.NgaySinh.Year < 17) continue;
                var clbDen = i == 1 ? ct.MaCLB : danhSachCLB[rnd.Next(danhSachCLB.Count)].MaCLB;
                var loai = (LoaiChuyenNhuong)(rnd.Next(100) switch
                {
                    < 70 => 0,
                    < 85 => 1,
                    < 95 => 2,
                    _ => 3
                });
                var phi = loai == LoaiChuyenNhuong.MuaDut
                    ? Math.Round(ct.GiaTriHienTai * (decimal)(0.5 + rnd.NextDouble()), 2)
                    : 0m;
                chuyenNhuongs.Add(new LichSuChuyenNhuong
                {
                    MaCauThu = ct.MaCauThu,
                    MaCLBDi = clbTruoc,
                    MaCLBDen = clbDen,
                    NgayChuyenNhuong = new DateTime(nam, rnd.Next(0, 2) == 0 ? 7 : 1, rnd.Next(1, 28)),
                    MuaGiai = $"{nam}/{nam + 1}",
                    PhiChuyenNhuong = phi,
                    GiaTriThiTruongLucDo = Math.Round(ct.GiaTriHienTai * (decimal)(0.4 + rnd.NextDouble() * 0.8), 2),
                    LoaiChuyenNhuong = loai
                });
                clbTruoc = clbDen;
            }
        }
        db.LichSuChuyenNhuongs.AddRange(chuyenNhuongs);

        // 8. Lịch sử chấn thương
        var chanThuongs = new List<LichSuChanThuong>();
        foreach (var ct in cauThus)
        {
            var soDot = rnd.Next(0, 4);
            for (var i = 0; i < soDot; i++)
            {
                var batDau = DateTime.UtcNow.Date.AddDays(-rnd.Next(30, 1000));
                var dangDieuTri = i == 0 && rnd.Next(100) < 8;
                chanThuongs.Add(new LichSuChanThuong
                {
                    MaCauThu = ct.MaCauThu,
                    LoaiChanThuong = LoaiChanThuongMau[rnd.Next(LoaiChanThuongMau.Length)],
                    NgayBatDau = batDau,
                    NgayKetThuc = dangDieuTri ? null : batDau.AddDays(rnd.Next(7, 120)),
                    SoTranVang = rnd.Next(1, 18),
                    MuaGiai = $"{batDau.Year}/{batDau.Year + 1}"
                });
            }
        }
        db.LichSuChanThuongs.AddRange(chanThuongs);

        // 9. Tài khoản mặc định
        db.NguoiDungs.AddRange(
            new NguoiDung
            {
                Email = "admin@playertransfer.local",
                HoTen = "Quản trị viên",
                MatKhauHash = MatKhauHelper.BamMatKhau("Admin@123"),
                VaiTro = VaiTro.QuanTriVien,
                TrangThai = TrangThaiTaiKhoan.DaKichHoat
            },
            new NguoiDung
            {
                Email = "member@playertransfer.local",
                HoTen = "Thành viên demo",
                MatKhauHash = MatKhauHelper.BamMatKhau("Member@123"),
                VaiTro = VaiTro.ThanhVien,
                TrangThai = TrangThaiTaiKhoan.DaKichHoat
            });

        await db.SaveChangesAsync();
    }

    private static ChiSoCauThu TaoChiSo(CauThu ct, NhomViTri nhom, decimal giaTri, Random rnd)
    {
        // Chỉ số nền suy ra từ giá trị thị trường, sau đó điều chỉnh theo nhóm vị trí.
        var nen = (int)Math.Clamp(58 + (double)giaTri * 0.18, 58, 90);
        int Bien(int cong) => Math.Clamp(nen + cong + rnd.Next(-4, 5), 40, 99);

        var chiSo = new ChiSoCauThu { MuaGiai = "2025/2026" };
        switch (nhom)
        {
            case NhomViTri.ThuMon:
                chiSo.TocDo = Bien(-20); chiSo.DutDiem = Bien(-35); chiSo.Chuyen = Bien(-8);
                chiSo.ReDat = Bien(-25); chiSo.PhongNgu = Bien(4); chiSo.TheLuc = Bien(-6);
                break;
            case NhomViTri.HauVe:
                chiSo.TocDo = Bien(0); chiSo.DutDiem = Bien(-22); chiSo.Chuyen = Bien(-4);
                chiSo.ReDat = Bien(-8); chiSo.PhongNgu = Bien(8); chiSo.TheLuc = Bien(3);
                break;
            case NhomViTri.TienVe:
                chiSo.TocDo = Bien(-2); chiSo.DutDiem = Bien(-6); chiSo.Chuyen = Bien(8);
                chiSo.ReDat = Bien(5); chiSo.PhongNgu = Bien(-4); chiSo.TheLuc = Bien(5);
                break;
            default:
                chiSo.TocDo = Bien(7); chiSo.DutDiem = Bien(9); chiSo.Chuyen = Bien(-3);
                chiSo.ReDat = Bien(6); chiSo.PhongNgu = Bien(-20); chiSo.TheLuc = Bien(0);
                break;
        }

        chiSo.SoTran = rnd.Next(14, 38);
        chiSo.SoPhutThiDau = chiSo.SoTran * rnd.Next(55, 91);
        chiSo.BanThang = nhom switch
        {
            NhomViTri.ThuMon => 0,
            NhomViTri.HauVe => rnd.Next(0, 5),
            NhomViTri.TienVe => rnd.Next(0, 12),
            _ => rnd.Next(4, 28)
        };
        chiSo.KienTao = nhom == NhomViTri.ThuMon ? 0 : rnd.Next(0, 14);
        chiSo.TheVang = rnd.Next(0, 10);
        chiSo.TheDo = rnd.Next(0, 100) < 12 ? 1 : 0;
        return chiSo;
    }
}
