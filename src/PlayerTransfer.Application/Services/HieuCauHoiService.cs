using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Tính năng 6 — dịch câu hỏi tiếng Việt thành bộ lọc cầu thủ.
///
/// Cách làm: phân tích theo luật (rule-based) chứ không gọi mô hình ngôn ngữ bên ngoài,
/// để hệ thống chạy được ngoại tuyến và kết quả luôn lặp lại được — quan trọng khi
/// bảo vệ đồ án. Mỗi điều kiện nhận ra đều được diễn giải ngược lại bằng tiếng Việt
/// để người dùng tự kiểm chứng máy có hiểu đúng ý mình không.
///
/// Ví dụ câu hỏi hiểu được:
///   "tiền vệ dưới 23 tuổi giá dưới 20 triệu trên 2000 phút"
///   "hậu vệ Ngoại hạng Anh trên 25 tuổi sắp hết hợp đồng"
///   "tiền đạo Brazil thuận chân trái giá từ 10 đến 50 triệu"
/// </summary>
public class HieuCauHoiService : IHieuCauHoiService
{
    private static readonly (string[] TuKhoa, string VietTat)[] TuViTri =
    {
        (new[] { "thu mon", "thu thanh", "goalkeeper", "gk" }, "GK"),
        (new[] { "trung ve", "centre back", "cb" }, "CB"),
        (new[] { "hau ve phai", "rb" }, "RB"),
        (new[] { "hau ve trai", "lb" }, "LB"),
        (new[] { "tien ve phong ngu", "cdm" }, "CDM"),
        (new[] { "tien ve tan cong", "cam" }, "CAM"),
        (new[] { "tien ve trung tam", "cm" }, "CM"),
        (new[] { "tien dao canh phai", "rw" }, "RW"),
        (new[] { "tien dao canh trai", "lw" }, "LW"),
        (new[] { "trung phong", "st" }, "ST"),
        (new[] { "tien dao lui", "cf" }, "CF")
    };

    private static readonly (string[] TuKhoa, string Nhom)[] TuNhomViTri =
    {
        (new[] { "hau ve", "phong ngu" }, "HauVe"),
        (new[] { "tien ve" }, "TienVe"),
        (new[] { "tien dao", "tan cong" }, "TienDao"),
        (new[] { "thu mon" }, "ThuMon")
    };

    private readonly ApplicationDbContext _db;

    public HieuCauHoiService(ApplicationDbContext db) => _db = db;

    public async Task<KetQuaHieuCauHoiDto> HieuAsync(string cauHoi, CancellationToken ct = default)
    {
        var ketQua = new KetQuaHieuCauHoiDto { CauHoiGoc = cauHoi };
        if (string.IsNullOrWhiteSpace(cauHoi)) return ketQua;

        var chuoi = ChuanHoa.BoDau(cauHoi).ToLowerInvariant();
        var boLoc = ketQua.BoLoc;

        DocTuoi(chuoi, boLoc, ketQua);
        DocGiaTri(chuoi, boLoc, ketQua);
        DocChieuCao(chuoi, boLoc, ketQua);
        DocChanThuan(chuoi, boLoc, ketQua);
        DocTrangThai(chuoi, boLoc, ketQua);
        DocSapXep(chuoi, boLoc, ketQua);

        await DocViTriAsync(chuoi, boLoc, ketQua, ct);
        await DocGiaiDauAsync(chuoi, boLoc, ketQua, ct);
        await DocQuocGiaAsync(chuoi, boLoc, ketQua, ct);
        await DocCauLacBoAsync(chuoi, boLoc, ketQua, ct);

        if (ketQua.DaHieu.Count == 0)
        {
            // Không nhận ra điều kiện nào thì coi cả câu là từ khóa tìm kiếm.
            boLoc.TuKhoa = cauHoi.Trim();
            ketQua.ChuaHieu.Add("Không nhận ra điều kiện lọc nào; đã tìm theo từ khóa.");
        }

        return ketQua;
    }

    // ------------------------------------------------------------------ tuổi

    private static void DocTuoi(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        var khoang = Regex.Match(s, @"tu\s+(\d{2})\s*(?:den|toi|-)\s*(\d{2})\s*tuoi");
        if (khoang.Success)
        {
            bl.TuoiTu = int.Parse(khoang.Groups[1].Value);
            bl.TuoiDen = int.Parse(khoang.Groups[2].Value);
            kq.DaHieu.Add($"Tuổi từ {bl.TuoiTu} đến {bl.TuoiDen}");
            return;
        }

        var duoi = Regex.Match(s, @"(?:duoi|nho hon|<)\s*(\d{2})\s*tuoi");
        if (duoi.Success)
        {
            bl.TuoiDen = int.Parse(duoi.Groups[1].Value) - 1;
            kq.DaHieu.Add($"Tuổi dưới {duoi.Groups[1].Value}");
        }

        var tren = Regex.Match(s, @"(?:tren|lon hon|>)\s*(\d{2})\s*tuoi");
        if (tren.Success)
        {
            bl.TuoiTu = int.Parse(tren.Groups[1].Value) + 1;
            kq.DaHieu.Add($"Tuổi trên {tren.Groups[1].Value}");
        }

        if (Regex.IsMatch(s, @"\btre\b|tai nang tre|trien vong"))
        {
            bl.TuoiDen = Math.Min(bl.TuoiDen ?? 99, 23);
            kq.DaHieu.Add("Cầu thủ trẻ (từ 23 tuổi trở xuống)");
        }
    }

