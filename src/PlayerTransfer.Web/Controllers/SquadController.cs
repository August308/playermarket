using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class SquadController : Controller
{
    private readonly ApiClient _api;

    public SquadController(ApiClient api) => _api = api;

    private bool DaDangNhap => !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));

    public async Task<IActionResult> Index(int? id, string soDo = "4-3-3")
    {
        if (!DaDangNhap)
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Url.Action("Index", "Squad") });

        var (_, _, danhSach) = await _api.GetAsync<List<DoiHinhDto>>("api/squads");

        var vm = new DoiHinhVm
        {
            DanhSach = danhSach ?? new List<DoiHinhDto>(),
            SoDo = SoDoChienThuat.DanhSach.ContainsKey(soDo) ? soDo : "4-3-3"
        };

        if (id.HasValue)
        {
            var (ok, _, chiTiet) = await _api.GetAsync<DoiHinhDto>($"api/squads/{id.Value}");
            if (ok && chiTiet != null)
            {
                vm.DangChon = chiTiet;
                vm.SoDo = chiTiet.SoDoChienThuat;
            }
        }

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Luu(int? maDoiHinh, string tenDoiHinh, string soDo,
        string[] viTri, int[] maCauThu)
    {
        var dto = new LuuDoiHinhDto
        {
            MaDoiHinh = maDoiHinh,
            TenDoiHinh = tenDoiHinh,
            SoDoChienThuat = soDo
        };

        for (var i = 0; i < viTri.Length && i < maCauThu.Length; i++)
        {
            if (maCauThu[i] <= 0) continue;
            dto.ViTris.Add(new ViTriLuuDto { ViTriTrenSan = viTri[i], ThuTu = i, MaCauThu = maCauThu[i] });
        }

        var (ok, thongDiep, ketQua) = await _api.PostAsync<DoiHinhDto>("api/squads", dto);
        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã lưu đội hình." : thongDiep;

        return ok && ketQua != null
            ? RedirectToAction(nameof(Index), new { id = ketQua.MaDoiHinh })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        await _api.DeleteAsync<object>($"api/squads/{id}");
        TempData["ThongBao"] = "Đã xóa đội hình.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>C6 — so sánh hai đội hình đã lưu.</summary>
    public async Task<IActionResult> SoSanh(int? a, int? b)
    {
        if (!DaDangNhap)
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Url.Action("SoSanh", "Squad") });

        var (_, _, danhSach) = await _api.GetAsync<List<DoiHinhDto>>("api/squads");
        ViewBag.DanhSach = danhSach ?? new List<DoiHinhDto>();
        ViewBag.MaA = a;
        ViewBag.MaB = b;

        if (a.HasValue && b.HasValue)
        {
            var (ok, thongDiep, ketQua) = await _api.GetAsync<SoSanhDoiHinhDto>(
                $"api/squads/compare?a={a}&b={b}");

            if (!ok) ViewBag.ThongBao = thongDiep;
            return View(ketQua);
        }

        return View((SoSanhDoiHinhDto)null);
    }

    /// <summary>Tìm cầu thủ để gắn vào một vị trí (dùng cho hộp thoại chọn cầu thủ).</summary>
    [HttpGet]
    public async Task<IActionResult> TimCauThu(string tuKhoa)
    {
        var (ok, _, duLieu) = await _api.GetAsync<List<CauThuTomTatDto>>(
            $"api/players/suggest?keyword={Uri.EscapeDataString(tuKhoa ?? string.Empty)}&limit=8");

        return Json(ok && duLieu != null
            ? duLieu.Select(x => new { x.MaCauThu, x.HoTen, x.ViTriVietTat, x.CauLacBo, x.GiaTriHienTai, x.ChiSoTongHop })
            : Enumerable.Empty<object>());
    }
}
