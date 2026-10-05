namespace PlayerTransfer.Domain.Entities;

public class TheoDoiCauThu
{
    public int MaTheoDoi { get; set; }
    public int MaNguoiDung { get; set; }
    public NguoiDung NguoiDung { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public DateTime NgayThem { get; set; } = DateTime.UtcNow;
    public decimal? NguongCanhBaoTren { get; set; }
    public decimal? NguongCanhBaoDuoi { get; set; }
    public string GhiChu { get; set; }

    /// <summary>
    /// Thời điểm đã gửi cảnh báo cho lần chạm ngưỡng hiện tại. Còn giá trị thì không gửi lại
    /// (trước đây cứ 24 giờ gửi một thư chừng nào giá còn vượt ngưỡng). Được xóa khi giá quay
    /// về trong khoảng ngưỡng hoặc khi người dùng đổi ngưỡng.
    /// </summary>
    public DateTime? DaCanhBaoLuc { get; set; }
}

public class DoiHinhAo
{
    public int MaDoiHinh { get; set; }
    public int MaNguoiDung { get; set; }
    public NguoiDung NguoiDung { get; set; }
    public string TenDoiHinh { get; set; }
    public string SoDoChienThuat { get; set; } = "4-3-3";
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    public ICollection<DoiHinhAoCauThu> ViTris { get; set; } = new List<DoiHinhAoCauThu>();
}

public class DoiHinhAoCauThu
{
    public int MaDoiHinhCauThu { get; set; }
    public int MaDoiHinh { get; set; }
    public DoiHinhAo DoiHinh { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public string ViTriTrenSan { get; set; }
    public int ThuTu { get; set; }
}

public class NhatKyDongBo
{
    public int MaNhatKy { get; set; }
    public DateTime ThoiDiemBatDau { get; set; }
    public DateTime? ThoiDiemKetThuc { get; set; }
    public string PhamVi { get; set; }
    public int SoBanGhiThemMoi { get; set; }
    public int SoBanGhiCapNhat { get; set; }
    public int SoBanGhiLoi { get; set; }
    public int SoLuotGoiApi { get; set; }
    public string TrangThai { get; set; }
    public string ThongDiep { get; set; }
}

/// <summary>Ghi nhận mỗi lần một cầu thủ được mở hồ sơ — dùng để tính xu hướng tuần (C1).</summary>
public class LuotXemCauThu
{
    public long MaLuotXem { get; set; }
    public int MaCauThu { get; set; }
    public CauThu CauThu { get; set; }
    public int? MaNguoiDung { get; set; }
    public DateTime ThoiDiem { get; set; } = DateTime.UtcNow;
}

/// <summary>C10 — một lượt chơi đoán giá đã hoàn tất.</summary>
public class LuotChoiDoanGia
{
    public int MaLuotChoi { get; set; }
    public int MaNguoiDung { get; set; }
    public NguoiDung NguoiDung { get; set; }
    public int SoCauDung { get; set; }
    public int TongSoCau { get; set; }
    public int Diem { get; set; }
    public int DoLechTrungBinh { get; set; }
    public DateTime ThoiDiem { get; set; } = DateTime.UtcNow;
}

public class NhatKyThaoTac
{
    public int MaNhatKy { get; set; }
    public int? MaNguoiDung { get; set; }
    public string HanhDong { get; set; }
    public string DoiTuong { get; set; }
    public string ChiTiet { get; set; }
    public DateTime ThoiDiem { get; set; } = DateTime.UtcNow;
}
