using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var vm = new TrangChuVm
        {
            DatGiaNhat = await _api.BangXepHangAsync("dat_gia", 8),
            TangGiaManh = await _api.BangXepHangAsync("tang_gia", 8),
            TreTrienVong = await _api.BangXepHangAsync("tre_trien_vong", 8)
        };

        var (ok, _, thuongVus) = await _api.GetAsync<List<ThuongVuDto>>(
            "api/statistics/top-transfers?limit=8");
        if (ok && thuongVus != null) vm.ThuongVuMoi = thuongVus;

        vm.SoGiaiDau = (await _api.DanhMucAsync("leagues")).Count;

        // C1 — cầu thủ được xem nhiều nhất 7 ngày qua.
        var (okXh, _, xuHuong) = await _api.GetAsync<List<XuHuongDto>>("api/scouting/trending?days=7&limit=6");
        if (okXh && xuHuong != null) vm.XuHuong = xuHuong;

        if (_api.ApiKhongKetNoiDuoc)
        {
            ViewBag.CanhBaoApi = "Không kết nối được tới máy chủ API (mặc định https://localhost:7011). "
                               + "Hãy chạy dự án PlayerTransfer.Api trước, hoặc sửa mục ApiBaseUrl trong appsettings.json của dự án Web.";
        }

        return View(vm);
    }

    /// <summary>Gợi ý tìm kiếm tức thời cho ô tìm kiếm trên thanh điều hướng.</summary>
    [HttpGet]
    public async Task<IActionResult> GoiY(string tuKhoa)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa)) return Json(Array.Empty<object>());

        var (ok, _, duLieu) = await _api.GetAsync<List<CauThuTomTatDto>>(
            $"api/players/suggest?keyword={Uri.EscapeDataString(tuKhoa)}&limit=6");

        if (!ok || duLieu == null) return Json(Array.Empty<object>());

        return Json(duLieu.Select(x => new
        {
            x.MaCauThu,
            x.HoTen,
            x.AnhDaiDien,
            x.CauLacBo,
            x.ViTriVietTat,
            x.GiaTriHienTai
        }));
    }

    public IActionResult Loi() => View();
}
