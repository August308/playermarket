using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Suy ra 6 chỉ số kiểu game và ước lượng giá trị thị trường từ số liệu thi đấu thật.
///
/// Vì sao cần lớp này: API-Football cung cấp số liệu thi đấu (bàn thắng, kiến tạo, phút,
/// tỉ lệ chuyền, tranh chấp, điểm trận đấu) nhưng KHÔNG cung cấp bộ 6 chỉ số kiểu game,
/// cũng KHÔNG cung cấp giá trị thị trường. Hai đại lượng này được tính lại theo công thức
/// công khai dưới đây để hệ thống có đủ dữ liệu cho chức năng so sánh và thống kê.
/// Mọi con số sinh ra đều được đánh dấu nguồn "UocLuong" trong lịch sử giá trị.
/// </summary>
public static class UocLuongHelper
{
    /// <summary>Quy đổi nhóm vị trí do API trả về ("Attacker", "Midfielder"…) sang nhóm nội bộ.</summary>
    public static NhomViTri NhomTuChuoi(string viTriApi) => (viTriApi ?? string.Empty).ToLowerInvariant() switch
    {
        "goalkeeper" => NhomViTri.ThuMon,
        "defender" => NhomViTri.HauVe,
        "midfielder" => NhomViTri.TienVe,
        "attacker" => NhomViTri.TienDao,
        _ => NhomViTri.TienVe
    };

    /// <summary>Mã viết tắt vị trí mặc định của mỗi nhóm, dùng khi API không nói rõ vị trí chi tiết.</summary>
    public static string VietTatMacDinh(NhomViTri nhom) => nhom switch
    {
        NhomViTri.ThuMon => "GK",
        NhomViTri.HauVe => "CB",
        NhomViTri.TienVe => "CM",
        _ => "ST"
    };

    /// <summary>
    /// Tính 6 chỉ số từ số liệu mùa giải.
    /// Nền tảng là điểm trận đấu trung bình của API (thang 1–10) quy về thang 40–99;
    /// sau đó cộng trừ theo nhóm vị trí và các chỉ số chuyên biệt.
    /// </summary>
    public static ChiSoCauThu TinhChiSo(
        NhomViTri nhom,
        double? diemTranDau,
        int soTran, int soPhut, int banThang, int kienTao,
        int? tiLeChuyen, int tranhChapThang, int tranhChapTong, int canPha,
        string muaGiai)
    {
        // 1) Chỉ số nền từ điểm trận đấu: 6.5 điểm ≈ 68, 7.5 điểm ≈ 82.
        var nen = diemTranDau.HasValue && diemTranDau.Value > 0
            ? (int)Math.Round((diemTranDau.Value - 5.2) / 2.6 * 45 + 55)
            : 62;

        nen = Math.Clamp(nen, 45, 93);

        // 2) Mật độ đóng góp trên 90 phút.
        var so90 = Math.Max(soPhut / 90.0, 0.5);
        var banThang90 = banThang / so90;
        var kienTao90 = kienTao / so90;
        var canPha90 = canPha / so90;
        var tiLeTranhChap = tranhChapTong > 0 ? tranhChapThang / (double)tranhChapTong : 0.5;

        int Gioi(double giaTri) => (int)Math.Clamp(Math.Round(giaTri), 35, 99);

        var chiSo = new ChiSoCauThu
        {
            MuaGiai = muaGiai,
            SoTran = soTran,
            SoPhutThiDau = soPhut,
            BanThang = banThang,
            KienTao = kienTao
        };

        switch (nhom)
        {
            case NhomViTri.ThuMon:
                chiSo.TocDo = Gioi(nen - 22);
                chiSo.DutDiem = Gioi(nen - 34);
                chiSo.Chuyen = Gioi(nen - 8 + (tiLeChuyen ?? 70) * 0.10);
                chiSo.ReDat = Gioi(nen - 26);
                chiSo.PhongNgu = Gioi(nen + 4);
                chiSo.TheLuc = Gioi(nen - 5);
                break;

            case NhomViTri.HauVe:
                chiSo.TocDo = Gioi(nen + 1);
                chiSo.DutDiem = Gioi(nen - 20 + banThang90 * 30);
                chiSo.Chuyen = Gioi(nen - 6 + (tiLeChuyen ?? 75) * 0.09);
                chiSo.ReDat = Gioi(nen - 9 + tiLeTranhChap * 8);
                chiSo.PhongNgu = Gioi(nen + 5 + canPha90 * 4 + tiLeTranhChap * 8);
                chiSo.TheLuc = Gioi(nen + 3);
                break;

            case NhomViTri.TienVe:
                chiSo.TocDo = Gioi(nen - 1);
                chiSo.DutDiem = Gioi(nen - 7 + banThang90 * 34);
                chiSo.Chuyen = Gioi(nen + 3 + (tiLeChuyen ?? 80) * 0.08 + kienTao90 * 16);
                chiSo.ReDat = Gioi(nen + 3 + tiLeTranhChap * 10);
                chiSo.PhongNgu = Gioi(nen - 5 + canPha90 * 5);
                chiSo.TheLuc = Gioi(nen + 5);
                break;

            default: // Tiền đạo
                chiSo.TocDo = Gioi(nen + 6);
                chiSo.DutDiem = Gioi(nen + 2 + banThang90 * 26);
                chiSo.Chuyen = Gioi(nen - 4 + kienTao90 * 20);
                chiSo.ReDat = Gioi(nen + 5 + tiLeTranhChap * 9);
                chiSo.PhongNgu = Gioi(nen - 20);
                chiSo.TheLuc = Gioi(nen);
                break;
        }

        return chiSo;
    }

