using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Web.Services;

/// <summary>
/// Danh sách mã cầu thủ mà người dùng hiện tại đang theo dõi, nạp MỘT lần cho mỗi yêu cầu.
/// Thẻ cầu thủ dùng nó để hiện đúng ★/☆ — trước đây thẻ luôn truyền cứng "chưa theo dõi".
/// </summary>
public class TheoDoiCuaToi
{
    private readonly ApiClient _api;
    private readonly IHttpContextAccessor _context;
    private HashSet<int> _danhSach;

    public TheoDoiCuaToi(ApiClient api, IHttpContextAccessor context)
    {
        _api = api;
        _context = context;
    }

    public async Task<bool> DangTheoDoiAsync(int maCauThu)
    {
        if (_danhSach == null)
        {
            _danhSach = new HashSet<int>();

            var daDangNhap = !string.IsNullOrEmpty(_context.HttpContext?.Session.GetString("AccessToken"));
            if (daDangNhap)
            {
                var (ok, _, ds) = await _api.GetAsync<List<TheoDoiDto>>("api/watchlist");
                if (ok && ds != null) _danhSach = ds.Where(x => x.CauThu != null).Select(x => x.CauThu.MaCauThu).ToHashSet();
            }
        }

        return _danhSach.Contains(maCauThu);
    }
}
