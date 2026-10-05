using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Domain.Entities;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// C5 — chỉ số gắn kết đội hình, lấy cảm hứng từ cơ chế "chemistry" của FC Online.
///
/// Cách tính (thang 0–100):
///  · 40 điểm — cùng câu lạc bộ: xét mọi cặp cầu thủ trong đội hình.
///  · 25 điểm — cùng quốc tịch.
///  · 15 điểm — cùng giải đấu.
///  · 20 điểm — xếp đúng nhóm vị trí sở trường.
/// Đội hình chưa đủ 11 người được tính theo số cầu thủ thực có.
/// </summary>
public static class GanKetHelper
{
    public static GanKetDto Tinh(List<DoiHinhAoCauThu> viTris)
    {
        var ketQua = new GanKetDto();

        var cauThus = viTris?
            .Where(x => x.CauThu != null)
            .ToList() ?? new List<DoiHinhAoCauThu>();

        if (cauThus.Count < 2)
        {
            ketQua.NhanXet.Add("Cần tối thiểu 2 cầu thủ để tính độ gắn kết.");
            return ketQua;
        }

        var soCap = cauThus.Count * (cauThus.Count - 1) / 2;
        int cungCLB = 0, cungQuocTich = 0, cungGiai = 0;

        for (var i = 0; i < cauThus.Count; i++)
        {
            for (var j = i + 1; j < cauThus.Count; j++)
            {
                var a = cauThus[i].CauThu;
                var b = cauThus[j].CauThu;

                if (a.MaCLB.HasValue && a.MaCLB == b.MaCLB) cungCLB++;
                if (a.MaQuocGia == b.MaQuocGia) cungQuocTich++;

                var giaiA = a.CauLacBo?.MaGiaiDau;
                var giaiB = b.CauLacBo?.MaGiaiDau;
                if (giaiA.HasValue && giaiA == giaiB) cungGiai++;
            }
        }

        // Đúng vị trí: nhóm vị trí sở trường khớp với ô trên sân.
        var dungViTri = cauThus.Count(x => KhopNhomViTri(x.ViTriTrenSan, x.CauThu));

        ketQua.DiemCungCLB = (int)Math.Round(cungCLB / (double)soCap * 40);
        ketQua.DiemCungQuocTich = (int)Math.Round(cungQuocTich / (double)soCap * 25);
        ketQua.DiemCungGiaiDau = (int)Math.Round(cungGiai / (double)soCap * 15);
        ketQua.DiemDungViTri = (int)Math.Round(dungViTri / (double)cauThus.Count * 20);

        ketQua.ChiSoGanKet = Math.Min(100,
            ketQua.DiemCungCLB + ketQua.DiemCungQuocTich + ketQua.DiemCungGiaiDau + ketQua.DiemDungViTri);

        // Nhận xét để người dùng biết nên cải thiện chỗ nào.
        if (ketQua.DiemCungCLB >= 25) ketQua.NhanXet.Add("Nhiều cầu thủ cùng câu lạc bộ — gắn kết rất tốt.");
        else if (ketQua.DiemCungCLB <= 8) ketQua.NhanXet.Add("Đội hình rải rác nhiều câu lạc bộ, nên gom thêm cầu thủ cùng đội.");

        if (ketQua.DiemCungQuocTich >= 15) ketQua.NhanXet.Add("Khối quốc tịch chung mạnh.");
        if (ketQua.DiemCungGiaiDau >= 12) ketQua.NhanXet.Add("Phần lớn cùng một giải đấu.");

        if (dungViTri < cauThus.Count)
            ketQua.NhanXet.Add($"{cauThus.Count - dungViTri} cầu thủ đang đá lệch nhóm vị trí sở trường.");
        else
            ketQua.NhanXet.Add("Mọi cầu thủ đều đúng nhóm vị trí sở trường.");

        return ketQua;
    }

    /// <summary>Ô trên sân (GK, CB1, LW…) có khớp nhóm vị trí sở trường của cầu thủ không.</summary>
    private static bool KhopNhomViTri(string oTrenSan, CauThu cauThu)
    {
        if (cauThu?.ViTri == null || string.IsNullOrWhiteSpace(oTrenSan)) return false;

        var o = oTrenSan.ToUpperInvariant();
        var nhomO = o.StartsWith("GK") ? Domain.Enums.NhomViTri.ThuMon
            : o.StartsWith("CB") || o.StartsWith("LB") || o.StartsWith("RB")
              || o.StartsWith("LWB") || o.StartsWith("RWB") ? Domain.Enums.NhomViTri.HauVe
            : o.StartsWith("CDM") || o.StartsWith("CM") || o.StartsWith("CAM")
              || o.StartsWith("LM") || o.StartsWith("RM") ? Domain.Enums.NhomViTri.TienVe
            : Domain.Enums.NhomViTri.TienDao;

        return cauThu.ViTri.NhomViTri == nhomO;
    }
}