    // --------------------------------------------------------------- giá trị

    private static decimal? DocSo(string chuoi)
        => decimal.TryParse(chuoi.Replace(",", "."), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var v) ? v : null;

    private static void DocGiaTri(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        var khoang = Regex.Match(s, @"(?:gia|gia tri)?\s*tu\s*([\d.,]+)\s*(?:den|toi|-)\s*([\d.,]+)\s*(?:trieu|tr|m)\b");
        if (khoang.Success)
        {
            bl.GiaTriTu = DocSo(khoang.Groups[1].Value);
            bl.GiaTriDen = DocSo(khoang.Groups[2].Value);
            kq.DaHieu.Add($"Giá trị từ {bl.GiaTriTu} đến {bl.GiaTriDen} triệu €");
            return;
        }

        var duoi = Regex.Match(s, @"(?:gia\s*)?(?:duoi|re hon|khong qua|toi da|<)\s*([\d.,]+)\s*(?:trieu|tr|m)\b");
        if (duoi.Success)
        {
            bl.GiaTriDen = DocSo(duoi.Groups[1].Value);
            kq.DaHieu.Add($"Giá trị dưới {bl.GiaTriDen} triệu €");
        }

        var tren = Regex.Match(s, @"(?:gia\s*)?(?:tren|hon|it nhat|toi thieu|>)\s*([\d.,]+)\s*(?:trieu|tr|m)\b");
        if (tren.Success)
        {
            bl.GiaTriTu = DocSo(tren.Groups[1].Value);
            kq.DaHieu.Add($"Giá trị trên {bl.GiaTriTu} triệu €");
        }

        if (Regex.IsMatch(s, @"\bre\b|gia re|it tien|tiet kiem"))
        {
            bl.GiaTriDen = Math.Min(bl.GiaTriDen ?? 999, 10);
            kq.DaHieu.Add("Giá rẻ (dưới 10 triệu €)");
        }

        if (Regex.IsMatch(s, @"ngoi sao|dat gia|dang cap the gioi"))
        {
            bl.GiaTriTu = Math.Max(bl.GiaTriTu ?? 0, 50);
            kq.DaHieu.Add("Cầu thủ đắt giá (từ 50 triệu € trở lên)");
        }
    }

    private static void DocChieuCao(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        var m = Regex.Match(s, @"(?:cao|chieu cao)\s*(?:tren|tu|hon|>)?\s*(\d{3})\s*(?:cm)?");
        if (!m.Success) return;

        var cao = int.Parse(m.Groups[1].Value);
        if (cao is < 150 or > 215) return;

        bl.ChieuCaoTu = cao;
        kq.DaHieu.Add($"Chiều cao từ {cao} cm");
    }

    private static void DocChanThuan(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        if (Regex.IsMatch(s, @"chan trai|thuan trai"))
        {
            bl.ChanThuan = "Trai";
            kq.DaHieu.Add("Thuận chân trái");
        }
        else if (Regex.IsMatch(s, @"chan phai|thuan phai"))
        {
            bl.ChanThuan = "Phai";
            kq.DaHieu.Add("Thuận chân phải");
        }
        else if (Regex.IsMatch(s, @"hai chan|ca hai chan"))
        {
            bl.ChanThuan = "CaHai";
            kq.DaHieu.Add("Thuận cả hai chân");
        }
    }

    private static void DocTrangThai(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        if (Regex.IsMatch(s, @"sap het hop dong|het hop dong|con 1 nam"))
        {
            bl.SapHetHopDong = true;
            kq.DaHieu.Add("Sắp hết hợp đồng trong vòng 1 năm");
        }

        if (Regex.IsMatch(s, @"tu do|chuyen nhuong tu do|khong hop dong"))
        {
            bl.CauThuTuDo = true;
            kq.DaHieu.Add("Cầu thủ tự do");
        }

        if (Regex.IsMatch(s, @"khong chan thuong|dang khoe|the luc tot"))
        {
            bl.DangChanThuong = false;
            kq.DaHieu.Add("Không đang chấn thương");
        }
        else if (Regex.IsMatch(s, @"dang chan thuong|chan thuong"))
        {
            bl.DangChanThuong = true;
            kq.DaHieu.Add("Đang chấn thương");
        }
    }

