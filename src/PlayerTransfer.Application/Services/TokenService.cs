using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlayerTransfer.Application.Interfaces;

namespace PlayerTransfer.Application.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _cauHinh;

    public TokenService(IConfiguration cauHinh) => _cauHinh = cauHinh;

    public string TaoAccessToken(int maNguoiDung, string email, string hoTen, string vaiTro, out DateTime hetHan)
    {
        var khoa = _cauHinh["Jwt:Key"] ?? "khoa-mac-dinh-chi-dung-cho-moi-truong-phat-trien-2026";
        var issuer = _cauHinh["Jwt:Issuer"] ?? "PlayerTransfer";
        var audience = _cauHinh["Jwt:Audience"] ?? "PlayerTransferClient";
        var soPhut = int.TryParse(_cauHinh["Jwt:AccessTokenMinutes"], out var m) ? m : 60;

        hetHan = DateTime.UtcNow.AddMinutes(soPhut);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, maNguoiDung.ToString()),
            new(ClaimTypes.NameIdentifier, maNguoiDung.ToString()),
            new(ClaimTypes.Email, email ?? string.Empty),
            new(ClaimTypes.Name, hoTen ?? string.Empty),
            new(ClaimTypes.Role, vaiTro ?? "ThanhVien"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(khoa)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: hetHan, signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string TaoRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }
}
