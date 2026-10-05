using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;

namespace PlayerTransfer.Application.Interfaces;

public interface IXacThucService
{
    Task<KetQua> DangKyAsync(DangKyDto dto);
    Task<KetQua<TokenDto>> DangNhapAsync(DangNhapDto dto);
    Task<KetQua> DangXuatAsync(string refreshToken);
    Task<KetQua<TokenDto>> LamMoiTokenAsync(string refreshToken);
    Task<KetQua> KichHoatAsync(string maKichHoat);
    Task<KetQua> DoiMatKhauAsync(int maNguoiDung, DoiMatKhauDto dto);
    Task<KetQua> QuenMatKhauAsync(string email);
    Task<KetQua> DatLaiMatKhauAsync(string maDatLai, string matKhauMoi);

    /// <summary>E4 — thu hồi mọi phiên của tài khoản (đăng xuất khỏi tất cả thiết bị).</summary>
    Task<KetQua> DangXuatTatCaAsync(int maNguoiDung);

    /// <summary>E4 — liệt kê các phiên đăng nhập còn hiệu lực.</summary>
    Task<List<PhienDangNhapDto>> LayPhienAsync(int maNguoiDung, string refreshTokenHienTai);
}

/// <summary>D1 — kênh đẩy thông báo thời gian thực xuống trình duyệt (SignalR).</summary>
public interface IThongBaoRealtime
{
    /// <summary>Phát cho mọi người dùng đang mở web.</summary>
    Task PhatChungAsync(string loai, object duLieu, CancellationToken ct = default);

    /// <summary>Gửi riêng cho một tài khoản (mọi thiết bị của họ).</summary>
    Task GuiNguoiDungAsync(int maNguoiDung, string loai, object duLieu, CancellationToken ct = default);
}

/// <summary>E6 + E7 — xuất/nhập dữ liệu Excel và xuất báo cáo PDF.</summary>
public interface IBaoCaoService
{
    /// <summary>E6 — xuất danh sách cầu thủ theo bộ lọc ra tệp .xlsx.</summary>
    Task<byte[]> XuatCauThuExcelAsync(BoLocCauThuDto boLoc, int soDongToiDa = 5000);

    /// <summary>E6 — nhập cầu thủ từ tệp .xlsx (chỉ quản trị viên).</summary>
    Task<KetQua<KetQuaNhapDto>> NhapCauThuExcelAsync(Stream tepExcel, int maNguoiDung, bool chayThu);

    /// <summary>E6 — tệp .xlsx mẫu để người dùng điền dữ liệu nhập.</summary>
    byte[] TaoMauNhapExcel();

    /// <summary>E7 — hồ sơ cầu thủ dạng PDF.</summary>
    Task<byte[]> XuatHoSoCauThuPdfAsync(int maCauThu);

    /// <summary>E7 — báo cáo thị trường dạng PDF (top thương vụ, phân bố, xu hướng).</summary>
    Task<byte[]> XuatBaoCaoThiTruongPdfAsync(string muaGiai, int? maGiaiDau);
}

public interface ICauThuService
{
    Task<TrangDuLieu<CauThuTomTatDto>> TimKiemAsync(string tuKhoa, int trang, int kichThuocTrang);
    Task<List<CauThuTomTatDto>> GoiYAsync(string tuKhoa, int soLuong);
    Task<TrangDuLieu<CauThuTomTatDto>> LocAsync(BoLocCauThuDto boLoc);
    Task<CauThuChiTietDto> LayChiTietAsync(int maCauThu);
    Task<List<ChuyenNhuongDto>> LayLichSuChuyenNhuongAsync(int maCauThu);
    Task<List<ChanThuongDto>> LayLichSuChanThuongAsync(int maCauThu);
    Task<List<DiemGiaTriDto>> LayLichSuGiaTriAsync(int maCauThu);
    Task<List<CauThuTomTatDto>> LayCauThuTuongTuAsync(int maCauThu, int soLuong);
    Task<KetQua<BangSoSanhDto>> SoSanhAsync(IEnumerable<int> maCauThus);
}

public interface IThongKeService
{
    Task<List<DiemThongKeDto>> TheoMuaGiaiAsync(int? maGiaiDau, int soMua);
    Task<List<DiemThongKeDto>> TheoGiaiDauAsync(string muaGiai);
    Task<List<ThuongVuDto>> TopThuongVuAsync(string muaGiai, int? maGiaiDau, int soLuong);
    Task<List<DiemThongKeDto>> SoDuCauLacBoAsync(string muaGiai, int? maGiaiDau, int soLuong);
    Task<List<DiemThongKeDto>> PhanBoTheoViTriAsync(int? maGiaiDau);
    Task<List<DiemThongKeDto>> GiaTriTheoDoTuoiAsync(int? maGiaiDau);
    Task<List<CauThuTomTatDto>> BangXepHangAsync(string loai, int soLuong);
    Task<List<string>> LayDanhSachMuaGiaiAsync();
}

