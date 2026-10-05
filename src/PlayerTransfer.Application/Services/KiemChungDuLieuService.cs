using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Tính năng 10 — tự kiểm chứng chất lượng dữ liệu.
///
/// Mỗi phép kiểm tra tương ứng với một lỗi ĐÃ TỪNG XẢY RA trong dự án này:
/// cầu thủ đã giải nghệ lọt vào, quốc tịch bị gán sai hàng loạt, câu lạc bộ không
/// thuộc giải nào, thương vụ ghi ngày tương lai, chỉ số bịa ra từ giá trị.
/// Mục đích là phát hiện sớm thay vì chờ nhìn thấy số liệu lạ trên giao diện.
/// </summary>
public class KiemChungDuLieuService : IKiemChungDuLieuService
{
    private readonly ApplicationDbContext _db;

    public KiemChungDuLieuService(ApplicationDbContext db) => _db = db;

    public async Task<BaoCaoKiemChungDto> ChayAsync(CancellationToken ct = default)
    {
        var bc = new BaoCaoKiemChungDto
        {
            TongCauThu = await _db.CauThus.CountAsync(ct),
            TongCauLacBo = await _db.CauLacBos.CountAsync(ct),
            TongGiaiDau = await _db.GiaiDaus.CountAsync(ct),
            TongMocGia = await _db.LichSuGiaTris.CountAsync(ct),
            TongThuongVu = await _db.LichSuChuyenNhuongs.CountAsync(ct)
        };

        bc.TheoNguonDuLieu = await _db.CauThus.AsNoTracking()
            .GroupBy(x => x.NguonDuLieu ?? "KhongRo")
            .Select(g => new { g.Key, So = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.So, ct);

        bc.TheoNguonChiSo = await _db.ChiSoCauThus.AsNoTracking()
            .GroupBy(x => x.NguonChiSo ?? "KhongRo")
            .Select(g => new { g.Key, So = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.So, ct);

        var tong = Math.Max(1, bc.TongCauThu);

        // ---- 1. Giá trị thị trường ----
        await ThemAsync(bc, "GIA_BANG_0", "Cầu thủ có giá trị bằng 0",
            "Hệ thống theo dõi giá trị thì mọi cầu thủ phải có giá. Giá 0 thường là dấu hiệu " +
            "dữ liệu nhập thiếu hoặc bị tiến trình đồng bộ ghi đè hỏng.",
            _db.CauThus.Where(x => x.GiaTriHienTai <= 0),
            x => x.HoTen, tong, "Loi",
            "Nạp lại bộ dữ liệu và bật tùy chọn bỏ qua cầu thủ không có giá.", ct);

        await ThemAsync(bc, "GIA_DINH_THAP", "Giá trị đỉnh thấp hơn giá hiện tại",
            "GiaTriCaoNhat phải luôn ≥ GiaTriHienTai.",
            _db.CauThus.Where(x => x.GiaTriCaoNhat < x.GiaTriHienTai),
            x => x.HoTen, tong, "CanhBao",
            "Chạy lại bước nạp lịch sử giá để đồng bộ lại giá trị đỉnh.", ct);

        // ---- 2. Quan hệ khóa ngoại ----
        await ThemAsync(bc, "KHONG_CLB", "Cầu thủ không thuộc câu lạc bộ nào",
            "Cầu thủ tự do là hợp lệ, nhưng số lượng lớn thường là do ánh xạ câu lạc bộ thất bại.",
            _db.CauThus.Where(x => x.MaCLB == null),
            x => x.HoTen, tong, "CanhBao",
            "Kiểm tra bước nạp clubs.csv và bộ lọc mùa giải tối thiểu.", ct);

        // CauLacBo.MaGiaiDau không nullable, nên "mồ côi" ở đây nghĩa là giá trị 0
        // hoặc trỏ tới một giải đấu không còn tồn tại.
        var soCLBKhongGiai = await _db.CauLacBos
            .CountAsync(x => x.MaGiaiDau == 0
                             || !_db.GiaiDaus.Any(g => g.MaGiaiDau == x.MaGiaiDau), ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "CLB_KHONG_GIAI",
            TenKiemTra = "Câu lạc bộ không thuộc giải đấu nào",
            MoTa = "Câu lạc bộ mồ côi khiến bộ lọc theo giải bỏ sót cầu thủ.",
            SoBanGhiLoi = soCLBKhongGiai,
            TongBanGhi = Math.Max(1, bc.TongCauLacBo),
            MucDo = soCLBKhongGiai == 0 ? "Tot" : "CanhBao",
            GoiYXuLy = "Nạp lại competitions.csv trước clubs.csv."
        });

        // ---- 3. Trùng lặp ----
        var trungTen = await _db.CauThus.AsNoTracking()
            .GroupBy(x => x.TenKhongDau)
            .Where(g => g.Count() > 1)
            .Select(g => new { Ten = g.Key, So = g.Count() })
            .OrderByDescending(x => x.So)
            .Take(20)
            .ToListAsync(ct);

        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "TRUNG_TEN",
            TenKiemTra = "Cầu thủ trùng tên",
            MoTa = "Trùng tên có thể là hai người thật khác nhau, nhưng cũng có thể là một người " +
                   "bị nhập hai lần do khác dấu hoặc khác nguồn dữ liệu.",
            SoBanGhiLoi = trungTen.Sum(x => x.So - 1),
            TongBanGhi = tong,
            MucDo = trungTen.Count == 0 ? "Tot" : "CanhBao",
            ViDu = trungTen.Take(5).Select(x => $"{x.Ten} ({x.So} bản ghi)").ToList(),
            GoiYXuLy = "Mở từng cặp để xác nhận; nếu là một người thì xóa bản ghi không có mã Transfermarkt."
        });

