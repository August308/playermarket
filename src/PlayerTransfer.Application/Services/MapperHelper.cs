using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Domain.Entities;

namespace PlayerTransfer.Application.Services;

/// <summary>Chuyển đổi thực thể sang DTO. Viết tay để tránh phụ thuộc thư viện ánh xạ.</summary>
public static class MapperHelper
{
    public static int TinhTuoi(DateTime ngaySinh)
    {
        var homNay = DateTime.UtcNow.Date;
        var tuoi = homNay.Year - ngaySinh.Year;
        if (ngaySinh.Date > homNay.AddYears(-tuoi)) tuoi--;
        return tuoi;
    }

    public static CauThuTomTatDto ToTomTat(CauThu ct, bool dangChanThuong = false)
    {
        return new CauThuTomTatDto
        {
            MaCauThu = ct.MaCauThu,
            HoTen = ct.HoTen,
            AnhDaiDien = ct.AnhDaiDien,
            Tuoi = TinhTuoi(ct.NgaySinh),
            MaViTri = ct.MaViTri,
            ViTri = ct.ViTri?.TenViTri,
            ViTriVietTat = ct.ViTri?.VietTat,
            CauLacBo = ct.CauLacBo?.TenCLB ?? "Tự do",
            GiaiDau = ct.CauLacBo?.GiaiDau?.TenGiaiDau,
            QuocTich = ct.QuocGia?.TenQuocGia,
            MaISO = ct.QuocGia?.MaISO,
            GiaTriHienTai = ct.GiaTriHienTai,
            ChiSoTongHop = ct.ChiSo?.ChiSoTongHop ?? 0,
            DangChanThuong = dangChanThuong
        };
    }

    public static ChiSoDto ToChiSo(ChiSoCauThu cs)
    {
        if (cs == null) return null;
        return new ChiSoDto
        {
            TocDo = cs.TocDo,
            DutDiem = cs.DutDiem,
            Chuyen = cs.Chuyen,
            ReDat = cs.ReDat,
            PhongNgu = cs.PhongNgu,
            TheLuc = cs.TheLuc,
            ChiSoTongHop = cs.ChiSoTongHop,
            NguonChiSo = cs.NguonChiSo,
            DiemPhongDo = cs.DiemPhongDo,
            MuaGiai = cs.MuaGiai,
            SoTran = cs.SoTran,
            SoPhutThiDau = cs.SoPhutThiDau,
            BanThang = cs.BanThang,
            KienTao = cs.KienTao,
            TheVang = cs.TheVang,
            TheDo = cs.TheDo
        };
    }

    public static string TenLoaiChuyenNhuong(Domain.Enums.LoaiChuyenNhuong loai) => loai switch
    {
        Domain.Enums.LoaiChuyenNhuong.MuaDut => "Mua đứt",
        Domain.Enums.LoaiChuyenNhuong.ChoMuon => "Cho mượn",
        Domain.Enums.LoaiChuyenNhuong.TuDo => "Không mất phí",
        Domain.Enums.LoaiChuyenNhuong.HetHan => "Hết hạn hợp đồng",
        _ => "Kết thúc cho mượn"
    };

    public static string TenChanThuan(Domain.Enums.ChanThuan? chan) => chan switch
    {
        Domain.Enums.ChanThuan.Trai => "Trái",
        Domain.Enums.ChanThuan.Phai => "Phải",
        Domain.Enums.ChanThuan.CaHai => "Cả hai",
        _ => "Chưa rõ"
    };

    /// <summary>Mùa giải tính từ 01/07 năm trước đến 30/06 năm sau.</summary>
    public static string MuaGiaiCuaNgay(DateTime ngay)
    {
        var namBatDau = ngay.Month >= 7 ? ngay.Year : ngay.Year - 1;
        return $"{namBatDau}/{namBatDau + 1}";
    }
}
