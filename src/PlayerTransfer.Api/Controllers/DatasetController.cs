using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

/// <summary>
/// Nạp bộ dữ liệu Transfermarkt tải từ Kaggle. Chỉ quản trị viên được dùng vì
/// thao tác này ghi đè phần lớn dữ liệu cầu thủ.
/// </summary>
[ApiController]
[Route("api/dataset")]
[Authorize(Policy = "ChiQuanTriVien")]
public class DatasetController : ControllerBase
{
    private readonly INhapCsvService _nhap;
    private readonly IThongBaoRealtime _realtime;

    public DatasetController(INhapCsvService nhap, IThongBaoRealtime realtime)
    {
        _nhap = nhap; _realtime = realtime;
    }

    /// <summary>Kiểm tra thư mục CSV: tệp nào có, thiếu tệp nào, mỗi tệp có cột gì.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> XemTruoc([FromQuery] string thuMuc)
    {
        var ketQua = await _nhap.XemTruocAsync(thuMuc);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep, data = ketQua.DuLieu })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>
    /// Nạp dữ liệu. Thao tác chạy lâu (vài phút) nên đặt thời gian chờ dài ở phía gọi.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Nhap([FromBody] TuyChonNhapCsvDto tuyChon, CancellationToken ct)
    {
        var ketQua = await _nhap.NhapAsync(tuyChon, ct);

        if (ketQua.ThanhCong)
        {
            await _realtime.PhatChungAsync("dong-bo-xong", new
            {
                trangThai = "ThanhCong",
                themMoi = ketQua.DuLieu.TongThemMoi,
                capNhat = ketQua.DuLieu.TongCapNhat,
                loi = ketQua.DuLieu.DanhSachLoi.Count,
                thongDiep = ketQua.ThongDiep
            }, CancellationToken.None);

            return Ok(new { message = ketQua.ThongDiep, data = ketQua.DuLieu });
        }

        return BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }
}
