using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

/// <summary>E6 + E7 — xuất Excel, nhập Excel và kết xuất báo cáo PDF.</summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private const string KieuExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const long KichThuocToiDa = 8 * 1024 * 1024;

    private readonly IBaoCaoService _baoCao;

    public ReportsController(IBaoCaoService baoCao) => _baoCao = baoCao;

    private int MaNguoiDungHienTai =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>E6 — xuất danh sách cầu thủ theo bộ lọc ra tệp Excel.</summary>
    [HttpGet("players.xlsx")]
    public async Task<IActionResult> XuatCauThu([FromQuery] BoLocCauThuDto boLoc)
    {
        var duLieu = await _baoCao.XuatCauThuExcelAsync(boLoc);
        var ten = $"cau-thu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(duLieu, KieuExcel, ten);
    }

    /// <summary>E6 — tải tệp Excel mẫu dùng cho chức năng nhập.</summary>
    [HttpGet("import-template.xlsx")]
    public IActionResult TaiMauNhap()
    {
        var duLieu = _baoCao.TaoMauNhapExcel();
        return File(duLieu, KieuExcel, "mau-nhap-cau-thu.xlsx");
    }

    /// <summary>
    /// E6 — nhập cầu thủ từ tệp Excel. Đặt chayThu = true để chỉ kiểm tra lỗi
    /// mà chưa ghi vào cơ sở dữ liệu.
    /// </summary>
    [Authorize(Policy = "ChiQuanTriVien")]
    [HttpPost("import")]
    [RequestSizeLimit(KichThuocToiDa)]
    public async Task<IActionResult> NhapCauThu(IFormFile tep, [FromQuery] bool chayThu = false)
    {
        if (tep == null || tep.Length == 0)
            return BadRequest(new { code = "THIEU_TEP", message = "Chưa chọn tệp Excel." });

        if (tep.Length > KichThuocToiDa)
            return BadRequest(new { code = "TEP_QUA_LON", message = "Tệp vượt quá 8 MB." });

        if (!tep.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "SAI_DINH_DANG", message = "Chỉ chấp nhận tệp .xlsx." });

        await using var luong = tep.OpenReadStream();
        var ketQua = await _baoCao.NhapCauThuExcelAsync(luong, MaNguoiDungHienTai, chayThu);

        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep, data = ketQua.DuLieu })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>E7 — hồ sơ cầu thủ dạng PDF.</summary>
    [HttpGet("players/{maCauThu:int}.pdf")]
    public async Task<IActionResult> HoSoCauThuPdf(int maCauThu)
    {
        var duLieu = await _baoCao.XuatHoSoCauThuPdfAsync(maCauThu);
        if (duLieu == null)
            return NotFound(new { code = "KHONG_TIM_THAY", message = "Không tìm thấy cầu thủ." });

        return File(duLieu, "application/pdf", $"ho-so-cau-thu-{maCauThu}.pdf");
    }

    /// <summary>E7 — báo cáo tổng quan thị trường dạng PDF.</summary>
    [HttpGet("market.pdf")]
    public async Task<IActionResult> BaoCaoThiTruongPdf([FromQuery] string muaGiai, [FromQuery] int? maGiaiDau)
    {
        var duLieu = await _baoCao.XuatBaoCaoThiTruongPdfAsync(muaGiai, maGiaiDau);
        return File(duLieu, "application/pdf", $"bao-cao-thi-truong-{DateTime.Now:yyyyMMdd}.pdf");
    }
}
