using Microsoft.AspNetCore.SignalR;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Hubs;

/// <summary>
/// D1 — điểm kết nối SignalR. Khách vãng lai cũng kết nối được để nhận thông báo
/// chung (đồng bộ xong, có thương vụ mới); người đã đăng nhập được xếp thêm vào
/// một nhóm riêng theo mã tài khoản để nhận cảnh báo giá của riêng mình.
/// </summary>
public class ThongBaoHub : Hub
{
    public const string DuongDan = "/hubs/thongbao";

    public static string TenNhomNguoiDung(int maNguoiDung) => $"nguoidung-{maNguoiDung}";

    public override async Task OnConnectedAsync()
    {
        var ma = LayMaNguoiDung();
        if (ma > 0)
            await Groups.AddToGroupAsync(Context.ConnectionId, TenNhomNguoiDung(ma));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var ma = LayMaNguoiDung();
        if (ma > 0)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TenNhomNguoiDung(ma));

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Cho phép trình duyệt theo dõi riêng một cầu thủ đang mở.</summary>
    public Task TheoDoiCauThu(int maCauThu) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"cauthu-{maCauThu}");

    public Task BoTheoDoiCauThu(int maCauThu) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"cauthu-{maCauThu}");

    private int LayMaNguoiDung() =>
        int.TryParse(Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var ma) ? ma : 0;
}

/// <summary>D1 — nối kênh nghiệp vụ IThongBaoRealtime vào SignalR.</summary>
public class ThongBaoSignalR : IThongBaoRealtime
{
    private readonly IHubContext<ThongBaoHub> _hub;

    public ThongBaoSignalR(IHubContext<ThongBaoHub> hub) => _hub = hub;

    public Task PhatChungAsync(string loai, object duLieu, CancellationToken ct = default) =>
        _hub.Clients.All.SendAsync("ThongBao", loai, duLieu, ct);

    public Task GuiNguoiDungAsync(int maNguoiDung, string loai, object duLieu, CancellationToken ct = default) =>
        _hub.Clients.Group(ThongBaoHub.TenNhomNguoiDung(maNguoiDung))
            .SendAsync("ThongBao", loai, duLieu, ct);
}