/// <summary>Các chức năng phân tích nâng cao: tuyển trạch, tìm người thay thế, xu hướng.</summary>
public interface ITuyenTrachService
{
    Task<List<MucTieuDto>> TimMucTieuAsync(TieuChiTuyenTrachDto tieuChi);
    Task<List<NguoiThayTheDto>> TimNguoiThayTheAsync(int maCauThu, decimal? nganSachToiDa, int soLuong);
    Task<TrungBinhViTriDto> TrungBinhViTriAsync(int maViTri);
    Task<List<XuHuongDto>> XuHuongAsync(int soNgay, int soLuong);
    Task GhiLuotXemAsync(int maCauThu, int? maNguoiDung);
}

public interface ITheoDoiService
{
    Task<List<TheoDoiDto>> LayDanhSachAsync(int maNguoiDung);
    Task<KetQua> ThemAsync(int maNguoiDung, int maCauThu);
    Task<KetQua> XoaAsync(int maNguoiDung, int maCauThu);
    Task<KetQua> CapNhatCanhBaoAsync(int maNguoiDung, int maCauThu, decimal? tren, decimal? duoi, string ghiChu);
}

public interface IDoiHinhService
{
    Task<List<DoiHinhDto>> LayDanhSachAsync(int maNguoiDung);
    Task<DoiHinhDto> LayChiTietAsync(int maNguoiDung, int maDoiHinh);
    Task<KetQua<DoiHinhDto>> LuuAsync(int maNguoiDung, LuuDoiHinhDto dto);
    Task<KetQua> XoaAsync(int maNguoiDung, int maDoiHinh);
    Task<KetQua<SoSanhDoiHinhDto>> SoSanhAsync(int maNguoiDung, int maDoiHinhA, int maDoiHinhB);
}

/// <summary>C10 — trò chơi đoán giá trị cầu thủ.</summary>
public interface ITroChoiService
{
    Task<List<CauHoiDoanGiaDto>> TaoCauHoiAsync(int soCau, decimal? giaTriToiThieu);
    Task<KetQua> LuuKetQuaAsync(int maNguoiDung, KetQuaChoiDto ketQua);
    Task<List<HangNguoiChoiDto>> BangXepHangAsync(int soLuong);
}

public interface IDanhMucService
{
    /// <param name="tatCa">false (mặc định): chỉ mục có cầu thủ — dùng cho bộ lọc. true: mọi mục — dùng cho form quản trị.</param>
    Task<List<DanhMucDto>> GiaiDauAsync(bool tatCa = false);
    Task<List<DanhMucDto>> CauLacBoAsync(IEnumerable<int> maGiaiDaus, bool tatCa = false);
    Task<List<DanhMucDto>> ViTriAsync();
    Task<List<DanhMucDto>> QuocGiaAsync(bool tatCa = false);
}

public interface IQuanTriService
{
    Task<KetQua<int>> LuuCauThuAsync(LuuCauThuDto dto, int maNguoiDung);
    Task<KetQua> XoaCauThuAsync(int maCauThu, int maNguoiDung);
    /// <summary>Dữ liệu gốc của một cầu thủ để nạp vào biểu mẫu sửa; null nếu không tồn tại.</summary>
    Task<LuuCauThuDto> LayDeSuaAsync(int maCauThu);
    Task<TrangDuLieu<CauThuTomTatDto>> DanhSachCauThuAsync(string tuKhoa, int trang, int kichThuocTrang);
    Task<List<object>> NhatKyDongBoAsync(int soLuong);
    Task<List<object>> NhatKyThaoTacAsync(int soLuong);
    Task<TrangDuLieu<object>> DanhSachNguoiDungAsync(int trang, int kichThuocTrang);
    Task<KetQua> DoiTrangThaiNguoiDungAsync(int maNguoiDung, string trangThai);
}

public interface ITokenService
{
    string TaoAccessToken(int maNguoiDung, string email, string hoTen, string vaiTro, out DateTime hetHan);
    string TaoRefreshToken();
}

/// <summary>Cổng giao tiếp với API bóng đá bên ngoài (API-Football v3).</summary>
public interface IFootballApiClient
{
    /// <summary>Đã cấu hình khóa API hay chưa.</summary>
    bool DaCauHinh { get; }

    /// <summary>Số lượt gọi đã dùng trong phiên làm việc hiện tại.</summary>
    int SoLuotDaGoi { get; }

    /// <summary>Đã chạm ngưỡng an toàn của hạn mức ngày (mặc định 90%).</summary>
    bool HetHanMuc { get; }

    /// <summary>Số lượt gọi bị API từ chối hoặc thất bại (lỗi nghiệp vụ trong trường "errors", lỗi mạng...).</summary>
    int SoLuotLoi { get; }

