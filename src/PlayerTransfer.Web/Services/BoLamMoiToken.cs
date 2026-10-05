using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Web.Services;

/// <summary>
/// Đảm bảo mỗi refresh token chỉ được đổi lấy token mới ĐÚNG MỘT LẦN, kể cả khi nhiều yêu cầu
/// cùng lúc phát hiện access token hết hạn.
///
/// Lý do: API xoay vòng refresh token và coi việc dùng lại token cũ là dấu hiệu bị đánh cắp —
/// khi đó nó thu hồi MỌI phiên của tài khoản. Trang web thường gửi vài yêu cầu song song (gợi ý
/// tìm kiếm, huy hiệu cảnh báo, nội dung giỏ so sánh...). Không gộp lại thì yêu cầu thứ hai dùng
/// lại token cũ và người dùng bị đăng xuất khỏi mọi thiết bị.
/// </summary>
public class BoLamMoiToken
{
    private readonly ConcurrentDictionary<string, Lazy<Task<TokenDto>>> _dangChay = new();
    private readonly IMemoryCache _dem;

    public BoLamMoiToken(IMemoryCache dem) => _dem = dem;

    public async Task<TokenDto> LamMoiAsync(string refreshToken, Func<string, Task<TokenDto>> goiApi)
    {
        // Token này vừa được đổi xong (bởi một yêu cầu khác): dùng luôn kết quả.
        if (_dem.TryGetValue(KhoaDem(refreshToken), out TokenDto daCo)) return daCo;

        var viec = _dangChay.GetOrAdd(refreshToken,
            t => new Lazy<Task<TokenDto>>(() => goiApi(t), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var ketQua = await viec.Value;
            if (ketQua != null)
            {
                // Giữ ngắn hạn để các yêu cầu đến muộn vài giây vẫn nhận đúng token mới.
                _dem.Set(KhoaDem(refreshToken), ketQua, TimeSpan.FromMinutes(2));
            }
            return ketQua;
        }
        finally
        {
            _dangChay.TryRemove(refreshToken, out _);
        }
    }

    private static string KhoaDem(string refreshToken) => "lammoi:" + refreshToken;
}
