using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Gửi thư qua máy chủ SMTP cấu hình trong Email:*.
/// Khi chưa cấu hình, hệ thống ghi nội dung thư ra nhật ký để vẫn chạy được ở môi trường phát triển.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _cauHinh;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration cauHinh, ILogger<EmailService> log)
    {
        _cauHinh = cauHinh; _log = log;
    }

    public bool DaCauHinh =>
        !string.IsNullOrWhiteSpace(_cauHinh["Email:Host"]) &&
        !string.IsNullOrWhiteSpace(_cauHinh["Email:NguoiGui"]);

    public async Task<bool> GuiAsync(string email, string tieuDe, string noiDungHtml, CancellationToken ct = default)
    {
        if (!DaCauHinh)
        {
            _log.LogInformation("Chưa cấu hình SMTP. Nội dung thư gửi tới {Email} — {TieuDe}: {NoiDung}",
                email, tieuDe, noiDungHtml);
            return false;
        }

        try
        {
            var cong = int.TryParse(_cauHinh["Email:Port"], out var p) ? p : 587;
            var batSsl = !bool.TryParse(_cauHinh["Email:BatSsl"], out var ssl) || ssl;

            using var mayChu = new SmtpClient(_cauHinh["Email:Host"], cong)
            {
                EnableSsl = batSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(
                    _cauHinh["Email:TaiKhoan"] ?? _cauHinh["Email:NguoiGui"],
                    _cauHinh["Email:MatKhau"])
            };

            using var thu = new MailMessage
            {
                From = new MailAddress(_cauHinh["Email:NguoiGui"], _cauHinh["Email:TenNguoiGui"] ?? "PlayerMarket"),
                Subject = tieuDe,
                Body = noiDungHtml,
                IsBodyHtml = true
            };
            thu.To.Add(email);

            await mayChu.SendMailAsync(thu, ct);
            _log.LogInformation("Đã gửi thư tới {Email}: {TieuDe}", email, tieuDe);
            return true;
        }
        catch (Exception ex)
        {
            // Không để lỗi gửi thư làm hỏng luồng đăng ký hoặc quên mật khẩu.
            _log.LogError(ex, "Gửi thư tới {Email} thất bại.", email);
            return false;
        }
    }
}
