using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

/// <summary>C10 — trò chơi đoán giá trị cầu thủ.</summary>
public class GameController : Controller
{
    private readonly ApiClient _api;

    public GameController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var (_, _, bxh) = await _api.GetAsync<List<HangNguoiChoiDto>>("api/game/leaderboard?limit=10");
        ViewBag.DaDangNhap = !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));

        return View(bxh ?? new List<HangNguoiChoiDto>());
    }

    /// <summary>Trả bộ câu hỏi cho trình duyệt.</summary>
    [HttpGet]
    public async Task<IActionResult> CauHoi(int soCau = 5)
    {
        var (ok, _, ds) = await _api.GetAsync<List<CauHoiDoanGiaDto>>($"api/game/questions?count={soCau}");
        return Json(ok && ds != null ? ds : new List<CauHoiDoanGiaDto>());
    }

    /// <summary>Nhận kết quả lượt chơi và lưu vào bảng xếp hạng.</summary>
    [HttpPost]
    public async Task<IActionResult> LuuDiem([FromBody] KetQuaChoiDto ketQua)
    {
        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/game/score", ketQua);
        return Json(new { ok, thongDiep });
    }
}
