using Microsoft.AspNetCore.DataProtection;
using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Web.Services;

/// <summary>
/// "Ghi nhớ đăng nhập (30 ngày)".
///
/// Phiên (session) của giao diện nằm trong bộ nhớ máy chủ và cookie phiên mất khi đóng trình duyệt,
/// nên trước đây ô "Ghi nhớ" không có tác dụng gì. Giờ refresh token được mã hóa bằng Data Protection
/// và lưu trong một cookie bền 30 ngày; khi phiên trống, middleware dùng nó để đăng nhập lại ngầm.
/// </summary>
public class GhiNhoDangNhap
{
    public const string TenCookie = "pm.ghinho";
    private static readonly TimeSpan ThoiHan = TimeSpan.FromDays(30);

    private readonly IDataProtector _baoVe;

    public GhiNhoDangNhap(IDataProtectionProvider baoVe)
        => _baoVe = baoVe.CreateProtector("PlayerTransfer.GhiNhoDangNhap.v1");

    public void Luu(HttpContext http, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        http.Response.Cookies.Append(TenCookie, _baoVe.Protect(refreshToken), new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(ThoiHan)
        });
    }

    public void Xoa(HttpContext http) => http.Response.Cookies.Delete(TenCookie);

    public bool DangBat(HttpContext http) => http.Request.Cookies.ContainsKey(TenCookie);

    /// <summary>Đọc refresh token trong cookie; trả về null nếu không có hoặc đã bị sửa/hỏng.</summary>
    public string Doc(HttpContext http)
    {
        if (!http.Request.Cookies.TryGetValue(TenCookie, out var giaTri) || string.IsNullOrEmpty(giaTri))
            return null;

        try
        {
            return _baoVe.Unprotect(giaTri);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Ghi thông tin đăng nhập vào phiên — dùng chung cho đăng nhập thường và đăng nhập lại ngầm.</summary>
    public static void GhiVaoPhien(ISession phien, TokenDto token)
    {
        phien.SetString("AccessToken", token.AccessToken);
        phien.SetString("RefreshToken", token.RefreshToken ?? string.Empty);
        phien.SetString("HoTen", token.HoTen ?? string.Empty);
        phien.SetString("VaiTro", token.VaiTro ?? "ThanhVien");
    }

    /// <summary>
    /// Xóa thông tin đăng nhập khỏi phiên nhưng GIỮ giỏ so sánh. Trước đây khi refresh token hết hạn
    /// chỉ xóa token, còn tên và vai trò vẫn ở lại — thanh điều hướng tiếp tục hiện avatar và menu
    /// Quản trị trong khi thực chất đã bị đăng xuất.
    /// </summary>
    public static void XoaKhoiPhien(ISession phien)
    {
        phien.Remove("AccessToken");
        phien.Remove("RefreshToken");
        phien.Remove("HoTen");
        phien.Remove("VaiTro");
    }
}
