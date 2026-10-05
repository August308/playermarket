using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

/// <summary>C10 — trò chơi đoán giá trị cầu thủ.</summary>
[ApiController]
[Route("api/game")]
public class GameController : ControllerBase
{
    private readonly ITroChoiService _troChoi;

    public GameController(ITroChoiService troChoi) => _troChoi = troChoi;

    private int MaNguoiDung =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Lấy bộ câu hỏi cho một lượt chơi.</summary>
    [HttpGet("questions")]
    public async Task<IActionResult> CauHoi([FromQuery] int count = 5, [FromQuery] decimal? minValue = null)
        => Ok(await _troChoi.TaoCauHoiAsync(count, minValue));

    /// <summary>Lưu kết quả một lượt chơi vào bảng xếp hạng.</summary>
    [Authorize]
    [HttpPost("score")]
    public async Task<IActionResult> LuuKetQua([FromBody] KetQuaChoiDto ketQua)
    {
        var kq = await _troChoi.LuuKetQuaAsync(MaNguoiDung, ketQua);
        return kq.ThanhCong
            ? Ok(new { message = kq.ThongDiep })
            : BadRequest(new { code = kq.Ma, message = kq.ThongDiep });
    }

    /// <summary>Bảng xếp hạng người chơi.</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> BangXepHang([FromQuery] int limit = 10)
        => Ok(await _troChoi.BangXepHangAsync(limit));
}
