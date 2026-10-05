using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

/// <summary>Màn hình nạp bộ dữ liệu Transfermarkt (Kaggle) — chỉ quản trị viên.</summary>
public class DatasetController : Controller
{
    private static readonly JsonSerializerOptions TuyChonJson = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiClient _api;
    private readonly IHttpClientFactory _xuongClient;

    public DatasetController(ApiClient api, IHttpClientFactory xuongClient)
    {
        _api = api; _xuongClient = xuongClient;
    }

    private bool LaQuanTri => HttpContext.Session.GetString("VaiTro") == "QuanTriVien";

    private IActionResult ChanNeuKhongPhaiQuanTri()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
            return RedirectToAction("DangNhap", "Account");
        return LaQuanTri ? null : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Index()
    {
        var chan = ChanNeuKhongPhaiQuanTri();
        if (chan != null) return chan;

        return View(new NhapDatasetVm());
    }

    /// <summary>Bước 1 — kiểm tra thư mục trước khi nạp (nhanh, dùng client chung).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XemTruoc(NhapDatasetVm vm)
    {
        var chan = ChanNeuKhongPhaiQuanTri();
        if (chan != null) return chan;

        vm ??= new NhapDatasetVm();

        if (string.IsNullOrWhiteSpace(vm.TuyChon?.ThuMuc))
        {
            ViewBag.Loi = "Hãy nhập đường dẫn thư mục chứa các tệp .csv.";
            return View(nameof(Index), vm);
        }

        var (ok, thongDiep, boc) = await _api.GetAsync<BocDuLieu<List<XemTruocTepDto>>>(
            $"api/dataset/preview?thuMuc={Uri.EscapeDataString(vm.TuyChon.ThuMuc)}");

        if (ok && boc != null)
        {
            vm.CacTep = boc.Data ?? new List<XemTruocTepDto>();
            ViewBag.ThongBao = boc.Message;
        }
        else
        {
            ViewBag.Loi = thongDiep ?? "Không kiểm tra được thư mục.";
        }

        return View(nameof(Index), vm);
    }

    /// <summary>
    /// Bước 2 — nạp thật. Dùng client "ApiDaiHan" vì thao tác có thể chạy nhiều phút,
    /// vượt quá thời gian chờ 30 giây của client dùng chung.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nhap(NhapDatasetVm vm, CancellationToken ct)
    {
        var chan = ChanNeuKhongPhaiQuanTri();
        if (chan != null) return chan;

        vm ??= new NhapDatasetVm();

        if (string.IsNullOrWhiteSpace(vm.TuyChon?.ThuMuc))
        {
            ViewBag.Loi = "Hãy nhập đường dẫn thư mục chứa các tệp .csv.";
            return View(nameof(Index), vm);
        }

        var client = _xuongClient.CreateClient("ApiDaiHan");
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var phanHoi = await client.PostAsJsonAsync("api/dataset/import", vm.TuyChon, ct);
            var chuoi = await phanHoi.Content.ReadAsStringAsync(ct);

            if (phanHoi.IsSuccessStatusCode)
            {
                var boc = JsonSerializer.Deserialize<BocDuLieu<KetQuaNhapCsvDto>>(chuoi, TuyChonJson);
                vm.KetQua = boc?.Data;
                ViewBag.ThongBao = boc?.Message ?? "Đã nạp xong.";
            }
            else
            {
                ViewBag.Loi = DocThongDiep(chuoi) ?? $"Nạp thất bại (mã {(int)phanHoi.StatusCode}).";
            }
        }
        catch (TaskCanceledException)
        {
            ViewBag.Loi = "Quá trình nạp vượt quá thời gian chờ. Hãy thử bỏ chọn " +
                          "«Gộp thống kê thi đấu» hoặc đặt mức giá tối thiểu để giảm khối lượng.";
        }
        catch (HttpRequestException)
        {
            ViewBag.Loi = "Không kết nối được tới máy chủ API. Hãy kiểm tra dự án PlayerTransfer.Api đã chạy chưa.";
        }

        return View(nameof(Index), vm);
    }

    private static string DocThongDiep(string chuoiJson)
    {
        try
        {
            using var taiLieu = JsonDocument.Parse(chuoiJson);
            return taiLieu.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Khớp với phản hồi dạng { message, data } của API.</summary>
public class BocDuLieu<T>
{
    public string Message { get; set; }
    public T Data { get; set; }
}
