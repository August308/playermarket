using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>
/// E11 — kiểm thử tích hợp: gọi thật qua tầng HTTP của API (định tuyến, xác thực,
/// phân quyền, tuần tự hóa JSON, truy vấn cơ sở dữ liệu) chứ không gọi trực tiếp service.
/// </summary>
public class ApiTichHopTests : IClassFixture<MayChuKiemThu>
{
    private static readonly JsonSerializerOptions TuyChon = new() { PropertyNameCaseInsensitive = true };

    private readonly MayChuKiemThu _mayChu;

    public ApiTichHopTests(MayChuKiemThu mayChu) => _mayChu = mayChu;

    private HttpClient TaoClient() => _mayChu.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    private async Task<TokenDto> DangNhapAsync(HttpClient client, string email, string matKhau)
    {
        var phanHoi = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            MatKhau = matKhau,
            GhiNhoDangNhap = false
        });

        phanHoi.EnsureSuccessStatusCode();
        var chuoi = await phanHoi.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenDto>(chuoi, TuyChon);
    }

    [Fact]
    public async Task DanhMuc_ViTri_TraVeDanhSachKhongRong()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/lookup/positions");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        var danhSach = JsonSerializer.Deserialize<List<DanhMucDto>>(
            await phanHoi.Content.ReadAsStringAsync(), TuyChon);
        Assert.NotNull(danhSach);
        Assert.NotEmpty(danhSach);
    }

    [Fact]
    public async Task TimKiem_CauThuCoThat_TraVeKetQua()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/players/search?keyword=haaland&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        var trang = JsonSerializer.Deserialize<TrangDuLieu<CauThuTomTatDto>>(
            await phanHoi.Content.ReadAsStringAsync(), TuyChon);
        Assert.NotNull(trang);
        Assert.Contains(trang.DanhSach, x => x.HoTen.Contains("Haaland", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DangNhap_SaiMatKhau_TraVe401()
    {
        var client = TaoClient();

        var phanHoi = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "admin@playertransfer.local",
            MatKhau = "sai-mat-khau",
            GhiNhoDangNhap = false
        });

        Assert.Equal(HttpStatusCode.Unauthorized, phanHoi.StatusCode);
    }

    [Fact]
    public async Task DangNhap_DungThongTin_TraVeToken()
    {
        var client = TaoClient();

        var token = await DangNhapAsync(client, "admin@playertransfer.local", "Admin@123");

        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.Equal("QuanTriVien", token.VaiTro);
    }

    [Fact]
    public async Task TheoDoi_ChuaDangNhap_TraVe401()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/watchlist");

        Assert.Equal(HttpStatusCode.Unauthorized, phanHoi.StatusCode);
    }

    [Fact]
    public async Task QuanTri_ThanhVienThuong_BiTuChoi()
    {
        var client = TaoClient();
        var token = await DangNhapAsync(client, "member@playertransfer.local", "Member@123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var phanHoi = await client.GetAsync("/api/admin/sync/logs");

        Assert.Equal(HttpStatusCode.Forbidden, phanHoi.StatusCode);
    }

    [Fact]
    public async Task LamMoiToken_XoayVongVaChanDungLai()
    {
        var client = TaoClient();
        var token = await DangNhapAsync(client, "member@playertransfer.local", "Member@123");

        // Lần làm mới đầu tiên phải trả về refresh token MỚI (E4 — xoay vòng).
        var lan1 = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, lan1.StatusCode);

        var tokenMoi = JsonSerializer.Deserialize<TokenDto>(await lan1.Content.ReadAsStringAsync(), TuyChon);
        Assert.NotEqual(token.RefreshToken, tokenMoi.RefreshToken);

        // Dùng lại token cũ đã bị thu hồi thì phải bị từ chối.
        var lan2 = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, lan2.StatusCode);

        // Và cả chuỗi phiên bị thu hồi theo, nên token mới cũng hết hiệu lực.
        var lan3 = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = tokenMoi.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, lan3.StatusCode);
    }

    [Fact]
    public async Task XuatExcel_TraVeTepXlsxHopLe()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/reports/players.xlsx?KichThuocTrang=20");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            phanHoi.Content.Headers.ContentType?.MediaType);

        var duLieu = await phanHoi.Content.ReadAsByteArrayAsync();
        Assert.True(duLieu.Length > 1000);
        // Tệp .xlsx là một kho ZIP, bắt đầu bằng "PK".
        Assert.Equal(0x50, duLieu[0]);
        Assert.Equal(0x4B, duLieu[1]);
    }

    [Fact]
    public async Task XuatPdf_HoSoCauThu_TraVeTepPdfHopLe()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/reports/players/1.pdf");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        Assert.Equal("application/pdf", phanHoi.Content.Headers.ContentType?.MediaType);

        var duLieu = await phanHoi.Content.ReadAsByteArrayAsync();
        Assert.True(duLieu.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(duLieu, 0, 4));
    }

    [Fact]
    public async Task NhapExcel_ThanhVienThuong_BiTuChoi()
    {
        var client = TaoClient();
        var token = await DangNhapAsync(client, "member@playertransfer.local", "Member@123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var bieuMau = new MultipartFormDataContent();
        var noiDung = new ByteArrayContent(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        noiDung.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        bieuMau.Add(noiDung, "tep", "thu.xlsx");

        var phanHoi = await client.PostAsync("/api/reports/import?chayThu=true", bieuMau);

        Assert.Equal(HttpStatusCode.Forbidden, phanHoi.StatusCode);
    }

    [Fact]
    public async Task ThongKe_BangXepHang_TraVeDungSoLuong()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/statistics/ranking?type=giatri&limit=5");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        var danhSach = JsonSerializer.Deserialize<List<CauThuTomTatDto>>(
            await phanHoi.Content.ReadAsStringAsync(), TuyChon);
        Assert.NotNull(danhSach);
        Assert.True(danhSach.Count <= 5);
        // Bảng xếp hạng theo giá trị phải giảm dần.
        for (var i = 1; i < danhSach.Count; i++)
            Assert.True(danhSach[i - 1].GiaTriHienTai >= danhSach[i].GiaTriHienTai);
    }

    [Fact]
    public async Task TroChoi_TaoCauHoi_TraVeDuSoLuongVaCoDapAn()
    {
        var client = TaoClient();

        var phanHoi = await client.GetAsync("/api/game/questions?count=5");

        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        var chuoi = await phanHoi.Content.ReadAsStringAsync();
        using var taiLieu = JsonDocument.Parse(chuoi);
        Assert.Equal(JsonValueKind.Array, taiLieu.RootElement.ValueKind);
        Assert.True(taiLieu.RootElement.GetArrayLength() > 0);
    }
}
