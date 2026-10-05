using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Nạp bộ dữ liệu Transfermarkt (Kaggle — davidcariboo/player-scores, giấy phép CC0).
///
/// Những điểm bắt buộc phải đúng, đã kiểm chứng trực tiếp trên tệp CSV thật:
///  1. players.csv chứa 50.149 cầu thủ của MỌI thời kỳ, kể cả người đã giải nghệ
///     (Miroslav Klose, last_season = 2015, vẫn ghi current_club_id là Lazio).
///     Không lọc theo last_season thì 5 giải lớn sẽ có 11.813 cầu thủ, trong đó
///     hơn 8.000 người không còn thi đấu. Lọc đúng còn 3.464 người.
///  2. clubs.csv cũng vậy: 176 câu lạc bộ từng thuộc 5 giải lớn, chỉ 96 đội còn
///     ở giải mùa 2025/26.
///  3. Quốc tịch trong dataset ghi bằng tiếng Anh ("England", "Spain"), còn CSDL
///     mẫu lưu tiếng Việt — phải đối chiếu qua BanDoQuocGia, nếu không toàn bộ
///     cầu thủ nhập vào sẽ mang chung một quốc tịch sai.
///  4. Tên giải trong competitions.csv là chuỗi slug ("premier-league"), phải
///     chuyển thành tên hiển thị.
///  5. transfers.csv có dòng ngày trong tương lai (2030-06-30) — đó là mốc hết hạn
///     hợp đồng, không phải thương vụ, phải loại bỏ.
///  6. Dataset KHÔNG có cân nặng, số áo, chấn thương và chỉ số kiểu game. Những
///     trường này để trống chứ không suy đoán, trừ chỉ số được tính từ thống kê
///     thi đấu thật và đánh dấu rõ nguồn.
/// </summary>
public class NhapCsvService : INhapCsvService
{
    /// <summary>Mã 5 giải lớn trên Transfermarkt.</summary>
    public static readonly string[] NamGiaiLon = { "GB1", "ES1", "IT1", "L1", "FR1" };

    private static readonly string[] TepCanCo =
        { "competitions.csv", "clubs.csv", "players.csv", "player_valuations.csv", "transfers.csv", "appearances.csv" };

