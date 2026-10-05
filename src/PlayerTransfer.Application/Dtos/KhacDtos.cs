namespace PlayerTransfer.Application.Dtos;

public class DanhMucDto
{
    public int Ma { get; set; }
    public string Ten { get; set; }
    public string ThongTinThem { get; set; }
}

public class DiemThongKeDto
{
    /// <summary>Mã của đối tượng (câu lạc bộ, giải đấu...) khi cần dựng liên kết.</summary>
    public int Ma { get; set; }

    public string Nhan { get; set; }
    public decimal GiaTri { get; set; }
    public decimal GiaTriPhu { get; set; }

    // Dùng cho biểu đồ hộp (báo cáo TK-05: phân bố giá trị theo vị trí).
    public decimal NhoNhat { get; set; }
    public decimal TuPhanVi1 { get; set; }
    public decimal TrungVi { get; set; }
    public decimal TuPhanVi3 { get; set; }
    public decimal LonNhat { get; set; }
}

public class ThuongVuDto
{
    public string CauThu { get; set; }
    public int MaCauThu { get; set; }
    public string MuaGiai { get; set; }
    public string CLBDi { get; set; }
    public string CLBDen { get; set; }
    public decimal PhiChuyenNhuong { get; set; }
    public decimal GiaTriThiTruongLucDo { get; set; }
    public decimal ChenhLech => PhiChuyenNhuong - GiaTriThiTruongLucDo;
}

public class TheoDoiDto
{
    public int MaTheoDoi { get; set; }
    public CauThuTomTatDto CauThu { get; set; }
    public decimal? NguongCanhBaoTren { get; set; }
    public decimal? NguongCanhBaoDuoi { get; set; }
    public string GhiChu { get; set; }
    public DateTime NgayThem { get; set; }
    public bool DaVuotNguong { get; set; }
}

public class DoiHinhDto
{
    public int MaDoiHinh { get; set; }
    public string TenDoiHinh { get; set; }
    public string SoDoChienThuat { get; set; }
    public decimal TongGiaTri { get; set; }
    public int ChiSoTrungBinh { get; set; }
    public int ChiSoGanKet { get; set; }
    public DateTime NgayTao { get; set; }
    public List<ViTriDoiHinhDto> ViTris { get; set; } = new();
}

public class ViTriDoiHinhDto
{
    public string ViTriTrenSan { get; set; }
    public int ThuTu { get; set; }
    public CauThuTomTatDto CauThu { get; set; }
}

public class LuuDoiHinhDto
{
    public int? MaDoiHinh { get; set; }
    public string TenDoiHinh { get; set; }
    public string SoDoChienThuat { get; set; }
    public List<ViTriLuuDto> ViTris { get; set; } = new();
}

public class ViTriLuuDto
{
    public string ViTriTrenSan { get; set; }
    public int ThuTu { get; set; }
    public int MaCauThu { get; set; }
}

public class LuuCauThuDto
{
    public int? MaCauThu { get; set; }
    public string HoTen { get; set; }
    public DateTime NgaySinh { get; set; }
    public int MaQuocGia { get; set; }
    public int? MaCLB { get; set; }
    public int MaViTri { get; set; }
    public decimal GiaTriHienTai { get; set; }
    public int? ChieuCao { get; set; }
    public int? CanNang { get; set; }
    public int? SoAo { get; set; }
    public string ChanThuan { get; set; }
    public DateTime? NgayHetHanHopDong { get; set; }
    public string AnhDaiDien { get; set; }
}

/// <summary>C2 — tiêu chí của chế độ tuyển trạch.</summary>
public class TieuChiTuyenTrachDto
{
    public decimal NganSach { get; set; } = 100;
    public List<int> MaViTris { get; set; } = new();
    public int? TuoiToiDa { get; set; }
    public int? ChiSoToiThieu { get; set; }
    public List<int> MaGiaiDaus { get; set; } = new();
    public bool ChiCauThuKhongChanThuong { get; set; }
    public bool UuTienSapHetHopDong { get; set; }
    public int SoLuong { get; set; } = 12;
}

/// <summary>Một mục tiêu chuyển nhượng kèm lý do được gợi ý.</summary>
public class MucTieuDto
{
    public CauThuTomTatDto CauThu { get; set; }
    public int DiemPhuHop { get; set; }
    public List<string> LyDo { get; set; } = new();
    public decimal PhanTramNganSach { get; set; }
}

