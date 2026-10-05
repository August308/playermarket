using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

/// <summary>Chế độ tuyển trạch, tìm người thay thế, xu hướng và trung bình vị trí.</summary>
[ApiController]
[Route("api/scouting")]
public class ScoutingController : ControllerBase
{
    private readonly ITuyenTrachService _tuyenTrach;

    public ScoutingController(ITuyenTrachService tuyenTrach) => _tuyenTrach = tuyenTrach;

    private int? MaNguoiDung =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>C2 — tìm danh sách mục tiêu chuyển nhượng theo ngân sách và tiêu chí.</summary>
    [Authorize]
    [HttpPost("targets")]
    public async Task<IActionResult> TimMucTieu([FromBody] TieuChiTuyenTrachDto tieuChi)
        => Ok(await _tuyenTrach.TimMucTieuAsync(tieuChi));

    /// <summary>C3 — tìm cầu thủ tương đồng có thể thay thế.</summary>
    [HttpGet("replacements/{maCauThu:int}")]
    public async Task<IActionResult> TimNguoiThayThe(int maCauThu,
        [FromQuery] decimal? maxBudget, [FromQuery] int limit = 10)
        => Ok(await _tuyenTrach.TimNguoiThayTheAsync(maCauThu, maxBudget, limit));

    /// <summary>C11 — chỉ số trung bình của một vị trí thi đấu.</summary>
    [OutputCache(PolicyName = "BangXepHang")]
    [HttpGet("position-average/{maViTri:int}")]
    public async Task<IActionResult> TrungBinhViTri(int maViTri)
        => Ok(await _tuyenTrach.TrungBinhViTriAsync(maViTri));

    /// <summary>C1 — cầu thủ được xem nhiều nhất trong khoảng ngày gần đây.</summary>
    [HttpGet("trending")]
    public async Task<IActionResult> XuHuong([FromQuery] int days = 7, [FromQuery] int limit = 10)
        => Ok(await _tuyenTrach.XuHuongAsync(days, limit));

    /// <summary>Ghi nhận một lượt xem hồ sơ cầu thủ.</summary>
    [HttpPost("view/{maCauThu:int}")]
    public async Task<IActionResult> GhiLuotXem(int maCauThu)
    {
        await _tuyenTrach.GhiLuotXemAsync(maCauThu, MaNguoiDung);
        return NoContent();
    }
}
