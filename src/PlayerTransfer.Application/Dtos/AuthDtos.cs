using System.ComponentModel.DataAnnotations;

namespace PlayerTransfer.Application.Dtos;

public class DangKyDto
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự.")]
    public string HoTen { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và số.")]
    public string MatKhau { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string NhapLaiMatKhau { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn cần đồng ý với điều khoản sử dụng.")]
    public bool DongYDieuKhoan { get; set; }
}

public class DangNhapDto
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string MatKhau { get; set; }

    public bool GhiNhoDangNhap { get; set; }

    // E4 — thông tin phiên, do tầng API điền, không nhận từ người dùng.
    [System.Text.Json.Serialization.JsonIgnore]
    public string ThietBi { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DiaChiIp { get; set; }
}

/// <summary>E4 — một phiên đăng nhập đang hoạt động của tài khoản.</summary>
public class PhienDangNhapDto
{
    public Guid MaPhien { get; set; }
    public DateTime ThoiDiemDangNhap { get; set; }
    public DateTime ThoiDiemHetHan { get; set; }
    public string ThietBi { get; set; }
    public string DiaChiIp { get; set; }
    public bool LaPhienHienTai { get; set; }
}

public class TokenDto
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime HetHanLuc { get; set; }
    public string HoTen { get; set; }
    public string Email { get; set; }
    public string VaiTro { get; set; }
}

public class DoiMatKhauDto
{
    [Required] public string MatKhauCu { get; set; }

    [Required]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và số.")]
    public string MatKhauMoi { get; set; }

    [Required]
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string NhapLaiMatKhauMoi { get; set; }
}
