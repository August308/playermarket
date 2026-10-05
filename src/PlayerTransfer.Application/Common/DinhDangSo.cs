using System.Globalization;

namespace PlayerTransfer.Application.Common;

/// <summary>
/// Định dạng số và ngày giờ để HIỂN THỊ theo kiểu Việt Nam (1.234,5), không phụ thuộc
/// vào ngôn ngữ cài trên máy chủ. Trước đây mỗi nơi tự gọi ToString("N1") nên cùng một trang
/// có chỗ ra "0,1" chỗ ra "0.4", và kết quả đổi theo máy chạy.
/// </summary>
public static class DinhDangSo
{
    public static readonly CultureInfo VN = TaoVanHoa();

    private static CultureInfo TaoVanHoa()
    {
        // Không dùng CultureInfo("vi-VN") vì máy chủ chạy chế độ globalization-invariant
        // (ảnh Docker tối giản) sẽ không có dữ liệu vùng — tự khai báo dấu phân cách cho chắc.
        var vh = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        vh.NumberFormat.NumberDecimalSeparator = ",";
        vh.NumberFormat.NumberGroupSeparator = ".";
        vh.NumberFormat.PercentDecimalSeparator = ",";
        vh.NumberFormat.PercentGroupSeparator = ".";
        return vh;
    }

    /// <summary>Số có phân cách hàng nghìn và đúng <paramref name="soLe"/> chữ số thập phân.</summary>
    public static string So(decimal giaTri, int soLe = 0) => giaTri.ToString("N" + soLe, VN);

    public static string So(double giaTri, int soLe = 0) => giaTri.ToString("N" + soLe, VN);

    public static string So(int giaTri) => giaTri.ToString("N0", VN);

    public static string So(long giaTri) => giaTri.ToString("N0", VN);

    /// <summary>Số gọn: bỏ phần thập phân nếu là số tròn (60 thay vì 60,0), ngược lại giữ tối đa <paramref name="soLeToiDa"/> chữ số.</summary>
    public static string SoGon(decimal giaTri, int soLeToiDa = 1)
    {
        // Giá trị dưới 1 (0,05 tr €) làm tròn 1 chữ số sẽ thành 0,1 hoặc 0 — giữ thêm một chữ số.
        var soLe = Math.Abs(giaTri) < 1 ? soLeToiDa + 1 : soLeToiDa;
        var lamTron = Math.Round(giaTri, soLe, MidpointRounding.AwayFromZero);

        return lamTron.ToString("#,##0.###", VN);
    }

    /// <summary>Giá trị thị trường tính bằng triệu euro: "180 tr €", "0,4 tr €" hoặc "1,25 tỷ €".</summary>
    public static string TrieuEuro(decimal trieuEuro)
    {
        if (Math.Abs(trieuEuro) >= 1000)
            return (trieuEuro / 1000).ToString("#,##0.##", VN) + " tỷ €";

        return SoGon(trieuEuro) + " tr €";
    }

    public static string TrieuEuro(double trieuEuro) => TrieuEuro((decimal)trieuEuro);

    /// <summary>Phần trăm có dấu: "+11,1%", "-33,3%", "0%".</summary>
    public static string PhanTramCoDau(decimal phanTram, int soLe = 1)
    {
        var so = Math.Round(phanTram, soLe, MidpointRounding.AwayFromZero);
        if (so == 0) return "0%";
        return (so > 0 ? "+" : "−") + Math.Abs(so).ToString("0.#", VN) + "%";
    }

    private static readonly TimeZoneInfo MuiGioVN = TimMuiGio();

    private static TimeZoneInfo TimMuiGio()
    {
        foreach (var ma in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(ma); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
    }

    /// <summary>
    /// Đổi mốc thời gian UTC (cách hệ thống lưu) sang giờ Việt Nam. Không dùng ToLocalTime()
    /// vì kết quả phụ thuộc múi giờ của máy chủ — chạy trong Docker (UTC) sẽ lệch 7 tiếng.
    /// </summary>
    public static DateTime GioVN(DateTime utc)
    {
        var mocUtc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(mocUtc, MuiGioVN);
    }

    public static string NgayGioVN(DateTime utc) => GioVN(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
