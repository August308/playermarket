using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// E6 — xuất/nhập Excel bằng ClosedXML; E7 — kết xuất PDF bằng QuestPDF.
/// </summary>
public class BaoCaoService : IBaoCaoService
{
    // Thứ tự cột của tệp nhập/xuất, dùng chung cho cả ba hàm để không lệch nhau.
    private static readonly string[] TieuDeCot =
    {
        "Mã cầu thủ", "Họ tên", "Câu lạc bộ", "Giải đấu", "Vị trí", "Quốc tịch",
        "Ngày sinh", "Tuổi", "Chiều cao (cm)", "Cân nặng (kg)", "Chân thuận", "Số áo",
        "Giá trị hiện tại (triệu €)", "Giá trị cao nhất (triệu €)", "Hết hạn hợp đồng",
        "Tốc độ", "Dứt điểm", "Chuyền", "Rê dắt", "Phòng ngự", "Thể lực", "Chỉ số tổng hợp"
    };

    private readonly ApplicationDbContext _db;
    private readonly ICauThuService _cauThu;
    private readonly IThongKeService _thongKe;
    private readonly ILogger<BaoCaoService> _log;

    public BaoCaoService(ApplicationDbContext db, ICauThuService cauThu,
        IThongKeService thongKe, ILogger<BaoCaoService> log)
    {
        _db = db; _cauThu = cauThu; _thongKe = thongKe; _log = log;
    }

    // ---------------------------------------------------------------- E6 xuất

