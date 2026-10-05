using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Tính năng 1, 2, 3, 7 — mô hình dự đoán giá trị cầu thủ và các ứng dụng của nó.
///
/// Mô hình: hồi quy tuyến tính bội trên LOGARIT của giá trị thị trường.
/// Lấy log vì giá trị cầu thủ phân bố lệch phải rất mạnh (đa số dưới 5 triệu €,
/// một số ít trên 100 triệu €); hồi quy thẳng trên giá sẽ bị vài ngôi sao kéo lệch
/// toàn bộ đường hồi quy. Sau khi dự đoán log thì lũy thừa ngược lại.
///
/// Đặc trưng đầu vào đều là số liệu THẬT từ bộ dữ liệu Transfermarkt:
///   tuổi, tuổi², log(số phút), bàn/90, kiến tạo/90, số trận, thẻ/trận,
///   nhóm vị trí (mã hóa one-hot, thủ môn là mốc so sánh),
///   giải đấu (one-hot, Ligue 1 là mốc so sánh).
///
/// Chia 80% huấn luyện / 20% kiểm tra theo mã cầu thủ để kết quả lặp lại được.
/// </summary>
public class DuDoanGiaService : IDuDoanGiaService
{
    private const string KhoaDem = "mohinh:dudoangia";
    private const int SoDacTrung = 13;

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _dem;
    private readonly ILogger<DuDoanGiaService> _log;

    public DuDoanGiaService(ApplicationDbContext db, IMemoryCache dem, ILogger<DuDoanGiaService> log)
    {
        _db = db; _dem = dem; _log = log;
    }

    // ------------------------------------------------------------- mẫu dữ liệu

    private sealed class MauHuanLuyen
    {
        public int MaCauThu;
        public string HoTen;
        public string CauLacBo;
        public string GiaiDau;
        public string ViTri;
        public int Tuoi;
        public decimal GiaThat;
        public double[] DacTrung;
    }

    /// <summary>Mô hình đã huấn luyện cùng chỉ số đánh giá, giữ trong bộ đệm 30 phút.</summary>
    private sealed class MoHinh
    {
        public double[] Beta;
        public ChiSoMoHinhDto ChiSo;
        public List<MauHuanLuyen> ToanBoMau;
    }

    private async Task<MoHinh> LayMoHinhAsync(CancellationToken ct)
    {
        if (_dem.TryGetValue(KhoaDem, out MoHinh daCo) && daCo != null) return daCo;

        var moHinh = await HuanLuyenAsync(ct);
        if (moHinh != null)
        {
            _dem.Set(KhoaDem, moHinh, TimeSpan.FromMinutes(30));
        }
        return moHinh;
    }

    /// <summary>Nạp mẫu: chỉ lấy cầu thủ có giá thật và có số liệu thi đấu thật.</summary>
    private async Task<List<MauHuanLuyen>> NapMauAsync(CancellationToken ct)
    {
        var tho = await _db.CauThus.AsNoTracking()
            .Where(x => x.GiaTriHienTai > 0
                        && x.ChiSo != null
                        && x.ChiSo.NguonChiSo == "TinhTuThongKe"
                        && x.ChiSo.SoPhutThiDau > 0)
            .Select(x => new
            {
                x.MaCauThu,
                x.HoTen,
                x.NgaySinh,
                x.GiaTriHienTai,
                CauLacBo = x.CauLacBo != null ? x.CauLacBo.TenCLB : null,
                GiaiDau = x.CauLacBo != null && x.CauLacBo.GiaiDau != null ? x.CauLacBo.GiaiDau.TenGiaiDau : null,
                TenViTri = x.ViTri.TenViTri,
                Nhom = x.ViTri.NhomViTri,
                x.ChiSo.SoTran,
                x.ChiSo.SoPhutThiDau,
                x.ChiSo.BanThang,
                x.ChiSo.KienTao,
                x.ChiSo.TheVang,
                x.ChiSo.TheDo
            })
            .ToListAsync(ct);

        var ketQua = new List<MauHuanLuyen>(tho.Count);

        foreach (var x in tho)
        {
            var tuoi = MapperHelper.TinhTuoi(x.NgaySinh);
            if (tuoi is < 15 or > 45) continue;

            ketQua.Add(new MauHuanLuyen
            {
                MaCauThu = x.MaCauThu,
                HoTen = x.HoTen,
                CauLacBo = x.CauLacBo,
                GiaiDau = x.GiaiDau,
                ViTri = x.TenViTri,
                Tuoi = tuoi,
                GiaThat = x.GiaTriHienTai,
                DacTrung = TaoDacTrung(tuoi, x.SoPhutThiDau, x.SoTran, x.BanThang,
                    x.KienTao, x.TheVang, x.TheDo, x.Nhom, x.GiaiDau)
            });
        }

        return ketQua;
    }

