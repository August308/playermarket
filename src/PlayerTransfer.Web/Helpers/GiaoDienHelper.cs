namespace PlayerTransfer.Web.Helpers;

/// <summary>Tiện ích hiển thị: cờ quốc gia, huy hiệu chữ cái, màu theo chỉ số.</summary>
public static class GiaoDienHelper
{
    // Cơ sở dữ liệu lưu mã ISO 3 ký tự, dịch vụ ảnh cờ dùng mã 2 ký tự.
    private static readonly Dictionary<string, string> BanDoISO = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NOR"] = "no", ["ESP"] = "es", ["ENG"] = "gb-eng", ["POR"] = "pt", ["CRO"] = "hr",
        ["BRA"] = "br", ["FRA"] = "fr", ["EGY"] = "eg", ["NED"] = "nl", ["ARG"] = "ar",
        ["ECU"] = "ec", ["SEN"] = "sn", ["KOR"] = "kr", ["SWE"] = "se", ["URU"] = "uy",
        ["GER"] = "de", ["BEL"] = "be", ["POL"] = "pl", ["SVN"] = "si", ["ITA"] = "it",
        ["GEO"] = "ge", ["NGA"] = "ng", ["SRB"] = "rs", ["TUR"] = "tr", ["CAN"] = "ca",
        ["SUI"] = "ch", ["GUI"] = "gn", ["MAR"] = "ma", ["HUN"] = "hu", ["USA"] = "us",
        ["CMR"] = "cm", ["COD"] = "cd", ["JPN"] = "jp", ["AUT"] = "at", ["CIV"] = "ci",
        ["DEN"] = "dk", ["SCO"] = "gb-sct", ["WAL"] = "gb-wls", ["IRL"] = "ie", ["MEX"] = "mx",
        ["COL"] = "co", ["CHI"] = "cl", ["PER"] = "pe", ["GHA"] = "gh", ["ALG"] = "dz",
        ["AUS"] = "au", ["CZE"] = "cz", ["UKR"] = "ua", ["RUS"] = "ru", ["GRE"] = "gr"
    };

    /// <summary>Đường dẫn ảnh cờ quốc gia; trả về chuỗi rỗng nếu không tra được mã.</summary>
    public static string DuongDanCo(string maISO, int chieuRong = 40)
    {
        if (string.IsNullOrWhiteSpace(maISO)) return string.Empty;

        var ma = maISO.Trim();

        // Dữ liệu nhập từ bộ dữ liệu Transfermarkt lưu sẵn mã cờ 2 ký tự
        // (hoặc dạng "gb-eng"); dữ liệu mẫu lưu mã ISO 3 ký tự nên phải tra bảng.
        if (ma.Length == 2 || ma.Contains('-'))
            return $"https://flagcdn.com/w{chieuRong}/{ma.ToLowerInvariant()}.png";

        return BanDoISO.TryGetValue(ma, out var ma2)
            ? $"https://flagcdn.com/w{chieuRong}/{ma2}.png"
            : string.Empty;
    }

    /// <summary>Lấy chữ cái đầu của tên để dựng huy hiệu khi chưa có ảnh thật.</summary>
    public static string ChuCaiDau(string ten, int soChu = 2)
    {
        if (string.IsNullOrWhiteSpace(ten)) return "?";

        var tu = ten.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tu.Length == 1) return tu[0][..Math.Min(soChu, tu[0].Length)].ToUpperInvariant();

        return string.Concat(tu.Take(soChu).Select(x => char.ToUpperInvariant(x[0])));
    }

    /// <summary>Sinh màu ổn định theo tên, để hai lần tải trang cho cùng một màu.</summary>
    public static string MauTheoTen(string ten)
    {
        // Tám sắc màu phân loại, trùng với bộ --bieu-1..--bieu-8 trong site.css.
        // Bộ này đã kiểm định khoảng cách màu cho người mù màu và tương phản
        // tối thiểu 3:1 với nền, nên chữ trắng đặt lên vẫn đọc được.
        string[] bangMau =
        {
            "#0f7a3d", "#2563eb", "#a16207", "#db2777",
            "#0891b2", "#ea580c", "#7c3aed", "#be123c"
        };

        if (string.IsNullOrWhiteSpace(ten)) return bangMau[0];

        var tong = ten.Sum(x => (int)x);
        return bangMau[Math.Abs(tong) % bangMau.Length];
    }

    /// <summary>
    /// Bậc chỉ số, dùng để tô màu vòng chỉ số trên thẻ cầu thủ. Chỉ số 0 nghĩa là CHƯA ĐỦ số liệu
    /// thi đấu để tính (khoảng 1/4 số cầu thủ) — tô trung tính, không tô đỏ như cầu thủ yếu.
    /// </summary>
    public static string BacChiSo(int chiSo) => chiSo switch
    {
        <= 0 => "chua-co",
        >= 88 => "xuat-sac",
        >= 80 => "tot",
        >= 72 => "kha",
        _ => "trung-binh"
    };

    /// <summary>Chữ hiển thị trong vòng chỉ số: "–" khi chưa đủ số liệu thay vì số 0 gây hiểu nhầm.</summary>
    public static string HienChiSo(int chiSo) => chiSo > 0 ? chiSo.ToString() : "–";

    /// <summary>Chú thích khi rê chuột lên vòng chỉ số.</summary>
    public static string ChuThichChiSo(int chiSo) => chiSo > 0
        ? "Chỉ số tổng hợp"
        : "Chưa đủ số liệu thi đấu để tính chỉ số";

    /// <summary>Định dạng giá trị thị trường gọn: 180 tr €, 0,4 tr € hoặc 1,25 tỷ €.</summary>
    public static string DinhDangGiaTri(decimal trieuEuro) => PlayerTransfer.Application.Common.DinhDangSo.TrieuEuro(trieuEuro);
}
