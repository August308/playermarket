using System.Globalization;
using System.Text;

namespace PlayerTransfer.Infrastructure.Security;

/// <summary>Tiện ích chuẩn hóa chuỗi phục vụ tìm kiếm không phân biệt dấu và chữ hoa/thường.</summary>
public static class ChuanHoa
{
    public static string BoDau(string chuoi)
    {
        if (string.IsNullOrWhiteSpace(chuoi)) return string.Empty;

        chuoi = chuoi.Replace('đ', 'd').Replace('Đ', 'D');
        var tachDau = chuoi.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(tachDau.Length);

        foreach (var kyTu in tachDau)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(kyTu) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(kyTu);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }
}
