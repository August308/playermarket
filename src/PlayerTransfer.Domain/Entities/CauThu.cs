using PlayerTransfer.Domain.Enums;

namespace PlayerTransfer.Domain.Entities;

public class CauThu
{
    public int MaCauThu { get; set; }
    public string MaNgoaiApi { get; set; }

    /// <summary>Mã cầu thủ trên Transfermarkt (từ dataset Kaggle), tách khỏi mã API-Football.</summary>
    public string MaTransfermarkt { get; set; }

    /// <summary>Nguồn dữ liệu: Mau, MoPhong, ApiFootball, Transfermarkt.</summary>
    public string NguonDuLieu { get; set; } = "Mau";
    public string HoTen { get; set; }
    public string TenKhongDau { get; set; }
    public string AnhDaiDien { get; set; }
    public DateTime NgaySinh { get; set; }
    public int? ChieuCao { get; set; }
    public int? CanNang { get; set; }
    public ChanThuan? ChanThuan { get; set; }
    public int? SoAo { get; set; }
    public DateTime? NgayHetHanHopDong { get; set; }
    public decimal GiaTriHienTai { get; set; }
    public decimal GiaTriCaoNhat { get; set; }
    public bool ChinhSuaThuCong { get; set; }

    /// <summary>E12 — cầu thủ do bộ sinh dữ liệu tạo ra để kiểm thử hiệu năng.</summary>
    public bool LaDuLieuMoPhong { get; set; }
    public DateTime NgayCapNhat { get; set; } = DateTime.UtcNow;

    public int MaQuocGia { get; set; }
    public QuocGia QuocGia { get; set; }
    public int? MaCLB { get; set; }
    public CauLacBo CauLacBo { get; set; }
    public int MaViTri { get; set; }
    public ViTriThiDau ViTri { get; set; }

    public ChiSoCauThu ChiSo { get; set; }
    public ICollection<LichSuChuyenNhuong> LichSuChuyenNhuongs { get; set; } = new List<LichSuChuyenNhuong>();
    public ICollection<LichSuChanThuong> LichSuChanThuongs { get; set; } = new List<LichSuChanThuong>();
    public ICollection<LichSuGiaTri> LichSuGiaTris { get; set; } = new List<LichSuGiaTri>();
}

public class ChiSoCauThu
{
    public int MaChiSo { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }

    // Chỉ số kiểu game (0-99)
    public int TocDo { get; set; }
    public int DutDiem { get; set; }
    public int Chuyen { get; set; }
    public int ReDat { get; set; }
    public int PhongNgu { get; set; }
    public int TheLuc { get; set; }

    // Chỉ số thi đấu thực tế của mùa gần nhất
    public string MuaGiai { get; set; }
    public int SoTran { get; set; }
    public int SoPhutThiDau { get; set; }
    public int BanThang { get; set; }
    public int KienTao { get; set; }
    public int TheVang { get; set; }
    public int TheDo { get; set; }

    /// <summary>
    /// Nguồn của sáu chỉ số kiểu game ở trên:
    ///   TinhTuThongKe  — tính từ số liệu thi đấu thật (bộ dữ liệu Transfermarkt);
    ///   UocLuongTuGia  — suy ra từ giá trị thị trường (dữ liệu mẫu, API-Football);
    ///   KhongCoDuLieu  — không đủ dữ liệu, KHÔNG được hiển thị như số liệu thật.
    /// </summary>
    public string NguonChiSo { get; set; } = "UocLuongTuGia";

    /// <summary>Điểm phong độ 0-100 tính từ thống kê thi đấu thật; 0 nghĩa là chưa tính được.</summary>
    public int DiemPhongDo { get; set; }

    /// <summary>Đúng khi sáu chỉ số phía trên là số liệu suy ra chứ không phải đo được.</summary>
    public bool LaChiSoUocLuong => NguonChiSo != "TinhTuThongKe";

    /// <summary>Đúng khi hoàn toàn không có số liệu để hiển thị.</summary>
    public bool ThieuDuLieu => NguonChiSo == "KhongCoDuLieu";

    public int ChiSoTongHop => NguonChiSo switch
    {
        "TinhTuThongKe" => DiemPhongDo,
        "KhongCoDuLieu" => 0,
        _ => (int)Math.Round((TocDo + DutDiem + Chuyen + ReDat + PhongNgu + TheLuc) / 6.0)
    };
}