    /// <summary>Vector đặc trưng 13 chiều. Thứ tự phải giữ nguyên giữa huấn luyện và dự đoán.</summary>
    private static double[] TaoDacTrung(int tuoi, int soPhut, int soTran, int banThang,
        int kienTao, int theVang, int theDo, NhomViTri nhom, string giaiDau)
    {
        var so90 = Math.Max(soPhut / 90.0, 0.5);
        var ten = giaiDau ?? string.Empty;

        return new[]
        {
            tuoi,                                           // 0  tuổi
            tuoi * (double)tuoi,                            // 1  tuổi² — đỉnh phong độ là đường cong, không phải đường thẳng
            Math.Log(soPhut + 1),                           // 2  log số phút
            banThang / so90,                                // 3  bàn / 90 phút
            kienTao / so90,                                 // 4  kiến tạo / 90 phút
            soTran,                                         // 5  số trận
            (theVang + 3.0 * theDo) / Math.Max(1, soTran),  // 6  thẻ / trận
            nhom == NhomViTri.HauVe ? 1 : 0,                // 7  one-hot nhóm vị trí
            nhom == NhomViTri.TienVe ? 1 : 0,               // 8     (thủ môn là mốc)
            nhom == NhomViTri.TienDao ? 1 : 0,              // 9
            ten.Contains("Premier", StringComparison.OrdinalIgnoreCase) ? 1 : 0,   // 10 one-hot giải
            ten.Contains("LaLiga", StringComparison.OrdinalIgnoreCase) ? 1 : 0,    // 11    (Ligue 1 là mốc)
            ten.Contains("Serie A", StringComparison.OrdinalIgnoreCase)
                || ten.Contains("Bundesliga", StringComparison.OrdinalIgnoreCase) ? 1 : 0   // 12
        };
    }

    // -------------------------------------------------- tính năng 1: huấn luyện

    public async Task<KetQua<ChiSoMoHinhDto>> HuanLuyenLaiAsync(CancellationToken ct = default)
    {
        _dem.Remove(KhoaDem);
        var moHinh = await HuanLuyenAsync(ct);

        return moHinh == null
            ? KetQua<ChiSoMoHinhDto>.Loi("THIEU_DU_LIEU",
                "Chưa đủ dữ liệu để huấn luyện. Cần ít nhất 100 cầu thủ có giá thật và số liệu thi đấu thật — " +
                "hãy nạp bộ dữ liệu Transfermarkt kèm tệp appearances.csv trước.")
            : KetQua<ChiSoMoHinhDto>.Ok(moHinh.ChiSo, "Đã huấn luyện xong mô hình.");
    }

    private async Task<MoHinh> HuanLuyenAsync(CancellationToken ct)
    {
        var mau = await NapMauAsync(ct);
        if (mau.Count < 100)
        {
            _log.LogWarning("Chỉ có {So} mẫu, không đủ để huấn luyện mô hình dự đoán giá.", mau.Count);
            return null;
        }

        // Chia tập theo phần dư của mã cầu thủ: cố định, không phụ thuộc thứ tự truy vấn.
        var tapHuan = mau.Where(x => x.MaCauThu % 5 != 0).ToList();
        var tapKiem = mau.Where(x => x.MaCauThu % 5 == 0).ToList();
        if (tapKiem.Count < 20) { tapKiem = mau; tapHuan = mau; }

        var X = tapHuan.Select(x => x.DacTrung).ToArray();
        var y = tapHuan.Select(x => Math.Log((double)x.GiaThat)).ToArray();

        var beta = HoiQuyTuyenTinh.Huan(X, y);
        if (beta == null)
        {
            _log.LogError("Không giải được phương trình chuẩn — ma trận đặc trưng suy biến.");
            return null;
        }

        var chiSo = DanhGia(beta, tapHuan, tapKiem, mau.Count);

        return new MoHinh { Beta = beta, ChiSo = chiSo, ToanBoMau = mau };
    }

