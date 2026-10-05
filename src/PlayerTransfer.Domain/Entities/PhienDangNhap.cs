namespace PlayerTransfer.Domain.Entities;

public class PhienDangNhap
{
    public Guid MaPhien { get; set; } = Guid.NewGuid();
    public int MaNguoiDung { get; set; }
    public NguoiDung NguoiDung { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ThoiDiemDangNhap { get; set; } = DateTime.UtcNow;
    public DateTime ThoiDiemHetHan { get; set; }
    public bool DaThuHoi { get; set; }

    // E4 — xoay vòng refresh token.
    /// <summary>Token mới sinh ra khi phiên này được làm mới (null nếu chưa làm mới).</summary>
    public string ThayTheBoiToken { get; set; }
    /// <summary>Thời điểm phiên bị thu hồi, phục vụ điều tra khi phát hiện dùng lại token.</summary>
    public DateTime? ThoiDiemThuHoi { get; set; }
    /// <summary>Lý do thu hồi: DangXuat, XoayVong, DungLaiToken, DangXuatTatCa.</summary>
    public string LyDoThuHoi { get; set; }
    /// <summary>Chuỗi User-Agent rút gọn của thiết bị đăng nhập.</summary>
    public string ThietBi { get; set; }
    public string DiaChiIp { get; set; }
}
