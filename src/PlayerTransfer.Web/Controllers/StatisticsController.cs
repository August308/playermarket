using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class StatisticsController : Controller
{
    private readonly ApiClient _api;

    public StatisticsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string baoCao = "by-season", string muaGiai = null, int? maGiaiDau = null)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction("DangNhap", "Account",
                new { returnUrl = Url.Action("Index", "Statistics") });

        var (_, _, muaGiais) = await _api.GetAsync<List<string>>("api/statistics/seasons");

        var vm = new ThongKeVm
        {
            BaoCao = baoCao,
            MuaGiai = muaGiai,
            MaGiaiDau = maGiaiDau,
            MuaGiais = muaGiais ?? new List<string>(),
            GiaiDaus = await _api.DanhMucAsync("leagues")
        };

        var thamSoGiai = maGiaiDau.HasValue ? $"leagueId={maGiaiDau}" : string.Empty;
        var thamSoMua = string.IsNullOrEmpty(muaGiai) ? string.Empty : $"season={Uri.EscapeDataString(muaGiai)}";
        var thamSo = string.Join("&", new[] { thamSoGiai, thamSoMua }.Where(x => !string.IsNullOrEmpty(x)));
        var hau = string.IsNullOrEmpty(thamSo) ? string.Empty : "?" + thamSo;

        switch (baoCao)
        {
            case "top-transfers":
                vm.TieuDe = "Top thương vụ đắt giá nhất";
                var (_, _, thuongVus) = await _api.GetAsync<List<ThuongVuDto>>(
                    $"api/statistics/top-transfers{hau}{(string.IsNullOrEmpty(hau) ? "?" : "&")}limit=20");
                vm.ThuongVus = thuongVus ?? new List<ThuongVuDto>();
                break;

            case "by-league":
                vm.TieuDe = "Tổng chi phí chuyển nhượng theo giải đấu";
                vm.DuLieu = await LayAsync($"api/statistics/by-league{hau}");
                break;

            case "club-balance":
                vm.TieuDe = "Chi và thu của câu lạc bộ";
                vm.DuLieu = await LayAsync($"api/statistics/club-balance{hau}");
                break;

            case "by-position":
                vm.TieuDe = "Giá trị trung bình theo vị trí thi đấu";
                vm.DuLieu = await LayAsync($"api/statistics/by-position{hau}");
                break;

            case "by-age":
                vm.TieuDe = "Giá trị trung bình theo độ tuổi";
                vm.NhanTrucY = "Triệu euro";
                vm.DuLieu = await LayAsync($"api/statistics/by-age{hau}");
                break;

            default:
                vm.TieuDe = "Tổng chi phí chuyển nhượng theo mùa giải";
                vm.DuLieu = await LayAsync($"api/statistics/by-season{hau}");
                break;
        }

        return View(vm);
    }

    private async Task<List<DiemThongKeDto>> LayAsync(string duongDan)
    {
        var (ok, _, duLieu) = await _api.GetAsync<List<DiemThongKeDto>>(duongDan);
        return ok && duLieu != null ? duLieu : new List<DiemThongKeDto>();
    }
}
