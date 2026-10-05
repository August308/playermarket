using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// D1 — bản cài đặt rỗng của kênh thời gian thực. Dùng khi tầng trình bày chưa
/// bật SignalR (unit test, tiến trình nền chạy riêng), để dịch vụ nghiệp vụ
/// không phải kiểm tra null ở mọi chỗ gọi.
/// </summary>
public class ThongBaoRong : IThongBaoRealtime
{
    public Task PhatChungAsync(string loai, object duLieu, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task GuiNguoiDungAsync(int maNguoiDung, string loai, object duLieu, CancellationToken ct = default)
        => Task.CompletedTask;
}
