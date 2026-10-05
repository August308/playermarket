using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Tests;

public static class TestHelper
{
    /// <summary>Tạo cơ sở dữ liệu trong bộ nhớ kèm vài bản ghi cố định cho kiểm thử.</summary>
    public static ApplicationDbContext TaoDbTrongBoNho(string ten = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(ten ?? Guid.NewGuid().ToString())
            .Options;

        var db = new ApplicationDbContext(options);

        var quocGia = new QuocGia { MaQuocGia = 1, TenQuocGia = "Anh", MaISO = "ENG" };
        var giaiDau = new GiaiDau { MaGiaiDau = 1, TenGiaiDau = "Premier League" };
        var clb = new CauLacBo { MaCLB = 1, TenCLB = "Manchester City", MaGiaiDau = 1 };
        var viTri = new ViTriThiDau { MaViTri = 1, TenViTri = "Trung phong", VietTat = "ST", NhomViTri = NhomViTri.TienDao };

        db.QuocGias.Add(quocGia);
        db.GiaiDaus.Add(giaiDau);
        db.CauLacBos.Add(clb);
        db.ViTriThiDaus.Add(viTri);

        db.CauThus.AddRange(
            new CauThu
            {
                MaCauThu = 1, HoTen = "Erling Haaland", TenKhongDau = ChuanHoa.BoDau("Erling Haaland"),
                NgaySinh = new DateTime(2000, 7, 21), MaQuocGia = 1, MaCLB = 1, MaViTri = 1,
                GiaTriHienTai = 180, GiaTriCaoNhat = 200,
                ChiSo = new ChiSoCauThu { MaChiSo = 1, MaCauThu = 1, TocDo = 89, DutDiem = 94, Chuyen = 65, ReDat = 80, PhongNgu = 45, TheLuc = 88 }
            },
            new CauThu
            {
                MaCauThu = 2, HoTen = "Nguyễn Văn A", TenKhongDau = ChuanHoa.BoDau("Nguyễn Văn A"),
                NgaySinh = new DateTime(1998, 3, 10), MaQuocGia = 1, MaCLB = 1, MaViTri = 1,
                GiaTriHienTai = 20, GiaTriCaoNhat = 25,
                ChiSo = new ChiSoCauThu { MaChiSo = 2, MaCauThu = 2, TocDo = 75, DutDiem = 70, Chuyen = 68, ReDat = 72, PhongNgu = 40, TheLuc = 78 }
            });

        db.NguoiDungs.Add(new NguoiDung
        {
            MaNguoiDung = 1,
            Email = "test@local",
            HoTen = "Người kiểm thử",
            MatKhauHash = MatKhauHelper.BamMatKhau("Test@1234"),
            TrangThai = TrangThaiTaiKhoan.DaKichHoat
        });

        db.SaveChanges();
        return db;
    }
}
