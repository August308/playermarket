using PlayerTransfer.Domain.Enums;

namespace PlayerTransfer.Domain.Entities;

public class NguoiDung
{
    public int MaNguoiDung { get; set; }
    public string Email { get; set; }
    public string MatKhauHash { get; set; }
    public string HoTen { get; set; }
    public string AnhDaiDien { get; set; }
    public VaiTro VaiTro { get; set; } = VaiTro.ThanhVien;
    public TrangThaiTaiKhoan TrangThai { get; set; } = TrangThaiTaiKhoan.ChuaKichHoat;
    public int SoLanDangNhapSai { get; set; }
    public DateTime? KhoaDenThoiDiem { get; set; }
    public string MaKichHoat { get; set; }
    public string MaDatLaiMatKhau { get; set; }
    public DateTime? HanMaDatLaiMatKhau { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;

    public ICollection<PhienDangNhap> PhienDangNhaps { get; set; } = new List<PhienDangNhap>();
    public ICollection<TheoDoiCauThu> DanhSachTheoDoi { get; set; } = new List<TheoDoiCauThu>();
    public ICollection<DoiHinhAo> DoiHinhAos { get; set; } = new List<DoiHinhAo>();
}
