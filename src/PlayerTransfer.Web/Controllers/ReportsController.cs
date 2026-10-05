using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

/// <summary>
/// E6 + E7 — trung chuyển các tệp xuất/nhập giữa trình duyệt và API, để trình duyệt
/// không phải gọi thẳng sang cổng của API (tránh vướng CORS và lộ access token).
/// </summary>
public class ReportsController : Controller
{
    private readonly ApiClient _api;

    public ReportsController(ApiClient api) => _api = api;

    private bool DaDangNhap => !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));
    private bool LaQuanTri => HttpContext.Session.GetString("VaiTro") == "QuanTriVien";

    /// <summary>E6 — xuất danh sách cầu thủ đang lọc ra Excel.</summary>
    [HttpGet]
    public async Task<IActionResult> CauThuExcel([FromQuery] BoLocCauThuDto boLoc)
    {
        var chuoiTruyVan = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        var (ok, thongDiep, duLieu, kieu, ten) = await _api.TaiTepAsync($"api/reports/players.xlsx{chuoiTruyVan}");

        if (!ok) return LoiTaiVe(thongDiep);
        return File(duLieu, kieu, ten);
    }

    /// <summary>E7 — hồ sơ cầu thủ dạng PDF.</summary>
    [HttpGet]
    public async Task<IActionResult> HoSoPdf(int id)
    {
        var (ok, thongDiep, duLieu, kieu, ten) = await _api.TaiTepAsync($"api/reports/players/{id}.pdf");
        if (!ok) return LoiTaiVe(thongDiep);
        return File(duLieu, kieu, ten);
    }

    /// <summary>E7 — báo cáo tổng quan thị trường dạng PDF.</summary>
    [HttpGet]
    public async Task<IActionResult> ThiTruongPdf(string muaGiai, int? maGiaiDau)
    {
        var truyVan = $"?muaGiai={Uri.EscapeDataString(muaGiai ?? string.Empty)}" +
                      (maGiaiDau.HasValue ? $"&maGiaiDau={maGiaiDau}" : string.Empty);
        var (ok, thongDiep, duLieu, kieu, ten) = await _api.TaiTepAsync($"api/reports/market.pdf{truyVan}");
        if (!ok) return LoiTaiVe(thongDiep);
        return File(duLieu, kieu, ten);
    }

    /// <summary>E6 — tải tệp Excel mẫu dùng cho chức năng nhập.</summary>
    [HttpGet]
    public async Task<IActionResult> MauNhap()
    {
        var (ok, thongDiep, duLieu, kieu, ten) = await _api.TaiTepAsync("api/reports/import-template.xlsx");
        if (!ok) return LoiTaiVe(thongDiep);
        return File(duLieu, kieu, ten);
    }

    /// <summary>E6 — màn hình nhập cầu thủ từ Excel (chỉ quản trị viên).</summary>
    [HttpGet]
    public IActionResult Nhap()
    {
        if (!DaDangNhap) return RedirectToAction("DangNhap", "Account");
        if (!LaQuanTri) return RedirectToAction("Index", "Home");
        return View(new KetQuaNhapDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Nhap(IFormFile tep, bool chayThu = true)
    {
        if (!DaDangNhap) return RedirectToAction("DangNhap", "Account");
        if (!LaQuanTri) return RedirectToAction("Index", "Home");

        if (tep == null || tep.Length == 0)
        {
            ViewBag.Loi = "Chưa chọn tệp Excel.";
            return View(new KetQuaNhapDto());
        }

        if (!tep.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Loi = "Chỉ chấp nhận tệp .xlsx.";
            return View(new KetQuaNhapDto());
        }

        await using var luong = tep.OpenReadStream();
        var (ok, thongDiep, ketQua) = await _api.NhapExcelAsync(luong, tep.FileName, chayThu);

        if (ok) ViewBag.ThongBao = thongDiep;
        else ViewBag.Loi = thongDiep;

        return View(ketQua ?? new KetQuaNhapDto { ChayThu = chayThu });
    }

    private IActionResult LoiTaiVe(string thongDiep)
    {
        TempData["Loi"] = thongDiep ?? "Không tạo được tệp.";
        var quayVe = Request.Headers.Referer.ToString();
        return string.IsNullOrEmpty(quayVe) ? RedirectToAction("Index", "Home") : Redirect(quayVe);
    }
}
