using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayerTransfer.Application.Common;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Application.Services;

public class XacThucService : IXacThucService
{
    private const int SoLanSaiToiDa = 5;
    private const int SoPhutKhoa = 15;

    private readonly ApplicationDbContext _db;
    private readonly ITokenService _token;
    private readonly IConfiguration _cauHinh;
    private readonly IEmailService _email;
    private readonly ILogger<XacThucService> _log;

    public XacThucService(ApplicationDbContext db, ITokenService token,
        IConfiguration cauHinh, IEmailService email, ILogger<XacThucService> log)
    {
        _db = db; _token = token; _cauHinh = cauHinh; _email = email; _log = log;
    }

    private string DiaChiWeb => (_cauHinh["Web:BaseUrl"] ?? "https://localhost:7211").TrimEnd('/');

    public async Task<KetQua> DangKyAsync(DangKyDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.NguoiDungs.AnyAsync(x => x.Email == email))
            return KetQua.Loi("EMAIL_TON_TAI", "Email đã được đăng ký.");

        var nguoiDung = new NguoiDung
        {
            Email = email,
            HoTen = dto.HoTen.Trim(),
            MatKhauHash = MatKhauHelper.BamMatKhau(dto.MatKhau),
            VaiTro = VaiTro.ThanhVien,
            // Môi trường đồ án chưa cấu hình máy chủ gửi email nên kích hoạt sẵn;
            // đặt Auth:YeuCauKichHoatEmail = true để bật luồng kích hoạt qua email.
            TrangThai = _cauHinh.GetValue("Auth:YeuCauKichHoatEmail", false)
                ? TrangThaiTaiKhoan.ChuaKichHoat
                : TrangThaiTaiKhoan.DaKichHoat,
            MaKichHoat = Guid.NewGuid().ToString("N")
        };

        _db.NguoiDungs.Add(nguoiDung);
        await _db.SaveChangesAsync();
        _log.LogInformation("Tài khoản mới: {Email}", email);

