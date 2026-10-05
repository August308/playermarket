using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Sửa những dữ liệu sai đã lỡ ghi vào cơ sở dữ liệu ở các phiên bản trước.
/// Chạy mỗi lần khởi động API, ngay sau khi nạp dữ liệu mẫu. Mọi bước đều idempotent:
/// chạy lại trên dữ liệu đã sạch thì không thay đổi gì.
/// </summary>
public static class DonDepDuLieuService
{
    /// <summary>
    /// Dự án tạo cơ sở dữ liệu bằng EnsureCreated (không dùng migration), nên cơ sở dữ liệu đã có
    /// sẽ KHÔNG tự có cột mới. Bước này thêm các cột còn thiếu; phải chạy trước mọi truy vấn khác.
    /// </summary>
    public static async Task NangCapLuocDoAsync(ApplicationDbContext db, ILogger log, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return;

        // (bảng, cột, kiểu SQLite, kiểu SQL Server)
        var cotCanCo = new[]
        {
            ("TheoDoiCauThus", "DaCanhBaoLuc", "TEXT NULL", "datetime2 NULL"),
            ("LichSuChuyenNhuongs", "TenCLBDiNgoai", "TEXT NULL", "nvarchar(150) NULL"),
            ("LichSuChuyenNhuongs", "TenCLBDenNgoai", "TEXT NULL", "nvarchar(150) NULL")
        };

        foreach (var (bang, cot, kieuSqlite, kieuSqlServer) in cotCanCo)
        {
            if (await CoCotAsync(db, bang, cot, ct)) continue;

            var laSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            var sql = $"ALTER TABLE \"{bang}\" ADD \"{cot}\" {(laSqlite ? kieuSqlite : kieuSqlServer)}";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            log.LogInformation("Nâng cấp lược đồ: thêm cột {Bang}.{Cot}.", bang, cot);
        }
    }

    private static async Task<bool> CoCotAsync(ApplicationDbContext db, string bang, string cot, CancellationToken ct)
    {
        var ketNoi = db.Database.GetDbConnection();
        var daMo = ketNoi.State == System.Data.ConnectionState.Open;
        if (!daMo) await ketNoi.OpenAsync(ct);

        try
        {
            await using var lenh = ketNoi.CreateCommand();
            var laSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            lenh.CommandText = laSqlite
                ? $"SELECT COUNT(*) FROM pragma_table_info('{bang}') WHERE name = '{cot}'"
                : $"SELECT CASE WHEN COL_LENGTH('{bang}', '{cot}') IS NULL THEN 0 ELSE 1 END";
            var ketQua = await lenh.ExecuteScalarAsync(ct);
            return Convert.ToInt32(ketQua) > 0;
        }
        finally
        {
            if (!daMo) await ketNoi.CloseAsync();
        }
    }

    public static async Task ChayAsync(ApplicationDbContext db, ILogger log, CancellationToken ct = default)
    {
        var soMocPhang = await XoaMocGiaPhangAsync(db, ct);
        if (soMocPhang > 0)
            log.LogInformation("Dọn dữ liệu: xóa {So} mốc giá chụp hằng ngày không đổi giá.", soMocPhang);

        var (soCLB, soGiai) = await XoaDanhMucMauBoTrongAsync(db, ct);
        if (soCLB + soGiai > 0)
            log.LogInformation("Dọn dữ liệu: xóa {CLB} câu lạc bộ và {Giai} giải đấu mẫu không còn dùng.", soCLB, soGiai);

        var soTenCLB = await RutGonTenCauLacBoAsync(db, ct);
        if (soTenCLB > 0)
            log.LogInformation("Dọn dữ liệu: rút gọn tên pháp nhân của {So} câu lạc bộ.", soTenCLB);

        var soQuocGia = await ChuanHoaQuocGiaAsync(db, ct);
        if (soQuocGia > 0)
            log.LogInformation("Dọn dữ liệu: chuẩn hóa tên tiếng Việt và mã cờ cho {So} quốc gia.", soQuocGia);

        var soNhatKy = await SuaNhatKyDongBoSaiAsync(db, ct);
        if (soNhatKy > 0)
            log.LogInformation("Dọn dữ liệu: sửa {So} dòng nhật ký đồng bộ ghi sai trạng thái.", soNhatKy);
    }

