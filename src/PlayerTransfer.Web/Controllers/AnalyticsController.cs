using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

/// <summary>Giao diện cho 10 tính năng phân tích mở rộng.</summary>
public class AnalyticsController : Controller
{
    private readonly ApiClient _api;

    public AnalyticsController(ApiClient api) => _api = api;

    private bool LaQuanTri => HttpContext.Session.GetString("VaiTro") == "QuanTriVien";

    /// <summary>
    /// Mọi trang phân tích (trừ "Hỏi bằng tiếng Việt") dành cho Thành viên theo đặc tả. Chuyển Khách
    /// sang đăng nhập NGAY, kèm đường quay lại, thay vì để trang hiện ra rỗng với lỗi 401 từ API.
    /// </summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        var hanhDong = context.RouteData.Values["action"]?.ToString();
        var daDangNhap = !string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken"));

        if (!daDangNhap && hanhDong != nameof(Hoi))
        {
            context.Result = RedirectToAction("DangNhap", "Account",
                new { returnUrl = Request.Path + Request.QueryString });
            return;
        }

        base.OnActionExecuting(context);
    }

    // ---------------------------------- 1 + 2: mô hình và cầu thủ bị định giá sai

    [HttpGet]
    public async Task<IActionResult> Index(decimal giaToiThieu = 1, int soLuong = 15)
    {
        var (ok, thongDiep, duLieu) = await _api.GetAsync<DinhGiaSaiDto>(
            $"api/analytics/mispriced?limit={soLuong}&minValue={giaToiThieu.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        if (!ok) ViewBag.Loi = thongDiep;

        return View(new DinhGiaVm
        {
            GiaToiThieu = giaToiThieu,
            SoLuong = soLuong,
            KetQua = duLieu
        });
    }

    /// <summary>Huấn luyện lại mô hình — chỉ quản trị viên.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HuanLuyen()
    {
        if (!LaQuanTri) return RedirectToAction(nameof(Index));

        var (ok, thongDiep, _) = await _api.PostAsync<object>("api/analytics/model/train");
        TempData[ok ? "ThongBao" : "LoiThongBao"] = thongDiep ?? "Đã huấn luyện lại mô hình.";

        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------- 7: đối chiếu giá thật và mô hình

    [HttpGet]
    public async Task<IActionResult> DoiChieu()
    {
        var (ok, thongDiep, duLieu) = await _api.GetAsync<DoiChieuNguonDto>("api/analytics/comparison");
        if (!ok) ViewBag.Loi = thongDiep;

        return View(duLieu ?? new DoiChieuNguonDto());
    }

    // ------------------------------------------------------------- 3: dự báo giá

    [HttpGet]
    public async Task<IActionResult> DuBao(int id, int soThang = 6)
    {
        if (id <= 0) return RedirectToAction("Index", "Players");

        var (ok, thongDiep, duLieu) = await _api.GetAsync<DuBaoGiaDto>(
            $"api/analytics/forecast/{id}?months={soThang}");

        if (!ok) ViewBag.Loi = thongDiep;
        ViewBag.SoThang = soThang;

        return View(duLieu ?? new DuBaoGiaDto { MaCauThu = id });
    }

    // ----------------------------------------------------- 4: mạng chuyển nhượng

    [HttpGet]
    public async Task<IActionResult> Mang(string muaGiai, int soCung = 60)
    {
        var muaGiais = await _api.GetAsync<List<string>>("api/statistics/seasons");
        ViewBag.MuaGiais = muaGiais.Item3 ?? new List<string>();

        var (ok, thongDiep, duLieu) = await _api.GetAsync<MangChuyenNhuongDto>(
            $"api/analytics/network?season={Uri.EscapeDataString(muaGiai ?? string.Empty)}&limit={soCung}");

        if (!ok) ViewBag.Loi = thongDiep;
        ViewBag.SoCung = soCung;

        return View(duLieu ?? new MangChuyenNhuongDto { MuaGiai = muaGiai });
    }

    // ------------------------------------------------- 5: cân đối câu lạc bộ

    [HttpGet]
    public async Task<IActionResult> CauLacBo(int id)
    {
        if (id <= 0)
        {
            var (okXh, _, xepHang) = await _api.GetAsync<List<DiemThongKeDto>>(
                "api/analytics/clubs/balance-ranking?limit=30");

            if (!okXh) ViewBag.Loi = "Không lấy được bảng xếp hạng số dư.";
            return View("DanhSachCauLacBo", xepHang ?? new List<DiemThongKeDto>());
        }

        var (ok, thongDiep, duLieu) = await _api.GetAsync<CanDoiCauLacBoDto>(
            $"api/analytics/clubs/{id}/balance");

        if (!ok)
        {
            ViewBag.Loi = thongDiep;
            return View("DanhSachCauLacBo", new List<DiemThongKeDto>());
        }

        return View(duLieu);
    }

    // ------------------------------------------------------ 8: cơ cấu độ tuổi

    [HttpGet]
    public async Task<IActionResult> CoCauTuoi(int id)
    {
        if (id <= 0) return RedirectToAction(nameof(CauLacBo));

        var (ok, thongDiep, duLieu) = await _api.GetAsync<CoCauTuoiDto>(
            $"api/analytics/clubs/{id}/age-structure");

        if (!ok) ViewBag.Loi = thongDiep;

        return View(duLieu ?? new CoCauTuoiDto { MaCLB = id });
    }

    // ------------------------------------------- 6: hỏi đáp bằng ngôn ngữ tự nhiên

    [HttpGet]
    public async Task<IActionResult> Hoi(string q)
    {
        var vm = new HoiDapVm { CauHoi = q };

        if (string.IsNullOrWhiteSpace(q)) return View(vm);

        var (ok, thongDiep, hieu) = await _api.GetAsync<KetQuaHieuCauHoiDto>(
            $"api/analytics/ask?q={Uri.EscapeDataString(q)}");

        if (!ok || hieu == null)
        {
            ViewBag.Loi = thongDiep ?? "Không phân tích được câu hỏi.";
            return View(vm);
        }

        vm.KetQuaHieu = hieu;

        // Áp bộ lọc vừa hiểu được vào đúng dịch vụ tìm kiếm sẵn có.
        hieu.BoLoc.KichThuocTrang = 24;
        vm.KetQuaTim = await _api.LocCauThuAsync(hieu.BoLoc);

        return View(vm);
    }

    // ------------------------------------------------------- 9: biến động theo kỳ

    [HttpGet]
    public async Task<IActionResult> BienDong(int soNgay = 7, int soLuong = 10)
    {
        var (ok, thongDiep, duLieu) = await _api.GetAsync<TongHopBienDongDto>(
            $"api/analytics/movers?days={soNgay}&limit={soLuong}");

        if (!ok) ViewBag.Loi = thongDiep;

        return View(duLieu ?? new TongHopBienDongDto { SoNgay = soNgay });
    }

    // --------------------------------------------------- 10: kiểm chứng dữ liệu

    [HttpGet]
    public async Task<IActionResult> KiemChung()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction("DangNhap", "Account");
        if (!LaQuanTri) return RedirectToAction("Index", "Home");

        var (ok, thongDiep, duLieu) = await _api.GetAsync<BaoCaoKiemChungDto>("api/analytics/data-quality");
        if (!ok) ViewBag.Loi = thongDiep;

        return View(duLieu ?? new BaoCaoKiemChungDto());
    }
}
