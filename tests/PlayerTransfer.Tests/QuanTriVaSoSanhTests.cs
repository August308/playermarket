using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Services;
using PlayerTransfer.Domain.Entities;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>Các lỗi phát hiện khi chạy thử: sửa cầu thủ làm mất ảnh, bảng so sánh luôn hiện 0,0%.</summary>
public class QuanTriVaSoSanhTests
{
    [Fact(DisplayName = "Quản trị: nạp đúng dữ liệu gốc để sửa")]
    public async Task LayDeSua_TraDungDuLieu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var service = new QuanTriService(db, new CauThuService(db));

        var dto = await service.LayDeSuaAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(1, dto.MaCauThu);
        Assert.Equal("Erling Haaland", dto.HoTen);
        Assert.Equal(180, dto.GiaTriHienTai);
        Assert.Null(await service.LayDeSuaAsync(999));
    }

    [Fact(DisplayName = "Quản trị: sửa cầu thủ không gửi ảnh thì giữ nguyên ảnh cũ")]
    public async Task LuuCauThu_KhongGuiAnh_GiuAnhCu()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        var ct = await db.CauThus.FindAsync(1);
        ct.AnhDaiDien = "https://example.com/haaland.jpg";
        await db.SaveChangesAsync();

        var service = new QuanTriService(db, new CauThuService(db));
        var dto = await service.LayDeSuaAsync(1);
        dto.AnhDaiDien = null;          // biểu mẫu quản trị không có ô ảnh
        dto.GiaTriHienTai = 190;

        var ketQua = await service.LuuCauThuAsync(dto, 1);

        Assert.True(ketQua.ThanhCong);
        var sau = await db.CauThus.FindAsync(1);
        Assert.Equal("https://example.com/haaland.jpg", sau.AnhDaiDien);
        Assert.Equal(190, sau.GiaTriHienTai);
    }

    [Fact(DisplayName = "So sánh: hàng thay đổi tính so với lần định giá trước, không phải 30 ngày")]
    public async Task SoSanh_ThayDoiSoVoiLanDinhGiaTruoc()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.LichSuGiaTris.AddRange(
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = new DateTime(2025, 5, 1), GiaTri = 150, Nguon = "Transfermarkt" },
            new LichSuGiaTri { MaCauThu = 1, NgayGhiNhan = new DateTime(2026, 6, 1), GiaTri = 180, Nguon = "Transfermarkt" });
        await db.SaveChangesAsync();

        var ketQua = await new CauThuService(db).SoSanhAsync(new[] { 1, 2 });

        var hang = ketQua.DuLieu.CacHang.First(x => x.Khoa == "thaydoi");
        Assert.Equal("+20% (từ 05/2025)", hang.GiaTriHienThi[0]);   // 150 → 180
        Assert.Equal("—", hang.GiaTriHienThi[1]);                   // chưa có lịch sử giá
        Assert.DoesNotContain(ketQua.DuLieu.CacHang, x => x.Khoa == "thaydoi30");
    }
}

public class ChuanHoaQuocGiaTests
{
    [Fact(DisplayName = "Dọn dữ liệu: quốc gia tên tiếng Anh được Việt hóa, gắn cờ và gộp bản trùng")]
    public async Task ChuanHoaQuocGia_VietHoaVaGopTrung()
    {
        using var db = TestHelper.TaoDbTrongBoNho();
        db.QuocGias.AddRange(
            new QuocGia { MaQuocGia = 10, TenQuocGia = "Vietnam" },
            new QuocGia { MaQuocGia = 11, TenQuocGia = "Neukaledonien" },
            new QuocGia { MaQuocGia = 12, TenQuocGia = "New Caledonia" },
            new QuocGia { MaQuocGia = 13, TenQuocGia = "Xứ Không Có Thật" });
        var ct = await db.CauThus.FindAsync(2);
        ct.MaQuocGia = 11;
        await db.SaveChangesAsync();

        var soDoi = await DonDepDuLieuService.ChuanHoaQuocGiaAsync(db);

        Assert.Equal(3, soDoi);
        var vn = await db.QuocGias.FindAsync(10);
        Assert.Equal("Việt Nam", vn.TenQuocGia);
        Assert.Equal("vn", vn.MaISO);

        // Hai bản "Neukaledonien" / "New Caledonia" còn một, và cầu thủ không bị mất quốc tịch.
        Assert.Single(db.QuocGias.Where(x => x.TenQuocGia == "New Caledonia"));
        var maConLai = db.QuocGias.Single(x => x.TenQuocGia == "New Caledonia").MaQuocGia;
        Assert.Equal(maConLai, (await db.CauThus.FindAsync(2)).MaQuocGia);

        Assert.Null((await db.QuocGias.FindAsync(13)).MaISO);     // không đoán bừa
        Assert.Equal(0, await DonDepDuLieuService.ChuanHoaQuocGiaAsync(db));   // chạy lại không đổi gì
    }
}
