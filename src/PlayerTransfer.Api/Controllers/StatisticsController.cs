using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IThongKeService _thongKe;

    public StatisticsController(IThongKeService thongKe) => _thongKe = thongKe;

    /// <summary>Tổng chi phí chuyển nhượng theo mùa giải.</summary>
    [Authorize]
    [HttpGet("by-season")]
    public async Task<IActionResult> TheoMuaGiai([FromQuery] int? leagueId, [FromQuery] int seasons = 5)
        => Ok(await _thongKe.TheoMuaGiaiAsync(leagueId, seasons));

    /// <summary>Tổng chi phí chuyển nhượng theo giải đấu.</summary>
    [Authorize]
    [HttpGet("by-league")]
    public async Task<IActionResult> TheoGiaiDau([FromQuery] string season)
        => Ok(await _thongKe.TheoGiaiDauAsync(season));

    /// <summary>Top thương vụ đắt giá nhất.</summary>
    [Authorize]
    [HttpGet("top-transfers")]
    public async Task<IActionResult> TopThuongVu([FromQuery] string season,
        [FromQuery] int? leagueId, [FromQuery] int limit = 20)
        => Ok(await _thongKe.TopThuongVuAsync(season, leagueId, limit));

    /// <summary>Số dư chi và thu của câu lạc bộ.</summary>
    [Authorize]
    [HttpGet("club-balance")]
    public async Task<IActionResult> SoDuCauLacBo([FromQuery] string season,
        [FromQuery] int? leagueId, [FromQuery] int limit = 20)
        => Ok(await _thongKe.SoDuCauLacBoAsync(season, leagueId, limit));

    /// <summary>Giá trị trung bình theo vị trí thi đấu.</summary>
    [Authorize]
    [HttpGet("by-position")]
    public async Task<IActionResult> TheoViTri([FromQuery] int? leagueId)
        => Ok(await _thongKe.PhanBoTheoViTriAsync(leagueId));

    /// <summary>Giá trị trung bình theo độ tuổi.</summary>
    [Authorize]
    [HttpGet("by-age")]
    public async Task<IActionResult> TheoDoTuoi([FromQuery] int? leagueId)
        => Ok(await _thongKe.GiaTriTheoDoTuoiAsync(leagueId));

    /// <summary>Bảng xếp hạng: dat_gia, tang_gia, giam_gia, tre_trien_vong, sap_het_hop_dong.</summary>
    [OutputCache(PolicyName = "BangXepHang")]
    [HttpGet("ranking")]
    public async Task<IActionResult> BangXepHang([FromQuery] string type = "dat_gia", [FromQuery] int limit = 10)
        => Ok(await _thongKe.BangXepHangAsync(type, limit));

    /// <summary>Danh sách mùa giải có dữ liệu.</summary>
    [OutputCache(PolicyName = "DanhMuc")]
    [HttpGet("seasons")]
    public async Task<IActionResult> MuaGiai() => Ok(await _thongKe.LayDanhSachMuaGiaiAsync());
}
