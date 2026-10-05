namespace PlayerTransfer.Application.Common;

/// <summary>
/// Tên hiển thị của sáu chỉ số, theo NGUỒN của chúng.
///
/// Bộ dữ liệu Transfermarkt chỉ có số trận, số phút, bàn thắng, kiến tạo và thẻ phạt. Sáu chỉ số
/// "TinhTuThongKe" vì thế thực chất đo: mức độ ra sân, hiệu suất ghi bàn, hiệu suất kiến tạo,
/// đóng góp tấn công, kỷ luật và thời lượng thi đấu. Gọi chúng là "Tốc độ", "Phòng ngự"… như
/// trong trò chơi điện tử là sai nghĩa — ví dụ một trung phong đá đủ 54 trận sẽ có "Phòng ngự 97".
/// Thứ tự phần tử luôn là: TocDo, DutDiem, Chuyen, ReDat, PhongNgu, TheLuc.
/// </summary>
public static class TenChiSo
{
    public const string NguonThongKe = "TinhTuThongKe";

    public static readonly string[] KieuGame =
        { "Tốc độ", "Dứt điểm", "Chuyền bóng", "Rê dắt", "Phòng ngự", "Thể lực" };

    public static readonly string[] TuThongKe =
        { "Ra sân", "Ghi bàn", "Kiến tạo", "Tấn công", "Kỷ luật", "Thể lực" };

    public static string[] Theo(string nguonChiSo) => nguonChiSo == NguonThongKe ? TuThongKe : KieuGame;

    /// <summary>Dùng khi hiển thị chung nhiều cầu thủ: chỉ dùng bộ tên thống kê nếu MỌI nguồn đều là thống kê.</summary>
    public static string[] TheoNhieuNguon(IEnumerable<string> cacNguon)
    {
        var ds = cacNguon.Where(x => !string.IsNullOrEmpty(x) && x != "KhongCoDuLieu").Distinct().ToList();
        return ds.Count > 0 && ds.All(x => x == NguonThongKe) ? TuThongKe : KieuGame;
    }
}
