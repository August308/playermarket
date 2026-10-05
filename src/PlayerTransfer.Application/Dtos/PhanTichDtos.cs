namespace PlayerTransfer.Application.Dtos;

// ===================================================== Tính năng 1, 2, 7 — mô hình

/// <summary>Chỉ số đánh giá chất lượng mô hình dự đoán giá.</summary>
public class ChiSoMoHinhDto
{
    public int SoMauHuanLuyen { get; set; }
    public int SoMauKiemTra { get; set; }
    public int TongMau { get; set; }

    /// <summary>Sai số tuyệt đối trung bình (triệu €).</summary>
    public double MAE { get; set; }

    /// <summary>Căn bậc hai của sai số bình phương trung bình (triệu €).</summary>
    public double RMSE { get; set; }

    /// <summary>Sai số phần trăm tuyệt đối trung bình.</summary>
    public double MAPE { get; set; }

    /// <summary>Hệ số xác định trên tập kiểm tra; càng gần 1 càng tốt.</summary>
    public double R2 { get; set; }

    /// <summary>Phần trăm cầu thủ được dự đoán lệch không quá 30% so với giá thật.</summary>
    public double TyLeTrongNguong30 { get; set; }

    public DateTime HuanLuyenLuc { get; set; }
}

public class DuDoanGiaDto
{
    public int MaCauThu { get; set; }
    public string HoTen { get; set; }
    public string CauLacBo { get; set; }
    public string GiaiDau { get; set; }
    public string ViTri { get; set; }
    public int Tuoi { get; set; }

    /// <summary>Giá trị thị trường thật của Transfermarkt (triệu €).</summary>
    public decimal GiaThat { get; set; }

    /// <summary>Giá trị mô hình dự đoán (triệu €).</summary>
    public decimal GiaDuDoan { get; set; }

    /// <summary>GiaThat - GiaDuDoan. Âm nghĩa là thị trường đang định giá thấp hơn mô hình.</summary>
    public decimal ChenhLech { get; set; }

    public decimal PhanTramChenh { get; set; }
}

public class DinhGiaSaiDto
{
    public List<DuDoanGiaDto> MonHoi { get; set; } = new();
    public List<DuDoanGiaDto> BiThoiGia { get; set; } = new();
    public int SoCauThuXet { get; set; }
    public ChiSoMoHinhDto ChiSo { get; set; }
}

public class HangDoiChieuDto
{
    public string Nhan { get; set; }
    public int SoCauThu { get; set; }
    public decimal GiaThatTrungBinh { get; set; }
    public decimal GiaDuDoanTrungBinh { get; set; }
    public decimal SaiSoTuyetDoiTrungBinh { get; set; }
    public decimal SaiSoPhanTramTrungBinh { get; set; }
}

public class DoiChieuNguonDto
{
    public ChiSoMoHinhDto ChiSo { get; set; }
    public List<HangDoiChieuDto> TheoGiaiDau { get; set; } = new();
    public List<HangDoiChieuDto> TheoViTri { get; set; } = new();
    public List<HangDoiChieuDto> TheoNhomTuoi { get; set; } = new();
    public List<HangDoiChieuDto> TheoKhoangGia { get; set; } = new();
}

// ============================================================ Tính năng 3 — dự báo

public class DiemDuBaoDto
{
    public DateTime Ngay { get; set; }
    public decimal GiaTri { get; set; }
    public decimal CanDuoi { get; set; }
    public decimal CanTren { get; set; }
}

public class DuBaoGiaDto
{
    public int MaCauThu { get; set; }
    public string HoTen { get; set; }
    public decimal GiaHienTai { get; set; }
    public int SoMocLichSu { get; set; }

    /// <summary>Tốc độ thay đổi trung bình mỗi tháng, tính bằng phần trăm.</summary>
    public decimal TocDoThayDoiThang { get; set; }

    public string NhanXet { get; set; }
    public List<DiemGiaTriDto> LichSu { get; set; } = new();
    public List<DiemDuBaoDto> DuBao { get; set; } = new();
}

// ================================================== Tính năng 4 — mạng chuyển nhượng

public class CungChuyenNhuongDto
{
    public int MaCLBDi { get; set; }
    public string CLBDi { get; set; }
    public int MaCLBDen { get; set; }
    public string CLBDen { get; set; }
    public int SoThuongVu { get; set; }
    public decimal TongPhi { get; set; }
}

public class NutCauLacBoDto
{
    public int MaCLB { get; set; }
    public string TenCLB { get; set; }
    public string GiaiDau { get; set; }
    public int SoBan { get; set; }
    public int SoMua { get; set; }
    public decimal TienThu { get; set; }
    public decimal TienChi { get; set; }
}

public class MangChuyenNhuongDto
{
    public string MuaGiai { get; set; }
    public List<NutCauLacBoDto> CacNut { get; set; } = new();
    public List<CungChuyenNhuongDto> CacCung { get; set; } = new();
    public int TongThuongVu { get; set; }
    public decimal TongPhi { get; set; }
}

// ================================================ Tính năng 5 — cân đối câu lạc bộ

