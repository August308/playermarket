using System.Text.RegularExpressions;

namespace PlayerTransfer.Application.Common;

/// <summary>
/// Rút gọn tên pháp nhân của câu lạc bộ về tên thường gọi.
/// Bộ dữ liệu Transfermarkt ghi một số đội bằng tên đăng ký đầy đủ
/// ("Società Sportiva Lazio S.p.A.", "Associazione Sportiva Roma", "1. Fußballclub Heidenheim 1846")
/// trong khi phần còn lại dùng tên ngắn ("AC Milan", "SSC Napoli") — bộ lọc và thẻ cầu thủ vì thế lệch nhau.
/// </summary>
public static class TenCauLacBo
{
    private static readonly (Regex Mau, string Thay)[] QuyTac =
    {
        (new Regex(@"\s+S\.p\.A\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), ""),
        (new Regex(@"^Associazione Sportiva\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "AS"),
        (new Regex(@"^Società Sportiva\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "SS"),
        (new Regex(@"\bFußballclub\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "FC"),
        (new Regex(@"\bFootball Club\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "FC"),
        (new Regex(@"\bSporting Club\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "SC"),
    };

    public static string RutGon(string ten)
    {
        if (string.IsNullOrWhiteSpace(ten)) return ten;

        var ketQua = ten.Trim();
        foreach (var (mau, thay) in QuyTac) ketQua = mau.Replace(ketQua, thay);

        return Regex.Replace(ketQua, @"\s{2,}", " ").Trim();
    }
}
