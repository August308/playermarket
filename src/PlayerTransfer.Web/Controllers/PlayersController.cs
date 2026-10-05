using Microsoft.AspNetCore.Mvc;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Web.Models;
using PlayerTransfer.Web.Services;

namespace PlayerTransfer.Web.Controllers;

public class PlayersController : Controller
{
    private readonly ApiClient _api;

    public PlayersController(ApiClient api) => _api = api;

    /// <summary>Trang khám phá cầu thủ với bộ lọc đa tiêu chí.</summary>
    public async Task<IActionResult> Index(BoLocCauThuDto boLoc)
    {
        boLoc ??= new BoLocCauThuDto();
        if (boLoc.Trang <= 0) boLoc.Trang = 1;
        if (boLoc.KichThuocTrang <= 0) boLoc.KichThuocTrang = 24;

        var vm = new KhamPhaVm
        {
            BoLoc = boLoc,
            KetQua = await _api.LocCauThuAsync(boLoc),
            GiaiDaus = await _api.DanhMucAsync("leagues"),
            ViTris = await _api.DanhMucAsync("positions"),
            QuocGias = await _api.DanhMucAsync("countries"),
            CauLacBos = await _api.DanhMucAsync("clubs",
                boLoc.MaGiaiDaus.Any() ? "leagueIds=" + string.Join(",", boLoc.MaGiaiDaus) : null)
        };

        return View(vm);
    }

    /// <summary>Trả về riêng phần lưới kết quả — dùng cho bộ lọc cập nhật tức thì.</summary>
    public async Task<IActionResult> LuoiKetQua(BoLocCauThuDto boLoc)
    {
        boLoc ??= new BoLocCauThuDto();
        if (boLoc.Trang <= 0) boLoc.Trang = 1;
        if (boLoc.KichThuocTrang <= 0) boLoc.KichThuocTrang = 24;

        var ketQua = await _api.LocCauThuAsync(boLoc);
        return PartialView("_LuoiCauThu", ketQua);
    }

    /// <summary>Trang kết quả tìm kiếm theo từ khóa.</summary>
    public async Task<IActionResult> TimKiem(string tuKhoa, int trang = 1)
    {
        var boLoc = new BoLocCauThuDto { TuKhoa = tuKhoa, Trang = trang };
        var vm = new KhamPhaVm
        {
            BoLoc = boLoc,
            KetQua = await _api.LocCauThuAsync(boLoc),
            GiaiDaus = await _api.DanhMucAsync("leagues"),
            ViTris = await _api.DanhMucAsync("positions"),
            QuocGias = await _api.DanhMucAsync("countries"),
            CauLacBos = await _api.DanhMucAsync("clubs")
        };

        return View(nameof(Index), vm);
    }

    /// <summary>Hồ sơ chi tiết của một cầu thủ.</summary>
    public async Task<IActionResult> ChiTiet(int id)
    {
        var (ok, _, cauThu) = await _api.GetAsync<CauThuChiTietDto>($"api/players/{id}");
        if (!ok || cauThu == null)
        {
            // Trả đúng mã 404 (trước đây là 200) để trình duyệt và công cụ tìm kiếm không coi đây là trang hợp lệ.
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("KhongTimThay");
        }

        var vm = new HoSoCauThuVm { CauThu = cauThu };

        var (_, _, chuyenNhuongs) = await _api.GetAsync<List<ChuyenNhuongDto>>($"api/players/{id}/transfers");
        var (_, _, chanThuongs) = await _api.GetAsync<List<ChanThuongDto>>($"api/players/{id}/injuries");
        var (_, _, giaTri) = await _api.GetAsync<List<DiemGiaTriDto>>($"api/players/{id}/value-history");
        var (_, _, tuongTu) = await _api.GetAsync<List<CauThuTomTatDto>>($"api/players/{id}/similar?limit=6");

        vm.ChuyenNhuongs = chuyenNhuongs ?? new List<ChuyenNhuongDto>();
        vm.ChanThuongs = chanThuongs ?? new List<ChanThuongDto>();
        vm.LichSuGiaTri = giaTri ?? new List<DiemGiaTriDto>();
        vm.TuongTu = tuongTu ?? new List<CauThuTomTatDto>();

        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
        {
            var (okTheoDoi, _, danhSach) = await _api.GetAsync<List<TheoDoiDto>>("api/watchlist");
            vm.DangTheoDoi = okTheoDoi && danhSach != null && danhSach.Any(x => x.CauThu?.MaCauThu == id);
        }

        // C11 — chỉ số trung bình của vị trí, để vẽ chung trên biểu đồ radar.
        var (okTb, _, trungBinh) = await _api.GetAsync<TrungBinhViTriDto>(
            $"api/scouting/position-average/{cauThu.MaViTri}");
        if (okTb) vm.TrungBinhViTri = trungBinh;

        // C1 — ghi nhận lượt xem phục vụ bảng xu hướng.
        await _api.PostAsync<object>($"api/scouting/view/{id}");

        return View(vm);
    }
}
