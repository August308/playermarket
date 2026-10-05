using Microsoft.EntityFrameworkCore;
using PlayerTransfer.Domain.Entities;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Security;

namespace PlayerTransfer.Infrastructure.Data;

/// <summary>
/// E12 — sinh thêm cầu thủ mô phỏng để kiểm thử hiệu năng trên tập dữ liệu lớn
/// (mặc định nâng tổng số lên khoảng 2.000 cầu thủ). Dữ liệu sinh ra hoàn toàn
/// xác định (cùng hạt ngẫu nhiên → cùng kết quả) và luôn được đánh dấu
/// LaDuLieuMoPhong = true để phân biệt với 149 cầu thủ có thật.
/// </summary>
public static partial class SeedData
{
    private static readonly string[] HoMau =
    {
        "Silva", "Martins", "Kovacs", "Nowak", "Andersen", "Larsen", "Fischer", "Weber",
        "Moreau", "Dubois", "Rossi", "Ferrari", "Garcia", "Lopez", "Navarro", "Costa",
        "Okafor", "Mensah", "Diallo", "Traore", "Yilmaz", "Demir", "Petrov", "Ivanov",
        "Horvat", "Novak", "Dvorak", "Vargas", "Rojas", "Suarez", "Murphy", "O'Brien",
        "Walsh", "Hughes", "Clarke", "Baker", "Tanaka", "Nakamura", "Kim", "Park"
    };

    private static readonly string[] TenMau =
    {
        "Luca", "Mateo", "Diego", "Rafael", "Bruno", "Tomas", "Pavel", "Marek",
        "Jonas", "Emil", "Nico", "Felix", "Louis", "Hugo", "Enzo", "Theo",
        "Kwame", "Amadou", "Ismael", "Yusuf", "Can", "Deniz", "Ivan", "Dimitri",
        "Ante", "Luka", "Filip", "Santiago", "Andres", "Julian", "Liam", "Sean",
        "Oscar", "Elliot", "Aaron", "Noah", "Kenji", "Haru", "Minjun", "Jiho"
    };

