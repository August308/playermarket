using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Services;
using Xunit;

namespace PlayerTransfer.Tests;

public class CauThuServiceTests
{
    [Fact(DisplayName = "TC-04-04: Tìm kiếm không phân biệt dấu tiếng Việt")]
    public async Task TimKiem_KhongPhanBietDau()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var ketQua = await service.TimKiemAsync("nguyen", 1, 20);

        Assert.Single(ketQua.DanhSach);
        Assert.Equal("Nguyễn Văn A", ketQua.DanhSach[0].HoTen);
    }

    [Fact(DisplayName = "TC-04-03: Tìm kiếm không phân biệt chữ hoa thường")]
    public async Task TimKiem_KhongPhanBietHoaThuong()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var hoa = await service.TimKiemAsync("HAALAND", 1, 20);
        var thuong = await service.TimKiemAsync("haaland", 1, 20);

        Assert.Equal(hoa.TongSo, thuong.TongSo);
        Assert.Equal(1, hoa.TongSo);
    }

    [Fact(DisplayName = "TC-05-03: Lọc theo khoảng giá trị trả đúng kết quả")]
    public async Task Loc_TheoKhoangGiaTri()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var ketQua = await service.LocAsync(new BoLocCauThuDto { GiaTriTu = 100, GiaTriDen = 250 });

        Assert.Single(ketQua.DanhSach);
        Assert.Equal("Erling Haaland", ketQua.DanhSach[0].HoTen);
    }

    [Fact(DisplayName = "TC-07-01: So sánh 2 cầu thủ trả về bảng đầy đủ")]
    public async Task SoSanh_HaiCauThu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var ketQua = await service.SoSanhAsync(new[] { 1, 2 });

        Assert.True(ketQua.ThanhCong);
        Assert.Equal(2, ketQua.DuLieu.CauThus.Count);
        Assert.NotEmpty(ketQua.DuLieu.CacHang);

        var hangGiaTri = ketQua.DuLieu.CacHang.First(x => x.Khoa == "giatri");
        Assert.Equal(0, hangGiaTri.ViTriTotNhat);   // Haaland có giá trị cao hơn
    }

    [Fact(DisplayName = "TC-07-03: Từ chối khi so sánh quá 4 cầu thủ")]
    public async Task SoSanh_VuotGioiHan()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var ketQua = await service.SoSanhAsync(new[] { 1, 2, 3, 4, 5 });

        Assert.False(ketQua.ThanhCong);
        Assert.Equal("VUOT_GIOI_HAN", ketQua.Ma);
    }

    [Fact(DisplayName = "Cần tối thiểu 2 cầu thủ mới so sánh được")]
    public async Task SoSanh_ThieuCauThu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var ketQua = await service.SoSanhAsync(new[] { 1 });

        Assert.False(ketQua.ThanhCong);
        Assert.Equal("THIEU_CAU_THU", ketQua.Ma);
    }

    [Fact(DisplayName = "Chấn thương: không có nguồn thì báo 'chưa có dữ liệu', không báo 0 ngày")]
    public async Task ChanThuong_KhongCoNguon_KhongCoDuLieu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new CauThuService(db);

        var chiTiet = await service.LayChiTietAsync(1);

        Assert.False(chiTiet.CoDuLieuChanThuong);
    }

    [Fact(DisplayName = "Chấn thương: có bản ghi thì coi là có dữ liệu")]
    public async Task ChanThuong_CoBanGhi_CoDuLieu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.LichSuChanThuongs.Add(new PlayerTransfer.Domain.Entities.LichSuChanThuong
        {
            MaCauThu = 1, LoaiChanThuong = "Gân kheo", MuaGiai = "2025/2026",
            NgayBatDau = DateTime.UtcNow.AddDays(-40), NgayKetThuc = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var chiTiet = await new CauThuService(db).LayChiTietAsync(1);

        Assert.True(chiTiet.CoDuLieuChanThuong);
        Assert.Equal(30, chiTiet.SoNgayNghiChanThuong3Mua);
    }
}