    private static ChiSoMoHinhDto DanhGia(double[] beta,
        List<MauHuanLuyen> tapHuan, List<MauHuanLuyen> tapKiem, int tongMau)
    {
        double tongSaiSo = 0, tongBinhPhuong = 0, tongSaiSoPhanTram = 0;
        var trongNguong = 0;

        var thuc = new double[tapKiem.Count];
        var duDoan = new double[tapKiem.Count];

        for (var i = 0; i < tapKiem.Count; i++)
        {
            var m = tapKiem[i];
            var gia = DuDoanMotMau(beta, m.DacTrung);

            thuc[i] = (double)m.GiaThat;
            duDoan[i] = gia;

            var saiSo = Math.Abs(gia - (double)m.GiaThat);
            tongSaiSo += saiSo;
            tongBinhPhuong += saiSo * saiSo;

            if (m.GiaThat > 0)
            {
                var phanTram = saiSo / (double)m.GiaThat;
                tongSaiSoPhanTram += phanTram;
                if (phanTram <= 0.30) trongNguong++;
            }
        }

        var n = Math.Max(1, tapKiem.Count);

        return new ChiSoMoHinhDto
        {
            SoMauHuanLuyen = tapHuan.Count,
            SoMauKiemTra = tapKiem.Count,
            TongMau = tongMau,
            MAE = Math.Round(tongSaiSo / n, 2),
            RMSE = Math.Round(Math.Sqrt(tongBinhPhuong / n), 2),
            MAPE = Math.Round(100 * tongSaiSoPhanTram / n, 1),
            R2 = Math.Round(HoiQuyTuyenTinh.TinhR2(thuc, duDoan), 4),
            TyLeTrongNguong30 = Math.Round(100.0 * trongNguong / n, 1),
            HuanLuyenLuc = DateTime.UtcNow
        };
    }

    private static double DuDoanMotMau(double[] beta, double[] dacTrung)
    {
        var logGia = HoiQuyTuyenTinh.DuDoan(beta, dacTrung);
        // Chặn hai đầu để một mẫu ngoại lai không cho ra con số vô lý.
        return Math.Clamp(Math.Exp(logGia), 0.05, 400);
    }

    public async Task<ChiSoMoHinhDto> LayChiSoMoHinhAsync(CancellationToken ct = default)
        => (await LayMoHinhAsync(ct))?.ChiSo;

    public async Task<KetQua<DuDoanGiaDto>> DuDoanChoCauThuAsync(int maCauThu, CancellationToken ct = default)
    {
        var moHinh = await LayMoHinhAsync(ct);
        if (moHinh == null)
            return KetQua<DuDoanGiaDto>.Loi("CHUA_CO_MO_HINH", "Mô hình chưa được huấn luyện.");

        var mau = moHinh.ToanBoMau.FirstOrDefault(x => x.MaCauThu == maCauThu);
        if (mau == null)
            return KetQua<DuDoanGiaDto>.Loi("KHONG_DU_DU_LIEU",
                "Cầu thủ này chưa có đủ số liệu thi đấu thật để mô hình đưa ra dự đoán.");

        return KetQua<DuDoanGiaDto>.Ok(TaoDuDoan(moHinh.Beta, mau));
    }

    private static DuDoanGiaDto TaoDuDoan(double[] beta, MauHuanLuyen m)
    {
        var duDoan = (decimal)Math.Round(DuDoanMotMau(beta, m.DacTrung), 2);
        var chenhLech = m.GiaThat - duDoan;

        return new DuDoanGiaDto
        {
            MaCauThu = m.MaCauThu,
            HoTen = m.HoTen,
            CauLacBo = m.CauLacBo,
            GiaiDau = m.GiaiDau,
            ViTri = m.ViTri,
            Tuoi = m.Tuoi,
            GiaThat = m.GiaThat,
            GiaDuDoan = duDoan,
            ChenhLech = chenhLech,
            PhanTramChenh = m.GiaThat > 0
                ? Math.Round(100 * chenhLech / m.GiaThat, 1)
                : 0
        };
    }

