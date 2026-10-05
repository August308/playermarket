namespace PlayerTransfer.Application.Dtos;

public class CauThuTomTatDto
{
    public int MaCauThu { get; set; }
    public string HoTen { get; set; }
    public string AnhDaiDien { get; set; }
    public int Tuoi { get; set; }
    public int MaViTri { get; set; }
    public string ViTri { get; set; }
    public string ViTriVietTat { get; set; }
    public string CauLacBo { get; set; }
    public string GiaiDau { get; set; }
    public string QuocTich { get; set; }
    public string MaISO { get; set; }
    public decimal GiaTriHienTai { get; set; }
    public int ChiSoTongHop { get; set; }
    public bool DangChanThuong { get; set; }
}

public class CauThuChiTietDto : CauThuTomTatDto
{
    public DateTime NgaySinh { get; set; }
    public int? ChieuCao { get; set; }
    public int? CanNang { get; set; }
    public string ChanThuan { get; set; }
    public int? SoAo { get; set; }
    public DateTime? NgayHetHanHopDong { get; set; }
    public decimal GiaTriCaoNhat { get; set; }
    public decimal PhanTramThayDoi30Ngay { get; set; }
    public ChiSoDto ChiSo { get; set; }
    public int TongSoThuongVu { get; set; }
    public decimal TongPhiChuyenNhuong { get; set; }
    public int SoNgayNghiChanThuong3Mua { get; set; }

    /// <summary>
    /// Hệ thống có nguồn dữ liệu chấn thương cho cầu thủ này hay không. Bộ dữ liệu Transfermarkt
    /// không có chấn thương, nên "0 ngày nghỉ" khi KHÔNG có nguồn nghĩa là "chưa biết",
    /// không phải "chưa từng chấn thương" — giao diện phải hiển thị khác nhau.
    /// </summary>
    public bool CoDuLieuChanThuong { get; set; }

    /// <summary>
    /// Mức định giá liền trước mức hiện tại (mốc gần nhất có giá KHÁC giá hiện tại) và ngày của nó.
    /// Khi nguồn dữ liệu đã ngừng cập nhật, "thay đổi 30 ngày" luôn bằng 0; so với lần định giá
    /// trước mới cho người xem biết giá đang đi lên hay đi xuống.
    /// </summary>
    public decimal? GiaTriMocTruoc { get; set; }
    public DateTime? NgayMocTruoc { get; set; }
    public decimal? PhanTramSoVoiMocTruoc { get; set; }
}

public class ChiSoDto
{
    public int TocDo { get; set; }
    public int DutDiem { get; set; }
    public int Chuyen { get; set; }
    public int ReDat { get; set; }
    public int PhongNgu { get; set; }
    public int TheLuc { get; set; }
    public int ChiSoTongHop { get; set; }

    /// <summary>TinhTuThongKe | UocLuongTuGia | KhongCoDuLieu — xem ChiSoCauThu.</summary>
    public string NguonChiSo { get; set; }

    /// <summary>Điểm phong độ tính từ thống kê thật; chính là ChiSoTongHop của nhóm TinhTuThongKe.</summary>
    public int DiemPhongDo { get; set; }

    /// <summary>Đúng khi sáu chỉ số là số liệu dẫn xuất, không phải đo được.</summary>
    public bool LaUocLuong => NguonChiSo != "TinhTuThongKe";

    /// <summary>Đúng khi không đủ dữ liệu để hiển thị chỉ số.</summary>
    public bool ThieuDuLieu => NguonChiSo == "KhongCoDuLieu";

    public string MuaGiai { get; set; }
    public int SoTran { get; set; }
    public int SoPhutThiDau { get; set; }
    public int BanThang { get; set; }
    public int KienTao { get; set; }
    public int TheVang { get; set; }
    public int TheDo { get; set; }
}

public class ChuyenNhuongDto
{
    public int MaChuyenNhuong { get; set; }
    public string MuaGiai { get; set; }
    public DateTime NgayChuyenNhuong { get; set; }
    public string CLBDi { get; set; }
    public string CLBDen { get; set; }
    public string LoaiChuyenNhuong { get; set; }

    /// <summary>Giá trị số của LoaiChuyenNhuong (2 = tự do, 5 = chưa công bố phí) để giao diện hiển thị cột phí.</summary>
    public int MaLoai { get; set; }

    public decimal PhiChuyenNhuong { get; set; }
    public decimal GiaTriThiTruongLucDo { get; set; }
}

public class ChanThuongDto
{
    public int MaChanThuong { get; set; }
    public string MuaGiai { get; set; }
    public string LoaiChanThuong { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int SoNgayNghi { get; set; }
    public int SoTranVang { get; set; }
    public bool DangDieuTri { get; set; }
}

public class DiemGiaTriDto
{
    public DateTime Ngay { get; set; }
    public decimal GiaTri { get; set; }
}

public class BoLocCauThuDto
{
    public string TuKhoa { get; set; }
    public List<int> MaGiaiDaus { get; set; } = new();
    public List<int> MaCLBs { get; set; } = new();
    public List<int> MaViTris { get; set; } = new();
    public List<int> MaQuocGias { get; set; } = new();
    public int? TuoiTu { get; set; }
    public int? TuoiDen { get; set; }
    public decimal? GiaTriTu { get; set; }
    public decimal? GiaTriDen { get; set; }
    public int? ChieuCaoTu { get; set; }
    public string ChanThuan { get; set; }
    public bool? DangChanThuong { get; set; }
    public bool? SapHetHopDong { get; set; }
    public bool? CauThuTuDo { get; set; }
    public string SapXep { get; set; } = "giatri_giam";
    public int Trang { get; set; } = 1;
    public int KichThuocTrang { get; set; } = 20;
}

public class BangSoSanhDto
{
    public List<CauThuTomTatDto> CauThus { get; set; } = new();
    public List<HangSoSanhDto> CacHang { get; set; } = new();
}

public class HangSoSanhDto
{
    public string Khoa { get; set; }
    public string Nhan { get; set; }
    public List<string> GiaTriHienThi { get; set; } = new();
    public List<decimal?> GiaTriSo { get; set; } = new();
    public int? ViTriTotNhat { get; set; }
    public bool CaoHonLaTot { get; set; } = true;
}