    public async Task<byte[]> XuatCauThuExcelAsync(BoLocCauThuDto boLoc, int soDongToiDa = 5000)
    {
        boLoc ??= new BoLocCauThuDto();
        boLoc.Trang = 1;
        boLoc.KichThuocTrang = Math.Clamp(soDongToiDa, 1, 20000);

        var trang = await _cauThu.LocAsync(boLoc);
        var maCauThus = trang.DanhSach.Select(x => x.MaCauThu).ToList();

        // Lấy thêm phần chi tiết không có trong DTO tóm tắt.
        var chiTiet = await _db.CauThus.AsNoTracking()
            .Where(x => maCauThus.Contains(x.MaCauThu))
            .Select(x => new
            {
                x.MaCauThu,
                x.NgaySinh,
                x.ChieuCao,
                x.CanNang,
                x.ChanThuan,
                x.SoAo,
                x.GiaTriCaoNhat,
                x.NgayHetHanHopDong,
                TocDo = x.ChiSo != null ? x.ChiSo.TocDo : 0,
                DutDiem = x.ChiSo != null ? x.ChiSo.DutDiem : 0,
                Chuyen = x.ChiSo != null ? x.ChiSo.Chuyen : 0,
                ReDat = x.ChiSo != null ? x.ChiSo.ReDat : 0,
                PhongNgu = x.ChiSo != null ? x.ChiSo.PhongNgu : 0,
                TheLuc = x.ChiSo != null ? x.ChiSo.TheLuc : 0
            })
            .ToDictionaryAsync(x => x.MaCauThu);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Cầu thủ");

        for (var c = 0; c < TieuDeCot.Length; c++)
            ws.Cell(1, c + 1).Value = TieuDeCot[c];

        var dauDe = ws.Range(1, 1, 1, TieuDeCot.Length);
        dauDe.Style.Font.Bold = true;
        dauDe.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B3A5C");
        dauDe.Style.Font.FontColor = XLColor.White;
        dauDe.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var dong = 2;
        foreach (var ct in trang.DanhSach)
        {
            chiTiet.TryGetValue(ct.MaCauThu, out var ex);

            ws.Cell(dong, 1).Value = ct.MaCauThu;
            ws.Cell(dong, 2).Value = ct.HoTen;
            ws.Cell(dong, 3).Value = ct.CauLacBo;
            ws.Cell(dong, 4).Value = ct.GiaiDau;
            ws.Cell(dong, 5).Value = ct.ViTriVietTat;
            ws.Cell(dong, 6).Value = ct.QuocTich;
            if (ex != null) ws.Cell(dong, 7).Value = ex.NgaySinh;
            ws.Cell(dong, 8).Value = ct.Tuoi;
            if (ex?.ChieuCao != null) ws.Cell(dong, 9).Value = ex.ChieuCao.Value;
            if (ex?.CanNang != null) ws.Cell(dong, 10).Value = ex.CanNang.Value;
            if (ex != null) ws.Cell(dong, 11).Value = ex.ChanThuan.ToString();
            if (ex?.SoAo != null) ws.Cell(dong, 12).Value = ex.SoAo.Value;
            ws.Cell(dong, 13).Value = ct.GiaTriHienTai;
            if (ex != null) ws.Cell(dong, 14).Value = ex.GiaTriCaoNhat;
            if (ex?.NgayHetHanHopDong != null) ws.Cell(dong, 15).Value = ex.NgayHetHanHopDong.Value;
            ws.Cell(dong, 16).Value = ex?.TocDo ?? 0;
            ws.Cell(dong, 17).Value = ex?.DutDiem ?? 0;
            ws.Cell(dong, 18).Value = ex?.Chuyen ?? 0;
            ws.Cell(dong, 19).Value = ex?.ReDat ?? 0;
            ws.Cell(dong, 20).Value = ex?.PhongNgu ?? 0;
            ws.Cell(dong, 21).Value = ex?.TheLuc ?? 0;
            ws.Cell(dong, 22).Value = ct.ChiSoTongHop;
            dong++;
        }

        if (dong > 2)
        {
            ws.Range(2, 7, dong - 1, 7).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Range(2, 15, dong - 1, 15).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Range(2, 13, dong - 1, 14).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(1, 1, dong - 1, TieuDeCot.Length).SetAutoFilter();
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        // Trang thứ hai: điều kiện lọc đã dùng, để người đọc biết số liệu lấy từ đâu.
        var wsLoc = wb.AddWorksheet("Điều kiện lọc");
        wsLoc.Cell(1, 1).Value = "Tiêu chí";
        wsLoc.Cell(1, 2).Value = "Giá trị";
        wsLoc.Range(1, 1, 1, 2).Style.Font.Bold = true;
        var d = 2;
        void Ghi(string ten, object gt)
        {
            wsLoc.Cell(d, 1).Value = ten;
            wsLoc.Cell(d, 2).Value = gt?.ToString() ?? "(không giới hạn)";
            d++;
        }
        Ghi("Thời điểm xuất", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        Ghi("Tổng bản ghi khớp", trang.TongSo);
        Ghi("Số dòng đã xuất", dong - 2);
        Ghi("Từ khóa", boLoc.TuKhoa);
        Ghi("Giải đấu", boLoc.MaGiaiDaus.Count > 0 ? string.Join(", ", boLoc.MaGiaiDaus) : null);
        Ghi("Câu lạc bộ", boLoc.MaCLBs.Count > 0 ? string.Join(", ", boLoc.MaCLBs) : null);
        Ghi("Vị trí", boLoc.MaViTris.Count > 0 ? string.Join(", ", boLoc.MaViTris) : null);
        Ghi("Tuổi", boLoc.TuoiTu.HasValue || boLoc.TuoiDen.HasValue ? $"{boLoc.TuoiTu} - {boLoc.TuoiDen}" : null);
        Ghi("Giá trị (triệu €)", boLoc.GiaTriTu.HasValue || boLoc.GiaTriDen.HasValue ? $"{boLoc.GiaTriTu} - {boLoc.GiaTriDen}" : null);
        Ghi("Sắp xếp", boLoc.SapXep);
        wsLoc.Columns().AdjustToContents();

        using var bo = new MemoryStream();
        wb.SaveAs(bo);
        return bo.ToArray();
    }

    public byte[] TaoMauNhapExcel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Cầu thủ");

        for (var c = 0; c < TieuDeCot.Length; c++)
            ws.Cell(1, c + 1).Value = TieuDeCot[c];

        var dauDe = ws.Range(1, 1, 1, TieuDeCot.Length);
        dauDe.Style.Font.Bold = true;
        dauDe.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B3A5C");
        dauDe.Style.Font.FontColor = XLColor.White;

        // Một dòng ví dụ để người dùng biết định dạng mong đợi.
        ws.Cell(2, 1).Value = "";
        ws.Cell(2, 2).Value = "Nguyễn Văn A";
        ws.Cell(2, 3).Value = "Manchester City";
        ws.Cell(2, 4).Value = "";
        ws.Cell(2, 5).Value = "ST";
        ws.Cell(2, 6).Value = "Việt Nam";
        ws.Cell(2, 7).Value = new DateTime(2002, 5, 14);
        ws.Cell(2, 11).Value = "Phai";
        ws.Cell(2, 13).Value = 12.5;
        ws.Cell(2, 16).Value = 78;
        ws.Cell(2, 17).Value = 80;
        ws.Cell(2, 18).Value = 72;
        ws.Cell(2, 19).Value = 75;
        ws.Cell(2, 20).Value = 45;
        ws.Cell(2, 21).Value = 77;
        ws.Cell(2, 7).Style.DateFormat.Format = "dd/MM/yyyy";

        var wsHd = wb.AddWorksheet("Hướng dẫn");
        var huongDan = new[]
        {
            "Cách nhập dữ liệu cầu thủ",
            "",
            "1. Giữ nguyên dòng tiêu đề, mỗi cầu thủ một dòng bắt đầu từ dòng 2.",
            "2. Cột bắt buộc: Họ tên, Câu lạc bộ, Vị trí, Quốc tịch, Ngày sinh, Giá trị hiện tại.",
            "3. 'Mã cầu thủ' để trống nếu là cầu thủ mới; điền mã nếu muốn cập nhật cầu thủ đã có.",
            "4. 'Câu lạc bộ', 'Vị trí', 'Quốc tịch' phải trùng tên đã có trong hệ thống.",
            "   - Vị trí dùng viết tắt: GK, CB, LB, RB, CDM, CM, CAM, LW, RW, ST ...",
            "5. 'Chân thuận' nhận một trong: Trai, Phai, CaHai.",
            "6. 'Giá trị hiện tại' tính bằng triệu euro, ví dụ 12.5.",
            "7. Các cột chỉ số nhận giá trị nguyên 1-99; bỏ trống sẽ lấy mặc định 60.",
            "8. Nên bấm 'Chạy thử' trước để xem lỗi mà chưa ghi vào cơ sở dữ liệu."
        };
        for (var i = 0; i < huongDan.Length; i++)
            wsHd.Cell(i + 1, 1).Value = huongDan[i];
        wsHd.Cell(1, 1).Style.Font.Bold = true;
        wsHd.Column(1).Width = 95;

        ws.Columns().AdjustToContents();

        using var bo = new MemoryStream();
        wb.SaveAs(bo);
        return bo.ToArray();
    }

    // ---------------------------------------------------------------- E6 nhập

    public async Task<KetQua<KetQuaNhapDto>> NhapCauThuExcelAsync(Stream tepExcel, int maNguoiDung, bool chayThu)
    {
        if (tepExcel == null)
            return KetQua<KetQuaNhapDto>.Loi("THIEU_TEP", "Chưa chọn tệp Excel.");

        var ketQua = new KetQuaNhapDto { ChayThu = chayThu };

        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(tepExcel);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tệp Excel không đọc được.");
            return KetQua<KetQuaNhapDto>.Loi("TEP_HONG",
                "Không đọc được tệp. Hãy dùng đúng định dạng .xlsx tải từ nút 'Tải tệp mẫu'.");
        }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null)
                return KetQua<KetQuaNhapDto>.Loi("TEP_RONG", "Tệp Excel không có trang tính nào.");

