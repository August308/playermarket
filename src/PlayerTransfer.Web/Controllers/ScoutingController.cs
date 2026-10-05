using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

/// <summary>Chế độ tuyển trạch (C2) và tìm cầu thủ thay thế (C3).</summary>
public class ScoutingController : Controller
{
    private readonly ApiClient _api;

    public ScoutingController(ApiClient api) => _api = api;

    private bool DaDangNhap => !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));

    public async Task<IActionResult> Index(TieuChiTuyenTrachDto tieuChi)
    {
        if (!DaDangNhap)
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Url.Action("Index", "Scouting") });

        tieuChi ??= new TieuChiTuyenTrachDto();
        if (tieuChi.NganSach <= 0) tieuChi.NganSach = 100;
        if (tieuChi.SoLuong <= 0) tieuChi.SoLuong = 12;

        var vm = new TuyenTrachVm
        {
            TieuChi = tieuChi,
            ViTris = await _api.DanhMucAsync("positions"),
            GiaiDaus = await _api.DanhMucAsync("leagues")
        };

        // Chỉ tìm khi người dùng đã bấm nút, để lần đầu vào trang không tải nặng.
        if (Request.Query.ContainsKey("daTim"))
        {
            var (ok, thongDiep, ketQua) = await _api.PostAsync<List<MucTieuDto>>("api/scouting/targets", tieuChi);

            if (ok && ketQua != null) vm.MucTieus = ketQua;
            else vm.ThongBao = thongDiep;

            vm.DaTim = true;
        }

        return View(vm);
    }

    /// <summary>Phần kết quả riêng, dùng cho cập nhật không tải lại trang.</summary>
    public async Task<IActionResult> KetQua(TieuChiTuyenTrachDto tieuChi)
    {
        var (ok, _, ketQua) = await _api.PostAsync<List<MucTieuDto>>("api/scouting/targets", tieuChi);
        return PartialView("_DanhSachMucTieu", ok && ketQua != null ? ketQua : new List<MucTieuDto>());
    }

    /// <summary>C3 — danh sách cầu thủ có thể thay thế một cầu thủ cho trước.</summary>
    public async Task<IActionResult> ThayThe(int id, decimal? nganSach)
    {
        var (okCt, _, cauThu) = await _api.GetAsync<CauThuChiTietDto>($"api/players/{id}");
        if (!okCt || cauThu == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("KhongTimThay");
        }

        var duong = $"api/scouting/replacements/{id}?limit=12"
                    + (nganSach.HasValue ? $"&maxBudget={nganSach.Value}" : string.Empty);

        var (_, _, danhSach) = await _api.GetAsync<List<NguoiThayTheDto>>(duong);

        return View(new ThayTheVm
        {
            CauThuGoc = cauThu,
            NganSach = nganSach,
            DanhSach = danhSach ?? new List<NguoiThayTheDto>()
        });
    }
}
