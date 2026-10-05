using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Interfaces;
using PlayerTransfer.Infrastructure.Data;

namespace PlayerTransfer.Application.Services;

public class DanhMucService : IDanhMucService
{
    // Danh mục thay đổi rất ít nên được lưu đệm 30 phút (yêu cầu NFR-01).
    private static readonly TimeSpan ThoiGianDem = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _dem;

    public DanhMucService(ApplicationDbContext db, IMemoryCache dem)
    {
        _db = db; _dem = dem;
    }

    private Task<T> LayTuDemAsync<T>(string khoa, Func<Task<T>> nap) =>
        _dem.GetOrCreateAsync(khoa, muc =>
        {
            muc.AbsoluteExpirationRelativeToNow = ThoiGianDem;
            return nap();
        });

    public Task<List<DanhMucDto>> GiaiDauAsync(bool tatCa = false) => LayTuDemAsync($"danhmuc:giaidau:{tatCa}", async () =>
        await _db.GiaiDaus.AsNoTracking()
            // Bộ lọc chỉ liệt kê giải có cầu thủ — chọn một mục rỗng chỉ ra trang trắng.
            .Where(g => tatCa || _db.CauThus.Any(ct => ct.CauLacBo != null && ct.CauLacBo.MaGiaiDau == g.MaGiaiDau))
            .OrderBy(x => x.TenGiaiDau)
            .Select(x => new DanhMucDto { Ma = x.MaGiaiDau, Ten = x.TenGiaiDau })
            .ToListAsync());

    public Task<List<DanhMucDto>> CauLacBoAsync(IEnumerable<int> maGiaiDaus, bool tatCa = false)
    {
        var ds = maGiaiDaus?.ToList() ?? new List<int>();
        var khoa = $"danhmuc:clb:{tatCa}:" + string.Join(",", ds.OrderBy(x => x));

        return LayTuDemAsync(khoa, async () =>
        {
            var truyVan = _db.CauLacBos.AsNoTracking()
                .Where(c => tatCa || _db.CauThus.Any(ct => ct.MaCLB == c.MaCLB));
            if (ds.Any()) truyVan = truyVan.Where(x => ds.Contains(x.MaGiaiDau));

            return await truyVan
            .OrderBy(x => x.TenCLB)
            .Select(x => new DanhMucDto
            {
                Ma = x.MaCLB,
                Ten = x.TenCLB,
                ThongTinThem = x.GiaiDau.TenGiaiDau
            })
            .ToListAsync();
        });
    }

    public Task<List<DanhMucDto>> ViTriAsync() => LayTuDemAsync("danhmuc:vitri", async () =>
        await _db.ViTriThiDaus.AsNoTracking()
            .OrderBy(x => x.MaViTri)
            .Select(x => new DanhMucDto
            {
                Ma = x.MaViTri,
                Ten = x.TenViTri,
                ThongTinThem = x.VietTat
            })
            .ToListAsync());

    public Task<List<DanhMucDto>> QuocGiaAsync(bool tatCa = false) => LayTuDemAsync($"danhmuc:quocgia:{tatCa}", async () =>
        await _db.QuocGias.AsNoTracking()
            .Where(q => tatCa || _db.CauThus.Any(ct => ct.MaQuocGia == q.MaQuocGia))
            .OrderBy(x => x.TenQuocGia)
            .Select(x => new DanhMucDto
            {
                Ma = x.MaQuocGia,
                Ten = x.TenQuocGia,
                ThongTinThem = x.MaISO
            })
            .ToListAsync());
}