    /// <summary>
    /// Bổ sung cầu thủ mô phỏng cho tới khi tổng số cầu thủ đạt <paramref name="tongMucTieu"/>.
    /// Không làm gì nếu số cầu thủ hiện có đã đủ.
    /// </summary>
    public static async Task SinhThemCauThuAsync(ApplicationDbContext db, int tongMucTieu)
    {
        if (tongMucTieu <= 0) return;

        var hienCo = await db.CauThus.CountAsync();
        var canThem = tongMucTieu - hienCo;
        if (canThem <= 0) return;

        var cauLacBos = await db.CauLacBos.AsNoTracking().Select(x => x.MaCLB).ToListAsync();
        var viTris = await db.ViTriThiDaus.AsNoTracking()
            .Select(x => new { x.MaViTri, x.NhomViTri }).ToListAsync();
        var quocGias = await db.QuocGias.AsNoTracking().Select(x => x.MaQuocGia).ToListAsync();

        if (cauLacBos.Count == 0 || viTris.Count == 0 || quocGias.Count == 0) return;

        // Hạt cố định để mỗi lần tạo lại cơ sở dữ liệu đều ra cùng một tập dữ liệu.
        var rnd = new Random(20260922);
        var tenDaDung = new HashSet<string>(
            await db.CauThus.AsNoTracking().Select(x => x.HoTen).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var loGhi = new List<CauThu>(500);
        var daThem = 0;

        while (daThem < canThem)
        {
            var hoTen = TaoTen(rnd, tenDaDung);
            var viTri = viTris[rnd.Next(viTris.Count)];

            // Giá trị lệch phải: đa số cầu thủ giá thấp, một số ít rất đắt.
            var giaTri = Math.Round((decimal)(0.3 + Math.Pow(rnd.NextDouble(), 3.1) * 145), 2);
            var tuoi = 17 + rnd.Next(0, 20);
            var ngaySinh = new DateTime(DateTime.UtcNow.Year - tuoi, rnd.Next(1, 13), rnd.Next(1, 28));
            var chieuCao = viTri.NhomViTri == NhomViTri.ThuMon ? rnd.Next(183, 200) : rnd.Next(165, 196);

            var ct = new CauThu
            {
                HoTen = hoTen,
                TenKhongDau = ChuanHoa.BoDau(hoTen),
                MaCLB = cauLacBos[rnd.Next(cauLacBos.Count)],
                MaViTri = viTri.MaViTri,
                MaQuocGia = quocGias[rnd.Next(quocGias.Count)],
                NgaySinh = ngaySinh,
                GiaTriHienTai = giaTri,
                GiaTriCaoNhat = giaTri,
                ChanThuan = rnd.Next(100) switch { < 72 => Domain.Enums.ChanThuan.Phai, < 94 => Domain.Enums.ChanThuan.Trai, _ => Domain.Enums.ChanThuan.CaHai },
                ChieuCao = chieuCao,
                CanNang = 62 + (chieuCao - 165) * 3 / 4,
                SoAo = rnd.Next(1, 100),
                NgayHetHanHopDong = new DateTime(DateTime.UtcNow.Year + rnd.Next(0, 5), 6, 30),
                NgayCapNhat = DateTime.UtcNow,
                LaDuLieuMoPhong = true
            };
            ct.ChiSo = TaoChiSo(ct, viTri.NhomViTri, giaTri, rnd);

            loGhi.Add(ct);
            daThem++;

            // Ghi theo lô để không giữ quá nhiều thực thể trong bộ nhớ.
            if (loGhi.Count >= 500)
            {
                db.CauThus.AddRange(loGhi);
                await db.SaveChangesAsync();
                foreach (var muc in db.ChangeTracker.Entries().ToList()) muc.State = EntityState.Detached;
                loGhi.Clear();
            }
        }

        if (loGhi.Count > 0)
        {
            db.CauThus.AddRange(loGhi);
            await db.SaveChangesAsync();
            foreach (var muc in db.ChangeTracker.Entries().ToList()) muc.State = EntityState.Detached;
        }

        await SinhLichSuGiaAsync(db, rnd);
    }

    /// <summary>Sinh 8 mốc giá gần nhất cho những cầu thủ mô phỏng chưa có lịch sử.</summary>
    private static async Task SinhLichSuGiaAsync(ApplicationDbContext db, Random rnd)
    {
        var moc = DateTime.UtcNow.Date;

        // Xử lý theo lô 500 cầu thủ để tránh nạp toàn bộ vào bộ nhớ một lần.
        var daXong = 0;
        while (true)
        {
            var lo = await db.CauThus.AsNoTracking()
                .Where(x => x.LaDuLieuMoPhong)
                .OrderBy(x => x.MaCauThu)
                .Skip(daXong)
                .Take(500)
                .Select(x => new { x.MaCauThu, x.GiaTriHienTai })
                .ToListAsync();

            if (lo.Count == 0) break;
            daXong += lo.Count;

            var maTrongLo = lo.Select(x => x.MaCauThu).ToList();
            var daCoLichSu = (await db.LichSuGiaTris.AsNoTracking()
                    .Where(x => maTrongLo.Contains(x.MaCauThu))
                    .Select(x => x.MaCauThu)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            var themMoi = new List<LichSuGiaTri>();
            foreach (var ct in lo)
            {
                if (daCoLichSu.Contains(ct.MaCauThu)) continue;

                var gia = ct.GiaTriHienTai;
                for (var i = 0; i < 8; i++)
                {
                    themMoi.Add(new LichSuGiaTri
                    {
                        MaCauThu = ct.MaCauThu,
                        NgayGhiNhan = moc.AddMonths(-3 * i),
                        GiaTri = Math.Round(gia, 2),
                        Nguon = "DuLieuMoPhong"
                    });
                    var bienDong = 1m + (decimal)(rnd.NextDouble() * 0.30 - 0.12);
                    gia = Math.Max(0.3m, Math.Round(gia / bienDong, 2));
                }
            }

            if (themMoi.Count > 0)
            {
                db.LichSuGiaTris.AddRange(themMoi);
                await db.SaveChangesAsync();
                foreach (var muc in db.ChangeTracker.Entries().ToList()) muc.State = EntityState.Detached;
            }
        }
    }

    private static string TaoTen(Random rnd, HashSet<string> daDung)
    {
        for (var lan = 0; lan < 40; lan++)
        {
            var ten = $"{TenMau[rnd.Next(TenMau.Length)]} {HoMau[rnd.Next(HoMau.Length)]}";
            if (daDung.Add(ten)) return ten;
        }

        // Hết tổ hợp thì thêm hậu tố số để bảo đảm không trùng.
        string tenPhu;
        var dem = 2;
        do
        {
            tenPhu = $"{TenMau[rnd.Next(TenMau.Length)]} {HoMau[rnd.Next(HoMau.Length)]} {dem++}";
        } while (!daDung.Add(tenPhu));

        return tenPhu;
    }
}
