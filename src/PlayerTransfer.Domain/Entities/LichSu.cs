using PlayerTransfer.Domain.Enums;

namespace PlayerTransfer.Domain.Entities;

public class LichSuChuyenNhuong
{
    public int MaChuyenNhuong { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public int? MaCLBDi { get; set; }
    public CauLacBo CLBDi { get; set; }
    public int? MaCLBDen { get; set; }
    public CauLacBo CLBDen { get; set; }
    public DateTime NgayChuyenNhuong { get; set; }
    public string MuaGiai { get; set; }
    public decimal PhiChuyenNhuong { get; set; }
    public decimal GiaTriThiTruongLucDo { get; set; }
    public LoaiChuyenNhuong LoaiChuyenNhuong { get; set; }

    /// <summary>
    /// Tên câu lạc bộ đi/đến khi câu lạc bộ đó không thuộc 5 giải trong hệ thống (học viện, giải khác...).
    /// Trước đây chỉ lưu khóa ngoại, CLB ngoài hệ thống bị mất tên và hiện thành "Tự do".
    /// </summary>
    public string TenCLBDiNgoai { get; set; }
    public string TenCLBDenNgoai { get; set; }
}

public class LichSuChanThuong
{
    public int MaChanThuong { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public string LoaiChanThuong { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int SoTranVang { get; set; }
    public string MuaGiai { get; set; }

    public int SoNgayNghi => (int)((NgayKetThuc ?? DateTime.UtcNow) - NgayBatDau).TotalDays;
    public bool DangDieuTri => NgayKetThuc == null;
}

public class LichSuGiaTri
{
    public int MaLichSuGiaTri { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public DateTime NgayGhiNhan { get; set; }
    public decimal GiaTri { get; set; }
    public string Nguon { get; set; }
}
