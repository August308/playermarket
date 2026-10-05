using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Web.Services;

/// <summary>
/// Lớp gọi sang PlayerTransfer.Api. Toàn bộ giao diện MVC lấy dữ liệu qua lớp này,
/// đúng mô hình tách Back-end (mục 5.1 WBS) và Front-end (mục 5.2 WBS).
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly BoLamMoiToken _boLamMoi;
    private readonly GhiNhoDangNhap _ghiNho;

    private static readonly JsonSerializerOptions TuyChon = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(HttpClient http, IHttpContextAccessor context, BoLamMoiToken boLamMoi, GhiNhoDangNhap ghiNho)
    {
        _http = http;
        _context = context;
        _boLamMoi = boLamMoi;
        _ghiNho = ghiNho;
    }

    /// <summary>
    /// Gắn access token lên TỪNG yêu cầu thay vì sửa DefaultRequestHeaders dùng chung của HttpClient
    /// (sửa header dùng chung không an toàn khi có nhiều yêu cầu chạy song song).
    /// </summary>
    /// <summary>
    /// Mọi yêu cầu tới API đều đi ra từ máy chủ Web, nên với API "người gọi" luôn là một địa chỉ IP
    /// duy nhất: giới hạn tần suất áp chung cho TẤT CẢ người dùng (10 lần đăng nhập/phút cho cả hệ
    /// thống) và danh sách thiết bị đăng nhập không có IP/trình duyệt thật. Chuyển tiếp IP và
    /// User-Agent của người dùng để API phân biệt từng người.
    /// </summary>
    private void ChuyenTiepNguoiDung(HttpRequestMessage yeuCau)
    {
        var http = _context.HttpContext;
        if (http == null) return;

        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip))
            yeuCau.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);

        var trinhDuyet = http.Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrEmpty(trinhDuyet))
            yeuCau.Headers.TryAddWithoutValidation("User-Agent", trinhDuyet);
    }

    private void GanToken(HttpRequestMessage yeuCau)
    {
        ChuyenTiepNguoiDung(yeuCau);
        var token = _context.HttpContext?.Session.GetString("AccessToken");
        yeuCau.Headers.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<T> DocAsync<T>(HttpResponseMessage phanHoi)
    {
        if (!phanHoi.IsSuccessStatusCode) return default;
        var noiDung = await phanHoi.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(noiDung)) return default;
        return JsonSerializer.Deserialize<T>(noiDung, TuyChon);
    }

    /// <summary>Đúng khi lần gọi gần nhất không kết nối được tới máy chủ API.</summary>
    public bool ApiKhongKetNoiDuoc { get; private set; }

    public Task<(bool ThanhCong, string ThongDiep, T DuLieu)> GuiAsync<T>(
        HttpMethod phuongThuc, string duongDan, object noiDung = null)
        => GuiAsync<T>(phuongThuc, duongDan, noiDung, choPhepLamMoi: true);

    private async Task<(bool ThanhCong, string ThongDiep, T DuLieu)> GuiAsync<T>(
        HttpMethod phuongThuc, string duongDan, object noiDung, bool choPhepLamMoi,
        IReadOnlyDictionary<string, string> tieuDe = null)
    {
        using var yeuCau = new HttpRequestMessage(phuongThuc, duongDan);
        GanToken(yeuCau);
        if (tieuDe != null)
            foreach (var (ten, giaTri) in tieuDe)
                yeuCau.Headers.TryAddWithoutValidation(ten, giaTri);
        if (noiDung != null) yeuCau.Content = JsonContent.Create(noiDung);

        HttpResponseMessage phanHoi;
        try
        {
            phanHoi = await _http.SendAsync(yeuCau);
        }
        catch (HttpRequestException)
        {
            // Máy chủ API chưa chạy hoặc sai cổng — không để lỗi này làm sập cả trang web.
            ApiKhongKetNoiDuoc = true;
            return (false, "Không kết nối được tới máy chủ API. Hãy kiểm tra dự án PlayerTransfer.Api đã chạy chưa.", default);
        }
        catch (TaskCanceledException)
        {
            ApiKhongKetNoiDuoc = true;
            return (false, "Máy chủ API phản hồi quá chậm, vui lòng thử lại.", default);
        }

        using (phanHoi)
        {
            if (phanHoi.IsSuccessStatusCode)
                return (true, null, await DocAsync<T>(phanHoi));

            // Access token hết hạn: thử làm mới một lần rồi gửi lại đúng yêu cầu ban đầu.
            if (phanHoi.StatusCode == HttpStatusCode.Unauthorized && choPhepLamMoi && await LamMoiTokenAsync())
                return await GuiAsync<T>(phuongThuc, duongDan, noiDung, choPhepLamMoi: false, tieuDe);

            var chuoi = await phanHoi.Content.ReadAsStringAsync();
            var thongDiep = "Đã xảy ra lỗi, vui lòng thử lại.";

            try
            {
                using var taiLieu = JsonDocument.Parse(chuoi);
                if (taiLieu.RootElement.TryGetProperty("message", out var m))
                    thongDiep = m.GetString();
            }
            catch
            {
                // Giữ thông điệp mặc định nếu phản hồi không phải JSON.
            }

            if (phanHoi.StatusCode == HttpStatusCode.Unauthorized && duongDan.StartsWith("api/auth/login"))
                thongDiep ??= "Email hoặc mật khẩu không đúng.";
            else if (phanHoi.StatusCode == HttpStatusCode.Unauthorized)
                thongDiep = "Bạn cần đăng nhập để sử dụng chức năng này.";
            else if (phanHoi.StatusCode == HttpStatusCode.TooManyRequests)
                thongDiep = "Bạn thao tác quá nhanh, vui lòng đợi khoảng một phút rồi thử lại.";

            return (false, thongDiep, default);
        }
    }

    /// <summary>
    /// Lấy access token mới từ refresh token đang lưu trong phiên.
    /// Nhờ vậy người dùng không bị đăng xuất đột ngột sau 60 phút.
    /// </summary>
    private async Task<bool> LamMoiTokenAsync()
    {
        var http = _context.HttpContext;
        var phien = http?.Session;
        var refreshToken = phien?.GetString("RefreshToken");
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        var token = await _boLamMoi.LamMoiAsync(refreshToken, GoiLamMoiAsync);

        if (token == null)
        {
            // Refresh token cũng hết hạn: đưa giao diện về hẳn trạng thái khách
            // (xóa cả tên, vai trò và cookie ghi nhớ, không chỉ riêng token).
            GhiNhoDangNhap.XoaKhoiPhien(phien);
            if (_ghiNho.DangBat(http)) _ghiNho.Xoa(http);
            http.Items[PhienHetHanFilter.Khoa] = true;
            return false;
        }

        GhiNhoDangNhap.GhiVaoPhien(phien, token);

        // Refresh token vừa được xoay vòng: cookie ghi nhớ phải giữ token MỚI, nếu không
        // lần sau dùng lại token cũ sẽ bị API coi là token bị lộ.
        if (_ghiNho.DangBat(http)) _ghiNho.Luu(http, token.RefreshToken);

        return true;
    }

    /// <summary>
    /// Đăng nhập lại ngầm bằng cookie "Ghi nhớ đăng nhập" khi phiên đang trống
    /// (máy chủ khởi động lại, hoặc người dùng mở lại trình duyệt).
    /// </summary>
    public async Task KhoiPhucTuGhiNhoAsync()
    {
        var http = _context.HttpContext;
        if (http == null || !_ghiNho.DangBat(http)) return;
        if (!string.IsNullOrEmpty(http.Session.GetString("AccessToken"))) return;

        var refreshToken = _ghiNho.Doc(http);
        var token = string.IsNullOrEmpty(refreshToken)
            ? null
            : await _boLamMoi.LamMoiAsync(refreshToken, GoiLamMoiAsync);

        if (token == null)
        {
            _ghiNho.Xoa(http);
            return;
        }

        GhiNhoDangNhap.GhiVaoPhien(http.Session, token);
        _ghiNho.Luu(http, token.RefreshToken);
    }

    /// <summary>Gọi thẳng API làm mới token; trả null nếu bị từ chối hoặc không kết nối được.</summary>
    private async Task<TokenDto> GoiLamMoiAsync(string refreshToken)
    {
        try
        {
            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = JsonContent.Create(new { RefreshToken = refreshToken })
            };
            ChuyenTiepNguoiDung(yeuCau);
            using var phanHoi = await _http.SendAsync(yeuCau);
            if (!phanHoi.IsSuccessStatusCode) return null;

            var noiDung = await phanHoi.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<TokenDto>(noiDung, TuyChon);
            return token == null || string.IsNullOrWhiteSpace(token.AccessToken) ? null : token;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    public Task<(bool, string, T)> GetAsync<T>(string duongDan) => GuiAsync<T>(HttpMethod.Get, duongDan);

    /// <summary>
    /// GET kèm tiêu đề riêng. Dùng cho dữ liệu nhạy cảm (refresh token) — đặt vào tiêu đề thay vì
    /// chuỗi truy vấn để không bị ghi vào nhật ký truy cập.
    /// </summary>
    public Task<(bool, string, T)> GetAsync<T>(string duongDan, IReadOnlyDictionary<string, string> tieuDe)
        => GuiAsync<T>(HttpMethod.Get, duongDan, null, choPhepLamMoi: true, tieuDe);
    public Task<(bool, string, T)> PostAsync<T>(string duongDan, object noiDung = null)
        => GuiAsync<T>(HttpMethod.Post, duongDan, noiDung);
    public Task<(bool, string, T)> PutAsync<T>(string duongDan, object noiDung = null)
        => GuiAsync<T>(HttpMethod.Put, duongDan, noiDung);
    public Task<(bool, string, T)> DeleteAsync<T>(string duongDan) => GuiAsync<T>(HttpMethod.Delete, duongDan);

    /// <summary>
    /// E6 + E7 — tải một tệp nhị phân (xlsx, pdf) từ API về, giữ nguyên tên và kiểu nội dung.
    /// Web chỉ đóng vai trò trung chuyển nên trình duyệt không cần biết địa chỉ API.
    /// </summary>
    public async Task<(bool ThanhCong, string ThongDiep, byte[] DuLieu, string KieuNoiDung, string TenTep)>
        TaiTepAsync(string duongDan, bool daThuLamMoi = false)
    {
        HttpResponseMessage phanHoi;
        try
        {
            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, duongDan);
            GanToken(yeuCau);
            phanHoi = await _http.SendAsync(yeuCau, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ApiKhongKetNoiDuoc = true;
            return (false, "Không kết nối được tới máy chủ API.", null, null, null);
        }

        using (phanHoi)
        {
            if (phanHoi.StatusCode == HttpStatusCode.Unauthorized && !daThuLamMoi && await LamMoiTokenAsync())
                return await TaiTepAsync(duongDan, daThuLamMoi: true);

            if (!phanHoi.IsSuccessStatusCode)
                return (false, "Không tạo được tệp, vui lòng thử lại.", null, null, null);

            var duLieu = await phanHoi.Content.ReadAsByteArrayAsync();
            var kieu = phanHoi.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var ten = phanHoi.Content.Headers.ContentDisposition?.FileNameStar
                      ?? phanHoi.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                      ?? "tai-ve";
            return (true, null, duLieu, kieu, ten);
        }
    }

    /// <summary>E6 — gửi tệp Excel lên API để nhập dữ liệu cầu thủ.</summary>
    public async Task<(bool ThanhCong, string ThongDiep, KetQuaNhapDto DuLieu)> NhapExcelAsync(
        Stream noiDungTep, string tenTep, bool chayThu)
    {
        using var bieuMau = new MultipartFormDataContent();
        var phanTep = new StreamContent(noiDungTep);
        phanTep.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        bieuMau.Add(phanTep, "tep", tenTep);

        HttpResponseMessage phanHoi;
        try
        {
            using var yeuCau = new HttpRequestMessage(HttpMethod.Post,
                $"api/reports/import?chayThu={chayThu.ToString().ToLowerInvariant()}") { Content = bieuMau };
            GanToken(yeuCau);
            phanHoi = await _http.SendAsync(yeuCau);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ApiKhongKetNoiDuoc = true;
            return (false, "Không kết nối được tới máy chủ API.", null);
        }

        using (phanHoi)
        {
            var chuoi = await phanHoi.Content.ReadAsStringAsync();
            string thongDiep = null;
            KetQuaNhapDto duLieu = null;

            try
            {
                using var taiLieu = JsonDocument.Parse(chuoi);
                if (taiLieu.RootElement.TryGetProperty("message", out var m)) thongDiep = m.GetString();
                if (taiLieu.RootElement.TryGetProperty("data", out var d))
                    duLieu = JsonSerializer.Deserialize<KetQuaNhapDto>(d.GetRawText(), TuyChon);
            }
            catch
            {
                // Bỏ qua, dùng thông điệp mặc định bên dưới.
            }

            if (phanHoi.IsSuccessStatusCode)
                return (true, thongDiep ?? "Đã nhập xong.", duLieu);

            if (phanHoi.StatusCode == HttpStatusCode.Unauthorized || phanHoi.StatusCode == HttpStatusCode.Forbidden)
                return (false, "Chỉ quản trị viên mới được nhập dữ liệu.", null);

            return (false, thongDiep ?? "Nhập dữ liệu thất bại.", null);
        }
    }

    // ----- Các lời gọi thường dùng -----

    public async Task<TrangDuLieu<CauThuTomTatDto>> LocCauThuAsync(BoLocCauThuDto boLoc)
    {
        var (ok, _, duLieu) = await PostAsync<TrangDuLieu<CauThuTomTatDto>>("api/players/filter", boLoc);
        return ok && duLieu != null ? duLieu : new TrangDuLieu<CauThuTomTatDto>();
    }

    public async Task<List<CauThuTomTatDto>> BangXepHangAsync(string loai, int soLuong)
    {
        var (ok, _, duLieu) = await GetAsync<List<CauThuTomTatDto>>(
            $"api/statistics/ranking?type={loai}&limit={soLuong}");
        return ok && duLieu != null ? duLieu : new List<CauThuTomTatDto>();
    }

    public async Task<List<DanhMucDto>> DanhMucAsync(string ten, string thamSo = null)
    {
        var (ok, _, duLieu) = await GetAsync<List<DanhMucDto>>(
            $"api/lookup/{ten}{(string.IsNullOrEmpty(thamSo) ? "" : "?" + thamSo)}");
        return ok && duLieu != null ? duLieu : new List<DanhMucDto>();
    }
}
