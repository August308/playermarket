using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Application.Services;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>Bản giả lập dịch vụ email để kiểm thử không phụ thuộc máy chủ SMTP.</summary>
public class EmailGiaLap : IEmailService
{
    public List<(string Email, string TieuDe)> DaGui { get; } = new();

    public bool DaCauHinh => true;

    public Task<bool> GuiAsync(string email, string tieuDe, string noiDungHtml, CancellationToken ct = default)
    {
        DaGui.Add((email, tieuDe));
        return Task.FromResult(true);
    }
}

public class XacThucServiceTests
{
    private static XacThucService TaoService(PlayerTransfer.Infrastructure.Data.ApplicationDbContext db)
    {
        var cauHinh = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jwt:Key"] = "khoa-kiem-thu-rat-dai-toi-thieu-32-ky-tu-1234567890",
                ["Auth:YeuCauKichHoatEmail"] = "false"
            })
            .Build();

        return new XacThucService(db, new TokenService(cauHinh), cauHinh,
            new EmailGiaLap(), NullLogger<XacThucService>.Instance);
    }

    [Fact(DisplayName = "TC-01-01: Đăng ký hợp lệ tạo được tài khoản")]
    public async Task DangKy_HopLe()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        var ketQua = await service.DangKyAsync(new DangKyDto
        {
            HoTen = "Người dùng mới",
            Email = "moi@local",
            MatKhau = "Abcd1234",
            NhapLaiMatKhau = "Abcd1234",
            DongYDieuKhoan = true
        });

        Assert.True(ketQua.ThanhCong);
        Assert.Equal(2, db.NguoiDungs.Count());
    }

    [Fact(DisplayName = "TC-01-02: Từ chối email đã tồn tại")]
    public async Task DangKy_EmailTrung()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        var ketQua = await service.DangKyAsync(new DangKyDto
        {
            HoTen = "Trùng email",
            Email = "TEST@LOCAL",
            MatKhau = "Abcd1234",
            NhapLaiMatKhau = "Abcd1234",
            DongYDieuKhoan = true
        });

        Assert.False(ketQua.ThanhCong);
        Assert.Equal("EMAIL_TON_TAI", ketQua.Ma);
    }

    [Fact(DisplayName = "TC-01-08: Mật khẩu được băm, không lưu văn bản thuần")]
    public async Task DangKy_MatKhauDuocBam()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        await service.DangKyAsync(new DangKyDto
        {
            HoTen = "Kiểm tra băm",
            Email = "bam@local",
            MatKhau = "Abcd1234",
            NhapLaiMatKhau = "Abcd1234",
            DongYDieuKhoan = true
        });

        var nd = db.NguoiDungs.First(x => x.Email == "bam@local");
        Assert.DoesNotContain("Abcd1234", nd.MatKhauHash);
    }

    [Fact(DisplayName = "TC-02-01: Đăng nhập đúng trả về access token")]
    public async Task DangNhap_ThanhCong()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        var ketQua = await service.DangNhapAsync(new DangNhapDto
        {
            Email = "test@local",
            MatKhau = "Test@1234"
        });

        Assert.True(ketQua.ThanhCong);
        Assert.False(string.IsNullOrWhiteSpace(ketQua.DuLieu.AccessToken));
    }

    [Fact(DisplayName = "TC-02-02/03: Sai mật khẩu và sai email cho cùng một thông điệp")]
    public async Task DangNhap_SaiThongTin()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        var saiMatKhau = await service.DangNhapAsync(new DangNhapDto { Email = "test@local", MatKhau = "Sai@1234" });
        var saiEmail = await service.DangNhapAsync(new DangNhapDto { Email = "khongco@local", MatKhau = "Test@1234" });

        Assert.False(saiMatKhau.ThanhCong);
        Assert.False(saiEmail.ThanhCong);
        Assert.Equal(saiMatKhau.ThongDiep, saiEmail.ThongDiep);
    }

    [Fact(DisplayName = "TC-02-04: Khóa tạm tài khoản sau 5 lần sai")]
    public async Task DangNhap_KhoaSau5LanSai()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        for (var i = 0; i < 5; i++)
            await service.DangNhapAsync(new DangNhapDto { Email = "test@local", MatKhau = "Sai@1234" });

        var ketQua = await service.DangNhapAsync(new DangNhapDto { Email = "test@local", MatKhau = "Test@1234" });

        Assert.False(ketQua.ThanhCong);
        Assert.Equal("TAM_KHOA", ketQua.Ma);
    }

    [Fact(DisplayName = "TC-03-02: Refresh token bị thu hồi sau khi đăng xuất")]
    public async Task DangXuat_ThuHoiToken()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = TaoService(db);

        var dangNhap = await service.DangNhapAsync(new DangNhapDto { Email = "test@local", MatKhau = "Test@1234" });
        await service.DangXuatAsync(dangNhap.DuLieu.RefreshToken);

        var lamMoi = await service.LamMoiTokenAsync(dangNhap.DuLieu.RefreshToken);

        Assert.False(lamMoi.ThanhCong);
    }
}
