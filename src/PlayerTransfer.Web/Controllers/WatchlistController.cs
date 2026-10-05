using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class WatchlistController : Controller
{
    private readonly ApiClient _api;
    private readonly TheoDoiCuaToi _theoDoi;

    public WatchlistController(ApiClient api, TheoDoiCuaToi theoDoi)
    {
        _api = api;
        _theoDoi = theoDoi;
    }

    private bool DaDangNhap => !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));

    public async Task<IActionResult> Index()
    {
        if (!DaDangNhap)
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Url.Action("Index", "Watchlist") });

        var (ok, _, danhSach) = await _api.GetAsync<List<TheoDoiDto>>("api/watchlist");
        return View(ok && danhSach != null ? danhSach : new List<TheoDoiDto>());
    }

    /// <summary>Số cầu thủ đang theo dõi đã chạm ngưỡng cảnh báo giá — dùng cho huy hiệu trên thanh điều hướng.</summary>
    [HttpGet]
    public async Task<IActionResult> SoCanhBao()
    {
        if (!DaDangNhap) return Json(new { so = 0 });

        var (ok, _, danhSach) = await _api.GetAsync<List<TheoDoiDto>>("api/watchlist");
        var so = ok && danhSach != null ? danhSach.Count(x => x.DaVuotNguong) : 0;

        return Json(new { so });
    }

    private bool LaHtmx => Request.Headers.ContainsKey("HX-Request");

    private void GuiToast(string noiDung, string loai)
    {
        Response.Headers["HX-Trigger"] =
            JsonSerializer.Serialize(new { pmToast = new { noiDung, loai } });
    }

    [HttpPost]
    public async Task<IActionResult> Them(int maCauThu, string quayLai)
    {
        if (!DaDangNhap)
        {
            if (LaHtmx)
            {
                GuiToast("Bạn cần đăng nhập để theo dõi cầu thủ.", "loi");
                return PartialView("_NutTheoDoi", (maCauThu, false));
            }
            return RedirectToAction("DangNhap", "Account", new { returnUrl = quayLai });
        }

        var (ok, thongDiep, _) = await _api.PostAsync<object>($"api/watchlist/{maCauThu}");

        if (LaHtmx)
        {
            GuiToast(ok ? "Đã thêm vào danh sách theo dõi." : thongDiep, ok ? "ok" : "loi");
            // Hiện đúng trạng thái THẬT sau thao tác (kể cả khi thất bại vì đã theo dõi từ trước).
            return PartialView("_NutTheoDoi", (maCauThu, ok || await _theoDoi.DangTheoDoiAsync(maCauThu)));
        }

        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã thêm vào danh sách theo dõi." : thongDiep;
        return Redirect(string.IsNullOrEmpty(quayLai) ? Url.Action("Index", "Players") : quayLai);
    }

    [HttpPost]
    public async Task<IActionResult> Bo(int maCauThu, string quayLai)
    {
        if (!DaDangNhap)
            return LaHtmx
                ? PartialView("_NutTheoDoi", (maCauThu, false))
                : RedirectToAction("DangNhap", "Account", new { returnUrl = quayLai });

        await _api.DeleteAsync<object>($"api/watchlist/{maCauThu}");

        if (LaHtmx)
        {
            GuiToast("Đã bỏ theo dõi.", "ok");
            return PartialView("_NutTheoDoi", (maCauThu, false));
        }

        TempData["ThongBao"] = "Đã bỏ theo dõi.";
        return Redirect(string.IsNullOrEmpty(quayLai) ? Url.Action("Index", "Watchlist") : quayLai);
    }

    [HttpPost]
    public async Task<IActionResult> CapNhatCanhBao(int maCauThu, decimal? nguongTren, decimal? nguongDuoi, string ghiChu)
    {
        var (ok, thongDiep, _) = await _api.PutAsync<object>($"api/watchlist/{maCauThu}/alert",
            new { NguongTren = nguongTren, NguongDuoi = nguongDuoi, GhiChu = ghiChu });

        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã cập nhật cảnh báo giá." : thongDiep;
        return RedirectToAction(nameof(Index));
    }
}