    /// <summary>Thông điệp lỗi gần nhất mà API trả về, để hiển thị trong nhật ký đồng bộ.</summary>
    string LoiGanNhat { get; }

    /// <summary>
    /// Đúng khi gói dịch vụ hiện tại không cho truy cập dữ liệu được yêu cầu (ví dụ gói miễn phí
    /// không xem được mùa giải mới). Gọi tiếp chỉ tốn hạn mức nên lượt đồng bộ phải dừng.
    /// </summary>
    bool BiChanBoiGoiDichVu { get; }

    /// <summary>Gọi một điểm cuối và trả về mảng "response" dưới dạng JSON thô.</summary>
    Task<IReadOnlyList<System.Text.Json.JsonElement>> LayAsync(string duongDan, CancellationToken ct = default);
}

/// <summary>Gửi thư điện tử (kích hoạt tài khoản, đặt lại mật khẩu).</summary>
public interface IEmailService
{
    bool DaCauHinh { get; }
    Task<bool> GuiAsync(string email, string tieuDe, string noiDungHtml, CancellationToken ct = default);
}

/// <summary>
/// Nạp bộ dữ liệu Transfermarkt tải từ Kaggle (davidcariboo/player-scores, CC0)
/// làm nền dữ liệu lịch sử, bổ sung cho việc đồng bộ theo ngày từ API-Football.
/// </summary>
public interface INhapCsvService
{
    Task<KetQua<List<XemTruocTepDto>>> XemTruocAsync(string thuMuc);
    Task<KetQua<KetQuaNhapCsvDto>> NhapAsync(TuyChonNhapCsvDto tuyChon, CancellationToken ct = default);
}

public interface IDongBoService
{
    Task<KetQua<string>> DongBoAsync(string phamVi, CancellationToken ct = default);
}

// ======================================================= 10 tính năng phân tích mở rộng

/// <summary>Tính năng 1, 2, 3, 7 — mô hình dự đoán giá và các ứng dụng.</summary>
public interface IDuDoanGiaService
{
    /// <summary>Huấn luyện lại mô hình và trả về chỉ số đánh giá.</summary>
    Task<KetQua<ChiSoMoHinhDto>> HuanLuyenLaiAsync(CancellationToken ct = default);

    Task<ChiSoMoHinhDto> LayChiSoMoHinhAsync(CancellationToken ct = default);

    Task<KetQua<DuDoanGiaDto>> DuDoanChoCauThuAsync(int maCauThu, CancellationToken ct = default);

    /// <summary>Tính năng 2 — cầu thủ thị trường đang định giá lệch nhiều nhất so với mô hình.</summary>
    Task<KetQua<DinhGiaSaiDto>> TimDinhGiaSaiAsync(int soLuong, decimal giaToiThieu, CancellationToken ct = default);

    /// <summary>Tính năng 7 — đối chiếu giá thật với giá mô hình theo giải, vị trí, tuổi, khoảng giá.</summary>
    Task<KetQua<DoiChieuNguonDto>> DoiChieuNguonAsync(CancellationToken ct = default);

    /// <summary>Tính năng 3 — dự báo giá trị các tháng tới kèm dải tin cậy.</summary>
    Task<KetQua<DuBaoGiaDto>> DuBaoAsync(int maCauThu, int soThang, CancellationToken ct = default);
}

/// <summary>Tính năng 4, 5, 8 — phân tích cấp câu lạc bộ.</summary>
public interface IPhanTichCauLacBoService
{
    Task<MangChuyenNhuongDto> MangChuyenNhuongAsync(string muaGiai, int soCungToiDa, CancellationToken ct = default);
    Task<KetQua<CanDoiCauLacBoDto>> CanDoiAsync(int maCLB, CancellationToken ct = default);
    Task<List<DiemThongKeDto>> XepHangSoDuAsync(int soLuong, CancellationToken ct = default);
    Task<KetQua<CoCauTuoiDto>> CoCauTuoiAsync(int maCLB, CancellationToken ct = default);
}

/// <summary>Tính năng 6 — dịch câu hỏi tiếng Việt thành bộ lọc cầu thủ.</summary>
public interface IHieuCauHoiService
{
    Task<KetQuaHieuCauHoiDto> HieuAsync(string cauHoi, CancellationToken ct = default);
}

/// <summary>Tính năng 9 — tổng hợp biến động giá theo kỳ.</summary>
public interface IBienDongGiaService
{
    Task<TongHopBienDongDto> TongHopAsync(int soNgay, int soLuong, CancellationToken ct = default);
    string DungThuHtml(TongHopBienDongDto tongHop, string tenNguoiNhan, string diaChiWeb);
}

/// <summary>Tính năng 10 — tự kiểm chứng chất lượng dữ liệu.</summary>
public interface IKiemChungDuLieuService
{
    Task<BaoCaoKiemChungDto> ChayAsync(CancellationToken ct = default);
}