            // Nạp sẵn danh mục để tra cứu theo tên, không truy vấn trong vòng lặp.
            var cauLacBos = await _db.CauLacBos.AsNoTracking()
                .ToDictionaryAsync(x => x.TenCLB.Trim().ToLowerInvariant(), x => x.MaCLB);
            var viTris = await _db.ViTriThiDaus.AsNoTracking().ToListAsync();
            var viTriTheoVietTat = viTris.ToDictionary(x => x.VietTat.Trim().ToLowerInvariant(), x => x);
            var quocGias = await _db.QuocGias.AsNoTracking()
                .ToDictionaryAsync(x => x.TenQuocGia.Trim().ToLowerInvariant(), x => x.MaQuocGia);

            var dongCuoi = ws.LastRowUsed()?.RowNumber() ?? 1;
            var themMoi = new List<CauThu>();

            for (var r = 2; r <= dongCuoi; r++)
            {
                var hoTen = ws.Cell(r, 2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(hoTen)) continue;

                ketQua.TongDong++;

                var loiDong = new List<string>();

                var tenCLB = ws.Cell(r, 3).GetString().Trim();
                if (!cauLacBos.TryGetValue(tenCLB.ToLowerInvariant(), out var maCLB))
                    loiDong.Add($"không tìm thấy câu lạc bộ '{tenCLB}'");

                var vietTat = ws.Cell(r, 5).GetString().Trim();
                if (!viTriTheoVietTat.TryGetValue(vietTat.ToLowerInvariant(), out var viTri))
                    loiDong.Add($"không tìm thấy vị trí '{vietTat}'");

                var tenQuocGia = ws.Cell(r, 6).GetString().Trim();
                if (!quocGias.TryGetValue(tenQuocGia.ToLowerInvariant(), out var maQuocGia))
                    loiDong.Add($"không tìm thấy quốc gia '{tenQuocGia}'");

                if (!ThuLayNgay(ws.Cell(r, 7), out var ngaySinh))
                    loiDong.Add("ngày sinh không hợp lệ");
                else if (ngaySinh > DateTime.Today.AddYears(-15) || ngaySinh < DateTime.Today.AddYears(-50))
                    loiDong.Add("ngày sinh nằm ngoài khoảng hợp lý (15-50 tuổi)");

                if (!ThuLaySo(ws.Cell(r, 13), out var giaTri) || giaTri < 0 || giaTri > 500)
                    loiDong.Add("giá trị hiện tại phải là số trong khoảng 0-500");

                if (loiDong.Count > 0)
                {
                    ketQua.SoDongBoQua++;
                    if (ketQua.DanhSachLoi.Count < 100)
                        ketQua.DanhSachLoi.Add($"Dòng {r} ({hoTen}): {string.Join("; ", loiDong)}.");
                    continue;
                }

                var maCauThu = (int)(ThuLaySo(ws.Cell(r, 1), out var maRaw) ? maRaw : 0);
                var cauThu = maCauThu > 0
                    ? await _db.CauThus.Include(x => x.ChiSo).FirstOrDefaultAsync(x => x.MaCauThu == maCauThu)
                    : null;

                var laThemMoi = cauThu == null;
                if (laThemMoi) cauThu = new CauThu();

                cauThu.HoTen = hoTen;
                cauThu.TenKhongDau = ChuanHoa.BoDau(hoTen);
                cauThu.MaCLB = maCLB;
                cauThu.MaViTri = viTri.MaViTri;
                cauThu.MaQuocGia = maQuocGia;
                cauThu.NgaySinh = ngaySinh;
                cauThu.GiaTriHienTai = Math.Round(giaTri, 2);
                if (cauThu.GiaTriCaoNhat < cauThu.GiaTriHienTai)
                    cauThu.GiaTriCaoNhat = cauThu.GiaTriHienTai;
                cauThu.NgayCapNhat = DateTime.UtcNow;
                // Dữ liệu do người dùng nhập tay thì không để tiến trình đồng bộ ghi đè.
                cauThu.ChinhSuaThuCong = true;

                if (ThuLaySo(ws.Cell(r, 9), out var chieuCao) && chieuCao > 140 && chieuCao < 220)
                    cauThu.ChieuCao = (int)chieuCao;
                if (ThuLaySo(ws.Cell(r, 10), out var canNang) && canNang > 40 && canNang < 140)
                    cauThu.CanNang = (int)canNang;
                if (ThuLaySo(ws.Cell(r, 12), out var soAo) && soAo >= 1 && soAo <= 99)
                    cauThu.SoAo = (int)soAo;

                var chanThuanRaw = ws.Cell(r, 11).GetString().Trim();
                if (Enum.TryParse<ChanThuan>(chanThuanRaw, true, out var chan))
                    cauThu.ChanThuan = chan;

                if (ThuLayNgay(ws.Cell(r, 15), out var hetHan))
                    cauThu.NgayHetHanHopDong = hetHan;

                cauThu.ChiSo ??= new ChiSoCauThu { MuaGiai = "2025/2026" };
                cauThu.ChiSo.TocDo = DocChiSo(ws.Cell(r, 16), cauThu.ChiSo.TocDo);
                cauThu.ChiSo.DutDiem = DocChiSo(ws.Cell(r, 17), cauThu.ChiSo.DutDiem);
                cauThu.ChiSo.Chuyen = DocChiSo(ws.Cell(r, 18), cauThu.ChiSo.Chuyen);
                cauThu.ChiSo.ReDat = DocChiSo(ws.Cell(r, 19), cauThu.ChiSo.ReDat);
                cauThu.ChiSo.PhongNgu = DocChiSo(ws.Cell(r, 20), cauThu.ChiSo.PhongNgu);
                cauThu.ChiSo.TheLuc = DocChiSo(ws.Cell(r, 21), cauThu.ChiSo.TheLuc);

                if (laThemMoi)
                {
                    themMoi.Add(cauThu);
                    ketQua.SoDongThem++;
                }
                else
                {
                    ketQua.SoDongCapNhat++;
                }
            }

