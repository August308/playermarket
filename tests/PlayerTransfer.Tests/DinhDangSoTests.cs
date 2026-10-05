using PlayerTransfer.Application.Common;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>Định dạng hiển thị phải giống nhau trên mọi máy chủ, bất kể ngôn ngữ hệ điều hành.</summary>
public class DinhDangSoTests
{
    [Theory]
    [InlineData(180, "180 tr €")]
    [InlineData(0.4, "0,4 tr €")]
    [InlineData(0.05, "0,05 tr €")]
    [InlineData(12.5, "12,5 tr €")]
    [InlineData(1250, "1,25 tỷ €")]
    [InlineData(0, "0 tr €")]
    public void TrieuEuro_DinhDangKieuVietNam(double giaTri, string mongDoi)
        => Assert.Equal(mongDoi, DinhDangSo.TrieuEuro((decimal)giaTri));

    [Fact]
    public void So_DungDauChamChoHangNghin_DauPhayChoThapPhan()
    {
        Assert.Equal("4.325", DinhDangSo.So(4325));
        Assert.Equal("1.234,5", DinhDangSo.So(1234.5m, 1));
    }

    [Theory]
    [InlineData(11.1, "+11,1%")]
    [InlineData(-33.3, "−33,3%")]
    [InlineData(0, "0%")]
    public void PhanTramCoDau_CoDauVaKhongMuiTenSaiKhiBangKhong(double pt, string mongDoi)
        => Assert.Equal(mongDoi, DinhDangSo.PhanTramCoDau((decimal)pt));

    [Fact]
    public void GioVN_CongBayTiengSoVoiUtc()
    {
        var utc = new DateTime(2026, 10, 1, 2, 43, 0, DateTimeKind.Utc);
        Assert.Equal("01/10/2026 09:43", DinhDangSo.NgayGioVN(utc));

        // Mốc đọc từ JSON/CSDL thường có Kind = Unspecified nhưng vẫn là giờ UTC.
        var khongRo = new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("02/10/2026 03:00", DinhDangSo.NgayGioVN(khongRo));
    }
}

public class TenCauLacBoTests
{
    [Theory]
    [InlineData("Società Sportiva Lazio S.p.A.", "SS Lazio")]
    [InlineData("Associazione Sportiva Roma", "AS Roma")]
    [InlineData("1. Fußballclub Heidenheim 1846", "1. FC Heidenheim 1846")]
    [InlineData("Bologna Football Club 1909", "Bologna FC 1909")]
    [InlineData("Pisa Sporting Club", "Pisa SC")]
    [InlineData("Real Madrid", "Real Madrid")]
    [InlineData("Paris Saint-Germain", "Paris Saint-Germain")]
    public void RutGon_DoiTenPhapNhanVeTenThuongGoi(string dauVao, string mongDoi)
        => Assert.Equal(mongDoi, TenCauLacBo.RutGon(dauVao));
}
