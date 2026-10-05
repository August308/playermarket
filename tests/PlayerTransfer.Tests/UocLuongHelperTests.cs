using PlayerTransfer.Application.Services;
using PlayerTransfer.Domain.Enums;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>
/// Kiểm thử công thức suy ra chỉ số và ước lượng giá trị thị trường
/// (phần bù đắp cho việc API-Football không cung cấp hai nhóm dữ liệu này).
/// </summary>
public class UocLuongHelperTests
{
    [Fact(DisplayName = "Chỉ số luôn nằm trong thang 35–99")]
    public void ChiSo_TrongThangHopLe()
    {
        var chiSo = UocLuongHelper.TinhChiSo(NhomViTri.TienDao, 7.4, 30, 2600, 24, 8, 82, 210, 400, 12, "2025/2026");

        foreach (var gia in new[] { chiSo.TocDo, chiSo.DutDiem, chiSo.Chuyen, chiSo.ReDat, chiSo.PhongNgu, chiSo.TheLuc })
        {
            Assert.InRange(gia, 35, 99);
        }
    }

    [Fact(DisplayName = "Tiền đạo ghi nhiều bàn có chỉ số dứt điểm cao hơn phòng ngự")]
    public void TienDao_DutDiemCaoHonPhongNgu()
    {
        var chiSo = UocLuongHelper.TinhChiSo(NhomViTri.TienDao, 7.5, 32, 2800, 28, 6, 78, 200, 380, 8, "2025/2026");
        Assert.True(chiSo.DutDiem > chiSo.PhongNgu);
    }

    [Fact(DisplayName = "Thủ môn có chỉ số dứt điểm thấp nhất")]
    public void ThuMon_DutDiemThap()
    {
        var chiSo = UocLuongHelper.TinhChiSo(NhomViTri.ThuMon, 6.9, 34, 3060, 0, 0, 70, 20, 40, 0, "2025/2026");
        Assert.True(chiSo.DutDiem < chiSo.PhongNgu);
    }

    [Fact(DisplayName = "Cầu thủ trẻ cùng chỉ số được định giá cao hơn cầu thủ lớn tuổi")]
    public void GiaTri_TreHonThiCaoHon()
    {
        var tre = UocLuongHelper.UocLuongGiaTri(85, 22, "Premier League", 2500);
        var gia = UocLuongHelper.UocLuongGiaTri(85, 34, "Premier League", 2500);

        Assert.True(tre > gia);
    }

    [Fact(DisplayName = "Giá trị ước lượng không bao giờ âm và không vượt 260 triệu")]
    public void GiaTri_TrongKhoangHopLe()
    {
        foreach (var chiSo in new[] { 40, 55, 70, 85, 99 })
        {
            foreach (var tuoi in new[] { 17, 25, 31, 38 })
            {
                var giaTri = UocLuongHelper.UocLuongGiaTri(chiSo, tuoi, "Premier League", 2700);
                Assert.InRange(giaTri, 0.2m, 260m);
            }
        }
    }

    [Theory(DisplayName = "Đọc đúng loại và phí chuyển nhượng từ chuỗi của API")]
    [InlineData("€ 60.0M", 60)]
    [InlineData("€ 900K", 0.9)]
    [InlineData("Free", 0)]
    [InlineData("Loan", 0)]
    [InlineData("N/A", 0)]
    public void DocLoaiChuyenNhuong_DungPhi(string kieu, decimal phiMongDoi)
    {
        var (_, phi) = UocLuongHelper.DocLoaiChuyenNhuong(kieu);
        Assert.Equal(phiMongDoi, phi);
    }

    [Fact(DisplayName = "Nhận diện đúng cho mượn và chuyển nhượng tự do")]
    public void DocLoaiChuyenNhuong_DungLoai()
    {
        Assert.Equal(LoaiChuyenNhuong.ChoMuon, UocLuongHelper.DocLoaiChuyenNhuong("Loan").Loai);
        Assert.Equal(LoaiChuyenNhuong.TuDo, UocLuongHelper.DocLoaiChuyenNhuong("Free").Loai);
        Assert.Equal(LoaiChuyenNhuong.MuaDut, UocLuongHelper.DocLoaiChuyenNhuong("€ 45M").Loai);
    }
}
