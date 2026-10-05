using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/watchlist")]
public class WatchlistController : ControllerBase
{
    private readonly ITheoDoiService _theoDoi;

    public WatchlistController(ITheoDoiService theoDoi) => _theoDoi = theoDoi;

    private int MaNguoiDung =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Danh sách cầu thủ đang theo dõi.</summary>
    [HttpGet]
    public async Task<IActionResult> DanhSach() => Ok(await _theoDoi.LayDanhSachAsync(MaNguoiDung));

    /// <summary>Thêm cầu thủ vào danh sách theo dõi.</summary>
    [HttpPost("{maCauThu:int}")]
    public async Task<IActionResult> Them(int maCauThu)
    {
        var ketQua = await _theoDoi.ThemAsync(MaNguoiDung, maCauThu);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Bỏ theo dõi một cầu thủ.</summary>
    [HttpDelete("{maCauThu:int}")]
    public async Task<IActionResult> Xoa(int maCauThu)
    {
        var ketQua = await _theoDoi.XoaAsync(MaNguoiDung, maCauThu);
        return ketQua.ThanhCong ? NoContent() : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Cập nhật ngưỡng cảnh báo giá.</summary>
    [HttpPut("{maCauThu:int}/alert")]
    public async Task<IActionResult> CapNhatCanhBao(int maCauThu, [FromBody] CanhBaoRequest request)
    {
        var ketQua = await _theoDoi.CapNhatCanhBaoAsync(MaNguoiDung, maCauThu,
            request?.NguongTren, request?.NguongDuoi, request?.GhiChu);

        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}

public class CanhBaoRequest
{
    public decimal? NguongTren { get; set; }
    public decimal? NguongDuoi { get; set; }
    public string GhiChu { get; set; }
}