public class CanDoiCauLacBoDto
{
    public int MaCLB { get; set; }
    public string TenCLB { get; set; }
    public string GiaiDau { get; set; }
    public string SanNha { get; set; }

    public int SoCauThu { get; set; }
    public decimal TongGiaTriDoiHinh { get; set; }
    public double TuoiTrungBinh { get; set; }
    public int SoCauThuSapHetHopDong { get; set; }

    public decimal TongChi { get; set; }
    public decimal TongThu { get; set; }
    public decimal SoDu => TongThu - TongChi;

    public List<DiemThongKeDto> TheoMua { get; set; } = new();
    public List<ThuongVuDto> MuaDatNhat { get; set; } = new();
    public List<ThuongVuDto> BanDatNhat { get; set; } = new();
    public List<CauThuTomTatDto> DoiHinhGiaTriNhat { get; set; } = new();
}

// ==================================================== Tính năng 8 — cơ cấu độ tuổi

public class BacTuoiDto
{
    public string Nhan { get; set; }
    public int TuTuoi { get; set; }
    public int DenTuoi { get; set; }
    public int SoCauThu { get; set; }
    public decimal TongGiaTri { get; set; }
    public decimal GiaTriTrungBinh { get; set; }
}

public class CoCauTuoiDto
{
    public int MaCLB { get; set; }
    public string TenCLB { get; set; }
    public double TuoiTrungBinh { get; set; }
    public double TuoiTrungBinhTheoPhut { get; set; }
    public int SoCauThu { get; set; }
    public List<BacTuoiDto> ThapTuoi { get; set; } = new();
    public List<CauThuTomTatDto> SapHetHopDong { get; set; } = new();
    public List<CauThuTomTatDto> TruCotLonTuoi { get; set; } = new();
    public string CanhBao { get; set; }
}

// ================================================= Tính năng 9 — biến động theo tuần

public class BienDongDto
{
    public int MaCauThu { get; set; }
    public string HoTen { get; set; }
    public string CauLacBo { get; set; }
    public string AnhDaiDien { get; set; }
    public decimal GiaCu { get; set; }
    public decimal GiaMoi { get; set; }
    public decimal ThayDoi => GiaMoi - GiaCu;
    public decimal PhanTram { get; set; }
    public DateTime NgayCu { get; set; }
    public DateTime NgayMoi { get; set; }
}

public class TongHopBienDongDto
{
    public int SoNgay { get; set; }
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    /// <summary>Đúng khi kỳ được neo vào ngày có dữ liệu mới nhất (dữ liệu cũ hơn hôm nay).</summary>
    public bool NeoTheoMocDuLieu { get; set; }

    public int SoCauThuCoBienDong { get; set; }
    public List<BienDongDto> TangManhNhat { get; set; } = new();
    public List<BienDongDto> GiamManhNhat { get; set; } = new();
}

// ============================================== Tính năng 10 — kiểm chứng dữ liệu

public class MucKiemTraDto
{
    public string Ma { get; set; }
    public string TenKiemTra { get; set; }
    public string MoTa { get; set; }

    /// <summary>Tot | CanhBao | Loi</summary>
    public string MucDo { get; set; } = "Tot";

    public int SoBanGhiLoi { get; set; }
    public int TongBanGhi { get; set; }
    public List<string> ViDu { get; set; } = new();
    public string GoiYXuLy { get; set; }

    public bool Dat => SoBanGhiLoi == 0;
    public double TyLeLoi => TongBanGhi == 0 ? 0 : Math.Round(100.0 * SoBanGhiLoi / TongBanGhi, 2);
}

public class BaoCaoKiemChungDto
{
    public DateTime ThoiDiem { get; set; } = DateTime.UtcNow;
    public int TongCauThu { get; set; }
    public int TongCauLacBo { get; set; }
    public int TongGiaiDau { get; set; }
    public int TongMocGia { get; set; }
    public int TongThuongVu { get; set; }

    public Dictionary<string, int> TheoNguonDuLieu { get; set; } = new();
    public Dictionary<string, int> TheoNguonChiSo { get; set; } = new();

    public List<MucKiemTraDto> CacMuc { get; set; } = new();

    public int SoMucLoi => CacMuc.Count(x => x.MucDo == "Loi");
    public int SoMucCanhBao => CacMuc.Count(x => x.MucDo == "CanhBao");
    public bool TatCaDat => CacMuc.All(x => x.Dat);
}

// ============================================ Tính năng 6 — hỏi đáp ngôn ngữ tự nhiên

public class KetQuaHieuCauHoiDto
{
    public string CauHoiGoc { get; set; }
    public BoLocCauThuDto BoLoc { get; set; } = new();

    /// <summary>Diễn giải lại điều kiện đã hiểu được, để người dùng kiểm chứng.</summary>
    public List<string> DaHieu { get; set; } = new();

    /// <summary>Những cụm từ không nhận ra.</summary>
    public List<string> ChuaHieu { get; set; } = new();

    public bool HieuDuoc => DaHieu.Count > 0;
}
