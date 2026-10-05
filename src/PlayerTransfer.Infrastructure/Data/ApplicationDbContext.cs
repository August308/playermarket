using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlayerTransfer.Domain.Entities;

namespace PlayerTransfer.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<PhienDangNhap> PhienDangNhaps => Set<PhienDangNhap>();
    public DbSet<QuocGia> QuocGias => Set<QuocGia>();
    public DbSet<GiaiDau> GiaiDaus => Set<GiaiDau>();
    public DbSet<CauLacBo> CauLacBos => Set<CauLacBo>();
    public DbSet<ViTriThiDau> ViTriThiDaus => Set<ViTriThiDau>();
    public DbSet<CauThu> CauThus => Set<CauThu>();
    public DbSet<ChiSoCauThu> ChiSoCauThus => Set<ChiSoCauThu>();
    public DbSet<LichSuChuyenNhuong> LichSuChuyenNhuongs => Set<LichSuChuyenNhuong>();
    public DbSet<LichSuChanThuong> LichSuChanThuongs => Set<LichSuChanThuong>();
    public DbSet<LichSuGiaTri> LichSuGiaTris => Set<LichSuGiaTri>();
    public DbSet<TheoDoiCauThu> TheoDoiCauThus => Set<TheoDoiCauThu>();
    public DbSet<DoiHinhAo> DoiHinhAos => Set<DoiHinhAo>();
    public DbSet<DoiHinhAoCauThu> DoiHinhAoCauThus => Set<DoiHinhAoCauThu>();
    public DbSet<NhatKyDongBo> NhatKyDongBos => Set<NhatKyDongBo>();
    public DbSet<NhatKyThaoTac> NhatKyThaoTacs => Set<NhatKyThaoTac>();
    public DbSet<LuotXemCauThu> LuotXemCauThus => Set<LuotXemCauThu>();
    public DbSet<LuotChoiDoanGia> LuotChoiDoanGias => Set<LuotChoiDoanGia>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<NguoiDung>(e =>
        {
            e.HasKey(x => x.MaNguoiDung);
            e.Property(x => x.Email).IsRequired().HasMaxLength(150);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.MatKhauHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.HoTen).IsRequired().HasMaxLength(100);
            e.Property(x => x.AnhDaiDien).HasMaxLength(255);
            e.Property(x => x.VaiTro).HasConversion<int>();
            e.Property(x => x.TrangThai).HasConversion<int>();
        });

        b.Entity<PhienDangNhap>(e =>
        {
            e.HasKey(x => x.MaPhien);
            e.Property(x => x.RefreshToken).IsRequired().HasMaxLength(255);
            e.HasIndex(x => x.RefreshToken);
            e.Property(x => x.ThayTheBoiToken).HasMaxLength(255);
            e.Property(x => x.LyDoThuHoi).HasMaxLength(40);
            e.Property(x => x.ThietBi).HasMaxLength(200);
            e.Property(x => x.DiaChiIp).HasMaxLength(64);
            e.HasOne(x => x.NguoiDung).WithMany(x => x.PhienDangNhaps)
             .HasForeignKey(x => x.MaNguoiDung).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuocGia>(e =>
        {
            e.HasKey(x => x.MaQuocGia);
            e.Property(x => x.TenQuocGia).IsRequired().HasMaxLength(100);
            e.Property(x => x.MaISO).HasMaxLength(10);
        });

        b.Entity<GiaiDau>(e =>
        {
            e.HasKey(x => x.MaGiaiDau);
            e.Property(x => x.TenGiaiDau).IsRequired().HasMaxLength(120);
            e.Property(x => x.MaNgoaiApi).HasMaxLength(50);
            e.Property(x => x.MaTransfermarkt).HasMaxLength(50);
            e.HasIndex(x => x.MaNgoaiApi);
            e.Property(x => x.Logo).HasMaxLength(255);
            e.HasOne(x => x.QuocGia).WithMany().HasForeignKey(x => x.MaQuocGia)
             .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<CauLacBo>(e =>
        {
            e.HasKey(x => x.MaCLB);
            e.Property(x => x.TenCLB).IsRequired().HasMaxLength(150);
            e.Property(x => x.MaNgoaiApi).HasMaxLength(50);
            e.Property(x => x.MaTransfermarkt).HasMaxLength(50);
            e.HasIndex(x => x.MaTransfermarkt);
            e.HasIndex(x => x.MaNgoaiApi);
            e.Property(x => x.Logo).HasMaxLength(255);
            e.Property(x => x.SanNha).HasMaxLength(150);
            e.HasOne(x => x.GiaiDau).WithMany(x => x.CauLacBos).HasForeignKey(x => x.MaGiaiDau)
             .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<ViTriThiDau>(e =>
        {
            e.HasKey(x => x.MaViTri);
            e.Property(x => x.TenViTri).IsRequired().HasMaxLength(60);
            e.Property(x => x.VietTat).IsRequired().HasMaxLength(5);
            e.Property(x => x.NhomViTri).HasConversion<int>();
        });

        b.Entity<CauThu>(e =>
        {
            e.HasKey(x => x.MaCauThu);
            e.Property(x => x.HoTen).IsRequired().HasMaxLength(150);
            e.Property(x => x.TenKhongDau).HasMaxLength(150);
            e.Property(x => x.AnhDaiDien).HasMaxLength(255);
            e.Property(x => x.MaNgoaiApi).HasMaxLength(50);
            e.Property(x => x.MaTransfermarkt).HasMaxLength(50);
            e.Property(x => x.NguonDuLieu).HasMaxLength(30);
            e.HasIndex(x => x.MaTransfermarkt);
            e.Property(x => x.GiaTriHienTai).HasPrecision(12, 2);
            e.Property(x => x.GiaTriCaoNhat).HasPrecision(12, 2);
            e.Property(x => x.ChanThuan).HasConversion<int?>();
            e.HasIndex(x => x.TenKhongDau);
            e.HasIndex(x => x.MaNgoaiApi);
            e.HasIndex(x => new { x.MaCLB, x.MaViTri, x.GiaTriHienTai });
            if (Database.IsRelational())
                e.ToTable(t => t.HasCheckConstraint("CK_CauThu_GiaTri", "[GiaTriHienTai] >= 0"));

            e.HasOne(x => x.QuocGia).WithMany(x => x.CauThus).HasForeignKey(x => x.MaQuocGia)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.CauLacBo).WithMany(x => x.CauThus).HasForeignKey(x => x.MaCLB)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ViTri).WithMany(x => x.CauThus).HasForeignKey(x => x.MaViTri)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ChiSo).WithOne(x => x.CauThu).HasForeignKey<ChiSoCauThu>(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChiSoCauThu>(e =>
        {
            e.HasKey(x => x.MaChiSo);
            e.Property(x => x.MuaGiai).HasMaxLength(9);
            e.Property(x => x.NguonChiSo).HasMaxLength(20);
            e.Ignore(x => x.ChiSoTongHop);
            e.Ignore(x => x.LaChiSoUocLuong);
            e.Ignore(x => x.ThieuDuLieu);
        });

        b.Entity<LichSuChuyenNhuong>(e =>
        {
            e.HasKey(x => x.MaChuyenNhuong);
            e.Property(x => x.MuaGiai).IsRequired().HasMaxLength(9);
            e.Property(x => x.TenCLBDiNgoai).HasMaxLength(150);
            e.Property(x => x.TenCLBDenNgoai).HasMaxLength(150);
            e.Property(x => x.PhiChuyenNhuong).HasPrecision(12, 2);
            e.Property(x => x.GiaTriThiTruongLucDo).HasPrecision(12, 2);
            e.Property(x => x.LoaiChuyenNhuong).HasConversion<int>();
            e.HasIndex(x => new { x.MuaGiai, x.NgayChuyenNhuong });
            if (Database.IsRelational())
                e.ToTable(t => t.HasCheckConstraint("CK_ChuyenNhuong_Phi", "[PhiChuyenNhuong] >= 0"));

            e.HasOne(x => x.CauThu).WithMany(x => x.LichSuChuyenNhuongs).HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CLBDi).WithMany().HasForeignKey(x => x.MaCLBDi).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.CLBDen).WithMany().HasForeignKey(x => x.MaCLBDen).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<LichSuChanThuong>(e =>
        {
            e.HasKey(x => x.MaChanThuong);
            e.Property(x => x.LoaiChanThuong).IsRequired().HasMaxLength(150);
            e.Property(x => x.MuaGiai).HasMaxLength(9);
            e.Ignore(x => x.SoNgayNghi);
            e.Ignore(x => x.DangDieuTri);
            e.HasOne(x => x.CauThu).WithMany(x => x.LichSuChanThuongs).HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LichSuGiaTri>(e =>
        {
            e.HasKey(x => x.MaLichSuGiaTri);
            e.Property(x => x.GiaTri).HasPrecision(12, 2);
            e.Property(x => x.Nguon).HasMaxLength(50);
            e.HasIndex(x => new { x.MaCauThu, x.NgayGhiNhan });
            e.HasOne(x => x.CauThu).WithMany(x => x.LichSuGiaTris).HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TheoDoiCauThu>(e =>
        {
            e.HasKey(x => x.MaTheoDoi);
            e.Property(x => x.GhiChu).HasMaxLength(255);
            e.Property(x => x.NguongCanhBaoTren).HasPrecision(12, 2);
            e.Property(x => x.NguongCanhBaoDuoi).HasPrecision(12, 2);
            e.HasIndex(x => new { x.MaNguoiDung, x.MaCauThu }).IsUnique();
            e.HasOne(x => x.NguoiDung).WithMany(x => x.DanhSachTheoDoi).HasForeignKey(x => x.MaNguoiDung)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CauThu).WithMany().HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DoiHinhAo>(e =>
        {
            e.HasKey(x => x.MaDoiHinh);
            e.Property(x => x.TenDoiHinh).IsRequired().HasMaxLength(100);
            e.Property(x => x.SoDoChienThuat).IsRequired().HasMaxLength(15);
            e.HasOne(x => x.NguoiDung).WithMany(x => x.DoiHinhAos).HasForeignKey(x => x.MaNguoiDung)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DoiHinhAoCauThu>(e =>
        {
            e.HasKey(x => x.MaDoiHinhCauThu);
            e.Property(x => x.ViTriTrenSan).IsRequired().HasMaxLength(10);
            e.HasOne(x => x.DoiHinh).WithMany(x => x.ViTris).HasForeignKey(x => x.MaDoiHinh)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CauThu).WithMany().HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<NhatKyDongBo>(e =>
        {
            e.HasKey(x => x.MaNhatKy);
            e.Property(x => x.PhamVi).HasMaxLength(100);
            e.Property(x => x.TrangThai).HasMaxLength(30);
            e.Property(x => x.ThongDiep).HasMaxLength(1000);
        });

        b.Entity<LuotXemCauThu>(e =>
        {
            e.HasKey(x => x.MaLuotXem);
            e.HasIndex(x => new { x.MaCauThu, x.ThoiDiem });
            e.HasOne(x => x.CauThu).WithMany().HasForeignKey(x => x.MaCauThu)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LuotChoiDoanGia>(e =>
        {
            e.HasKey(x => x.MaLuotChoi);
            e.HasIndex(x => new { x.Diem, x.ThoiDiem });
            e.HasOne(x => x.NguoiDung).WithMany().HasForeignKey(x => x.MaNguoiDung)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NhatKyThaoTac>(e =>
        {
            e.HasKey(x => x.MaNhatKy);
            e.Property(x => x.HanhDong).HasMaxLength(100);
            e.Property(x => x.DoiTuong).HasMaxLength(100);
            e.Property(x => x.ChiTiet).HasMaxLength(1000);
        });

        // SQLite không có kiểu decimal: EF lưu thành chuỗi nên so sánh và sắp xếp theo
        // giá trị sẽ sai ("9" đứng sau "180"). Chuyển sang double để lọc và sắp xếp đúng.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var boChuyenDoi = new ValueConverter<decimal, double>(
                giaTri => (double)giaTri,
                giaTri => (decimal)giaTri);

            var boChuyenDoiNullable = new ValueConverter<decimal?, double?>(
                giaTri => giaTri.HasValue ? (double)giaTri.Value : (double?)null,
                giaTri => giaTri.HasValue ? (decimal)giaTri.Value : (decimal?)null);

            foreach (var thucThe in b.Model.GetEntityTypes())
            {
                foreach (var thuocTinh in thucThe.GetProperties())
                {
                    if (thuocTinh.ClrType == typeof(decimal))
                        thuocTinh.SetValueConverter(boChuyenDoi);
                    else if (thuocTinh.ClrType == typeof(decimal?))
                        thuocTinh.SetValueConverter(boChuyenDoiNullable);
                }
            }
        }
    }
}
