using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Cổng gọi API-Football v3.
/// Cấu hình: FootballApi:BaseUrl, FootballApi:Key, FootballApi:HanMucNgay.
/// Gói miễn phí giới hạn 100 lượt gọi/ngày nên lớp này tự dừng khi đạt 90% hạn mức.
/// </summary>
public class FootballApiClient : IFootballApiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cauHinh;
    private readonly ILogger<FootballApiClient> _log;

    private int _soLuotDaGoi;
    private int _soLuotLoi;
    private bool _hetHanMuc;
    private bool _biChanBoiGoi;
    private string _loiGanNhat;

    public FootballApiClient(HttpClient http, IConfiguration cauHinh, ILogger<FootballApiClient> log)
    {
        _http = http;
        _cauHinh = cauHinh;
        _log = log;

        var baseUrl = _cauHinh["FootballApi:BaseUrl"] ?? "https://v3.football.api-sports.io/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(30);

        var khoa = _cauHinh["FootballApi:Key"];
        if (!string.IsNullOrWhiteSpace(khoa))
        {
            _http.DefaultRequestHeaders.Remove("x-apisports-key");
            _http.DefaultRequestHeaders.Add("x-apisports-key", khoa);
        }
    }

    public bool DaCauHinh => !string.IsNullOrWhiteSpace(_cauHinh["FootballApi:Key"]);

    public int SoLuotDaGoi => _soLuotDaGoi;

    public bool HetHanMuc => _hetHanMuc;

    public int SoLuotLoi => _soLuotLoi;

    public string LoiGanNhat => _loiGanNhat;

    public bool BiChanBoiGoiDichVu => _biChanBoiGoi;

    private void GhiLoi(string thongDiep)
    {
        _soLuotLoi++;
        _loiGanNhat = thongDiep;
    }

    private int HanMucNgay => int.TryParse(_cauHinh["FootballApi:HanMucNgay"], out var v) && v > 0 ? v : 100;

    public async Task<IReadOnlyList<JsonElement>> LayAsync(string duongDan, CancellationToken ct = default)
    {
        var rong = Array.Empty<JsonElement>();

        if (!DaCauHinh)
            throw new InvalidOperationException("Chưa cấu hình khóa API bóng đá (FootballApi:Key).");

        if (_hetHanMuc || _biChanBoiGoi) return rong;

        if (_soLuotDaGoi >= HanMucNgay * 0.9)
        {
            _hetHanMuc = true;
            _log.LogWarning("Đã dùng {DaGoi}/{HanMuc} lượt gọi API, tạm dừng để giữ hạn mức.",
                _soLuotDaGoi, HanMucNgay);
            return rong;
        }

        for (var lanThu = 1; lanThu <= 3; lanThu++)
        {
            try
            {
                _soLuotDaGoi++;
                using var phanHoi = await _http.GetAsync(duongDan, ct);

                if (phanHoi.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _hetHanMuc = true;
                    GhiLoi("API trả HTTP 429 — đã hết hạn mức gọi trong ngày.");
                    _log.LogWarning("API trả HTTP 429 — đã hết hạn mức, dừng đồng bộ.");
                    return rong;
                }

                if (phanHoi.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _hetHanMuc = true;
                    GhiLoi($"Khóa API không hợp lệ hoặc bị từ chối (HTTP {(int)phanHoi.StatusCode}).");
                    _log.LogError("Khóa API không hợp lệ hoặc bị từ chối (HTTP {Ma}).", (int)phanHoi.StatusCode);
                    throw new InvalidOperationException("Khóa API bóng đá không hợp lệ.");
                }

                if ((int)phanHoi.StatusCode >= 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(lanThu * lanThu), ct);
                    continue;
                }

                phanHoi.EnsureSuccessStatusCode();

                var noiDung = await phanHoi.Content.ReadAsStringAsync(ct);
                using var taiLieu = JsonDocument.Parse(noiDung);
                var goc = taiLieu.RootElement;

                // API-Football báo lỗi nghiệp vụ trong trường "errors" mà vẫn trả HTTP 200.
                if (goc.TryGetProperty("errors", out var loi)
                    && loi.ValueKind == JsonValueKind.Object
                    && loi.EnumerateObject().Any())
                {
                    _log.LogWarning("API báo lỗi tại {DuongDan}: {Loi}", duongDan, loi.GetRawText());

                    var cacLoi = loi.EnumerateObject().ToList();
                    GhiLoi(string.Join("; ", cacLoi.Select(x =>
                        x.Value.ValueKind == JsonValueKind.String ? x.Value.GetString() : x.Value.GetRawText())));

                    // "plan": gói dịch vụ không cho xem dữ liệu này (thường là mùa giải quá mới với gói miễn phí).
                    // "token"/"access": khóa sai hoặc tài khoản bị khóa. Cả hai trường hợp gọi tiếp đều vô ích.
                    if (cacLoi.Any(x => x.Name is "plan" or "token" or "access" or "requests"))
                        _biChanBoiGoi = true;

                    return rong;
                }

                if (!goc.TryGetProperty("response", out var duLieu) || duLieu.ValueKind != JsonValueKind.Array)
                    return rong;

                // Nhân bản để dữ liệu còn dùng được sau khi JsonDocument bị giải phóng.
                return JsonSerializer.Deserialize<List<JsonElement>>(duLieu.GetRawText()) ?? new List<JsonElement>();
            }
            catch (HttpRequestException ex) when (lanThu < 3)
            {
                _log.LogWarning(ex, "Lỗi mạng khi gọi {DuongDan} (lần {Lan}), sẽ thử lại.", duongDan, lanThu);
                await Task.Delay(TimeSpan.FromSeconds(lanThu * lanThu), ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && lanThu < 3)
            {
                _log.LogWarning("Quá thời gian chờ khi gọi {DuongDan} (lần {Lan}), sẽ thử lại.", duongDan, lanThu);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // Lần thử cuối cùng vẫn lỗi mạng: ghi nhận lỗi thay vì để ngoại lệ làm hỏng cả lượt đồng bộ.
                _log.LogError(ex, "Gọi {DuongDan} thất bại sau 3 lần thử.", duongDan);
                GhiLoi($"Không gọi được {duongDan} sau 3 lần thử (lỗi mạng hoặc máy chủ API).");
                return rong;
            }
        }

        _log.LogError("Gọi {DuongDan} thất bại sau 3 lần thử.", duongDan);
        GhiLoi($"Không gọi được {duongDan} sau 3 lần thử (lỗi mạng hoặc máy chủ API).");
        return rong;
    }
}

/// <summary>Bản giả lập dùng khi chưa cấu hình khóa API — giúp hệ thống và kiểm thử chạy được.</summary>
public class MockFootballApiClient : IFootballApiClient
{
    public bool DaCauHinh => false;
    public int SoLuotDaGoi => 0;
    public bool HetHanMuc => false;
    public int SoLuotLoi => 0;
    public string LoiGanNhat => null;
    public bool BiChanBoiGoiDichVu => false;

    public Task<IReadOnlyList<JsonElement>> LayAsync(string duongDan, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<JsonElement>>(Array.Empty<JsonElement>());
}
