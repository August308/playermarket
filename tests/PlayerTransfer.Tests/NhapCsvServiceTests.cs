using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlayerTransfer.Application.Dtos;
using PlayerTransfer.Application.Services;
using PlayerTransfer.Domain.Enums;
using PlayerTransfer.Infrastructure.Data;
using Xunit;

namespace PlayerTransfer.Tests;

/// <summary>Nạp bộ dữ liệu Transfermarkt thu nhỏ (đúng tên cột của bộ thật) vào SQLite trong bộ nhớ.</summary>
public class NhapCsvServiceTests : IDisposable
{
    private readonly SqliteConnection _ketNoi;
    private readonly ApplicationDbContext _db;
    private readonly string _thuMuc;

    public NhapCsvServiceTests()
    {
        _ketNoi = new SqliteConnection("DataSource=:memory:");
        _ketNoi.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_ketNoi).Options);
        _db.Database.EnsureCreated();
        // Bảng vị trí thi đấu và quốc gia đến từ dữ liệu mẫu, giống lúc API khởi động thật.
        SeedData.SeedAsync(_db).GetAwaiter().GetResult();

        _thuMuc = Path.Combine(Path.GetTempPath(), "tm-thu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_thuMuc);

        Ghi("competitions.csv", "competition_id,name,type,country_name",
            "GB1,premier-league,domestic_league,England");
        Ghi("clubs.csv", "club_id,name,domestic_competition_id,stadium_name,last_season",
            "11,Arsenal FC,GB1,Emirates Stadium,2025",
            "631,Chelsea FC,GB1,Stamford Bridge,2025");
        Ghi("players.csv",
            "player_id,name,date_of_birth,country_of_citizenship,sub_position,position,foot,height_in_cm,market_value_in_eur,highest_market_value_in_eur,current_club_id,contract_expiration_date,image_url,last_season",
            "1001,Cau Thu Mot,2000-01-01,England,Centre-Forward,Attack,right,185,50000000,60000000,11,2028-06-30,,2025");
        Ghi("player_valuations.csv", "player_id,date,market_value_in_eur",
            "1001,2025-01-01,40000000", "1001,2025-12-01,50000000");
        Ghi("transfers.csv",
            "player_id,transfer_date,transfer_season,from_club_id,to_club_id,from_club_name,to_club_name,transfer_fee,market_value_in_eur",
            // Phí TRỐNG: không công bố (thường là cho mượn) — từ CLB ngoài hệ thống.
            "1001,2021-07-01,21/22,9999,631,PSV Eindhoven,Chelsea FC,,10000000",
            // Phí 0: tự do.
            "1001,2023-07-01,23/24,631,11,Chelsea FC,Arsenal FC,0,20000000",
            // Có phí.
            "1001,2024-07-01,24/25,631,11,Chelsea FC,Arsenal FC,35000000,30000000");
        Ghi("appearances.csv", "player_id,date,goals,assists,minutes_played,yellow_cards,red_cards",
            "1001,2025-09-01,1,0,90,0,0");
    }

    private void Ghi(string ten, params string[] dong) =>
        File.WriteAllLines(Path.Combine(_thuMuc, ten), dong);

    [Fact(DisplayName = "Nhập CSV: phí trống = chưa công bố, phí 0 = tự do; giữ tên CLB ngoài hệ thống")]
    public async Task NhapChuyenNhuong_PhanLoaiDung()
    {
        var dichVu = new NhapCsvService(_db, NullLogger<NhapCsvService>.Instance);

        var kq = await dichVu.NhapAsync(new TuyChonNhapCsvDto { ThuMuc = _thuMuc, MuaGiaiToiThieu = 2025 });
        Assert.True(kq.ThanhCong, kq.ThongDiep);
        Assert.True(kq.DuLieu.CauThuThemMoi + kq.DuLieu.CauThuCapNhat > 0, "Lỗi: " + string.Join(" | ", kq.DuLieu.DanhSachLoi) + " / " + kq.ThongDiep);

        var ds = _db.LichSuChuyenNhuongs.OrderBy(x => x.NgayChuyenNhuong).ToList();
        Assert.Equal(3, ds.Count);

        Assert.Equal(LoaiChuyenNhuong.KhongRo, ds[0].LoaiChuyenNhuong);
        Assert.Null(ds[0].MaCLBDi);
        Assert.Equal("PSV Eindhoven", ds[0].TenCLBDiNgoai);

        Assert.Equal(LoaiChuyenNhuong.TuDo, ds[1].LoaiChuyenNhuong);
        Assert.Equal(LoaiChuyenNhuong.MuaDut, ds[2].LoaiChuyenNhuong);
        Assert.Equal(35m, ds[2].PhiChuyenNhuong);

        // Hiển thị: CLB ngoài hệ thống phải ra đúng tên, không phải "Tự do".
        var hienThi = await new CauThuService(_db).LayLichSuChuyenNhuongAsync(ds[0].MaCauThu);
        Assert.Contains(hienThi, x => x.CLBDi == "PSV Eindhoven" && x.LoaiChuyenNhuong == "Chưa công bố phí");
    }

    public void Dispose()
    {
        _db.Dispose();
        _ketNoi.Dispose();
        try { Directory.Delete(_thuMuc, true); } catch { /* bỏ qua */ }
    }
}
