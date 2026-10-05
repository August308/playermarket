using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PlayerTransfer.Application.Services;
using PlayerTransfer.Domain.Entities;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>
/// Đồng bộ API-Football phải báo đúng khi API từ chối (trước đây nhật ký luôn ghi
/// "ThanhCong — lỗi 0" dù gói miễn phí từ chối mọi lượt gọi).
/// </summary>
public class DongBoServiceTests
{
    /// <summary>Máy chủ giả: trả đúng nội dung API-Football gửi về khi gói miễn phí hỏi mùa mới.</summary>
    private sealed class MayChuGia : HttpMessageHandler
    {
        public int SoLanGoi { get; private set; }
        private readonly string _noiDung;

        public MayChuGia(string noiDung) => _noiDung = noiDung;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SoLanGoi++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_noiDung, Encoding.UTF8, "application/json")
            });
        }
    }

    private static (DongBoService DichVu, MayChuGia MayChu, PlayerTransfer.Infrastructure.Data.ApplicationDbContext Db)
        TaoDichVu(string noiDungApi)
    {
        var db = TestHelper.TaoDbTrongBoNho();
        var cauHinh = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["FootballApi:Key"] = "khoa-thu",
            ["FootballApi:MuaGiai"] = "2026"
        }).Build();

        var mayChu = new MayChuGia(noiDungApi);
        var api = new FootballApiClient(new HttpClient(mayChu), cauHinh, NullLogger<FootballApiClient>.Instance);
        var dichVu = new DongBoService(db, api, cauHinh, NullLogger<DongBoService>.Instance, new ThongBaoRong());
        return (dichVu, mayChu, db);
    }

    [Fact(DisplayName = "Đồng bộ: gói miễn phí từ chối mùa giải → ghi Thất bại, đếm lỗi, dừng ngay")]
    public async Task GoiMienPhi_TuChoiMuaGiai_GhiThatBai()
    {
        const string phanHoi =
            "{\"errors\":{\"plan\":\"Free plans do not have access to this season, try from 2022 to 2024.\"},\"response\":[]}";
        var (dichVu, mayChu, db) = TaoDichVu(phanHoi);

        var ketQua = await dichVu.DongBoAsync("Toàn bộ");

        var nhatKy = db.NhatKyDongBos.Single();
        Assert.False(ketQua.ThanhCong);
        Assert.Equal("ThatBai", nhatKy.TrangThai);
        Assert.True(nhatKy.SoBanGhiLoi >= 1);
        Assert.Contains("mùa 2026/2027", nhatKy.ThongDiep);
        // Biết chắc gọi tiếp cũng vô ích nên chỉ tốn 1 lượt gọi thay vì 5.
        Assert.Equal(1, mayChu.SoLanGoi);
    }

    [Fact(DisplayName = "Đồng bộ: lượt bị treo ở trạng thái Đang chạy được đóng lại")]
    public async Task LuotBiTreo_DuocDongLai()
    {
        var (dichVu, _, db) = TaoDichVu("{\"errors\":[],\"response\":[]}");
        db.NhatKyDongBos.Add(new NhatKyDongBo
        {
            ThoiDiemBatDau = DateTime.UtcNow.AddDays(-3),
            PhamVi = "Toàn bộ",
            TrangThai = "DangChay"
        });
        await db.SaveChangesAsync();

        await dichVu.DongBoAsync("Toàn bộ");

        Assert.DoesNotContain(db.NhatKyDongBos, x => x.TrangThai == "DangChay");
        Assert.Contains(db.NhatKyDongBos, x => x.TrangThai == "BiGianDoan");
    }
}

public class LichSuGiaTests
{
    [Fact(DisplayName = "Chụp ảnh giá: không ghi mốc khi giá không đổi, ghi khi giá đổi")]
    public async Task ChupAnh_ChiGhiKhiGiaDoi()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.LichSuGiaTris.AddRange(
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = DateTime.UtcNow.Date.AddDays(-30), GiaTri = 180, Nguon = "Transfermarkt" },
            new LichSuGiaTri { MaCauThu = 2, NgayGhiNhan = DateTime.UtcNow.Date.AddDays(-30), GiaTri = 15, Nguon = "Transfermarkt" });
        await db.SaveChangesAsync();

        // Cầu thủ 1 vẫn 180 (không đổi), cầu thủ 2 đã lên 20.
        var soMoi = await ChupAnhGiaNenService.ChupAnhAsync(db, default);

        Assert.Equal(1, soMoi);
        Assert.Single(db.LichSuGiaTris.Where(x => x.Nguon == "ChupAnhNgay" && x.MaCauThu == 2));
    }

    [Fact(DisplayName = "Dọn dữ liệu: xóa mốc chụp ảnh phẳng, giữ mốc đổi giá và mốc gốc")]
    public async Task DonDep_XoaMocPhang()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var goc = DateTime.UtcNow.Date.AddDays(-20);
        db.LichSuGiaTris.AddRange(
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = goc, GiaTri = 180, Nguon = "Transfermarkt" },
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = goc.AddDays(1), GiaTri = 180, Nguon = "ChupAnhNgay" },
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = goc.AddDays(2), GiaTri = 180, Nguon = "ChupAnhNgay" },
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = goc.AddDays(3), GiaTri = 190, Nguon = "ChupAnhNgay" },
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = goc.AddDays(4), GiaTri = 190, Nguon = "ChupAnhNgay" });
        await db.SaveChangesAsync();

        var soXoa = await DonDepDuLieuService.XoaMocGiaPhangAsync(db);
        var lanHai = await DonDepDuLieuService.XoaMocGiaPhangAsync(db);

        Assert.Equal(3, soXoa);
        Assert.Equal(0, lanHai);   // idempotent
        Assert.Equal(new[] { 180m, 190m }, db.LichSuGiaTris.OrderBy(x => x.NgayGhiNhan).Select(x => x.GiaTri).ToArray());
    }
}