/// <summary>C3 — một ứng viên thay thế kèm mức độ tương đồng.</summary>
public class NguoiThayTheDto
{
    public CauThuTomTatDto CauThu { get; set; }
    public int DoTuongDong { get; set; }
    public decimal ChenhLechGia { get; set; }
    public List<string> DiemManhHon { get; set; } = new();
    public List<string> DiemYeuHon { get; set; } = new();
}

/// <summary>C11 — chỉ số trung bình của một vị trí, để so sánh trên biểu đồ radar.</summary>
public class TrungBinhViTriDto
{
    public string ViTri { get; set; }
    public int SoCauThu { get; set; }
    public int TocDo { get; set; }
    public int DutDiem { get; set; }
    public int Chuyen { get; set; }
    public int ReDat { get; set; }
    public int PhongNgu { get; set; }
    public int TheLuc { get; set; }
}

/// <summary>C1 — cầu thủ nổi bật theo lượt xem trong tuần.</summary>
public class XuHuongDto
{
    public CauThuTomTatDto CauThu { get; set; }
    public int LuotXem { get; set; }
    public int ThuHang { get; set; }
}

/// <summary>C5 — phân tích độ gắn kết của một đội hình.</summary>
public class GanKetDto
{
    public int ChiSoGanKet { get; set; }
    public int DiemCungCLB { get; set; }
    public int DiemCungQuocTich { get; set; }
    public int DiemCungGiaiDau { get; set; }
    public int DiemDungViTri { get; set; }
    public List<string> NhanXet { get; set; } = new();
}

/// <summary>C6 — kết quả so sánh hai đội hình.</summary>
public class SoSanhDoiHinhDto
{
    public DoiHinhDto DoiA { get; set; }
    public DoiHinhDto DoiB { get; set; }
    public GanKetDto GanKetA { get; set; }
    public GanKetDto GanKetB { get; set; }
    public List<HangSoSanhDto> CacHang { get; set; } = new();
    public string KetLuan { get; set; }
}

/// <summary>C10 — một câu hỏi của trò chơi đoán giá.</summary>
public class CauHoiDoanGiaDto
{
    public int MaCauThu { get; set; }
    public string HoTen { get; set; }
    public string AnhDaiDien { get; set; }
    public int Tuoi { get; set; }
    public string ViTri { get; set; }
    public string CauLacBo { get; set; }
    public string GiaiDau { get; set; }
    public string QuocTich { get; set; }
    public string MaISO { get; set; }
    public int ChiSoTongHop { get; set; }
    public decimal GiaTriThat { get; set; }
}

/// <summary>C10 — kết quả một lượt chơi gửi lên để tính điểm.</summary>
public class KetQuaChoiDto
{
    public int SoCauDung { get; set; }
    public int TongSoCau { get; set; }
    public int Diem { get; set; }
    public int DoLechTrungBinh { get; set; }
}

/// <summary>C10 — một dòng trong bảng xếp hạng người chơi.</summary>
public class HangNguoiChoiDto
{
    public int ThuHang { get; set; }
    public string HoTen { get; set; }
    public int Diem { get; set; }
    public int SoCauDung { get; set; }
    public int TongSoCau { get; set; }
    public int DoLechTrungBinh { get; set; }
    public DateTime ThoiDiem { get; set; }
}

/// <summary>E6 — kết quả của một lần nhập dữ liệu từ Excel.</summary>
public class KetQuaNhapDto
{
    public int TongDong { get; set; }
    public int SoDongThem { get; set; }
    public int SoDongCapNhat { get; set; }
    public int SoDongBoQua { get; set; }
    public bool ChayThu { get; set; }
    public List<string> DanhSachLoi { get; set; } = new();
}

/// <summary>Thông tin một tệp CSV khi xem trước bộ dữ liệu Transfermarkt.</summary>
public class XemTruocTepDto
{
    public string TenTep { get; set; }
    public bool TonTai { get; set; }
    public double KichThuocMB { get; set; }
    public int SoDongMau { get; set; }
    public List<string> CacCot { get; set; } = new();
    public string Loi { get; set; }
}

/// <summary>Tùy chọn cho một lần nhập bộ dữ liệu Transfermarkt.</summary>
public class TuyChonNhapCsvDto
{
    /// <summary>Thư mục chứa các tệp .csv đã giải nén.</summary>
    public string ThuMuc { get; set; }

