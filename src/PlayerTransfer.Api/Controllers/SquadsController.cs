using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/squads")]
public class SquadsController : ControllerBase
{
    private readonly IDoiHinhService _doiHinh;

    public SquadsController(IDoiHinhService doiHinh) => _doiHinh = doiHinh;

    private int MaNguoiDung =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Danh sách đội hình ảo của người dùng.</summary>
    [HttpGet]
    public async Task<IActionResult> DanhSach() => Ok(await _doiHinh.LayDanhSachAsync(MaNguoiDung));

    /// <summary>Chi tiết một đội hình.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var ketQua = await _doiHinh.LayChiTietAsync(MaNguoiDung, id);
        return ketQua == null
            ? NotFound(new { code = "KHONG_TIM_THAY", message = "Không tìm thấy đội hình." })
            : Ok(ketQua);
    }

    /// <summary>Tạo mới hoặc cập nhật đội hình.</summary>
    [HttpPost]
    public async Task<IActionResult> Luu([FromBody] LuuDoiHinhDto dto)
    {
        var ketQua = await _doiHinh.LuuAsync(MaNguoiDung, dto);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>C6 — so sánh hai đội hình.</summary>
    [HttpGet("compare")]
    public async Task<IActionResult> SoSanh([FromQuery] int a, [FromQuery] int b)
    {
        var ketQua = await _doiHinh.SoSanhAsync(MaNguoiDung, a, b);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Xóa một đội hình.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var ketQua = await _doiHinh.XoaAsync(MaNguoiDung, id);
        return ketQua.ThanhCong ? NoContent() : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}
