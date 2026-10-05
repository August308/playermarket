using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

/// <summary>
/// Điểm cuối cho 10 tính năng phân tích mở rộng: mô hình dự đoán giá, phát hiện
/// định giá sai, dự báo, mạng chuyển nhượng, cân đối câu lạc bộ, cơ cấu tuổi,
/// hỏi đáp ngôn ngữ tự nhiên, biến động theo kỳ và kiểm chứng dữ liệu.
/// </summary>
[ApiController]
[Route("api/analytics")]
// Theo đặc tả (mục 1.4), phân tích/thống kê chuyên sâu là quyền của Thành viên.
// Riêng "hỏi bằng tiếng Việt" thực chất là tra cứu nên Khách vẫn dùng được.
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IDuDoanGiaService _duDoan;
    private readonly IPhanTichCauLacBoService _phanTich;
    private readonly IHieuCauHoiService _hieuCauHoi;
    private readonly IBienDongGiaService _bienDong;
    private readonly IKiemChungDuLieuService _kiemChung;

    public AnalyticsController(IDuDoanGiaService duDoan, IPhanTichCauLacBoService phanTich,
        IHieuCauHoiService hieuCauHoi, IBienDongGiaService bienDong, IKiemChungDuLieuService kiemChung)
    {
        _duDoan = duDoan; _phanTich = phanTich; _hieuCauHoi = hieuCauHoi;
        _bienDong = bienDong; _kiemChung = kiemChung;
    }

    // ------------------------------------------------- 1. Mô hình dự đoán giá

    /// <summary>Chỉ số đánh giá của mô hình hiện tại (MAE, RMSE, MAPE, R²).</summary>
    [HttpGet("model")]
    public async Task<IActionResult> ChiSoMoHinh(CancellationToken ct)
    {
        var chiSo = await _duDoan.LayChiSoMoHinhAsync(ct);
        return chiSo == null
            ? NotFound(new { code = "CHUA_CO_MO_HINH", message = "Chưa đủ dữ liệu để huấn luyện mô hình." })
            : Ok(chiSo);
    }

    /// <summary>Huấn luyện lại mô hình (chỉ quản trị viên, vì tốn CPU).</summary>
    [Authorize(Policy = "ChiQuanTriVien")]
    [HttpPost("model/train")]
    public async Task<IActionResult> HuanLuyen(CancellationToken ct)
    {
        var ketQua = await _duDoan.HuanLuyenLaiAsync(ct);
        return ketQua.ThanhCong
            ? Ok(new { message = ketQua.ThongDiep, data = ketQua.DuLieu })
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    /// <summary>Giá trị mô hình dự đoán cho một cầu thủ.</summary>
    [HttpGet("predict/{maCauThu:int}")]
    public async Task<IActionResult> DuDoan(int maCauThu, CancellationToken ct)
    {
        var ketQua = await _duDoan.DuDoanChoCauThuAsync(maCauThu, ct);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    // ------------------------------------------------ 2. Định giá sai

    [HttpGet("mispriced")]
    public async Task<IActionResult> DinhGiaSai([FromQuery] int limit = 15, [FromQuery] decimal minValue = 1)
    {
        var ketQua = await _duDoan.TimDinhGiaSaiAsync(limit, minValue);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    // ------------------------------------------------ 3. Dự báo giá

    [HttpGet("forecast/{maCauThu:int}")]
    public async Task<IActionResult> DuBao(int maCauThu, [FromQuery] int months = 6, CancellationToken ct = default)
    {
        var ketQua = await _duDoan.DuBaoAsync(maCauThu, months, ct);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    // ------------------------------------------------ 7. Đối chiếu hai nguồn

    [HttpGet("comparison")]
    public async Task<IActionResult> DoiChieu(CancellationToken ct)
    {
        var ketQua = await _duDoan.DoiChieuNguonAsync(ct);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : BadRequest(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    // ------------------------------------------------ 4. Mạng chuyển nhượng

    [HttpGet("network")]
    public async Task<IActionResult> Mang([FromQuery] string season, [FromQuery] int limit = 60,
        CancellationToken ct = default)
        => Ok(await _phanTich.MangChuyenNhuongAsync(season, limit, ct));

    // ------------------------------------------------ 5. Cân đối câu lạc bộ

    [HttpGet("clubs/{maCLB:int}/balance")]
    public async Task<IActionResult> CanDoi(int maCLB, CancellationToken ct)
    {
        var ketQua = await _phanTich.CanDoiAsync(maCLB, ct);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    [HttpGet("clubs/balance-ranking")]
    public async Task<IActionResult> XepHangSoDu([FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _phanTich.XepHangSoDuAsync(limit, ct));

    // ------------------------------------------------ 8. Cơ cấu độ tuổi

    [HttpGet("clubs/{maCLB:int}/age-structure")]
    public async Task<IActionResult> CoCauTuoi(int maCLB, CancellationToken ct)
    {
        var ketQua = await _phanTich.CoCauTuoiAsync(maCLB, ct);
        return ketQua.ThanhCong
            ? Ok(ketQua.DuLieu)
            : NotFound(new { code = ketQua.Ma, message = ketQua.ThongDiep });
    }

    // ------------------------------------------------ 6. Hỏi đáp tự nhiên

    [AllowAnonymous]
    [HttpGet("ask")]
    public async Task<IActionResult> Hoi([FromQuery] string q, CancellationToken ct)
        => Ok(await _hieuCauHoi.HieuAsync(q, ct));

    // ------------------------------------------------ 9. Biến động theo kỳ

    [HttpGet("movers")]
    public async Task<IActionResult> BienDong([FromQuery] int days = 7, [FromQuery] int limit = 10,
        CancellationToken ct = default)
        => Ok(await _bienDong.TongHopAsync(days, limit, ct));

    // ------------------------------------------------ 10. Kiểm chứng dữ liệu

    [Authorize(Policy = "ChiQuanTriVien")]
    [HttpGet("data-quality")]
    public async Task<IActionResult> KiemChung(CancellationToken ct)
        => Ok(await _kiemChung.ChayAsync(ct));
}
