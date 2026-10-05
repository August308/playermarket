using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;
    private readonly GhiNhoDangNhap _ghiNho;

    public AccountController(ApiClient api, GhiNhoDangNhap ghiNho)
    {
        _api = api;
        _ghiNho = ghiNho;
    }

    [HttpGet]
    public IActionResult DangKy() => View(new DangKyDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangKy(DangKyDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/auth/register", dto);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, thongDiep ?? "Đăng ký thất bại.");
            return View(dto);
        }

        TempData["ThongBao"] = "Đăng ký thành công, mời bạn đăng nhập.";
        return RedirectToAction(nameof(DangNhap));
    }

    [HttpGet]
    public IActionResult DangNhap(string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new DangNhapDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangNhap(DangNhapDto dto, string returnUrl = null)
    {
        // Giữ đường quay lại khi phải hiện lại biểu mẫu (nhập sai) — trước đây bị mất,
        // đăng nhập lại xong thì bị đưa về trang chủ thay vì trang đang muốn vào.
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid) return View(dto);

        var (ok, thongDiep, token) = await _api.PostAsync<TokenDto>("api/auth/login", dto);
        if (!ok || token == null)
        {
            ModelState.AddModelError(string.Empty, thongDiep ?? "Email hoặc mật khẩu không đúng.");
            return View(dto);
        }

        GhiNhoDangNhap.GhiVaoPhien(HttpContext.Session, token);

        if (dto.GhiNhoDangNhap) _ghiNho.Luu(HttpContext, token.RefreshToken);
        else _ghiNho.Xoa(HttpContext);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Access token cho kênh thời gian thực (SignalR). Trình duyệt gọi khi mở kết nối thay vì
    /// đọc token in sẵn trong mã HTML của mọi trang: token không còn nằm trong nguồn trang,
    /// bộ đệm hay ảnh chụp màn hình, và luôn là token mới nhất sau khi được làm mới.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult TokenRealtime()
        => Json(new { token = HttpContext.Session.GetString("AccessToken") ?? string.Empty });

    /// <summary>E4 — xem các thiết bị đang đăng nhập vào tài khoản.</summary>
    [HttpGet]
    public async Task<IActionResult> Phien()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction(nameof(DangNhap));

        var refreshToken = HttpContext.Session.GetString("RefreshToken") ?? string.Empty;
        // Gửi qua tiêu đề, không đặt lên URL: URL bị ghi nguyên văn vào tệp nhật ký.
        var (ok, thongDiep, danhSach) = await _api.GetAsync<List<PhienDangNhapDto>>(
            "api/auth/sessions",
            new Dictionary<string, string> { ["X-Refresh-Token"] = refreshToken });

        if (!ok) ViewBag.Loi = thongDiep;
        return View(danhSach ?? new List<PhienDangNhapDto>());
    }

    /// <summary>E4 — thu hồi toàn bộ phiên, kể cả phiên hiện tại.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangXuatTatCa()
    {
        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/auth/logout-all");

        HttpContext.Session.Clear();
        _ghiNho.Xoa(HttpContext);
        TempData["ThongBao"] = ok
            ? (thongDiep ?? "Đã đăng xuất khỏi mọi thiết bị.")
            : "Không thực hiện được, vui lòng thử lại.";

        return RedirectToAction(nameof(DangNhap));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangXuat()
    {
        var refreshToken = HttpContext.Session.GetString("RefreshToken");
        await _api.PostAsync<object>("api/auth/logout", new { RefreshToken = refreshToken });

        HttpContext.Session.Clear();
        _ghiNho.Xoa(HttpContext);
        TempData["ThongBao"] = "Bạn đã đăng xuất.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Kích hoạt tài khoản từ liên kết gửi qua email.</summary>
    [HttpGet]
    public async Task<IActionResult> KichHoat(string ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
        {
            TempData["LoiThongBao"] = "Liên kết kích hoạt không hợp lệ.";
            return RedirectToAction(nameof(DangNhap));
        }

        var (ok, thongDiep, _) = await _api.GetAsync<object>($"api/auth/activate/{Uri.EscapeDataString(ma)}");
        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok
            ? "Kích hoạt tài khoản thành công, mời bạn đăng nhập."
            : thongDiep ?? "Mã kích hoạt không hợp lệ hoặc đã hết hạn.";

        return RedirectToAction(nameof(DangNhap));
    }

    [HttpGet]
    public IActionResult QuenMatKhau() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuenMatKhau(string email)
    {
        await _api.PostAsync<object>("api/auth/forgot-password", new { Email = email });

        // Thông điệp cố định để không tiết lộ email nào đang tồn tại trong hệ thống.
        TempData["ThongBao"] = "Nếu email tồn tại, hướng dẫn đặt lại mật khẩu đã được gửi đi.";
        return RedirectToAction(nameof(DangNhap));
    }

    [HttpGet]
    public IActionResult DatLaiMatKhau(string ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
        {
            TempData["LoiThongBao"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(DangNhap));
        }

        ViewBag.Ma = ma;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DatLaiMatKhau(string ma, string matKhauMoi, string nhapLai)
    {
        ViewBag.Ma = ma;

        if (string.IsNullOrWhiteSpace(matKhauMoi) || matKhauMoi != nhapLai)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu nhập lại không khớp.");
            return View();
        }

        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/auth/reset-password",
            new { MaDatLai = ma, MatKhauMoi = matKhauMoi });

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, thongDiep ?? "Không đặt lại được mật khẩu.");
            return View();
        }

        TempData["ThongBao"] = "Đặt lại mật khẩu thành công, mời bạn đăng nhập.";
        return RedirectToAction(nameof(DangNhap));
    }

    [HttpGet]
    public IActionResult DoiMatKhau()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction(nameof(DangNhap));
        return View(new DoiMatKhauDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/auth/change-password", dto);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, thongDiep ?? "Đổi mật khẩu thất bại.");
            return View(dto);
        }

        TempData["ThongBao"] = "Đổi mật khẩu thành công.";
        return RedirectToAction("Index", "Home");
    }
}