    /// <summary>
    /// Ước lượng giá trị thị trường (triệu euro).
    /// Công thức: giá nền theo chỉ số tổng hợp, nhân hệ số độ tuổi, hệ số giải đấu
    /// và hệ số thời lượng ra sân.
    /// </summary>
    public static decimal UocLuongGiaTri(int chiSoTongHop, int tuoi, string tenGiaiDau, int soPhut)
    {
        // Giá nền: đường cong lũy thừa để khoảng 90 điểm cho ra ~105 triệu, 70 điểm ~21 triệu.
        var nen = Math.Pow(Math.Max(chiSoTongHop - 50, 1), 2.35) / 55.0;

        var heSoTuoi = tuoi switch
        {
            <= 20 => 1.25,
            <= 23 => 1.18,
            <= 27 => 1.00,
            <= 30 => 0.78,
            <= 33 => 0.52,
            _ => 0.28
        };

        var heSoGiai = (tenGiaiDau ?? string.Empty).ToLowerInvariant() switch
        {
            var t when t.Contains("premier league") => 1.18,
            var t when t.Contains("la liga") => 1.06,
            var t when t.Contains("serie a") => 1.00,
            var t when t.Contains("bundesliga") => 1.00,
            var t when t.Contains("ligue 1") => 0.90,
            _ => 0.78
        };

        // Ra sân càng đều, giá trị càng được xác nhận.
        var heSoPhut = 0.80 + 0.30 * Math.Min(soPhut / 2400.0, 1.0);

        var giaTri = nen * heSoTuoi * heSoGiai * heSoPhut;
        return Math.Round((decimal)Math.Clamp(giaTri, 0.2, 260), 2);
    }

    /// <summary>
    /// Đọc loại chuyển nhượng và phí từ trường "type" của API-Football.
    /// Ví dụ: "€ 60.0M" → mua đứt 60 triệu; "Loan" → cho mượn; "Free" → tự do.
    /// </summary>
    public static (LoaiChuyenNhuong Loai, decimal Phi) DocLoaiChuyenNhuong(string kieu)
    {
        if (string.IsNullOrWhiteSpace(kieu)) return (LoaiChuyenNhuong.MuaDut, 0);

        var chuan = kieu.Trim().ToLowerInvariant();

        if (chuan.Contains("loan")) return (LoaiChuyenNhuong.ChoMuon, 0);
        if (chuan.Contains("free")) return (LoaiChuyenNhuong.TuDo, 0);
        if (chuan.Contains("n/a") || chuan == "-") return (LoaiChuyenNhuong.HetHan, 0);

        // Dạng "€ 60.0M" hoặc "€ 900K".
        var soChuoi = new string(chuan.Where(x => char.IsDigit(x) || x == '.' || x == ',').ToArray())
            .Replace(",", ".");

        if (!decimal.TryParse(soChuoi, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var so))
            return (LoaiChuyenNhuong.MuaDut, 0);

        if (chuan.Contains('k')) so /= 1000m;            // nghìn euro → triệu euro
        else if (chuan.Contains('b')) so *= 1000m;       // tỷ euro → triệu euro

        return (LoaiChuyenNhuong.MuaDut, Math.Round(so, 2));
    }
}