    /// <summary>
    /// Sau khi nạp bộ dữ liệu Transfermarkt, các câu lạc bộ và giải đấu của DỮ LIỆU MẪU cũ vẫn nằm lại
    /// dù không còn cầu thủ nào: "Arsenal" cạnh "Arsenal FC", "La Liga" cạnh "LaLiga"... Bộ lọc hiện
    /// trùng và chọn bản cũ thì ra 0 kết quả. Chỉ xóa bản ghi KHÔNG có mã nguồn ngoài, KHÔNG có cầu thủ
    /// và KHÔNG được thương vụ nào tham chiếu — tức là chắc chắn không còn dùng.
    /// </summary>
    public static async Task<(int SoCLB, int SoGiai)> XoaDanhMucMauBoTrongAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var clbBoTrong = await db.CauLacBos
            .Where(c => c.MaTransfermarkt == null && c.MaNgoaiApi == null)
            .Where(c => !db.CauThus.Any(x => x.MaCLB == c.MaCLB))
            .Where(c => !db.LichSuChuyenNhuongs.Any(x => x.MaCLBDi == c.MaCLB || x.MaCLBDen == c.MaCLB))
            .ToListAsync(ct);

        if (clbBoTrong.Count > 0)
        {
            db.CauLacBos.RemoveRange(clbBoTrong);
            await db.SaveChangesAsync(ct);
        }

        var giaiBoTrong = await db.GiaiDaus
            .Where(g => g.MaTransfermarkt == null && g.MaNgoaiApi == null)
            .Where(g => !db.CauLacBos.Any(c => c.MaGiaiDau == g.MaGiaiDau))
            .ToListAsync(ct);

        if (giaiBoTrong.Count > 0)
        {
            db.GiaiDaus.RemoveRange(giaiBoTrong);
            await db.SaveChangesAsync(ct);
        }