    // ------------------------------------- tính năng 2: cầu thủ bị định giá sai

    public async Task<KetQua<DinhGiaSaiDto>> TimDinhGiaSaiAsync(int soLuong, decimal giaToiThieu,
        CancellationToken ct = default)
    {
        var moHinh = await LayMoHinhAsync(ct);
        if (moHinh == null)
            return KetQua<DinhGiaSaiDto>.Loi("CHUA_CO_MO_HINH",
                "Mô hình chưa được huấn luyện — hãy nạp bộ dữ liệu trước.");

        var soLay = Math.Clamp(soLuong, 1, 50);

        var tatCa = moHinh.ToanBoMau
            .Where(x => x.GiaThat >= giaToiThieu)
            .Select(x => TaoDuDoan(moHinh.Beta, x))
            .ToList();

        return KetQua<DinhGiaSaiDto>.Ok(new DinhGiaSaiDto
        {
            // Giá thật THẤP hơn dự đoán nhiều nhất → mô hình cho rằng đang rẻ.
            MonHoi = tatCa.OrderBy(x => x.PhanTramChenh).Take(soLay).ToList(),
            // Giá thật CAO hơn dự đoán nhiều nhất → mô hình cho rằng đang đắt.
            BiThoiGia = tatCa.OrderByDescending(x => x.PhanTramChenh).Take(soLay).ToList(),
            SoCauThuXet = tatCa.Count,
            ChiSo = moHinh.ChiSo
        });
    }

    // ------------------------------------------- tính năng 7: đối chiếu hai nguồn

    public async Task<KetQua<DoiChieuNguonDto>> DoiChieuNguonAsync(CancellationToken ct = default)
    {
        var moHinh = await LayMoHinhAsync(ct);
        if (moHinh == null)
            return KetQua<DoiChieuNguonDto>.Loi("CHUA_CO_MO_HINH", "Mô hình chưa được huấn luyện.");

        var duDoans = moHinh.ToanBoMau.Select(x => TaoDuDoan(moHinh.Beta, x)).ToList();

        List<HangDoiChieuDto> Gom(Func<DuDoanGiaDto, string> chonKhoa)
            => duDoans
                .Where(x => !string.IsNullOrWhiteSpace(chonKhoa(x)))
                .GroupBy(chonKhoa)
                .Select(g => new HangDoiChieuDto
                {
                    Nhan = g.Key,
                    SoCauThu = g.Count(),
                    GiaThatTrungBinh = Math.Round(g.Average(x => x.GiaThat), 2),
                    GiaDuDoanTrungBinh = Math.Round(g.Average(x => x.GiaDuDoan), 2),
                    SaiSoTuyetDoiTrungBinh = Math.Round(g.Average(x => Math.Abs(x.ChenhLech)), 2),
                    SaiSoPhanTramTrungBinh = Math.Round(g.Average(x => Math.Abs(x.PhanTramChenh)), 1)
                })
                .OrderByDescending(x => x.SoCauThu)
                .ToList();

        return KetQua<DoiChieuNguonDto>.Ok(new DoiChieuNguonDto
        {
            ChiSo = moHinh.ChiSo,
            TheoGiaiDau = Gom(x => x.GiaiDau),
            TheoViTri = Gom(x => x.ViTri),
            TheoNhomTuoi = Gom(x => x.Tuoi switch
            {
                < 21 => "Dưới 21",
                < 25 => "21 - 24",
                < 29 => "25 - 28",
                < 33 => "29 - 32",
                _ => "33 trở lên"
            }),
            TheoKhoangGia = Gom(x => x.GiaThat switch
            {
                < 1 => "Dưới 1 tr €",
                < 5 => "1 - 5 tr €",
                < 20 => "5 - 20 tr €",
                < 50 => "20 - 50 tr €",
                _ => "Trên 50 tr €"
            })
        });
    }

    // ------------------------------------------------- tính năng 3: dự báo giá

    public async Task<KetQua<DuBaoGiaDto>> DuBaoAsync(int maCauThu, int soThang, CancellationToken ct = default)
    {
        var soThangDuBao = Math.Clamp(soThang, 1, 24);

        var lichSu = await _db.LichSuGiaTris.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu)
            .OrderBy(x => x.NgayGhiNhan)
            .Select(x => new { x.NgayGhiNhan, x.GiaTri })
            .ToListAsync(ct);