public class CanhBaoGiaTests
{
    private sealed class EmailGia : PlayerTransfer.Application.Interfaces.IEmailService
    {
        public int SoThu { get; private set; }
        public bool DaCauHinh => true;
        public Task<bool> GuiAsync(string email, string tieuDe, string noiDungHtml, CancellationToken ct = default)
        {
            SoThu++;
            return Task.FromResult(true);
        }
    }

    [Fact(DisplayName = "Cảnh báo giá: chỉ gửi một lần cho mỗi lần chạm ngưỡng, gửi lại khi chạm lần sau")]
    public async Task CanhBao_KhongGuiLapLai()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.TheoDoiCauThus.Add(new TheoDoiCauThu { MaNguoiDung = 1, MaCauThu = 1, NguongCanhBaoTren = 150 });
        await db.SaveChangesAsync();
        var email = new EmailGia();

        // Giá 180 ≥ 150: ngày 1 gửi, ngày 2 và 3 không gửi lại.
        await ChupAnhGiaNenService.GuiCanhBaoAsync(db, email, new ThongBaoRong(), default);
        await ChupAnhGiaNenService.GuiCanhBaoAsync(db, email, new ThongBaoRong(), default);
        await ChupAnhGiaNenService.GuiCanhBaoAsync(db, email, new ThongBaoRong(), default);
        Assert.Equal(1, email.SoThu);

        // Giá về dưới ngưỡng rồi vượt lại: phải báo lần nữa.
        var ct = db.CauThus.Single(x => x.MaCauThu == 1);
        ct.GiaTriHienTai = 120; await db.SaveChangesAsync();
        await ChupAnhGiaNenService.GuiCanhBaoAsync(db, email, new ThongBaoRong(), default);
        ct.GiaTriHienTai = 160; await db.SaveChangesAsync();
        await ChupAnhGiaNenService.GuiCanhBaoAsync(db, email, new ThongBaoRong(), default);
        Assert.Equal(2, email.SoThu);
    }
}

public class DonDepDanhMucTests
{
    [Fact(DisplayName = "Dọn dữ liệu: xóa CLB/giải mẫu bỏ trống, giữ CLB còn được thương vụ tham chiếu")]
    public async Task XoaDanhMucBoTrong()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.GiaiDaus.Add(new GiaiDau { MaGiaiDau = 2, TenGiaiDau = "La Liga" });                      // giải mẫu trùng
        db.CauLacBos.AddRange(
            new CauLacBo { MaCLB = 2, TenCLB = "Arsenal", MaGiaiDau = 2 },                             // bỏ trống → xóa
            new CauLacBo { MaCLB = 3, TenCLB = "Leicester City", MaGiaiDau = 1 },                       // còn thương vụ → giữ
            new CauLacBo { MaCLB = 4, TenCLB = "Chelsea FC", MaGiaiDau = 1, MaTransfermarkt = "631" }); // dữ liệu thật → giữ
        db.LichSuChuyenNhuongs.Add(new LichSuChuyenNhuong
        {
            MaCauThu = 1, MaCLBDi = 3, MaCLBDen = 1, MuaGiai = "2024/2025", NgayChuyenNhuong = new DateTime(2024, 7, 1)
        });
        await db.SaveChangesAsync();

        var (soCLB, soGiai) = await DonDepDuLieuService.XoaDanhMucMauBoTrongAsync(db);

        Assert.Equal(1, soCLB);
        Assert.Equal(1, soGiai);
        Assert.DoesNotContain(db.CauLacBos, x => x.MaCLB == 2);
        Assert.Contains(db.CauLacBos, x => x.MaCLB == 3);
        Assert.Contains(db.CauLacBos, x => x.MaCLB == 4);
        Assert.DoesNotContain(db.GiaiDaus, x => x.MaGiaiDau == 2);
    }
}