        return (clbBoTrong.Count, giaiBoTrong.Count);
    }

    /// <summary>
    /// Đổi tên đăng ký pháp nhân ("Società Sportiva Lazio S.p.A.") về tên thường gọi ("SS Lazio").
    /// Bỏ qua nếu tên rút gọn đã thuộc về một câu lạc bộ khác.
    /// </summary>
    public static async Task<int> RutGonTenCauLacBoAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var tatCa = await db.CauLacBos.ToListAsync(ct);
        var tenDaCo = new HashSet<string>(tatCa.Select(x => x.TenCLB), StringComparer.OrdinalIgnoreCase);
        var soDoi = 0;

        foreach (var clb in tatCa)
        {
            var gon = PlayerTransfer.Application.Common.TenCauLacBo.RutGon(clb.TenCLB);
            if (gon == clb.TenCLB || tenDaCo.Contains(gon)) continue;

            tenDaCo.Remove(clb.TenCLB);
            clb.TenCLB = gon;
            tenDaCo.Add(gon);
            soDoi++;
        }

        if (soDoi > 0) await db.SaveChangesAsync(ct);
        return soDoi;
    }

    /// <summary>
    /// Danh mục quốc gia nạp từ Transfermarkt có ~86 nước giữ tên tiếng Anh (thậm chí tiếng Đức:
    /// "Neukaledonien") và không có mã cờ, nằm lẫn với các nước đã Việt hóa ("Na Uy", "Tây Ban Nha").
    /// Bước này đổi sang tên tiếng Việt + mã cờ theo <see cref="BanDoQuocGia"/>; nếu tên tiếng Việt
    /// đã thuộc về một bản ghi khác thì gộp vào bản ghi đó.
    /// </summary>
    public static async Task<int> ChuanHoaQuocGiaAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var tatCa = await db.QuocGias.ToListAsync(ct);
        var theoTen = new Dictionary<string, PlayerTransfer.Domain.Entities.QuocGia>(StringComparer.OrdinalIgnoreCase);
        foreach (var qg in tatCa) theoTen.TryAdd(qg.TenQuocGia, qg);

        var soDoi = 0;

        foreach (var qg in tatCa.Where(x => string.IsNullOrWhiteSpace(x.MaISO)).ToList())
        {
            var (tenViet, maCo) = BanDoQuocGia.Tra(qg.TenQuocGia);
            if (maCo == null) continue;                     // không có trong bảng đối chiếu

            if (theoTen.TryGetValue(tenViet, out var trung) && trung != qg)
            {
                // Tên tiếng Việt đã có bản ghi khác: chuyển cầu thủ (nếu có) sang đó rồi xóa bản trùng.
                var cauThus = await db.CauThus.Where(x => x.MaQuocGia == qg.MaQuocGia).ToListAsync(ct);
                foreach (var c in cauThus) c.MaQuocGia = trung.MaQuocGia;
                if (string.IsNullOrWhiteSpace(trung.MaISO)) trung.MaISO = maCo;

                // Giải đấu cũng tham chiếu quốc gia.
                var giais = await db.GiaiDaus.Where(x => x.MaQuocGia == qg.MaQuocGia).ToListAsync(ct);
                foreach (var g in giais) g.MaQuocGia = trung.MaQuocGia;

                await db.SaveChangesAsync(ct);
                db.QuocGias.Remove(qg);
                theoTen.Remove(qg.TenQuocGia);
            }
            else
            {
                theoTen.Remove(qg.TenQuocGia);
                qg.TenQuocGia = tenViet;
                qg.MaISO = maCo;
                theoTen[tenViet] = qg;
            }

            soDoi++;
        }

        if (soDoi > 0) await db.SaveChangesAsync(ct);
        return soDoi;
    }

    /// <summary>
    /// Phiên bản cũ nuốt mất lỗi của API bóng đá nên ghi "ThanhCong" cho những lượt không lấy được gì.
    /// Một lượt có gọi API mà không thêm/cập nhật nổi một bản ghi nào (kể cả giải đấu — bước đầu tiên,
    /// luôn được cập nhật nếu API trả dữ liệu) chắc chắn đã thất bại.
    /// </summary>
    public static async Task<int> SuaNhatKyDongBoSaiAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var saiTrangThai = await db.NhatKyDongBos
            .Where(x => x.TrangThai == "ThanhCong" && x.PhamVi != "NhapCsvTransfermarkt"
                        && x.SoLuotGoiApi > 0 && x.SoBanGhiThemMoi == 0 && x.SoBanGhiCapNhat == 0)
            .ToListAsync(ct);

        foreach (var nk in saiTrangThai)
        {
            nk.TrangThai = "ThatBai";
            nk.SoBanGhiLoi = Math.Max(nk.SoBanGhiLoi, nk.SoLuotGoiApi);
            nk.ThongDiep = "(Ghi lại) API bóng đá đã từ chối mọi lượt gọi — thường do gói miễn phí không cho " +
                           "truy cập mùa giải hiện tại. Phiên bản cũ ghi nhầm là thành công.";
        }

        var biTreo = await db.NhatKyDongBos
            .Where(x => x.TrangThai == "DangChay" && x.ThoiDiemBatDau < DateTime.UtcNow.AddHours(-2))
            .ToListAsync(ct);

        foreach (var nk in biTreo)
        {
            nk.TrangThai = "BiGianDoan";
            nk.ThoiDiemKetThuc ??= nk.ThoiDiemBatDau;
            nk.ThongDiep ??= "Lượt đồng bộ bị ngắt giữa chừng (máy chủ dừng trước khi chạy xong).";
        }

        if (saiTrangThai.Count + biTreo.Count > 0) await db.SaveChangesAsync(ct);
        return saiTrangThai.Count + biTreo.Count;
    }

    /// <summary>
    /// Dịch vụ chụp ảnh giá từng ghi một mốc MỖI NGÀY cho MỌI cầu thủ, kể cả khi giá không đổi
    /// (nguồn Transfermarkt đã ngừng cập nhật). Hàng nghìn mốc phẳng làm méo biểu đồ, kéo phần trăm
    /// biến động về 0 và làm lệch mô hình dự báo. Chỉ xóa mốc "ChupAnhNgay" trùng giá với mốc ngay trước.
    /// </summary>
    public static async Task<int> XoaMocGiaPhangAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var cacMoc = await db.LichSuGiaTris.AsNoTracking()
            .Select(x => new { x.MaLichSuGiaTri, x.MaCauThu, x.NgayGhiNhan, x.GiaTri, x.Nguon })
            .ToListAsync(ct);

        var canXoa = new List<int>();

        foreach (var nhom in cacMoc.GroupBy(x => x.MaCauThu))
        {
            decimal? giaTruoc = null;
            foreach (var moc in nhom.OrderBy(x => x.NgayGhiNhan).ThenBy(x => x.MaLichSuGiaTri))
            {
                if (moc.Nguon == "ChupAnhNgay" && giaTruoc == moc.GiaTri)
                {
                    canXoa.Add(moc.MaLichSuGiaTri);
                    continue;   // giữ giaTruoc như cũ
                }
                giaTruoc = moc.GiaTri;
            }
        }

        // Xóa theo lô qua RemoveRange (không dùng ExecuteDelete) để chạy được cả với nhà cung cấp
        // InMemory trong kiểm thử.
        foreach (var lo in canXoa.Chunk(500))
        {
            var ids = lo.ToList();
            var banGhi = await db.LichSuGiaTris.Where(x => ids.Contains(x.MaLichSuGiaTri)).ToListAsync(ct);
            db.LichSuGiaTris.RemoveRange(banGhi);
            await db.SaveChangesAsync(ct);
        }

        return canXoa.Count;
    }
}