        if (lichSu.Count < 4)
            return KetQua<DuBaoGiaDto>.Loi("THIEU_LICH_SU",
                "Cần ít nhất 4 mốc giá trong lịch sử mới dự báo được.");

        var cauThu = await _db.CauThus.AsNoTracking()
            .Where(x => x.MaCauThu == maCauThu)
            .Select(x => new { x.HoTen, x.NgaySinh, x.GiaTriHienTai })
            .FirstOrDefaultAsync(ct);

        if (cauThu == null)
            return KetQua<DuBaoGiaDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy cầu thủ.");

        // Hồi quy tuyến tính trên log(giá) theo trục thời gian tính bằng tháng.
        var goc = lichSu[0].NgayGhiNhan;
        var X = lichSu.Select(x => new[] { (x.NgayGhiNhan - goc).TotalDays / 30.0 }).ToArray();
        var y = lichSu.Select(x => Math.Log(Math.Max((double)x.GiaTri, 0.05))).ToArray();

        var beta = HoiQuyTuyenTinh.Huan(X, y);
        if (beta == null)
            return KetQua<DuBaoGiaDto>.Loi("KHONG_TINH_DUOC", "Không dựng được đường xu hướng.");

        // Độ lệch chuẩn phần dư → dải tin cậy khoảng 80% (±1,28σ).
        double tongDu = 0;
        for (var i = 0; i < lichSu.Count; i++)
        {
            var du = y[i] - HoiQuyTuyenTinh.DuDoan(beta, X[i]);
            tongDu += du * du;
        }
        var sigma = Math.Sqrt(tongDu / Math.Max(1, lichSu.Count - 2));

        var thangCuoi = X[^1][0];
        var ketQua = new DuBaoGiaDto
        {
            MaCauThu = maCauThu,
            HoTen = cauThu.HoTen,
            GiaHienTai = cauThu.GiaTriHienTai,
            SoMocLichSu = lichSu.Count,
            // Hệ số tháng đổi ra phần trăm thay đổi mỗi tháng.
            TocDoThayDoiThang = Math.Round((decimal)((Math.Exp(beta[1]) - 1) * 100), 2),
            LichSu = lichSu.Select(x => new DiemGiaTriDto { Ngay = x.NgayGhiNhan, GiaTri = x.GiaTri }).ToList()
        };

        for (var thang = 1; thang <= soThangDuBao; thang++)
        {
            var x = thangCuoi + thang;
            var logGia = HoiQuyTuyenTinh.DuDoan(beta, new[] { x });

            // Dải tin cậy nới dần theo khoảng cách dự báo.
            var noi = sigma * 1.28 * Math.Sqrt(1 + thang / 6.0);

            ketQua.DuBao.Add(new DiemDuBaoDto
            {
                Ngay = lichSu[^1].NgayGhiNhan.AddMonths(thang),
                GiaTri = (decimal)Math.Round(Math.Clamp(Math.Exp(logGia), 0.05, 400), 2),
                CanDuoi = (decimal)Math.Round(Math.Clamp(Math.Exp(logGia - noi), 0.05, 400), 2),
                CanTren = (decimal)Math.Round(Math.Clamp(Math.Exp(logGia + noi), 0.05, 400), 2)
            });
        }

        var cuoi = ketQua.DuBao[^1];
        ketQua.NhanXet = ketQua.TocDoThayDoiThang switch
        {
            > 1.5m => $"Xu hướng tăng rõ: khoảng {ketQua.TocDoThayDoiThang}%/tháng.",
            > 0.3m => "Xu hướng tăng nhẹ.",
            < -1.5m => $"Xu hướng giảm rõ: khoảng {ketQua.TocDoThayDoiThang}%/tháng.",
            < -0.3m => "Xu hướng giảm nhẹ.",
            _ => "Giá trị đi ngang."
        } + string.Create(DinhDangSo.VN, $" Dự báo sau {soThangDuBao} tháng: {cuoi.GiaTri:N1} tr € ") +
            string.Create(DinhDangSo.VN, $"(khoảng tin cậy {cuoi.CanDuoi:N1} - {cuoi.CanTren:N1}).");

        return KetQua<DuBaoGiaDto>.Ok(ketQua);
    }
}