        var trungMaTm = await _db.CauThus.AsNoTracking()
            .Where(x => x.MaTransfermarkt != null)
            .GroupBy(x => x.MaTransfermarkt)
            .Where(g => g.Count() > 1)
            .CountAsync(ct);

        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "TRUNG_MA_TM",
            TenKiemTra = "Trùng mã Transfermarkt",
            MoTa = "Mỗi mã Transfermarkt chỉ được ứng với đúng một bản ghi cầu thủ.",
            SoBanGhiLoi = trungMaTm,
            TongBanGhi = tong,
            MucDo = trungMaTm == 0 ? "Tot" : "Loi",
            GoiYXuLy = "Xóa sạch dữ liệu cũ rồi nạp lại bộ dữ liệu."
        });

        // ---- 4. Thời gian ----
        var homNay = DateTime.UtcNow.Date;

        await ThemAsync(bc, "NGAY_SINH_LA", "Ngày sinh không hợp lý",
            "Cầu thủ chuyên nghiệp nằm trong khoảng 15-45 tuổi.",
            _db.CauThus.Where(x => x.NgaySinh > homNay.AddYears(-15) || x.NgaySinh < homNay.AddYears(-45)),
            x => x.HoTen, tong, "Loi",
            "Kiểm tra định dạng cột date_of_birth khi nạp.", ct);

        var cnTuongLai = await _db.LichSuChuyenNhuongs.CountAsync(x => x.NgayChuyenNhuong > homNay, ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "CN_TUONG_LAI",
            TenKiemTra = "Thương vụ ghi ngày trong tương lai",
            MoTa = "Bộ dữ liệu Transfermarkt ghi chung mốc hết hạn hợp đồng vào bảng chuyển nhượng; " +
                   "những dòng này không phải thương vụ đã xảy ra.",
            SoBanGhiLoi = cnTuongLai,
            TongBanGhi = Math.Max(1, bc.TongThuongVu),
            MucDo = cnTuongLai == 0 ? "Tot" : "Loi",
            GoiYXuLy = "Nạp lại bảng chuyển nhượng; bộ nhập hiện tại đã tự loại các dòng này."
        });

        var giaTuongLai = await _db.LichSuGiaTris.CountAsync(x => x.NgayGhiNhan > homNay, ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "GIA_TUONG_LAI",
            TenKiemTra = "Mốc giá ghi ngày trong tương lai",
            MoTa = "Lịch sử giá không được có mốc ở tương lai, nếu không biểu đồ sẽ kéo dài vô lý.",
            SoBanGhiLoi = giaTuongLai,
            TongBanGhi = Math.Max(1, bc.TongMocGia),
            MucDo = giaTuongLai == 0 ? "Tot" : "Loi",
            GoiYXuLy = "Nạp lại lịch sử giá."
        });

        // ---- 5. Chỉ số ----
        var khongChiSo = await _db.CauThus.CountAsync(x => x.ChiSo == null, ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "KHONG_CHI_SO",
            TenKiemTra = "Cầu thủ không có bản ghi chỉ số",
            MoTa = "Thiếu bản ghi chỉ số khiến các truy vấn sắp xếp theo chỉ số trả về NULL.",
            SoBanGhiLoi = khongChiSo,
            TongBanGhi = tong,
            MucDo = khongChiSo == 0 ? "Tot" : "CanhBao",
            GoiYXuLy = "Nạp lại bộ dữ liệu; bộ nhập luôn tạo bản ghi chỉ số cho mọi cầu thủ."
        });

        var chiSoUocLuong = bc.TheoNguonChiSo.TryGetValue("UocLuongTuGia", out var u) ? u : 0;
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "CHI_SO_UOC_LUONG",
            TenKiemTra = "Chỉ số suy ra từ giá trị thị trường",
            MoTa = "Đây là số liệu KHÔNG đo được, chỉ nên dùng khi không còn nguồn nào khác. " +
                   "Nếu đã nạp appearances.csv thì con số này nên bằng 0.",
            SoBanGhiLoi = chiSoUocLuong,
            TongBanGhi = tong,
            MucDo = chiSoUocLuong == 0 ? "Tot" : "CanhBao",
            GoiYXuLy = "Nạp lại bộ dữ liệu kèm tệp appearances.csv để tính chỉ số từ số liệu thi đấu thật."
        });

        var chiSoLech = await _db.ChiSoCauThus
            .CountAsync(x => x.NguonChiSo == "TinhTuThongKe" && x.SoPhutThiDau == 0, ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "CHI_SO_LECH_NGUON",
            TenKiemTra = "Chỉ số ghi nhãn 'tính từ thống kê' nhưng không có phút thi đấu",
            MoTa = "Nhãn nguồn phải khớp với dữ liệu thật; lệch nhau nghĩa là có tiến trình " +
                   "đã ghi đè mà không cập nhật nhãn.",
            SoBanGhiLoi = chiSoLech,
            TongBanGhi = tong,
            MucDo = chiSoLech == 0 ? "Tot" : "Loi",
            GoiYXuLy = "Kiểm tra tiến trình đồng bộ API-Football."
        });

        // ---- 6. Dữ liệu mô phỏng còn sót ----
        var moPhong = await _db.CauThus.CountAsync(x => x.LaDuLieuMoPhong, ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "CON_MO_PHONG",
            TenKiemTra = "Cầu thủ mô phỏng còn trong cơ sở dữ liệu",
            MoTa = "Dữ liệu sinh tự động chỉ dùng để thử hiệu năng, không được lẫn vào số liệu báo cáo.",
            SoBanGhiLoi = moPhong,
            TongBanGhi = tong,
            MucDo = moPhong == 0 ? "Tot" : "Loi",
            GoiYXuLy = "Đặt Seed:TongCauThu = 0 trong appsettings.json rồi nạp lại bộ dữ liệu."
        });

        // ---- 7. Lịch sử giá ----
        var khongLichSu = await _db.CauThus
            .CountAsync(x => !_db.LichSuGiaTris.Any(g => g.MaCauThu == x.MaCauThu), ct);
        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = "KHONG_LICH_SU_GIA",
            TenKiemTra = "Cầu thủ không có mốc giá nào",
            MoTa = "Không có lịch sử thì biểu đồ biến động và chức năng dự báo đều không chạy được.",
            SoBanGhiLoi = khongLichSu,
            TongBanGhi = tong,
            MucDo = khongLichSu == 0 ? "Tot" : "CanhBao",
            GoiYXuLy = "Nạp lại kèm tệp player_valuations.csv, hoặc nới số năm lịch sử giá."
        });

        return bc;
    }

    /// <summary>Chạy một phép kiểm tra trên truy vấn cầu thủ và ghi vào báo cáo.</summary>
    private static async Task ThemAsync(BaoCaoKiemChungDto bc, string ma, string ten, string moTa,
        IQueryable<Domain.Entities.CauThu> truyVan,
        Func<Domain.Entities.CauThu, string> layNhan,
        int tongBanGhi, string mucDoKhiLoi, string goiY, CancellationToken ct)
    {
        var soLoi = await truyVan.CountAsync(ct);
        var viDu = soLoi == 0
            ? new List<Domain.Entities.CauThu>()
            : await truyVan.AsNoTracking().Take(5).ToListAsync(ct);

        bc.CacMuc.Add(new MucKiemTraDto
        {
            Ma = ma,
            TenKiemTra = ten,
            MoTa = moTa,
            SoBanGhiLoi = soLoi,
            TongBanGhi = tongBanGhi,
            MucDo = soLoi == 0 ? "Tot" : mucDoKhiLoi,
            ViDu = viDu.Select(layNhan).ToList(),
            GoiYXuLy = goiY
        });
    }
}
