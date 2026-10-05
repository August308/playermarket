using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly ICauThuService _cauThu;

    public PlayersController(ICauThuService cauThu) => _cauThu = cauThu;

    /// <summary>Tìm kiếm cầu thủ theo từ khóa, có phân trang.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> TimKiem([FromQuery] string keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _cauThu.TimKiemAsync(keyword, page, pageSize));

    /// <summary>Gợi ý nhanh khi người dùng đang gõ.</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> GoiY([FromQuery] string keyword, [FromQuery] int limit = 5)
        => Ok(await _cauThu.GoiYAsync(keyword, limit));

    /// <summary>Lọc cầu thủ theo bộ tiêu chí kết hợp.</summary>
    [HttpPost("filter")]
    public async Task<IActionResult> Loc([FromBody] BoLocCauThuDto boLoc)
        => Ok(await _cauThu.LocAsync(boLoc));

    /// <summary>Lấy thông tin cơ bản của một cầu thủ.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var ketQua = await _cauThu.LayChiTietAsync(id);
        return ketQua == null
            ? NotFound(new { code = "KHONG_TIM_THAY", message = "Không tìm thấy cầu thủ." })
            : Ok(ketQua);
    }

    /// <summary>Lịch sử chuyển nhượng của cầu thủ.</summary>
    [HttpGet("{id:int}/transfers")]
    public async Task<IActionResult> LichSuChuyenNhuong(int id)
        => Ok(await _cauThu.LayLichSuChuyenNhuongAsync(id));

    /// <summary>Lịch sử chấn thương của cầu thủ.</summary>
    [HttpGet("{id:int}/injuries")]
    public async Task<IActionResult> LichSuChanThuong(int id)
        => Ok(await _cauThu.LayLichSuChanThuongAsync(id));

    /// <summary>Chuỗi giá trị thị trường theo thời gian.</summary>
    [HttpGet("{id:int}/value-history")]
    public async Task<IActionResult> LichSuGiaTri(int id)
        => Ok(await _cauThu.LayLichSuGiaTriAsync(id));

    /// <summary>Danh sách cầu thủ tương đồng.</summary>
    [HttpGet("{id:int}/similar")]
    public async Task<IActionResult> TuongTu(int id, [FromQuery] int limit = 6)
        => Ok(await _cauThu.LayCauThuTuongTuAsync(id, limit));

    /// <summary>So sánh từ 2 đến 4 cầu thủ.</summary>
    [Authorize]
    [HttpGet("compare")]
    public async Task<IActionResult> SoSanh([FromQuery] string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest(new { code = "THIEU_THAM_SO", message = "Thiếu tham số ids." });

        var danhSach = new List<int>();
        foreach (var phan in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(phan.Trim(), out var ma))
                return BadRequest(new { code = "THAM_SO_SAI", message = "Danh sách id không hợp lệ." });
            danhSach.Add(ma);
        }

        var ketQua = await _cauThu.SoSanhAsync(danhSach);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}