        if (nguoiDung.TrangThai == TrangThaiTaiKhoan.ChuaKichHoat)
        {
            var lienKet = $"{DiaChiWeb}/Account/KichHoat?ma={nguoiDung.MaKichHoat}";
            await _email.GuiAsync(nguoiDung.Email, "Kích hoạt tài khoản PlayerMarket",
                $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(nguoiDung.HoTen)},</p>" +
                $"<p>Nhấn vào liên kết sau để kích hoạt tài khoản (có hiệu lực 24 giờ):</p>" +
                $"<p><a href=\"{lienKet}\">{lienKet}</a></p>");
        }

        return KetQua.Ok(nguoiDung.TrangThai == TrangThaiTaiKhoan.ChuaKichHoat
            ? "Đăng ký thành công, vui lòng kiểm tra email để kích hoạt tài khoản."
            : "Đăng ký thành công, bạn có thể đăng nhập ngay.");
    }

    public async Task<KetQua<TokenDto>> DangNhapAsync(DangNhapDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var nguoiDung = await _db.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email);

        // Kiểm tra khóa tạm trước, để lần thử khi đang bị khóa không cộng dồn thêm số lần sai.
        if (nguoiDung != null && nguoiDung.KhoaDenThoiDiem.HasValue && nguoiDung.KhoaDenThoiDiem > DateTime.UtcNow)
        {
            var soPhutConLai = (int)Math.Ceiling((nguoiDung.KhoaDenThoiDiem.Value - DateTime.UtcNow).TotalMinutes);
            return KetQua<TokenDto>.Loi("TAM_KHOA",
                $"Tài khoản đang bị khóa tạm, vui lòng thử lại sau {soPhutConLai} phút.");
        }

        if (nguoiDung == null || !MatKhauHelper.KiemTra(dto.MatKhau, nguoiDung.MatKhauHash))
        {
            if (nguoiDung != null)
            {
                nguoiDung.SoLanDangNhapSai++;
                if (nguoiDung.SoLanDangNhapSai >= SoLanSaiToiDa)
                    nguoiDung.KhoaDenThoiDiem = DateTime.UtcNow.AddMinutes(SoPhutKhoa);
                await _db.SaveChangesAsync();
            }
            return KetQua<TokenDto>.Loi("SAI_THONG_TIN", "Email hoặc mật khẩu không đúng.");
        }

        if (nguoiDung.TrangThai == TrangThaiTaiKhoan.ChuaKichHoat)
            return KetQua<TokenDto>.Loi("CHUA_KICH_HOAT", "Tài khoản chưa được kích hoạt.");

        if (nguoiDung.TrangThai == TrangThaiTaiKhoan.BiKhoa)
            return KetQua<TokenDto>.Loi("BI_KHOA", "Tài khoản đã bị khóa, vui lòng liên hệ quản trị viên.");

        nguoiDung.SoLanDangNhapSai = 0;
        nguoiDung.KhoaDenThoiDiem = null;

        var accessToken = _token.TaoAccessToken(nguoiDung.MaNguoiDung, nguoiDung.Email,
            nguoiDung.HoTen, nguoiDung.VaiTro.ToString(), out var hetHan);
        var refreshToken = _token.TaoRefreshToken();

        _db.PhienDangNhaps.Add(new PhienDangNhap
        {
            MaNguoiDung = nguoiDung.MaNguoiDung,
            RefreshToken = refreshToken,
            ThoiDiemHetHan = DateTime.UtcNow.AddDays(dto.GhiNhoDangNhap ? 30 : 1),
            ThietBi = RutGon(dto.ThietBi, 200),
            DiaChiIp = RutGon(dto.DiaChiIp, 64)
        });
        await _db.SaveChangesAsync();

        return KetQua<TokenDto>.Ok(new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            HetHanLuc = hetHan,
            HoTen = nguoiDung.HoTen,
            Email = nguoiDung.Email,
            VaiTro = nguoiDung.VaiTro.ToString()
        });
    }

    public async Task<KetQua> DangXuatAsync(string refreshToken)
    {
        var phien = await _db.PhienDangNhaps.FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);
        if (phien != null)
        {
            phien.DaThuHoi = true;
            phien.ThoiDiemThuHoi = DateTime.UtcNow;
            phien.LyDoThuHoi = "DangXuat";
            await _db.SaveChangesAsync();
        }
        return KetQua.Ok("Đã đăng xuất.");
    }

    /// <summary>
    /// E4 — xoay vòng refresh token: mỗi lần làm mới sẽ sinh token mới và thu hồi token cũ.
    /// Nếu một token đã bị thu hồi lại được dùng tiếp, coi như token bị đánh cắp và
    /// thu hồi toàn bộ phiên của tài khoản đó.
    /// </summary>
    public async Task<KetQua<TokenDto>> LamMoiTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return KetQua<TokenDto>.Loi("TOKEN_KHONG_HOP_LE", "Phiên đăng nhập đã hết hạn.");

        var phien = await _db.PhienDangNhaps.Include(x => x.NguoiDung)
            .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);

        if (phien == null)
            return KetQua<TokenDto>.Loi("TOKEN_KHONG_HOP_LE", "Phiên đăng nhập đã hết hạn.");

        // Token đã bị thu hồi mà vẫn được dùng => nghi ngờ bị lộ.
        if (phien.DaThuHoi)
        {
            var soPhienThuHoi = await ThuHoiToanBoAsync(phien.MaNguoiDung, "DungLaiToken");
            _log.LogWarning("Phát hiện dùng lại refresh token đã thu hồi của tài khoản {Ma}; " +
                            "đã thu hồi {SoPhien} phiên.", phien.MaNguoiDung, soPhienThuHoi);
            return KetQua<TokenDto>.Loi("TOKEN_BI_LO",
                "Phát hiện phiên đăng nhập bất thường, vui lòng đăng nhập lại.");
        }

        if (phien.ThoiDiemHetHan < DateTime.UtcNow)
            return KetQua<TokenDto>.Loi("TOKEN_KHONG_HOP_LE", "Phiên đăng nhập đã hết hạn.");

        var nd = phien.NguoiDung;
        if (nd == null || nd.TrangThai != TrangThaiTaiKhoan.DaKichHoat)
            return KetQua<TokenDto>.Loi("TAI_KHOAN_KHONG_HOAT_DONG",
                "Tài khoản không còn hoạt động, vui lòng liên hệ quản trị viên.");

        var tokenMoi = _token.TaoRefreshToken();

        phien.DaThuHoi = true;
        phien.ThoiDiemThuHoi = DateTime.UtcNow;
        phien.LyDoThuHoi = "XoayVong";
        phien.ThayTheBoiToken = tokenMoi;

        _db.PhienDangNhaps.Add(new PhienDangNhap
        {
            MaNguoiDung = nd.MaNguoiDung,
            RefreshToken = tokenMoi,
            // Giữ nguyên hạn của chuỗi phiên ban đầu để không kéo dài vô hạn.
            ThoiDiemHetHan = phien.ThoiDiemHetHan,
            ThietBi = phien.ThietBi,
            DiaChiIp = phien.DiaChiIp
        });

        var accessToken = _token.TaoAccessToken(nd.MaNguoiDung, nd.Email, nd.HoTen,
            nd.VaiTro.ToString(), out var hetHan);

        await _db.SaveChangesAsync();

        return KetQua<TokenDto>.Ok(new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = tokenMoi,
            HetHanLuc = hetHan,
            HoTen = nd.HoTen,
            Email = nd.Email,
            VaiTro = nd.VaiTro.ToString()
        });
    }

    /// <summary>E4 — đăng xuất khỏi mọi thiết bị.</summary>
    public async Task<KetQua> DangXuatTatCaAsync(int maNguoiDung)
    {
        var soPhien = await ThuHoiToanBoAsync(maNguoiDung, "DangXuatTatCa");
        return KetQua.Ok($"Đã đăng xuất khỏi {soPhien} phiên trên mọi thiết bị.");
    }

    /// <summary>E4 — danh sách phiên đăng nhập còn hiệu lực của tài khoản.</summary>
    public async Task<List<PhienDangNhapDto>> LayPhienAsync(int maNguoiDung, string refreshTokenHienTai)
    {
        var bayGio = DateTime.UtcNow;
        var danhSach = await _db.PhienDangNhaps.AsNoTracking()
            .Where(x => x.MaNguoiDung == maNguoiDung && !x.DaThuHoi && x.ThoiDiemHetHan > bayGio)
            .OrderByDescending(x => x.ThoiDiemDangNhap)
            .Select(x => new PhienDangNhapDto
            {
                MaPhien = x.MaPhien,
                ThoiDiemDangNhap = x.ThoiDiemDangNhap,
                ThoiDiemHetHan = x.ThoiDiemHetHan,
                ThietBi = x.ThietBi,
                DiaChiIp = x.DiaChiIp,
                LaPhienHienTai = x.RefreshToken == refreshTokenHienTai
            })
            .ToListAsync();
        return danhSach;
    }

    private async Task<int> ThuHoiToanBoAsync(int maNguoiDung, string lyDo)
    {
        var danhSach = await _db.PhienDangNhaps
            .Where(x => x.MaNguoiDung == maNguoiDung && !x.DaThuHoi)
            .ToListAsync();

        foreach (var p in danhSach)
        {
            p.DaThuHoi = true;
            p.ThoiDiemThuHoi = DateTime.UtcNow;
            p.LyDoThuHoi = lyDo;
        }

        if (danhSach.Count > 0) await _db.SaveChangesAsync();
        return danhSach.Count;
    }

    private static string RutGon(string chuoi, int doDai) =>
        string.IsNullOrWhiteSpace(chuoi) ? null
        : (chuoi.Length <= doDai ? chuoi : chuoi.Substring(0, doDai));

    public async Task<KetQua> KichHoatAsync(string maKichHoat)
    {
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(x => x.MaKichHoat == maKichHoat);
        if (nd == null) return KetQua.Loi("KHONG_HOP_LE", "Mã kích hoạt không hợp lệ.");

        nd.TrangThai = TrangThaiTaiKhoan.DaKichHoat;
        nd.MaKichHoat = null;
        await _db.SaveChangesAsync();
        return KetQua.Ok("Kích hoạt tài khoản thành công.");
    }

    public async Task<KetQua> DoiMatKhauAsync(int maNguoiDung, DoiMatKhauDto dto)
    {
        var nd = await _db.NguoiDungs.FindAsync(maNguoiDung);
        if (nd == null) return KetQua.Loi("KHONG_TIM_THAY", "Không tìm thấy tài khoản.");

        if (!MatKhauHelper.KiemTra(dto.MatKhauCu, nd.MatKhauHash))
            return KetQua.Loi("SAI_MAT_KHAU", "Mật khẩu hiện tại không đúng.");

        nd.MatKhauHash = MatKhauHelper.BamMatKhau(dto.MatKhauMoi);
        await _db.SaveChangesAsync();

        // Đổi mật khẩu thì mọi phiên cũ phải mất hiệu lực.
        await ThuHoiToanBoAsync(maNguoiDung, "DoiMatKhau");
        return KetQua.Ok("Đổi mật khẩu thành công, các thiết bị khác đã bị đăng xuất.");
    }

    public async Task<KetQua> QuenMatKhauAsync(string email)
    {
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant());
        if (nd != null)
        {
            nd.MaDatLaiMatKhau = Guid.NewGuid().ToString("N");
            nd.HanMaDatLaiMatKhau = DateTime.UtcNow.AddHours(2);
            await _db.SaveChangesAsync();

            var lienKet = $"{DiaChiWeb}/Account/DatLaiMatKhau?ma={nd.MaDatLaiMatKhau}";
            var daGui = await _email.GuiAsync(nd.Email, "Đặt lại mật khẩu PlayerMarket",
                $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(nd.HoTen)},</p>" +
                $"<p>Nhấn vào liên kết sau để đặt lại mật khẩu (có hiệu lực 2 giờ):</p>" +
                $"<p><a href=\"{lienKet}\">{lienKet}</a></p>" +
                $"<p>Nếu bạn không yêu cầu, hãy bỏ qua thư này.</p>");

            if (!daGui)
                _log.LogInformation("Mã đặt lại mật khẩu của {Email}: {Ma}", nd.Email, nd.MaDatLaiMatKhau);
        }
        // Luôn trả về thông điệp giống nhau để không tiết lộ email có tồn tại hay không.
        return KetQua.Ok("Nếu email tồn tại, hướng dẫn đặt lại mật khẩu đã được gửi đi.");
    }

    public async Task<KetQua> DatLaiMatKhauAsync(string maDatLai, string matKhauMoi)
    {
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(x => x.MaDatLaiMatKhau == maDatLai);
        if (nd == null || nd.HanMaDatLaiMatKhau < DateTime.UtcNow)
            return KetQua.Loi("KHONG_HOP_LE", "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");

        nd.MatKhauHash = MatKhauHelper.BamMatKhau(matKhauMoi);
        nd.MaDatLaiMatKhau = null;
        nd.HanMaDatLaiMatKhau = null;
        nd.SoLanDangNhapSai = 0;
        nd.KhoaDenThoiDiem = null;
        await _db.SaveChangesAsync();

        await ThuHoiToanBoAsync(nd.MaNguoiDung, "DatLaiMatKhau");
        return KetQua.Ok("Đặt lại mật khẩu thành công.");
    }
}