    private static void DocSapXep(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq)
    {
        if (Regex.IsMatch(s, @"dat nhat|gia tri nhat|cao nhat"))
        {
            bl.SapXep = "giatri_giam";
            kq.DaHieu.Add("Sắp xếp theo giá trị giảm dần");
        }
        else if (Regex.IsMatch(s, @"re nhat|thap nhat"))
        {
            bl.SapXep = "giatri_tang";
            kq.DaHieu.Add("Sắp xếp theo giá trị tăng dần");
        }
        else if (Regex.IsMatch(s, @"hay nhat|chi so cao|gioi nhat"))
        {
            bl.SapXep = "chiso_giam";
            kq.DaHieu.Add("Sắp xếp theo chỉ số giảm dần");
        }
        else if (Regex.IsMatch(s, @"tre nhat"))
        {
            bl.SapXep = "tuoi_tang";
            kq.DaHieu.Add("Sắp xếp theo tuổi tăng dần");
        }
    }

    // --------------------------------------------------- tra cứu trong CSDL

    private async Task DocViTriAsync(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq, CancellationToken ct)
    {
        var viTris = await _db.ViTriThiDaus.AsNoTracking().ToListAsync(ct);

        // Ưu tiên vị trí cụ thể ("trung vệ") trước nhóm chung ("hậu vệ").
        foreach (var (tuKhoa, vietTat) in TuViTri)
        {
            if (!tuKhoa.Any(t => s.Contains(t))) continue;

            var vt = viTris.FirstOrDefault(x =>
                string.Equals(x.VietTat, vietTat, StringComparison.OrdinalIgnoreCase));
            if (vt == null) continue;

            bl.MaViTris.Add(vt.MaViTri);
            kq.DaHieu.Add($"Vị trí: {vt.TenViTri}");
            return;
        }

        foreach (var (tuKhoa, nhom) in TuNhomViTri)
        {
            if (!tuKhoa.Any(t => s.Contains(t))) continue;

            var trongNhom = viTris.Where(x => x.NhomViTri.ToString() == nhom).ToList();
            if (trongNhom.Count == 0) continue;

            bl.MaViTris.AddRange(trongNhom.Select(x => x.MaViTri));
            kq.DaHieu.Add($"Nhóm vị trí: {string.Join(", ", trongNhom.Select(x => x.VietTat))}");
            return;
        }
    }

    private async Task DocGiaiDauAsync(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq, CancellationToken ct)
    {
        var biDanh = new Dictionary<string, string[]>
        {
            ["Premier League"] = new[] { "ngoai hang anh", "premier league", "epl", "giai anh" },
            ["LaLiga"] = new[] { "la liga", "laliga", "giai tay ban nha" },
            ["Serie A"] = new[] { "serie a", "giai y" },
            ["Bundesliga"] = new[] { "bundesliga", "giai duc" },
            ["Ligue 1"] = new[] { "ligue 1", "giai phap" }
        };

        var giaiDaus = await _db.GiaiDaus.AsNoTracking().ToListAsync(ct);

        foreach (var gd in giaiDaus)
        {
            var tenKhongDau = ChuanHoa.BoDau(gd.TenGiaiDau).ToLowerInvariant();
            var khop = s.Contains(tenKhongDau)
                       || (biDanh.TryGetValue(gd.TenGiaiDau, out var ds) && ds.Any(t => s.Contains(t)));

            if (!khop) continue;

            bl.MaGiaiDaus.Add(gd.MaGiaiDau);
            kq.DaHieu.Add($"Giải đấu: {gd.TenGiaiDau}");
        }
    }

    private async Task DocQuocGiaAsync(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq, CancellationToken ct)
    {
        var quocGias = await _db.QuocGias.AsNoTracking()
            .Select(x => new { x.MaQuocGia, x.TenQuocGia }).ToListAsync(ct);

        foreach (var qg in quocGias)
        {
            var ten = ChuanHoa.BoDau(qg.TenQuocGia).ToLowerInvariant();
            // Tên quá ngắn dễ khớp nhầm với từ thường ("Y", "Anh"), cần ranh giới từ.
            if (ten.Length < 4)
            {
                if (!Regex.IsMatch(s, $@"\b{Regex.Escape(ten)}\b")) continue;
            }
            else if (!s.Contains(ten)) continue;

            bl.MaQuocGias.Add(qg.MaQuocGia);
            kq.DaHieu.Add($"Quốc tịch: {qg.TenQuocGia}");

            if (bl.MaQuocGias.Count >= 3) break;
        }
    }

    private async Task DocCauLacBoAsync(string s, BoLocCauThuDto bl, KetQuaHieuCauHoiDto kq, CancellationToken ct)
    {
        // Chỉ tra câu lạc bộ khi chưa xác định được giải đấu, để tránh khớp thừa.
        if (bl.MaGiaiDaus.Count > 0) return;

        var cauLacBos = await _db.CauLacBos.AsNoTracking()
            .Select(x => new { x.MaCLB, x.TenCLB }).ToListAsync(ct);

        foreach (var clb in cauLacBos)
        {
            var ten = ChuanHoa.BoDau(clb.TenCLB).ToLowerInvariant();
            if (ten.Length < 5 || !s.Contains(ten)) continue;

            bl.MaCLBs.Add(clb.MaCLB);
            kq.DaHieu.Add($"Câu lạc bộ: {clb.TenCLB}");

            if (bl.MaCLBs.Count >= 3) break;
        }
    }
}
