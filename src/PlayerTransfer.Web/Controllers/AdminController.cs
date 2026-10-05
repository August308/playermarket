using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class AdminController : Controller
{
    private readonly ApiClient _api;

    public AdminController(ApiClient api) => _api = api;

    private bool LaQuanTriVien => HttpContext.Session.GetString("VaiTro") == "QuanTriVien";

    private IActionResult KiemTraQuyen()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            // Quay lại đúng trang đang mở (trước đây luôn về "Quản lý cầu thủ").
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Request.Path + Request.QueryString });

        if (LaQuanTriVien) return null;

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("KhongCoQuyen");
    }

    public async Task<IActionResult> CauThu(string tuKhoa, int trang = 1, int? sua = null)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (_, _, ketQua) = await _api.GetAsync<TrangDuLieu<CauThuTomTatDto>>(
            $"api/admin/players?keyword={Uri.EscapeDataString(tuKhoa ?? string.Empty)}&page={trang}&pageSize=20");

        var vm = new QuanTriCauThuVm
        {
            TuKhoa = tuKhoa,
            KetQua = ketQua ?? new TrangDuLieu<CauThuTomTatDto>(),
            // Form quản trị cần ĐỦ danh mục (kể cả CLB/quốc tịch chưa có cầu thủ nào).
            CauLacBos = await _api.DanhMucAsync("clubs", "all=true"),
            ViTris = await _api.DanhMucAsync("positions"),
            QuocGias = await _api.DanhMucAsync("countries", "all=true"),
            DangSua = new LuuCauThuDto { NgaySinh = new DateTime(2000, 1, 1), GiaTriHienTai = 1 }
        };

        // Bấm "Sửa" ở một dòng: nạp dữ liệu gốc của cầu thủ đó vào biểu mẫu phía trên.
        if (sua.HasValue)
        {
            var (okSua, _, dangSua) = await _api.GetAsync<LuuCauThuDto>($"api/admin/players/{sua.Value}");
            if (okSua && dangSua != null) vm.DangSua = dangSua;
            else TempData["LoiThongBao"] = "Không tìm thấy cầu thủ cần sửa.";
        }

        ViewBag.Trang = trang;

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> LuuCauThu(LuuCauThuDto dto)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (ok, thongDiep, _) = dto.MaCauThu.HasValue
            ? await _api.PutAsync<object>($"api/admin/players/{dto.MaCauThu}", dto)
            : await _api.PostAsync<object>("api/admin/players", dto);

        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã lưu thông tin cầu thủ." : thongDiep;
        return RedirectToAction(nameof(CauThu));
    }

    [HttpPost]
    public async Task<IActionResult> XoaCauThu(int id)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (ok, thongDiep, _) = await _api.DeleteAsync<object>($"api/admin/players/{id}");
        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã xóa cầu thủ." : thongDiep;
        return RedirectToAction(nameof(CauThu));
    }

    public async Task<IActionResult> NguoiDung(int trang = 1)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (_, _, ketQua) = await _api.GetAsync<TrangDuLieu<System.Text.Json.JsonElement>>(
            $"api/admin/users?page={trang}&pageSize=20");

        return View(ketQua ?? new TrangDuLieu<System.Text.Json.JsonElement>());
    }

    [HttpPost]
    public async Task<IActionResult> DoiTrangThai(int id, string trangThai)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (ok, thongDiep, _) = await _api.PutAsync<object>($"api/admin/users/{id}/status",
            new { TrangThai = trangThai });

        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? "Đã cập nhật trạng thái." : thongDiep;
        return RedirectToAction(nameof(NguoiDung));
    }

    public async Task<IActionResult> DongBo()
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (_, _, nhatKy) = await _api.GetAsync<List<System.Text.Json.JsonElement>>("api/admin/sync/logs?limit=30");
        return View(nhatKy ?? new List<System.Text.Json.JsonElement>());
    }

    [HttpPost]
    public async Task<IActionResult> ChayDongBo(string phamVi)
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (ok, thongDiep, duLieu) = await _api.PostAsync<System.Text.Json.JsonElement>(
            $"api/admin/sync/players?scope={Uri.EscapeDataString(phamVi ?? "Toàn bộ")}");

        // Hiện đúng kết quả API trả về (số bản ghi, hoặc lý do thất bại) thay vì một câu chung chung.
        var ketQua = ok && duLieu.ValueKind == System.Text.Json.JsonValueKind.Object
                     && duLieu.TryGetProperty("message", out var m)
            ? m.GetString()
            : null;

        TempData[ok ? "ThongBao" : "LoiThongBao"] = ok ? (ketQua ?? "Đã chạy đồng bộ.") : thongDiep;
        return RedirectToAction(nameof(DongBo));
    }

    public async Task<IActionResult> NhatKy()
    {
        var chan = KiemTraQuyen();
        if (chan != null) return chan;

        var (_, _, nhatKy) = await _api.GetAsync<List<System.Text.Json.JsonElement>>("api/admin/audit-logs?limit=50");
        return View(nhatKy ?? new List<System.Text.Json.JsonElement>());
    }
}
