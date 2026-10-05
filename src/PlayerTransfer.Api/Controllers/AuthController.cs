using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("XacThuc")]
public class AuthController : ControllerBase
{
    private readonly IXacThucService _xacThuc;

    public AuthController(IXacThucService xacThuc) => _xacThuc = xacThuc;

    private int MaNguoiDungHienTai =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Đăng ký tài khoản thành viên mới.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> DangKy([FromBody] DangKyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ketQua = await _xacThuc.DangKyAsync(dto);
        if (!ketQua.ThanhCong)
            return ketQua.Ma == "EMAIL_TON_TAI"
                ? Conflict(new { code = ketQua.Ma, message = ketQua.ThongDiep })
                : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });

        return StatusCode(201, new { message = ketQua.ThongDiep });
    }

    /// <summary>Đăng nhập và nhận access token cùng refresh token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> DangNhap([FromBody] DangNhapDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // E4 — ghi lại thiết bị và địa chỉ IP của phiên, để người dùng nhận ra
        // phiên lạ trong danh sách thiết bị đang đăng nhập.
        dto.ThietBi = Request.Headers.UserAgent.ToString();
        dto.DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var ketQua = await _xacThuc.DangNhapAsync(dto);
        if (!ketQua.ThanhCong)
        {
            return ketQua.Ma is "CHUA_KICH_HOAT" or "BI_KHOA" or "TAM_KHOA"
                ? StatusCode(403, new { code = ketQua.Ma, message = ketQua.ThongDiep })
                : Unauthorized(new { code = ketQua.Ma, message = ketQua.ThongDiep });
        }

        return Ok(ketQua.DuLieu);
    }

    /// <summary>Thu hồi refresh token, kết thúc phiên làm việc.</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> DangXuat([FromBody] RefreshTokenRequest request)
    {
        await _xacThuc.DangXuatAsync(request?.RefreshToken);
        return NoContent();
    }

    /// <summary>Cấp access token mới từ refresh token còn hiệu lực.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> LamMoi([FromBody] RefreshTokenRequest request)
    {
        var ketQua = await _xacThuc.LamMoiTokenAsync(request?.RefreshToken);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : Unauthorized(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>E4 — đăng xuất khỏi mọi thiết bị, thu hồi toàn bộ refresh token.</summary>
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> DangXuatTatCa()
    {
        var ketQua = await _xacThuc.DangXuatTatCaAsync(MaNguoiDungHienTai);
        return Ok(new { message = ketQua.ThongDiep });
    }

    /// <summary>E4 — danh sách phiên đăng nhập còn hiệu lực của tài khoản.</summary>
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> DanhSachPhien(
        [FromHeader(Name = "X-Refresh-Token")] string refreshTokenTieuDe,
        [FromQuery] string refreshToken)
    {
        // Refresh token của phiên hiện tại (để đánh dấu "thiết bị này") nhận qua TIÊU ĐỀ. Trước đây nó nằm
        // trên chuỗi truy vấn nên bị ghi nguyên văn vào nhật ký của cả Web lẫn API. Tham số truy vấn chỉ
        // còn giữ để không làm hỏng ứng dụng khách cũ.
        var danhSach = await _xacThuc.LayPhienAsync(MaNguoiDungHienTai,
            string.IsNullOrEmpty(refreshTokenTieuDe) ? refreshToken : refreshTokenTieuDe);
        return Ok(danhSach);
    }

    /// <summary>Kích hoạt tài khoản qua mã gửi kèm email.</summary>
    [HttpGet("activate/{maKichHoat}")]
    public async Task<IActionResult> KichHoat(string maKichHoat)
    {
        var ketQua = await _xacThuc.KichHoatAsync(maKichHoat);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Đổi mật khẩu của tài khoản đang đăng nhập.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ketQua = await _xacThuc.DoiMatKhauAsync(MaNguoiDungHienTai, dto);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Gửi yêu cầu đặt lại mật khẩu.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> QuenMatKhau([FromBody] EmailRequest request)
    {
        var ketQua = await _xacThuc.QuenMatKhauAsync(request?.Email ?? string.Empty);
        return Ok(new { message = ketQua.ThongDiep });
    }

    /// <summary>Đặt lại mật khẩu bằng mã nhận được.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> DatLaiMatKhau([FromBody] DatLaiMatKhauRequest request)
    {
        var ketQua = await _xacThuc.DatLaiMatKhauAsync(request?.MaDatLai, request?.MatKhauMoi);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; }
}

public class EmailRequest
{
    public string Email { get; set; }
}

public class DatLaiMatKhauRequest
{
    public string MaDatLai { get; set; }
    public string MatKhauMoi { get; set; }
}
