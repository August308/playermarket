namespace PlayerTransfer.Infrastructure.Security;

/// <summary>Băm và kiểm tra mật khẩu bằng BCrypt (work factor 11).</summary>
public static class MatKhauHelper
{
    private const int WorkFactor = 11;

    public static string BamMatKhau(string matKhau) =>
        BCrypt.Net.BCrypt.HashPassword(matKhau, WorkFactor);

    public static bool KiemTra(string matKhau, string hash)
    {
        if (string.IsNullOrEmpty(matKhau) || string.IsNullOrEmpty(hash)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(matKhau, hash);
        }
        catch
        {
            return false;
        }
    }
}