    /// <summary>Chỉ nạp 5 giải lớn (GB1, ES1, IT1, L1, FR1).</summary>
    public bool ChiNamGiaiLon { get; set; } = true;

    /// <summary>
    /// Mùa giải tối thiểu theo cột last_season của bộ dữ liệu. 2025 nghĩa là
    /// chỉ lấy cầu thủ và câu lạc bộ còn thi đấu ở mùa 2025/26. Đây là bộ lọc
    /// quan trọng nhất: bỏ nó đi thì cầu thủ đã giải nghệ vẫn hiện lên.
    /// </summary>
    public int MuaGiaiToiThieu { get; set; } = 2025;

    /// <summary>Nhãn mùa giải hiển thị, ví dụ "2025/2026".</summary>
    public string TenMuaGiai { get; set; } = "2025/2026";

    /// <summary>Xóa cầu thủ không thuộc bộ dữ liệu trước khi nạp.</summary>
    public bool XoaDuLieuCu { get; set; } = true;

    /// <summary>
    /// Xóa cả 149 cầu thủ mẫu ban đầu, không chỉ cầu thủ mô phỏng. Bật để cơ sở
    /// dữ liệu chỉ còn dữ liệu thật từ bộ dữ liệu.
    /// </summary>
    public bool XoaCaDuLieuMau { get; set; } = true;

    /// <summary>Bỏ qua cầu thủ không có giá trị thị trường trong bộ dữ liệu.</summary>
    public bool BoQuaKhongCoGia { get; set; } = true;

    public bool NapLichSuGia { get; set; } = true;
    public bool NapChuyenNhuong { get; set; } = true;
    public bool NapThongKeThiDau { get; set; } = true;

    /// <summary>Bỏ qua cầu thủ có giá thấp hơn mức này (triệu €). 0 = lấy tất cả.</summary>
    public decimal GiaToiThieuTrieuEur { get; set; }

    /// <summary>Chỉ lấy lịch sử giá trong ngần này năm gần nhất. 0 = lấy tất cả.</summary>
    public int SoNamLichSuGia { get; set; } = 5;

    /// <summary>
    /// Số phút thi đấu tối thiểu trong mùa để tính chỉ số. Dưới mức này thì đánh
    /// dấu thiếu dữ liệu thay vì cho ra con số không đáng tin.
    /// </summary>
    public int PhutToiThieuTinhChiSo { get; set; } = 270;
}

/// <summary>Kết quả một lần nhập bộ dữ liệu Transfermarkt.</summary>
public class KetQuaNhapCsvDto
{
    public DateTime BatDau { get; set; }
    public DateTime KetThuc { get; set; }
    public double SoGiay => (KetThuc - BatDau).TotalSeconds;

    public int SoCauThuDaXoa { get; set; }
    public int ChanThuongDaXoa { get; set; }
    public long SoDongDaDoc { get; set; }

    public int QuocGiaThemMoi { get; set; }
    public int GiaiDauThemMoi { get; set; }
    public int GiaiDauCapNhat { get; set; }
    public int CauLacBoThemMoi { get; set; }
    public int CauLacBoCapNhat { get; set; }
    public int CauThuThemMoi { get; set; }
    public int CauThuCapNhat { get; set; }
    public int CauThuBoQua { get; set; }
    public int CauThuThieuGia { get; set; }
    public int CauThuThieuThongKe { get; set; }
    public int MocGiaThemMoi { get; set; }
    public int ChuyenNhuongThemMoi { get; set; }
    public int ChuyenNhuongTuongLaiBoQua { get; set; }
    public int ChiSoTinhTuThongKe { get; set; }
    public int GiaCaoNhatSuaLai { get; set; }

    public int TongThemMoi => QuocGiaThemMoi + GiaiDauThemMoi + CauLacBoThemMoi
                              + CauThuThemMoi + MocGiaThemMoi + ChuyenNhuongThemMoi;
    public int TongCapNhat => GiaiDauCapNhat + CauLacBoCapNhat + CauThuCapNhat + ChiSoTinhTuThongKe;

    public List<string> DanhSachLoi { get; set; } = new();

    /// <summary>Ghi thêm một cảnh báo, giữ tối đa 100 dòng để không phình bộ nhớ.</summary>
    public void ThemLoi(string noiDung)
    {
        if (DanhSachLoi.Count < 100) DanhSachLoi.Add(noiDung);
    }
}