    /// <summary>Tên hiển thị của các giải, thay cho chuỗi slug trong tệp CSV.</summary>
    private static readonly Dictionary<string, string> TenGiaiHienThi =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GB1"] = "Premier League",
            ["ES1"] = "LaLiga",
            ["IT1"] = "Serie A",
            ["L1"] = "Bundesliga",
            ["FR1"] = "Ligue 1"
        };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<NhapCsvService> _log;

    public NhapCsvService(ApplicationDbContext db, ILogger<NhapCsvService> log)
    {
        _db = db; _log = log;
    }

    // ------------------------------------------------------------- xem trước

    public Task<KetQua<List<XemTruocTepDto>>> XemTruocAsync(string thuMuc)
    {
        if (string.IsNullOrWhiteSpace(thuMuc) || !Directory.Exists(thuMuc))
            return Task.FromResult(KetQua<List<XemTruocTepDto>>.Loi("KHONG_TIM_THAY",
                $"Không tìm thấy thư mục '{thuMuc}'. Hãy giải nén tệp tải từ Kaggle rồi dán đường dẫn thư mục chứa các tệp .csv."));

        var ketQua = new List<XemTruocTepDto>();

        foreach (var ten in TepCanCo)
        {
            var duongDan = Path.Combine(thuMuc, ten);
            var mucTep = new XemTruocTepDto { TenTep = ten, TonTai = File.Exists(duongDan) };

            if (mucTep.TonTai)
            {
                var thongTin = new FileInfo(duongDan);
                mucTep.KichThuocMB = Math.Round(thongTin.Length / 1024d / 1024d, 2);

                try
                {
                    using var doc = new DocCsv(duongDan);
                    mucTep.CacCot = doc.TieuDe.ToList();
                    mucTep.SoDongMau = doc.DuyetDong().Take(2000).Count();
                }
                catch (Exception ex)
                {
                    mucTep.Loi = ex.Message;
                }
            }

            ketQua.Add(mucTep);
        }

        var thieu = ketQua.Where(x => !x.TonTai).Select(x => x.TenTep).ToList();
        var thongDiep = thieu.Count == 0
            ? "Đã tìm thấy đủ 6 tệp cần thiết."
            : $"Thiếu {thieu.Count} tệp: {string.Join(", ", thieu)}.";

        return Task.FromResult(KetQua<List<XemTruocTepDto>>.Ok(ketQua, thongDiep));
    }

    // ------------------------------------------------------------------ nhập

    public async Task<KetQua<KetQuaNhapCsvDto>> NhapAsync(TuyChonNhapCsvDto tuyChon, CancellationToken ct = default)
    {
        if (tuyChon == null || string.IsNullOrWhiteSpace(tuyChon.ThuMuc) || !Directory.Exists(tuyChon.ThuMuc))
            return KetQua<KetQuaNhapCsvDto>.Loi("KHONG_TIM_THAY", "Không tìm thấy thư mục chứa tệp CSV.");

        var kq = new KetQuaNhapCsvDto { BatDau = DateTime.UtcNow };
        var nhatKy = new NhatKyDongBo
        {
            PhamVi = "NhapCsvTransfermarkt",
            TrangThai = "DangChay",
            ThoiDiemBatDau = DateTime.UtcNow
        };
        _db.NhatKyDongBos.Add(nhatKy);
        await _db.SaveChangesAsync(ct);

        try
        {
            if (tuyChon.XoaDuLieuCu)
                kq.SoCauThuDaXoa = await XoaDuLieuKhongThatAsync(tuyChon.XoaCaDuLieuMau, ct);

            var quocGiaTheoTen = await NapQuocGiaAsync(tuyChon, kq, ct);
            var giaiDauTheoMa = await NapGiaiDauAsync(tuyChon, quocGiaTheoTen, kq, ct);
            var clbTheoMa = await NapCauLacBoAsync(tuyChon, giaiDauTheoMa, kq, ct);
            var cauThuTheoMa = await NapCauThuAsync(tuyChon, clbTheoMa, quocGiaTheoTen, kq, ct);

            if (cauThuTheoMa.Count == 0)
            {
                kq.DanhSachLoi.Add("Không nạp được cầu thủ nào — hãy kiểm tra lại mùa giải lọc và thư mục CSV.");
            }
            else
            {
                if (tuyChon.NapLichSuGia)
                    await NapLichSuGiaAsync(tuyChon, cauThuTheoMa, kq, ct);

                if (tuyChon.NapChuyenNhuong)
                    await NapChuyenNhuongAsync(tuyChon, cauThuTheoMa, clbTheoMa, kq, ct);

                if (tuyChon.NapThongKeThiDau)
                    await NapThongKeAsync(tuyChon, cauThuTheoMa, kq, ct);

                await DongBoGiaTriCaoNhatAsync(cauThuTheoMa, kq, ct);
            }

            nhatKy.TrangThai = "ThanhCong";
        }
        catch (OperationCanceledException)
        {
            nhatKy.TrangThai = "ThatBai";
            nhatKy.ThongDiep = "Người dùng đã dừng tiến trình nhập.";
            kq.DanhSachLoi.Add(nhatKy.ThongDiep);
        }
        catch (Exception ex)
        {
            nhatKy.TrangThai = "ThatBai";
            nhatKy.ThongDiep = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            kq.DanhSachLoi.Add(ex.Message);
            _log.LogError(ex, "Nhập CSV Transfermarkt thất bại.");
        }
        finally
        {
            kq.KetThuc = DateTime.UtcNow;
            nhatKy.ThoiDiemKetThuc = DateTime.UtcNow;
            nhatKy.SoBanGhiThemMoi = kq.TongThemMoi;
            nhatKy.SoBanGhiCapNhat = kq.TongCapNhat;
            nhatKy.SoBanGhiLoi = kq.DanhSachLoi.Count;
            await _db.SaveChangesAsync(CancellationToken.None);
        }

        return nhatKy.TrangThai == "ThatBai"
            ? KetQua<KetQuaNhapCsvDto>.Loi("THAT_BAI", nhatKy.ThongDiep)
            : KetQua<KetQuaNhapCsvDto>.Ok(kq,
                string.Create(DinhDangSo.VN, $"Nhập xong sau {kq.SoGiay:N0} giây: {kq.CauThuThemMoi:N0} cầu thủ, ") +
                string.Create(DinhDangSo.VN, $"{kq.MocGiaThemMoi:N0} mốc giá thật, {kq.ChuyenNhuongThemMoi:N0} thương vụ."));
    }

    /// <summary>
    /// Xóa mọi cầu thủ không đến từ bộ dữ liệu thật, để bảng dữ liệu không lẫn
    /// cầu thủ mô phỏng hay dữ liệu mẫu. Xóa cả lịch sử phụ thuộc.
    /// </summary>
    private async Task<int> XoaDuLieuKhongThatAsync(bool xoaCaDuLieuMau, CancellationToken ct)
    {
        var truyVan = _db.CauThus.AsQueryable();

        truyVan = xoaCaDuLieuMau
            ? truyVan.Where(x => x.MaTransfermarkt == null)
            : truyVan.Where(x => x.LaDuLieuMoPhong);

        var maCanXoa = await truyVan.Select(x => x.MaCauThu).ToListAsync(ct);
        if (maCanXoa.Count == 0) return 0;

        foreach (var lo in ChiaLo(maCanXoa, 400))
        {
            await _db.LichSuGiaTris.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.LichSuChuyenNhuongs.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.LichSuChanThuongs.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.ChiSoCauThus.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.TheoDoiCauThus.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.DoiHinhAoCauThus.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.LuotXemCauThus.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
            await _db.CauThus.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);
        }

        _log.LogInformation("Đã xóa {So} cầu thủ không thuộc bộ dữ liệu thật.", maCanXoa.Count);
        return maCanXoa.Count;
    }

    // ------------------------------------------------------------- quốc gia

    /// <summary>
    /// Tạo sẵn toàn bộ quốc tịch xuất hiện trong players.csv, đối chiếu sang tên
    /// tiếng Việt và mã cờ. Trả về bảng tra theo TÊN TIẾNG ANH.
    /// </summary>
    private async Task<Dictionary<string, int>> NapQuocGiaAsync(
        TuyChonNhapCsvDto tuyChon, KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var ketQua = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duongDan = Path.Combine(tuyChon.ThuMuc, "players.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu players.csv."); return ketQua; }

        var tenTiengAnh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var doc = new DocCsv(duongDan))
        {
            var cotQuocTich = doc.CotDauTienCo("country_of_citizenship", "country_of_birth");
            if (cotQuocTich == null)
            {
                kq.DanhSachLoi.Add("players.csv không có cột quốc tịch.");
                return ketQua;
            }

            foreach (var dong in doc.DuyetDong())
            {
                ct.ThrowIfCancellationRequested();
                var ten = doc.O(dong, cotQuocTich);
                if (ten != null) tenTiengAnh.Add(ten);
            }
        }

        var daCo = await _db.QuocGias.ToListAsync(ct);
        var theoTenViet = new Dictionary<string, QuocGia>(StringComparer.OrdinalIgnoreCase);
        foreach (var qg in daCo) theoTenViet.TryAdd(qg.TenQuocGia, qg);

        foreach (var ten in tenTiengAnh)
        {
            var (tenViet, maCo) = BanDoQuocGia.Tra(ten);
            if (string.IsNullOrWhiteSpace(tenViet)) continue;

            if (!theoTenViet.TryGetValue(tenViet, out var qg))
            {
                qg = new QuocGia { TenQuocGia = tenViet, MaISO = maCo };
                _db.QuocGias.Add(qg);
                theoTenViet[tenViet] = qg;
                kq.QuocGiaThemMoi++;
            }
            else if (maCo != null && string.IsNullOrWhiteSpace(qg.MaISO))
            {
                qg.MaISO = maCo;
            }

            ketQua[ten] = 0;
        }

        await _db.SaveChangesAsync(ct);

        foreach (var ten in ketQua.Keys.ToList())
        {
            var tenViet = BanDoQuocGia.Tra(ten).TenViet;
            if (tenViet != null && theoTenViet.TryGetValue(tenViet, out var qg))
                ketQua[ten] = qg.MaQuocGia;
        }

        return ketQua;
    }

    // ------------------------------------------------------------- giải đấu

    private async Task<Dictionary<string, int>> NapGiaiDauAsync(
        TuyChonNhapCsvDto tuyChon, Dictionary<string, int> quocGiaTheoTen,
        KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var ketQua = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duongDan = Path.Combine(tuyChon.ThuMuc, "competitions.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu competitions.csv."); return ketQua; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("competition_id", "competitionId");
        var cotTen = doc.CotDauTienCo("name", "competition_name");
        var cotLoai = doc.CotDauTienCo("type");
        var cotQuocGia = doc.CotDauTienCo("country_name", "country");

        if (cotMa == null || cotTen == null)
        {
            kq.DanhSachLoi.Add($"competitions.csv thiếu cột mã hoặc tên. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return ketQua;
        }

        var daCo = await _db.GiaiDaus.ToListAsync(ct);
        var theoTen = new Dictionary<string, GiaiDau>(StringComparer.OrdinalIgnoreCase);
        foreach (var gd in daCo) theoTen.TryAdd(gd.TenGiaiDau, gd);

        var maDaNap = new List<string>();

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();

            var ma = doc.O(dong, cotMa);
            if (ma == null) continue;

            if (tuyChon.ChiNamGiaiLon && !NamGiaiLon.Contains(ma, StringComparer.OrdinalIgnoreCase))
                continue;

            // Chỉ lấy giải vô địch quốc gia, bỏ cúp và giải đội tuyển.
            var loai = doc.O(dong, cotLoai);
            if (!tuyChon.ChiNamGiaiLon && loai != null &&
                !loai.Equals("domestic_league", StringComparison.OrdinalIgnoreCase))
                continue;

            var ten = TenGiaiHienThi.TryGetValue(ma, out var tenDep)
                ? tenDep
                : LamDepSlug(doc.O(dong, cotTen)) ?? ma;

            if (!theoTen.TryGetValue(ten, out var giaiDau))
            {
                giaiDau = new GiaiDau { TenGiaiDau = ten };
                _db.GiaiDaus.Add(giaiDau);
                theoTen[ten] = giaiDau;
                kq.GiaiDauThemMoi++;
            }
            else
            {
                kq.GiaiDauCapNhat++;
            }

            giaiDau.MaTransfermarkt = ma;

            var tenQuocGia = doc.O(dong, cotQuocGia);
            if (tenQuocGia != null && quocGiaTheoTen.TryGetValue(tenQuocGia, out var maQg) && maQg > 0)
                giaiDau.MaQuocGia = maQg;

            maDaNap.Add(ma);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var ma in maDaNap)
        {
            var gd = theoTen.Values.FirstOrDefault(x =>
                string.Equals(x.MaTransfermarkt, ma, StringComparison.OrdinalIgnoreCase));
            if (gd != null) ketQua[ma] = gd.MaGiaiDau;
        }

        return ketQua;
    }

    /// <summary>"premier-league" → "Premier League".</summary>
    private static string LamDepSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var tu = slug.Replace('-', ' ').Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..]);

        return string.Join(' ', tu);
    }

    // ----------------------------------------------------------- câu lạc bộ

    private async Task<Dictionary<string, int>> NapCauLacBoAsync(
        TuyChonNhapCsvDto tuyChon, Dictionary<string, int> giaiDauTheoMa,
        KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var ketQua = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duongDan = Path.Combine(tuyChon.ThuMuc, "clubs.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu clubs.csv."); return ketQua; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("club_id", "clubId");
        var cotTen = doc.CotDauTienCo("name", "club_name", "pretty_name");
        var cotGiai = doc.CotDauTienCo("domestic_competition_id", "competition_id");
        var cotSan = doc.CotDauTienCo("stadium_name");
        var cotMuaCuoi = doc.CotDauTienCo("last_season");

        if (cotMa == null || cotTen == null)
        {
            kq.DanhSachLoi.Add($"clubs.csv thiếu cột mã hoặc tên. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return ketQua;
        }

        var daCoTheoTm = await _db.CauLacBos.Where(x => x.MaTransfermarkt != null)
            .ToDictionaryAsync(x => x.MaTransfermarkt, x => x, StringComparer.OrdinalIgnoreCase, ct);
        var daCoTheoTen = new Dictionary<string, CauLacBo>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in await _db.CauLacBos.ToListAsync(ct)) daCoTheoTen.TryAdd(c.TenCLB, c);

        var maDaNap = new List<string>();

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();

            var ma = doc.O(dong, cotMa);
            var ten = TenCauLacBo.RutGon(doc.O(dong, cotTen));   // "Associazione Sportiva Roma" → "AS Roma"
            if (ma == null || ten == null) continue;

            // Bỏ câu lạc bộ đã xuống hạng / không còn ở giải trong mùa đang xét.
            // 176 đội từng thuộc 5 giải lớn, nhưng chỉ 96 đội còn thi đấu mùa 2025/26.
            var muaCuoi = doc.So(dong, cotMuaCuoi);
            if (muaCuoi.HasValue && muaCuoi.Value < tuyChon.MuaGiaiToiThieu) continue;

            var maGiaiTm = doc.O(dong, cotGiai);
            if (maGiaiTm == null || !giaiDauTheoMa.TryGetValue(maGiaiTm, out var maGiai) || maGiai <= 0)
                continue;

            if (!daCoTheoTm.TryGetValue(ma, out var clb))
                daCoTheoTen.TryGetValue(ten, out clb);

            if (clb == null)
            {
                clb = new CauLacBo { TenCLB = ten };
                _db.CauLacBos.Add(clb);
                daCoTheoTm[ma] = clb;
                daCoTheoTen[ten] = clb;
                kq.CauLacBoThemMoi++;
            }
            else
            {
                kq.CauLacBoCapNhat++;
            }

            clb.MaTransfermarkt = ma;
            clb.TenCLB = ten;
            clb.MaGiaiDau = maGiai;

            var san = doc.O(dong, cotSan);
            if (san != null) clb.SanNha = san;

            maDaNap.Add(ma);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var ma in maDaNap)
            if (daCoTheoTm.TryGetValue(ma, out var clb)) ketQua[ma] = clb.MaCLB;

        return ketQua;
    }

    // ---------------------------------------------------------------- cầu thủ

    private async Task<Dictionary<string, int>> NapCauThuAsync(
        TuyChonNhapCsvDto tuyChon, Dictionary<string, int> clbTheoMa,
        Dictionary<string, int> quocGiaTheoTen, KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var ketQua = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duongDan = Path.Combine(tuyChon.ThuMuc, "players.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu players.csv."); return ketQua; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("player_id", "playerId");
        var cotTen = doc.CotDauTienCo("name", "player_name");
        var cotNgaySinh = doc.CotDauTienCo("date_of_birth", "dateOfBirth");
        var cotQuocTich = doc.CotDauTienCo("country_of_citizenship", "country_of_birth");
        var cotViTriPhu = doc.CotDauTienCo("sub_position");
        var cotViTri = doc.CotDauTienCo("position");
        var cotChan = doc.CotDauTienCo("foot");
        var cotChieuCao = doc.CotDauTienCo("height_in_cm", "height");
        var cotGia = doc.CotDauTienCo("market_value_in_eur", "market_value");
        var cotGiaCaoNhat = doc.CotDauTienCo("highest_market_value_in_eur");
        var cotCLB = doc.CotDauTienCo("current_club_id", "club_id");
        var cotHetHan = doc.CotDauTienCo("contract_expiration_date");
        var cotAnh = doc.CotDauTienCo("image_url");
        var cotMuaCuoi = doc.CotDauTienCo("last_season");

        if (cotMa == null || cotTen == null)
        {
            kq.DanhSachLoi.Add($"players.csv thiếu cột mã hoặc tên. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return ketQua;
        }

        var viTriTheoVietTat = (await _db.ViTriThiDaus.ToListAsync(ct))
            .ToDictionary(x => x.VietTat, x => x, StringComparer.OrdinalIgnoreCase);
        if (viTriTheoVietTat.Count == 0)
        {
            kq.DanhSachLoi.Add("Chưa có bảng vị trí thi đấu trong cơ sở dữ liệu.");
            return ketQua;
        }

        // Khớp cầu thủ đã có: ưu tiên mã Transfermarkt, sau đó tên đã bỏ dấu
        // (tránh tạo trùng với dữ liệu mẫu do khác dấu: "Mbappé" ↔ "Mbappe").
        var theoTm = await _db.CauThus.Where(x => x.MaTransfermarkt != null)
            .Select(x => new { x.MaCauThu, x.MaTransfermarkt })
            .ToDictionaryAsync(x => x.MaTransfermarkt, x => x.MaCauThu, StringComparer.OrdinalIgnoreCase, ct);

        var theoTenKhongDau = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in await _db.CauThus.Select(x => new { x.MaCauThu, x.TenKhongDau }).ToListAsync(ct))
            if (!string.IsNullOrWhiteSpace(x.TenKhongDau)) theoTenKhongDau.TryAdd(x.TenKhongDau, x.MaCauThu);

        var loThem = new List<CauThu>(500);
        var maTmCuaLo = new List<string>(500);
        var soDong = 0;

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();
            soDong++;

            var maTm = doc.O(dong, cotMa);
            var hoTen = doc.O(dong, cotTen);
            if (maTm == null || string.IsNullOrWhiteSpace(hoTen)) continue;

            // LỌC 1 — cầu thủ còn thi đấu. Không có bước này thì Miroslav Klose
            // (giải nghệ 2016) vẫn hiện ra như cầu thủ Lazio đương thời.
            var muaCuoi = doc.So(dong, cotMuaCuoi);
            if (muaCuoi.HasValue && muaCuoi.Value < tuyChon.MuaGiaiToiThieu) continue;
            if (!muaCuoi.HasValue && tuyChon.MuaGiaiToiThieu > 0) continue;

            // LỌC 2 — thuộc câu lạc bộ đang thi đấu ở giải đã nạp.
            var maCLBTm = doc.O(dong, cotCLB);
            if (maCLBTm == null || !clbTheoMa.TryGetValue(maCLBTm, out var maCLB) || maCLB <= 0) continue;

            // LỌC 3 — phải có giá trị thị trường, vì đây là hệ thống theo dõi giá.
            var giaEur = doc.SoThuc(dong, cotGia);
            if (giaEur == null || giaEur.Value <= 0)
            {
                if (tuyChon.BoQuaKhongCoGia) { kq.CauThuThieuGia++; continue; }
                giaEur = 0m;
            }

            var giaTrieu = Math.Round(giaEur.Value / 1_000_000m, 2);
            if (tuyChon.GiaToiThieuTrieuEur > 0 && giaTrieu < tuyChon.GiaToiThieuTrieuEur) continue;

            var ngaySinh = doc.Ngay(dong, cotNgaySinh);
            if (ngaySinh == null || ngaySinh.Value.Year < 1960)
            {
                kq.CauThuBoQua++;
                continue;
            }

            var tenKhongDau = ChuanHoa.BoDau(hoTen);

            CauThu cauThu = null;
            if (theoTm.TryGetValue(maTm, out var maCu) && maCu > 0)
                cauThu = await _db.CauThus.Include(x => x.ChiSo).FirstOrDefaultAsync(x => x.MaCauThu == maCu, ct);
            else if (theoTenKhongDau.TryGetValue(tenKhongDau, out maCu) && maCu > 0)
                cauThu = await _db.CauThus.Include(x => x.ChiSo).FirstOrDefaultAsync(x => x.MaCauThu == maCu, ct);

            var laMoi = cauThu == null;
            if (laMoi)
            {
                cauThu = new CauThu();
            }
            else if (cauThu.ChinhSuaThuCong)
            {
                kq.CauThuBoQua++;
                continue;
            }

            cauThu.MaTransfermarkt = maTm;
            cauThu.NguonDuLieu = "Transfermarkt";
            cauThu.LaDuLieuMoPhong = false;
            cauThu.HoTen = hoTen;
            cauThu.TenKhongDau = tenKhongDau;
            cauThu.NgaySinh = ngaySinh.Value;
            cauThu.NgayCapNhat = DateTime.UtcNow;
            cauThu.MaCLB = maCLB;
            cauThu.GiaTriHienTai = giaTrieu;

            var giaCaoNhat = doc.SoThuc(dong, cotGiaCaoNhat);
            cauThu.GiaTriCaoNhat = giaCaoNhat.HasValue && giaCaoNhat.Value > 0
                ? Math.Round(giaCaoNhat.Value / 1_000_000m, 2)
                : giaTrieu;

            var vietTat = VietTatTuViTriTm(doc.O(dong, cotViTriPhu), doc.O(dong, cotViTri));
            cauThu.MaViTri = viTriTheoVietTat.TryGetValue(vietTat, out var vt)
                ? vt.MaViTri
                : viTriTheoVietTat.Values.First().MaViTri;

            var tenQuocTich = doc.O(dong, cotQuocTich);
            if (tenQuocTich != null && quocGiaTheoTen.TryGetValue(tenQuocTich, out var maQg) && maQg > 0)
                cauThu.MaQuocGia = maQg;
            else if (laMoi)
            {
                kq.CauThuBoQua++;
                kq.ThemLoi($"Không xác định được quốc tịch '{tenQuocTich}' của {hoTen}.");
                continue;
            }

            var chieuCao = doc.So(dong, cotChieuCao);
            cauThu.ChieuCao = chieuCao is > 140 and < 220 ? chieuCao : null;

            // Bộ dữ liệu KHÔNG có cân nặng và số áo — để trống thay vì suy đoán.
            cauThu.CanNang = null;
            cauThu.SoAo = null;

            cauThu.ChanThuan = (doc.O(dong, cotChan) ?? "").ToLowerInvariant() switch
            {
                "left" => ChanThuan.Trai,
                "right" => ChanThuan.Phai,
                "both" => ChanThuan.CaHai,
                _ => null
            };

            cauThu.NgayHetHanHopDong = doc.Ngay(dong, cotHetHan);
            cauThu.AnhDaiDien = doc.O(dong, cotAnh);

            // Chỉ số kiểu game chưa tính được ở bước này; đánh dấu thiếu dữ liệu để
            // giao diện không hiển thị con số bịa. Bước nạp appearances sẽ tính lại.
            cauThu.ChiSo ??= new ChiSoCauThu();
            cauThu.ChiSo.NguonChiSo = "KhongCoDuLieu";
            cauThu.ChiSo.DiemPhongDo = 0;
            cauThu.ChiSo.TocDo = cauThu.ChiSo.DutDiem = cauThu.ChiSo.Chuyen = 0;
            cauThu.ChiSo.ReDat = cauThu.ChiSo.PhongNgu = cauThu.ChiSo.TheLuc = 0;
            cauThu.ChiSo.SoTran = cauThu.ChiSo.SoPhutThiDau = 0;
            cauThu.ChiSo.BanThang = cauThu.ChiSo.KienTao = 0;
            cauThu.ChiSo.TheVang = cauThu.ChiSo.TheDo = 0;
            cauThu.ChiSo.MuaGiai = tuyChon.TenMuaGiai;

            if (laMoi)
            {
                loThem.Add(cauThu);
                maTmCuaLo.Add(maTm);
                theoTenKhongDau.TryAdd(tenKhongDau, -1);
                kq.CauThuThemMoi++;
            }
            else
            {
                ketQua[maTm] = cauThu.MaCauThu;
                theoTm[maTm] = cauThu.MaCauThu;
                kq.CauThuCapNhat++;
            }

            if (loThem.Count >= 500)
            {
                _db.CauThus.AddRange(loThem);
                await _db.SaveChangesAsync(ct);
                for (var i = 0; i < loThem.Count; i++)
                {
                    ketQua[maTmCuaLo[i]] = loThem[i].MaCauThu;
                    theoTenKhongDau[loThem[i].TenKhongDau] = loThem[i].MaCauThu;
                }
                loThem.Clear(); maTmCuaLo.Clear();
                GoTheoDoi();
            }
        }

        if (loThem.Count > 0)
        {
            _db.CauThus.AddRange(loThem);
            await _db.SaveChangesAsync(ct);
            for (var i = 0; i < loThem.Count; i++)
                ketQua[maTmCuaLo[i]] = loThem[i].MaCauThu;
            GoTheoDoi();
        }
        else if (_db.ChangeTracker.HasChanges())
        {
            // Lô cuối chỉ toàn bản ghi CẬP NHẬT (không có cầu thủ mới) thì nhánh trên
            // không chạy; thiếu lần lưu này là toàn bộ cập nhật cuối bị mất.
            await _db.SaveChangesAsync(ct);
            GoTheoDoi();
        }

        kq.SoDongDaDoc += soDong;

        // Xóa lịch sử chấn thương giả còn sót của dữ liệu mẫu: bộ dữ liệu
        // Transfermarkt không có thông tin chấn thương, nên để trống mới là đúng.
        var maCauThus = ketQua.Values.Where(x => x > 0).Distinct().ToList();
        foreach (var lo in ChiaLo(maCauThus, 400))
            kq.ChanThuongDaXoa += await _db.LichSuChanThuongs
                .Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);

        return ketQua;
    }

    // ------------------------------------------------------------ lịch sử giá

    private async Task NapLichSuGiaAsync(TuyChonNhapCsvDto tuyChon,
        Dictionary<string, int> cauThuTheoMa, KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var duongDan = Path.Combine(tuyChon.ThuMuc, "player_valuations.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu player_valuations.csv."); return; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("player_id", "playerId");
        var cotNgay = doc.CotDauTienCo("date", "datetime");
        var cotGia = doc.CotDauTienCo("market_value_in_eur", "market_value");

        if (cotMa == null || cotNgay == null || cotGia == null)
        {
            kq.DanhSachLoi.Add($"player_valuations.csv thiếu cột. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return;
        }

        var maCauThus = cauThuTheoMa.Values.Where(x => x > 0).Distinct().ToList();
        foreach (var lo in ChiaLo(maCauThus, 400))
        {
            await _db.LichSuGiaTris
                .Where(x => lo.Contains(x.MaCauThu) && x.Nguon != "ChupAnhNgay")
                .ExecuteDeleteAsync(ct);
        }

        var moc = tuyChon.SoNamLichSuGia > 0
            ? DateTime.UtcNow.Date.AddYears(-tuyChon.SoNamLichSuGia)
            : DateTime.MinValue;

        var lo2 = new List<LichSuGiaTri>(2000);
        var daGhi = new HashSet<(int, DateTime)>();

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();

            var maTm = doc.O(dong, cotMa);
            if (maTm == null || !cauThuTheoMa.TryGetValue(maTm, out var maCauThu) || maCauThu <= 0) continue;

            var ngay = doc.Ngay(dong, cotNgay);
            var gia = doc.SoThuc(dong, cotGia);
            if (ngay == null || gia == null || gia <= 0 || ngay < moc) continue;
            if (ngay.Value.Date > DateTime.UtcNow.Date) continue;

            // Mỗi cầu thủ chỉ giữ một mốc cho mỗi ngày.
            if (!daGhi.Add((maCauThu, ngay.Value.Date))) continue;

            lo2.Add(new LichSuGiaTri
            {
                MaCauThu = maCauThu,
                NgayGhiNhan = ngay.Value.Date,
                GiaTri = Math.Round(gia.Value / 1_000_000m, 2),
                Nguon = "Transfermarkt"
            });
            kq.MocGiaThemMoi++;

            if (lo2.Count >= 2000)
            {
                _db.LichSuGiaTris.AddRange(lo2);
                await _db.SaveChangesAsync(ct);
                GoTheoDoi();
                lo2.Clear();
            }
        }

        if (lo2.Count > 0)
        {
            _db.LichSuGiaTris.AddRange(lo2);
            await _db.SaveChangesAsync(ct);
            GoTheoDoi();
        }
    }

    /// <summary>Đồng bộ GiaTriCaoNhat với mốc cao nhất thật trong lịch sử giá.</summary>
    private async Task DongBoGiaTriCaoNhatAsync(Dictionary<string, int> cauThuTheoMa,
        KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var maCauThus = cauThuTheoMa.Values.Where(x => x > 0).Distinct().ToList();

        foreach (var lo in ChiaLo(maCauThus, 400))
        {
            var dinh = await _db.LichSuGiaTris.AsNoTracking()
                .Where(x => lo.Contains(x.MaCauThu))
                .GroupBy(x => x.MaCauThu)
                .Select(g => new { MaCauThu = g.Key, CaoNhat = g.Max(x => x.GiaTri) })
                .ToListAsync(ct);

            if (dinh.Count == 0) continue;

            var theoMa = dinh.ToDictionary(x => x.MaCauThu, x => x.CaoNhat);
            var cauThus = await _db.CauThus.Where(x => lo.Contains(x.MaCauThu)).ToListAsync(ct);

            foreach (var ct2 in cauThus)
            {
                if (!theoMa.TryGetValue(ct2.MaCauThu, out var caoNhat)) continue;
                var moi = Math.Max(caoNhat, ct2.GiaTriHienTai);
                if (ct2.GiaTriCaoNhat != moi) { ct2.GiaTriCaoNhat = moi; kq.GiaCaoNhatSuaLai++; }
            }

            await _db.SaveChangesAsync(ct);
            GoTheoDoi();
        }
    }

    // ------------------------------------------------------- chuyển nhượng

    private async Task NapChuyenNhuongAsync(TuyChonNhapCsvDto tuyChon,
        Dictionary<string, int> cauThuTheoMa, Dictionary<string, int> clbTheoMa,
        KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var duongDan = Path.Combine(tuyChon.ThuMuc, "transfers.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu transfers.csv."); return; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("player_id", "playerId");
        var cotNgay = doc.CotDauTienCo("transfer_date", "date");
        var cotMua = doc.CotDauTienCo("transfer_season", "season");
        var cotTu = doc.CotDauTienCo("from_club_id");
        var cotDen = doc.CotDauTienCo("to_club_id");
        var cotTenTu = doc.CotDauTienCo("from_club_name");
        var cotTenDen = doc.CotDauTienCo("to_club_name");
        var cotPhi = doc.CotDauTienCo("transfer_fee");
        var cotGiaLucDo = doc.CotDauTienCo("market_value_in_eur", "market_value");

        if (cotMa == null || cotNgay == null)
        {
            kq.DanhSachLoi.Add($"transfers.csv thiếu cột. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return;
        }

        var maCauThus = cauThuTheoMa.Values.Where(x => x > 0).Distinct().ToList();
        foreach (var lo in ChiaLo(maCauThus, 400))
            await _db.LichSuChuyenNhuongs.Where(x => lo.Contains(x.MaCauThu)).ExecuteDeleteAsync(ct);

        var homNay = DateTime.UtcNow.Date;
        var loGhi = new List<LichSuChuyenNhuong>(2000);

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();

            var maTm = doc.O(dong, cotMa);
            if (maTm == null || !cauThuTheoMa.TryGetValue(maTm, out var maCauThu) || maCauThu <= 0) continue;

            var ngay = doc.Ngay(dong, cotNgay);
            if (ngay == null) continue;

            // Dòng có ngày trong tương lai (ví dụ 2030-06-30) là mốc hết hạn hợp đồng
            // mà Transfermarkt ghi chung bảng, không phải một thương vụ đã xảy ra.
            if (ngay.Value.Date > homNay) { kq.ChuyenNhuongTuongLaiBoQua++; continue; }

            var maTu = doc.O(dong, cotTu);
            var maDen = doc.O(dong, cotDen);

            int? clbDi = maTu != null && clbTheoMa.TryGetValue(maTu, out var a) && a > 0 ? a : null;
            int? clbDen = maDen != null && clbTheoMa.TryGetValue(maDen, out var b) && b > 0 ? b : null;

            if (clbDen == null) continue;

            // Ô phí TRỐNG nghĩa là không công bố (thường gồm cả cho mượn), khác với phí 0 (tự do).
            var phiEurGoc = doc.SoThuc(dong, cotPhi);
            var phiEur = phiEurGoc ?? 0m;
            var giaLucDoEur = doc.SoThuc(dong, cotGiaLucDo) ?? 0m;
            var phi = Math.Round(phiEur / 1_000_000m, 2);
            if (phi < 0) phi = 0;

            var loai = phiEurGoc == null ? LoaiChuyenNhuong.KhongRo
                : phi > 0 ? LoaiChuyenNhuong.MuaDut
                : LoaiChuyenNhuong.TuDo;

            loGhi.Add(new LichSuChuyenNhuong
            {
                MaCauThu = maCauThu,
                MaCLBDi = clbDi,
                MaCLBDen = clbDen.Value,
                NgayChuyenNhuong = ngay.Value.Date,
                MuaGiai = ChuanHoaMuaGiai(doc.O(dong, cotMua), ngay.Value),
                PhiChuyenNhuong = phi,
                GiaTriThiTruongLucDo = Math.Round(Math.Max(giaLucDoEur, 0) / 1_000_000m, 2),
                LoaiChuyenNhuong = loai,
                TenCLBDiNgoai = clbDi == null ? CatTen(doc.O(dong, cotTenTu)) : null
            });
            kq.ChuyenNhuongThemMoi++;

            if (loGhi.Count >= 2000)
            {
                _db.LichSuChuyenNhuongs.AddRange(loGhi);
                await _db.SaveChangesAsync(ct);
                GoTheoDoi();
                loGhi.Clear();
            }
        }

        if (loGhi.Count > 0)
        {
            _db.LichSuChuyenNhuongs.AddRange(loGhi);
            await _db.SaveChangesAsync(ct);
            GoTheoDoi();
        }
    }

    private static string CatTen(string ten) =>
        string.IsNullOrWhiteSpace(ten) ? null : (ten.Length > 150 ? ten[..150] : ten.Trim());

    /// <summary>"25/26" → "2025/2026"; thiếu thì suy từ ngày chuyển nhượng.</summary>
    private static string ChuanHoaMuaGiai(string mua, DateTime ngay)
    {
        if (!string.IsNullOrWhiteSpace(mua) && mua.Length == 5 && mua[2] == '/')
        {
            if (int.TryParse(mua[..2], out var n1) && int.TryParse(mua[3..], out var n2))
                return $"{2000 + n1}/{2000 + n2}";
        }

        // Mùa bóng châu Âu bắt đầu từ tháng 7.
        var namDau = ngay.Month >= 7 ? ngay.Year : ngay.Year - 1;
        return $"{namDau}/{namDau + 1}";
    }

    // ----------------------------------------------------- thống kê thi đấu

    private async Task NapThongKeAsync(TuyChonNhapCsvDto tuyChon,
        Dictionary<string, int> cauThuTheoMa, KetQuaNhapCsvDto kq, CancellationToken ct)
    {
        var duongDan = Path.Combine(tuyChon.ThuMuc, "appearances.csv");
        if (!File.Exists(duongDan)) { kq.DanhSachLoi.Add("Thiếu appearances.csv."); return; }

        using var doc = new DocCsv(duongDan);
        var cotMa = doc.CotDauTienCo("player_id", "playerId");
        var cotNgay = doc.CotDauTienCo("date");
        var cotBan = doc.CotDauTienCo("goals");
        var cotKienTao = doc.CotDauTienCo("assists");
        var cotPhut = doc.CotDauTienCo("minutes_played");
        var cotVang = doc.CotDauTienCo("yellow_cards");
        var cotDo = doc.CotDauTienCo("red_cards");

        if (cotMa == null || cotNgay == null)
        {
            kq.DanhSachLoi.Add($"appearances.csv thiếu cột. Cột đọc được: {string.Join(", ", doc.TieuDe)}");
            return;
        }

        // Cửa sổ theo MÙA BÓNG (1/7 → 30/6), không phải "12 tháng gần nhất", để số
        // liệu khớp với mùa giải ghi trên giao diện.
        var dau = new DateTime(tuyChon.MuaGiaiToiThieu, 7, 1);
        var cuoi = new DateTime(tuyChon.MuaGiaiToiThieu + 1, 6, 30);

        var tong = new Dictionary<int, GomThongKe>();
        var soDong = 0;

        foreach (var dong in doc.DuyetDong())
        {
            ct.ThrowIfCancellationRequested();
            soDong++;

            var maTm = doc.O(dong, cotMa);
            if (maTm == null || !cauThuTheoMa.TryGetValue(maTm, out var maCauThu) || maCauThu <= 0) continue;

            var ngay = doc.Ngay(dong, cotNgay);
            if (ngay == null || ngay < dau || ngay > cuoi) continue;

            if (!tong.TryGetValue(maCauThu, out var g))
            {
                g = new GomThongKe();
                tong[maCauThu] = g;
            }

            g.SoTran++;
            g.Phut += doc.So(dong, cotPhut) ?? 0;
            g.Ban += doc.So(dong, cotBan) ?? 0;
            g.KienTao += doc.So(dong, cotKienTao) ?? 0;
            g.TheVang += doc.So(dong, cotVang) ?? 0;
            g.TheDo += doc.So(dong, cotDo) ?? 0;
        }

        kq.SoDongDaDoc += soDong;
        if (tong.Count == 0)
        {
            kq.DanhSachLoi.Add($"Không có trận nào trong khoảng {dau:dd/MM/yyyy} - {cuoi:dd/MM/yyyy}. " +
                               "Hãy kiểm tra lại mùa giải đã chọn.");
            return;
        }

        var maCanCapNhat = tong.Keys.ToList();
        var nhomTheoViTri = await _db.ViTriThiDaus.AsNoTracking()
            .ToDictionaryAsync(x => x.MaViTri, x => x.NhomViTri, ct);

        foreach (var lo in ChiaLo(maCanCapNhat, 400))
        {
            var cauThus = await _db.CauThus.Include(x => x.ChiSo)
                .Where(x => lo.Contains(x.MaCauThu)).ToListAsync(ct);

            foreach (var cauThu in cauThus)
            {
                var g = tong[cauThu.MaCauThu];

                cauThu.ChiSo ??= new ChiSoCauThu { MaCauThu = cauThu.MaCauThu };
                var cs = cauThu.ChiSo;

                cs.MuaGiai = tuyChon.TenMuaGiai;
                cs.SoTran = g.SoTran;
                cs.SoPhutThiDau = g.Phut;
                cs.BanThang = g.Ban;
                cs.KienTao = g.KienTao;
                cs.TheVang = g.TheVang;
                cs.TheDo = g.TheDo;

                // Chỉ tính chỉ số khi có đủ thời lượng thi đấu; dưới ngưỡng thì
                // giữ nguyên trạng thái thiếu dữ liệu chứ không suy đoán.
                if (g.Phut >= tuyChon.PhutToiThieuTinhChiSo)
                {
                    var nhom = nhomTheoViTri.TryGetValue(cauThu.MaViTri, out var n) ? n : (NhomViTri?)null;
                    TinhChiSoTuThongKe(cs, g, cauThu.MaViTri, nhom);
                    cs.NguonChiSo = "TinhTuThongKe";
                    kq.ChiSoTinhTuThongKe++;
                }
                else
                {
                    cs.NguonChiSo = "KhongCoDuLieu";
                    cs.DiemPhongDo = 0;
                }
            }

            await _db.SaveChangesAsync(ct);
            GoTheoDoi();
        }

        kq.CauThuThieuThongKe = cauThuTheoMa.Values.Where(x => x > 0).Distinct().Count() - tong.Count;
    }

    /// <summary>
    /// Tính sáu chỉ số và điểm phong độ TỪ SỐ LIỆU THI ĐẤU THẬT.
    ///
    /// Công thức (ghi rõ để đưa vào báo cáo):
    ///   banTren90    = bàn thắng / (phút / 90)
    ///   kienTaoTren90= kiến tạo / (phút / 90)
    ///   thamGia      = phút thi đấu / 3420 (38 trận × 90 phút), giới hạn 1
    ///   kyLuat       = 1 - min(1, (thẻ vàng + 3 × thẻ đỏ) / max(1, số trận))
    ///
    /// Sáu chỉ số là ánh xạ tuyến tính của các đại lượng trên về thang 1-99, có
    /// trọng số theo nhóm vị trí. Đây là chỉ số DẪN XUẤT, không phải chỉ số đo
    /// được, và luôn được đánh dấu NguonChiSo = "TinhTuThongKe".
    /// </summary>
    private static void TinhChiSoTuThongKe(ChiSoCauThu cs, GomThongKe g, int maViTri, NhomViTri? nhom)
    {
        var soChin = Math.Max(g.Phut / 90.0, 0.5);
        var banTren90 = g.Ban / soChin;
        var kienTaoTren90 = g.KienTao / soChin;
        var thamGia = Math.Clamp(g.Phut / 3420.0, 0, 1);
        var kyLuat = 1 - Math.Clamp((g.TheVang + 3.0 * g.TheDo) / Math.Max(1, g.SoTran), 0, 1);

        int Thang(double giaTri, double toiDa, int san = 40, int tran = 99)
            => (int)Math.Round(Math.Clamp(san + (tran - san) * Math.Clamp(giaTri / toiDa, 0, 1), 1, 99));

        var diemBan = Thang(banTren90, 0.8);
        var diemKienTao = Thang(kienTaoTren90, 0.5);
        var diemThamGia = Thang(thamGia, 0.9);
        var diemKyLuat = Thang(kyLuat, 1.0, 30);

        cs.DutDiem = diemBan;
        cs.Chuyen = diemKienTao;
        cs.TheLuc = diemThamGia;
        cs.PhongNgu = nhom == NhomViTri.HauVe || nhom == NhomViTri.ThuMon
            ? (int)Math.Round(diemKyLuat * 0.4 + diemThamGia * 0.6)
            : (int)Math.Round(diemKyLuat * 0.6 + diemThamGia * 0.4);

        // Hai chỉ số này không suy được từ dữ liệu có sẵn; đặt bằng mức đóng góp
        // để không tạo ra con số vô căn cứ, và toàn bộ khối được đánh dấu là dẫn xuất.
        cs.TocDo = diemThamGia;
        cs.ReDat = (int)Math.Round(diemBan * 0.5 + diemKienTao * 0.5);

        // Điểm phong độ: trọng số theo nhóm vị trí.
        var diem = nhom switch
        {
            NhomViTri.ThuMon => diemThamGia * 0.70 + diemKyLuat * 0.30,
            NhomViTri.HauVe => diemThamGia * 0.55 + diemKyLuat * 0.25 + diemKienTao * 0.12 + diemBan * 0.08,
            NhomViTri.TienVe => diemThamGia * 0.40 + diemKienTao * 0.28 + diemBan * 0.20 + diemKyLuat * 0.12,
            _ => diemBan * 0.45 + diemKienTao * 0.22 + diemThamGia * 0.25 + diemKyLuat * 0.08
        };

        cs.DiemPhongDo = (int)Math.Round(Math.Clamp(diem, 1, 99));
    }

    private sealed class GomThongKe
    {
        public int SoTran, Phut, Ban, KienTao, TheVang, TheDo;
    }

    // ------------------------------------------------------------- tiện ích

    private void GoTheoDoi()
    {
        foreach (var muc in _db.ChangeTracker.Entries().ToList())
            muc.State = EntityState.Detached;
    }

    private static IEnumerable<List<T>> ChiaLo<T>(List<T> nguon, int kichThuoc)
    {
        for (var i = 0; i < nguon.Count; i += kichThuoc)
            yield return nguon.GetRange(i, Math.Min(kichThuoc, nguon.Count - i));
    }

    /// <summary>
    /// Ánh xạ vị trí Transfermarkt sang viết tắt của hệ thống. Đã đối chiếu với
    /// 13 giá trị sub_position thực có trong dữ liệu.
    /// </summary>
    public static string VietTatTuViTriTm(string viTriPhu, string viTriChinh)
    {
        switch ((viTriPhu ?? "").Trim().ToLowerInvariant())
        {
            case "goalkeeper": return "GK";
            case "centre-back": return "CB";
            case "left-back": return "LB";
            case "right-back": return "RB";
            case "defensive midfield": return "CDM";
            case "central midfield": return "CM";
            case "attacking midfield": return "CAM";
            case "left midfield": return "LM";
            case "right midfield": return "RM";
            case "left winger": return "LW";
            case "right winger": return "RW";
            case "centre-forward": return "ST";
            case "second striker": return "CF";
        }

        return (viTriChinh ?? "").Trim().ToLowerInvariant() switch
        {
            "goalkeeper" => "GK",
            "defender" => "CB",
            "midfield" => "CM",
            "attack" => "ST",
            _ => "CM"
        };
    }

    public static NhomViTri NhomTuViTriTm(string viTri)
    {
        var v = (viTri ?? "").ToLowerInvariant();

        if (v.Contains("goalkeeper")) return NhomViTri.ThuMon;
        if (v.Contains("back") || v.Contains("defender")) return NhomViTri.HauVe;
        if (v.Contains("midfield")) return NhomViTri.TienVe;
        if (v.Contains("winger") || v.Contains("striker") || v.Contains("forward") || v.Contains("attack"))
            return NhomViTri.TienDao;

        return NhomViTri.TienVe;
    }
}