            if (chayThu)
            {
                // Không ghi gì cả: bỏ mọi thay đổi đang theo dõi.
                foreach (var muc in _db.ChangeTracker.Entries().ToList())
                    muc.State = EntityState.Detached;

                return KetQua<KetQuaNhapDto>.Ok(ketQua,
                    $"Chạy thử: {ketQua.SoDongThem} thêm mới, {ketQua.SoDongCapNhat} cập nhật, " +
                    $"{ketQua.SoDongBoQua} bỏ qua. Chưa ghi vào cơ sở dữ liệu.");
            }

            if (themMoi.Count > 0) _db.CauThus.AddRange(themMoi);

            _db.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaNguoiDung = maNguoiDung,
                HanhDong = "NhapExcel",
                DoiTuong = "CauThu",
                ChiTiet = $"Nhập Excel: +{ketQua.SoDongThem} mới, ~{ketQua.SoDongCapNhat} cập nhật, " +
                          $"bỏ qua {ketQua.SoDongBoQua}/{ketQua.TongDong} dòng."
            });

            await _db.SaveChangesAsync();
        }

        return KetQua<KetQuaNhapDto>.Ok(ketQua,
            $"Đã nhập xong: {ketQua.SoDongThem} thêm mới, {ketQua.SoDongCapNhat} cập nhật, " +
            $"{ketQua.SoDongBoQua} dòng bị bỏ qua.");
    }

    private static int DocChiSo(IXLCell o, int macDinh)
    {
        if (!ThuLaySo(o, out var v)) return macDinh > 0 ? macDinh : 60;
        return (int)Math.Clamp(v, 1, 99);
    }

    private static bool ThuLaySo(IXLCell o, out decimal ketQua)
    {
        ketQua = 0;
        if (o == null || o.IsEmpty()) return false;
        if (o.DataType == XLDataType.Number) { ketQua = (decimal)o.GetDouble(); return true; }
        var chuoi = o.GetString().Trim().Replace(",", ".");
        return decimal.TryParse(chuoi, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out ketQua);
    }

    private static bool ThuLayNgay(IXLCell o, out DateTime ketQua)
    {
        ketQua = default;
        if (o == null || o.IsEmpty()) return false;
        if (o.DataType == XLDataType.DateTime) { ketQua = o.GetDateTime(); return true; }
        var chuoi = o.GetString().Trim();
        string[] dinhDang = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };
        return DateTime.TryParseExact(chuoi, dinhDang, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out ketQua)
               || DateTime.TryParse(chuoi, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out ketQua);
    }

    // ---------------------------------------------------------------- E7 PDF

    public async Task<byte[]> XuatHoSoCauThuPdfAsync(int maCauThu)
    {
        var ct = await _cauThu.LayChiTietAsync(maCauThu);
        if (ct == null) return null;

        var chuyenNhuongs = await _cauThu.LayLichSuChuyenNhuongAsync(maCauThu);
        var giaTris = await _cauThu.LayLichSuGiaTriAsync(maCauThu);
        var tuongTus = await _cauThu.LayCauThuTuongTuAsync(maCauThu, 5);

        var tep = Document.Create(tl =>
        {
            tl.Page(trang =>
            {
                TrangChuan(trang, "HỒ SƠ CẦU THỦ", ct.HoTen);

                trang.Content().PaddingVertical(12).Column(cot =>
                {
                    cot.Spacing(14);

                    cot.Item().Background("#F1F5F9").Padding(10).Row(hang =>
                    {
                        hang.RelativeItem().Column(c =>
                        {
                            c.Item().Text(ct.HoTen).FontSize(18).Bold().FontColor("#0F172A");
                            c.Item().Text($"{ct.ViTri} · {ct.CauLacBo} · {ct.GiaiDau}").FontSize(10).FontColor("#475569");
                            c.Item().Text($"{ct.QuocTich} · {ct.Tuoi} tuổi · {ct.NgaySinh:dd/MM/yyyy}")
                                .FontSize(10).FontColor("#475569");
                        });
                        hang.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(string.Create(DinhDangSo.VN, $"{ct.GiaTriHienTai:N1} tr €"))
                                .FontSize(20).Bold().FontColor("#166534");
                            c.Item().AlignRight().Text(string.Create(DinhDangSo.VN, $"Cao nhất: {ct.GiaTriCaoNhat:N1} tr €"))
                                .FontSize(9).FontColor("#475569");
                            c.Item().AlignRight().Text($"Chỉ số tổng hợp: {ct.ChiSoTongHop}")
                                .FontSize(9).FontColor("#475569");
                        });
                    });

                    cot.Item().Element(e => TieuDeMuc(e, "1. Thông tin cơ bản"));
                    cot.Item().Table(bang =>
                    {
                        bang.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        ODoi(bang, "Chiều cao", ct.ChieuCao.HasValue ? $"{ct.ChieuCao} cm" : "—");
                        ODoi(bang, "Cân nặng", ct.CanNang.HasValue ? $"{ct.CanNang} kg" : "—");
                        ODoi(bang, "Chân thuận", ct.ChanThuan ?? "—");
                        ODoi(bang, "Số áo", ct.SoAo?.ToString() ?? "—");
                        ODoi(bang, "Hết hạn hợp đồng",
                            ct.NgayHetHanHopDong.HasValue ? ct.NgayHetHanHopDong.Value.ToString("dd/MM/yyyy") : "—");
                        ODoi(bang, "So với lần định giá trước",
                            ct.PhanTramSoVoiMocTruoc is decimal ptTruoc && ct.NgayMocTruoc is DateTime ngayTruoc
                                ? $"{DinhDangSo.PhanTramCoDau(ptTruoc)} (từ {ngayTruoc:MM/yyyy})"
                                : "—");
                        ODoi(bang, "Tổng thương vụ", ct.TongSoThuongVu.ToString());
                        ODoi(bang, "Tổng phí chuyển nhượng", string.Create(DinhDangSo.VN, $"{ct.TongPhiChuyenNhuong:N1} tr €"));
                    });

                    if (ct.ChiSo != null)
                    {
                        cot.Item().Element(e => TieuDeMuc(e, "2. Chỉ số chuyên môn"));
                        cot.Item().Table(bang =>
                        {
                            bang.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            var tenChiSo = TenChiSo.Theo(ct.ChiSo.NguonChiSo);
                            ODoi(bang, tenChiSo[0], ct.ChiSo.TocDo.ToString());
                            ODoi(bang, tenChiSo[1], ct.ChiSo.DutDiem.ToString());
                            ODoi(bang, tenChiSo[2], ct.ChiSo.Chuyen.ToString());
                            ODoi(bang, tenChiSo[3], ct.ChiSo.ReDat.ToString());
                            ODoi(bang, tenChiSo[4], ct.ChiSo.PhongNgu.ToString());
                            ODoi(bang, tenChiSo[5], ct.ChiSo.TheLuc.ToString());
                            ODoi(bang, "Số trận", ct.ChiSo.SoTran.ToString());
                            ODoi(bang, "Số phút", ct.ChiSo.SoPhutThiDau.ToString("N0", DinhDangSo.VN));
                            ODoi(bang, "Bàn thắng", ct.ChiSo.BanThang.ToString());
                            ODoi(bang, "Kiến tạo", ct.ChiSo.KienTao.ToString());
                            ODoi(bang, "Thẻ vàng", ct.ChiSo.TheVang.ToString());
                            ODoi(bang, "Thẻ đỏ", ct.ChiSo.TheDo.ToString());
                        });
                    }

                    cot.Item().Element(e => TieuDeMuc(e, "3. Diễn biến giá trị thị trường"));
                    if (giaTris.Count == 0)
                    {
                        cot.Item().Text("Chưa có dữ liệu lịch sử giá trị.").FontSize(9).Italic();
                    }
                    else
                    {
                        cot.Item().Table(bang =>
                        {
                            bang.ColumnsDefinition(c => { c.ConstantColumn(90); c.ConstantColumn(90); c.RelativeColumn(); });
                            DauBang(bang, "Ngày ghi nhận", "Giá trị (tr €)", "Biểu diễn");

                            var lonNhat = giaTris.Max(x => x.GiaTri);
                            foreach (var diem in giaTris.OrderByDescending(x => x.Ngay).Take(12))
                            {
                                bang.Cell().Element(OThuong).Text(diem.Ngay.ToString("dd/MM/yyyy"));
                                bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{diem.GiaTri:N1}"));
                                var tyLe = lonNhat <= 0 ? 0 : (float)(diem.GiaTri / lonNhat);
                                bang.Cell().Element(OThuong).Height(10).Row(h =>
                                {
                                    h.RelativeItem(Math.Max(tyLe, 0.01f)).Background("#2563EB");
                                    h.RelativeItem(Math.Max(1 - tyLe, 0.01f));
                                });
                            }
                        });
                    }

                    cot.Item().Element(e => TieuDeMuc(e, "4. Lịch sử chuyển nhượng"));
                    if (chuyenNhuongs.Count == 0)
                    {
                        cot.Item().Text("Chưa có dữ liệu chuyển nhượng.").FontSize(9).Italic();
                    }
                    else
                    {
                        cot.Item().Table(bang =>
                        {
                            bang.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(60); c.RelativeColumn(); c.RelativeColumn();
                                c.ConstantColumn(70); c.ConstantColumn(70);
                            });
                            DauBang(bang, "Mùa giải", "CLB đi", "CLB đến", "Loại", "Phí (tr €)");
                            foreach (var cn in chuyenNhuongs.OrderByDescending(x => x.NgayChuyenNhuong).Take(12))
                            {
                                bang.Cell().Element(OThuong).Text(cn.MuaGiai ?? "—");
                                bang.Cell().Element(OThuong).Text(cn.CLBDi ?? "—");
                                bang.Cell().Element(OThuong).Text(cn.CLBDen ?? "—");
                                bang.Cell().Element(OThuong).Text(cn.LoaiChuyenNhuong ?? "—");
                                bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{cn.PhiChuyenNhuong:N1}"));
                            }
                        });
                    }

                    if (tuongTus.Count > 0)
                    {
                        cot.Item().Element(e => TieuDeMuc(e, "5. Cầu thủ có hồ sơ tương tự"));
                        cot.Item().Table(bang =>
                        {
                            bang.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(); c.ConstantColumn(45); c.RelativeColumn();
                                c.ConstantColumn(40); c.ConstantColumn(70);
                            });
                            DauBang(bang, "Họ tên", "Tuổi", "Câu lạc bộ", "CS", "Giá trị (tr €)");
                            foreach (var tt in tuongTus)
                            {
                                bang.Cell().Element(OThuong).Text(tt.HoTen);
                                bang.Cell().Element(OThuong).AlignRight().Text(tt.Tuoi.ToString());
                                bang.Cell().Element(OThuong).Text(tt.CauLacBo ?? "—");
                                bang.Cell().Element(OThuong).AlignRight().Text(tt.ChiSoTongHop.ToString());
                                bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{tt.GiaTriHienTai:N1}"));
                            }
                        });
                    }
                });
            });
        });

        return tep.GeneratePdf();
    }

    public async Task<byte[]> XuatBaoCaoThiTruongPdfAsync(string muaGiai, int? maGiaiDau)
    {
        if (string.IsNullOrWhiteSpace(muaGiai))
        {
            var muaGiais = await _thongKe.LayDanhSachMuaGiaiAsync();
            muaGiai = muaGiais.FirstOrDefault() ?? $"{DateTime.UtcNow.Year - 1}/{DateTime.UtcNow.Year}";
        }

        var topThuongVu = await _thongKe.TopThuongVuAsync(muaGiai, maGiaiDau, 15);
        var theoGiai = await _thongKe.TheoGiaiDauAsync(muaGiai);
        var soDuCLB = await _thongKe.SoDuCauLacBoAsync(muaGiai, maGiaiDau, 12);
        var theoTuoi = await _thongKe.GiaTriTheoDoTuoiAsync(maGiaiDau);
        var topGiaTri = await _thongKe.BangXepHangAsync("giatri", 10);

        var tep = Document.Create(tl =>
        {
            tl.Page(trang =>
            {
                TrangChuan(trang, "BÁO CÁO THỊ TRƯỜNG CHUYỂN NHƯỢNG", $"Mùa giải {muaGiai}");

                trang.Content().PaddingVertical(12).Column(cot =>
                {
                    cot.Spacing(14);

                    cot.Item().Element(e => TieuDeMuc(e, "1. Thương vụ có phí cao nhất"));
                    if (topThuongVu.Count == 0)
                    {
                        cot.Item().Text("Không có thương vụ nào trong mùa giải này.").FontSize(9).Italic();
                    }
                    else
                    {
                        cot.Item().Table(bang =>
                        {
                            bang.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(22); c.RelativeColumn(); c.RelativeColumn();
                                c.RelativeColumn(); c.ConstantColumn(70);
                            });
                            DauBang(bang, "#", "Cầu thủ", "CLB đi", "CLB đến", "Phí (tr €)");
                            var i = 1;
                            foreach (var tv in topThuongVu)
                            {
                                bang.Cell().Element(OThuong).Text((i++).ToString());
                                bang.Cell().Element(OThuong).Text(tv.CauThu ?? "—");
                                bang.Cell().Element(OThuong).Text(tv.CLBDi ?? "—");
                                bang.Cell().Element(OThuong).Text(tv.CLBDen ?? "—");
                                bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{tv.PhiChuyenNhuong:N1}"));
                            }
                        });
                    }

                    cot.Item().Element(e => TieuDeMuc(e, "2. Chi tiêu theo giải đấu"));
                    cot.Item().Element(e => BangDiem(e, theoGiai, "Giải đấu", "Tổng phí (tr €)"));

                    cot.Item().Element(e => TieuDeMuc(e, "3. Số dư chuyển nhượng của câu lạc bộ"));
                    cot.Item().Element(e => BangDiem(e, soDuCLB, "Câu lạc bộ", "Số dư (tr €)"));

                    cot.Item().Element(e => TieuDeMuc(e, "4. Giá trị trung bình theo độ tuổi"));
                    cot.Item().Element(e => BangDiem(e, theoTuoi, "Nhóm tuổi", "Giá trị TB (tr €)"));

                    cot.Item().Element(e => TieuDeMuc(e, "5. Cầu thủ giá trị nhất hiện tại"));
                    cot.Item().Table(bang =>
                    {
                        bang.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22); c.RelativeColumn(); c.RelativeColumn();
                            c.ConstantColumn(40); c.ConstantColumn(70);
                        });
                        DauBang(bang, "#", "Cầu thủ", "Câu lạc bộ", "CS", "Giá trị (tr €)");
                        var i = 1;
                        foreach (var ct in topGiaTri)
                        {
                            bang.Cell().Element(OThuong).Text((i++).ToString());
                            bang.Cell().Element(OThuong).Text(ct.HoTen);
                            bang.Cell().Element(OThuong).Text(ct.CauLacBo ?? "—");
                            bang.Cell().Element(OThuong).AlignRight().Text(ct.ChiSoTongHop.ToString());
                            bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{ct.GiaTriHienTai:N1}"));
                        }
                    });

                    cot.Item().PaddingTop(8).Text(
                            "Ghi chú: giá trị thị trường và chỉ số chuyên môn là số liệu ước lượng do hệ thống " +
                            "tính toán từ thống kê thi đấu, không phải số liệu chính thức của câu lạc bộ.")
                        .FontSize(8).Italic().FontColor("#64748B");
                });
            });
        });

        return tep.GeneratePdf();
    }

    // ------------------------------------------------------------ tiện ích PDF

    private static void TrangChuan(PageDescriptor trang, string tieuDe, string phuDe)
    {
        trang.Size(PageSizes.A4);
        trang.Margin(28);
        trang.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor("#0F172A"));

        trang.Header().BorderBottom(2).BorderColor("#1B3A5C").PaddingBottom(6).Row(hang =>
        {
            hang.RelativeItem().Column(c =>
            {
                c.Item().Text(tieuDe).FontSize(14).Bold().FontColor("#1B3A5C");
                c.Item().Text(phuDe).FontSize(10).FontColor("#475569");
            });
            hang.ConstantItem(170).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text("PlayerMarket").FontSize(11).Bold().FontColor("#1B3A5C");
                c.Item().AlignRight().Text($"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}")
                    .FontSize(8).FontColor("#64748B");
            });
        });

        trang.Footer().BorderTop(1).BorderColor("#CBD5E1").PaddingTop(4).Row(hang =>
        {
            hang.RelativeItem().Text("Hệ thống theo dõi giá trị chuyển nhượng cầu thủ")
                .FontSize(8).FontColor("#64748B");
            hang.ConstantItem(90).AlignRight().Text(x =>
            {
                x.Span("Trang ").FontSize(8).FontColor("#64748B");
                x.CurrentPageNumber().FontSize(8).FontColor("#64748B");
                x.Span(" / ").FontSize(8).FontColor("#64748B");
                x.TotalPages().FontSize(8).FontColor("#64748B");
            });
        });
    }

    private static void TieuDeMuc(IContainer o, string chu) =>
        o.BorderLeft(3).BorderColor("#2563EB").PaddingLeft(6)
            .Text(chu).FontSize(11).Bold().FontColor("#1B3A5C");

    private static IContainer OThuong(IContainer o) =>
        o.BorderBottom(1).BorderColor("#E2E8F0").PaddingVertical(3).PaddingHorizontal(4);

    private static IContainer ODauDe(IContainer o) =>
        o.Background("#E2E8F0").BorderBottom(1).BorderColor("#CBD5E1")
            .PaddingVertical(4).PaddingHorizontal(4).DefaultTextStyle(x => x.Bold().FontSize(9));

    private static void DauBang(TableDescriptor bang, params string[] tieuDe)
    {
        bang.Header(h =>
        {
            foreach (var t in tieuDe) h.Cell().Element(ODauDe).Text(t);
        });
    }

    private static void ODoi(TableDescriptor bang, string nhan, string giaTri)
    {
        bang.Cell().Element(OThuong).Text(nhan).FontSize(9).FontColor("#475569");
        bang.Cell().Element(OThuong).Text(giaTri).FontSize(9).Bold();
    }

    private static void BangDiem(IContainer o, List<DiemThongKeDto> diem, string cot1, string cot2)
    {
        if (diem == null || diem.Count == 0)
        {
            o.Text("Không có dữ liệu.").FontSize(9).Italic();
            return;
        }

        o.Table(bang =>
        {
            bang.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); });
            DauBang(bang, cot1, cot2);
            foreach (var d in diem)
            {
                bang.Cell().Element(OThuong).Text(d.Nhan ?? "—");
                bang.Cell().Element(OThuong).AlignRight().Text(string.Create(DinhDangSo.VN, $"{d.GiaTri:N1}"));
            }
        });
    }
}
