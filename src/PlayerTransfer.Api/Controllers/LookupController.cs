using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Api.Controllers;

[ApiController]
[Route("api/lookup")]
[OutputCache(PolicyName = "DanhMuc")]
public class LookupController : ControllerBase
{
    private readonly IDanhMucService _danhMuc;

    public LookupController(IDanhMucService danhMuc) => _danhMuc = danhMuc;

    // Mặc định chỉ trả mục có cầu thủ (cho bộ lọc); ?all=true trả đủ (cho form quản trị).

    /// <summary>Danh sách giải đấu.</summary>
    [HttpGet("leagues")]
    public async Task<IActionResult> GiaiDau([FromQuery] bool all = false) => Ok(await _danhMuc.GiaiDauAsync(all));

    /// <summary>Danh sách câu lạc bộ, có thể lọc theo giải đấu.</summary>
    [HttpGet("clubs")]
    public async Task<IActionResult> CauLacBo([FromQuery] string leagueIds, [FromQuery] bool all = false)
    {
        var ds = new List<int>();
        if (!string.IsNullOrWhiteSpace(leagueIds))
        {
            foreach (var phan in leagueIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(phan.Trim(), out var ma)) ds.Add(ma);
        }
        return Ok(await _danhMuc.CauLacBoAsync(ds, all));
    }

    /// <summary>Danh sách vị trí thi đấu.</summary>
    [HttpGet("positions")]
    public async Task<IActionResult> ViTri() => Ok(await _danhMuc.ViTriAsync());

    /// <summary>Danh sách quốc gia.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> QuocGia([FromQuery] bool all = false) => Ok(await _danhMuc.QuocGiaAsync(all));
}
