using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Web.Models;

public class TrangChuVm
{
    /// <summary>Số giải đấu đang có cầu thủ — trước đây ghi cứng "5" trên trang chủ.</summary>
    public int SoGiaiDau { get; set; }

    public List<XuHuongDto> XuHuong { get; set; } = new();
    public List<CauThuTomTatDto> DatGiaNhat { get; set; } = new();
    public List<CauThuTomTatDto> TangGiaManh { get; set; } = new();
    public List<CauThuTomTatDto> TreTrienVong { get; set; } = new();
    public List<ThuongVuDto> ThuongVuMoi { get; set; } = new();
}

public class KhamPhaVm
{
    public BoLocCauThuDto BoLoc { get; set; } = new();
    public TrangDuLieu<CauThuTomTatDto> KetQua { get; set; } = new();
    public List<DanhMucDto> GiaiDaus { get; set; } = new();
    public List<DanhMucDto> CauLacBos { get; set; } = new();
    public List<DanhMucDto> ViTris { get; set; } = new();
    public List<DanhMucDto> QuocGias { get; set; } = new();
}

public class HoSoCauThuVm
{
    public CauThuChiTietDto CauThu { get; set; }
    public List<ChuyenNhuongDto> ChuyenNhuongs { get; set; } = new();
    public List<ChanThuongDto> ChanThuongs { get; set; } = new();
    public List<DiemGiaTriDto> LichSuGiaTri { get; set; } = new();
    public List<CauThuTomTatDto> TuongTu { get; set; } = new();
    public bool DangTheoDoi { get; set; }
    public TrungBinhViTriDto TrungBinhViTri { get; set; }
}

public class ThongKeVm
{
    public string BaoCao { get; set; } = "by-season";
    public string MuaGiai { get; set; }
    public int? MaGiaiDau { get; set; }
    public List<string> MuaGiais { get; set; } = new();
    public List<DanhMucDto> GiaiDaus { get; set; } = new();
    public List<DiemThongKeDto> DuLieu { get; set; } = new();
    public List<ThuongVuDto> ThuongVus { get; set; } = new();
    public string TieuDe { get; set; }
    public string NhanTrucY { get; set; } = "Triệu euro";
}

public class DoiHinhVm
{
    public List<DoiHinhDto> DanhSach { get; set; } = new();
    public DoiHinhDto DangChon { get; set; }
    public string SoDo { get; set; } = "4-3-3";
}

public class QuanTriCauThuVm
{
    public TrangDuLieu<CauThuTomTatDto> KetQua { get; set; } = new();
    public string TuKhoa { get; set; }
    public LuuCauThuDto DangSua { get; set; }
    public List<DanhMucDto> CauLacBos { get; set; } = new();
    public List<DanhMucDto> ViTris { get; set; } = new();
    public List<DanhMucDto> QuocGias { get; set; } = new();
}

/// <summary>Sơ đồ chiến thuật và các vị trí tương ứng trên sân.</summary>
public static class SoDoChienThuat
{
    public static readonly Dictionary<string, string[]> DanhSach = new()
    {
        ["4-3-3"] = new[] { "GK", "LB", "CB1", "CB2", "RB", "CM1", "CM2", "CM3", "LW", "ST", "RW" },
        ["4-4-2"] = new[] { "GK", "LB", "CB1", "CB2", "RB", "LM", "CM1", "CM2", "RM", "ST1", "ST2" },
        ["4-2-3-1"] = new[] { "GK", "LB", "CB1", "CB2", "RB", "CDM1", "CDM2", "LW", "CAM", "RW", "ST" },
        ["3-5-2"] = new[] { "GK", "CB1", "CB2", "CB3", "LWB", "CM1", "CM2", "CM3", "RWB", "ST1", "ST2" },
        ["5-3-2"] = new[] { "GK", "LWB", "CB1", "CB2", "CB3", "RWB", "CM1", "CM2", "CM3", "ST1", "ST2" }
    };
}

/// <summary>C2 — màn hình chế độ tuyển trạch.</summary>
public class TuyenTrachVm
{
    public TieuChiTuyenTrachDto TieuChi { get; set; } = new();
    public List<MucTieuDto> MucTieus { get; set; } = new();
    public List<DanhMucDto> ViTris { get; set; } = new();
    public List<DanhMucDto> GiaiDaus { get; set; } = new();
    public bool DaTim { get; set; }
    public string ThongBao { get; set; }
}

/// <summary>C3 — màn hình tìm cầu thủ thay thế.</summary>
public class ThayTheVm
{
    public CauThuChiTietDto CauThuGoc { get; set; }
    public decimal? NganSach { get; set; }
    public List<NguoiThayTheDto> DanhSach { get; set; } = new();
}

/// <summary>Màn hình nạp bộ dữ liệu Transfermarkt từ Kaggle.</summary>
public class NhapDatasetVm
{
    public TuyChonNhapCsvDto TuyChon { get; set; } = new();
    public List<XemTruocTepDto> CacTep { get; set; } = new();
    public KetQuaNhapCsvDto KetQua { get; set; }
}

/// <summary>Màn hình mô hình dự đoán giá và cầu thủ bị định giá sai (tính năng 1 + 2).</summary>
public class DinhGiaVm
{
    public decimal GiaToiThieu { get; set; } = 1;
    public int SoLuong { get; set; } = 15;
    public DinhGiaSaiDto KetQua { get; set; }
}

/// <summary>Màn hình hỏi đáp bằng ngôn ngữ tự nhiên (tính năng 6).</summary>
public class HoiDapVm
{
    public string CauHoi { get; set; }
    public KetQuaHieuCauHoiDto KetQuaHieu { get; set; }
    public TrangDuLieu<CauThuTomTatDto> KetQuaTim { get; set; }
}
