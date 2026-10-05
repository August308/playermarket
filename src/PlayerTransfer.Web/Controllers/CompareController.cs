using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class CompareController : Controller
{
    private const string KhoaGio = "GioSoSanh";
    private const int SoToiDa = 4;

    private readonly ApiClient _api;

    public CompareController(ApiClient api) => _api = api;

    private List<int> LayGio()
    {
        var chuoi = HttpContext.Session.GetString(KhoaGio);
        if (string.IsNullOrWhiteSpace(chuoi)) return new List<int>();

        return chuoi.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var ma) ? ma : 0)
            .Where(x => x > 0).ToList();
    }

    private void LuuGio(List<int> gio) =>
        HttpContext.Session.SetString(KhoaGio, string.Join(",", gio));

    /// <summary>Đúng khi yêu cầu do HTMX gửi — khi đó trả phản hồi gọn thay vì tải lại cả trang.</summary>
    private bool LaHtmx => Request.Headers.ContainsKey("HX-Request");

    /// <summary>Gửi sự kiện cho trình duyệt: hiện thông báo trượt và cập nhật số trong giỏ.</summary>
    private void GuiSuKien(string noiDung, string loai, int soTrongGio)
    {
        var suKien = new
        {
            pmToast = new { noiDung, loai },
            pmGioSoSanh = new { so = soTrongGio }
        };

        Response.Headers["HX-Trigger"] = JsonSerializer.Serialize(suKien);
    }

    [HttpPost]
    public IActionResult Them(int maCauThu, string quayLai)
    {
        var gio = LayGio();
        string thongBao;
        string loai;

        if (gio.Contains(maCauThu))
        {
            thongBao = "Cầu thủ đã có trong giỏ so sánh.";
            loai = "";
        }
        else if (gio.Count >= SoToiDa)
        {
            thongBao = $"Chỉ so sánh được tối đa {SoToiDa} cầu thủ.";
            loai = "loi";
        }
        else
        {
            gio.Add(maCauThu);
            LuuGio(gio);
            thongBao = "Đã thêm vào giỏ so sánh.";
            loai = "ok";
        }

        if (LaHtmx)
        {
            GuiSuKien(thongBao, loai, gio.Count);
            return NoContent();
        }

        TempData[loai == "loi" ? "LoiThongBao" : "ThongBao"] = thongBao;
        return Redirect(string.IsNullOrEmpty(quayLai) ? Url.Action("Index", "Players") : quayLai);
    }

    [HttpPost]
    public async Task<IActionResult> Bo(int maCauThu)
    {
        var gio = LayGio();
        gio.Remove(maCauThu);
        LuuGio(gio);

        if (LaHtmx)
        {
            GuiSuKien("Đã bỏ khỏi giỏ so sánh.", "ok", gio.Count);
            return await Gio();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult XoaHet()
    {
        HttpContext.Session.Remove(KhoaGio);
        return RedirectToAction("Index", "Players");
    }

    /// <summary>B3 — nội dung giỏ so sánh, hiển thị trong ngăn kéo trượt.</summary>
    public async Task<IActionResult> Gio()
    {
        var danhSach = new List<CauThuTomTatDto>();

        foreach (var ma in LayGio())
        {
            var (ok, _, ct) = await _api.GetAsync<CauThuChiTietDto>($"api/players/{ma}");
            if (ok && ct != null) danhSach.Add(ct);
        }

        return PartialView("_NoiDungGio", danhSach);
    }

    public async Task<IActionResult> Index()
    {
        var gio = LayGio();
        ViewBag.SoLuong = gio.Count;

        if (gio.Count < 2)
        {
            ViewBag.ThongBao = "Hãy chọn tối thiểu 2 cầu thủ để so sánh.";
            return View(new BangSoSanhDto());
        }

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction("DangNhap", "Account", new { returnUrl = Url.Action("Index", "Compare") });

        var (ok, thongDiep, bang) = await _api.GetAsync<BangSoSanhDto>(
            "api/players/compare?ids=" + string.Join(",", gio));

        if (!ok || bang == null)
        {
            ViewBag.ThongBao = thongDiep ?? "Không lấy được dữ liệu so sánh.";
            return View(new BangSoSanhDto());
        }

        return View(bang);
    }

    /// <summary>Xuất bảng so sánh ra tệp CSV.</summary>
    public async Task<IActionResult> XuatCsv()
    {
        var gio = LayGio();
        if (gio.Count < 2) return RedirectToAction(nameof(Index));

        var (ok, _, bang) = await _api.GetAsync<BangSoSanhDto>(
            "api/players/compare?ids=" + string.Join(",", gio));
        if (!ok || bang == null) return RedirectToAction(nameof(Index));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Chỉ tiêu," + string.Join(",", bang.CauThus.Select(x => $"\"{x.HoTen}\"")));
        foreach (var hang in bang.CacHang)
            sb.AppendLine($"\"{hang.Nhan}\"," + string.Join(",", hang.GiaTriHienThi.Select(x => $"\"{x}\"")));

        var noiDung = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

        return File(noiDung, "text/csv", $"so-sanh-cau-thu-{PlayerTransfer.Application.Common.DinhDangSo.GioVN(DateTime.UtcNow):yyyyMMdd-HHmm}.csv");
    }
}
