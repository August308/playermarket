using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Authorize(Policy = "ChiQuanTriVien")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IQuanTriService _quanTri;
    private readonly IDongBoService _dongBo;

    public AdminController(IQuanTriService quanTri, IDongBoService dongBo)
    {
        _quanTri = quanTri; _dongBo = dongBo;
    }

    private int MaNguoiDung =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Danh sách cầu thủ dành cho trang quản trị.</summary>
    [HttpGet("players")]
    public async Task<IActionResult> DanhSachCauThu([FromQuery] string keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _quanTri.DanhSachCauThuAsync(keyword, page, pageSize));

    /// <summary>Dữ liệu gốc của một cầu thủ, dùng để nạp biểu mẫu sửa.</summary>
    [HttpGet("players/{id:int}")]
    public async Task<IActionResult> LayCauThuDeSua(int id)
    {
        var dto = await _quanTri.LayDeSuaAsync(id);
        return dto == null
            ? NotFound(new { code = "KHONG_TIM_THAY", message = "Không tìm thấy cầu thủ." })
            : Ok(dto);
    }

    /// <summary>Thêm cầu thủ thủ công.</summary>
    [HttpPost("players")]
    public async Task<IActionResult> ThemCauThu([FromBody] LuuCauThuDto dto)
    {
        dto.MaCauThu = null;
        var ketQua = await _quanTri.LuuCauThuAsync(dto, MaNguoiDung);
        return ketQua.ThanhCong
            ? StatusCode(201, new { maCauThu = ketQua.DuLieu, message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Cập nhật thông tin cầu thủ.</summary>
    [HttpPut("players/{id:int}")]
    public async Task<IActionResult> CapNhatCauThu(int id, [FromBody] LuuCauThuDto dto)
    {
        dto.MaCauThu = id;
        var ketQua = await _quanTri.LuuCauThuAsync(dto, MaNguoiDung);
        return ketQua.ThanhCong
            ? Ok(new { maCauThu = ketQua.DuLieu, message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Xóa cầu thủ.</summary>
    [HttpDelete("players/{id:int}")]
    public async Task<IActionResult> XoaCauThu(int id)
    {
        var ketQua = await _quanTri.XoaCauThuAsync(id, MaNguoiDung);
        return ketQua.ThanhCong ? NoContent() : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Kích hoạt đồng bộ dữ liệu thủ công.</summary>
    [HttpPost("sync/players")]
    public async Task<IActionResult> DongBo([FromQuery] string scope = "Toàn bộ")
    {
        var ketQua = await _dongBo.DongBoAsync(scope);
        return ketQua.ThanhCong
            ? Accepted(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Nhật ký các lần đồng bộ.</summary>
    [HttpGet("sync/logs")]
    public async Task<IActionResult> NhatKyDongBo([FromQuery] int limit = 50)
        => Ok(await _quanTri.NhatKyDongBoAsync(limit));

    /// <summary>Nhật ký thao tác của quản trị viên.</summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> NhatKyThaoTac([FromQuery] int limit = 50)
        => Ok(await _quanTri.NhatKyThaoTacAsync(limit));

    /// <summary>Danh sách người dùng.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> DanhSachNguoiDung([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _quanTri.DanhSachNguoiDungAsync(page, pageSize));

    /// <summary>Đổi trạng thái tài khoản: DaKichHoat, BiKhoa, ChuaKichHoat.</summary>
    [HttpPut("users/{id:int}/status")]
    public async Task<IActionResult> DoiTrangThai(int id, [FromBody] TrangThaiRequest request)
    {
        var ketQua = await _quanTri.DoiTrangThaiNguoiDungAsync(id, request?.TrangThai);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}

public class TrangThaiRequest
{
    public string TrangThai { get; set; }
}
