using System.Globalization;
using System.Text.Json;

namespace PlayerTransfer.Application.Services;

/// <summary>Đọc JSON trả về từ API bóng đá một cách an toàn — thiếu trường thì trả giá trị rỗng.</summary>
public static class JsonHelper
{
    public static JsonElement? Nhanh(this JsonElement goc, params string[] duongDan)
    {
        var hienTai = goc;
        foreach (var khoa in duongDan)
        {
            if (hienTai.ValueKind != JsonValueKind.Object) return null;
            if (!hienTai.TryGetProperty(khoa, out var con)) return null;
            hienTai = con;
        }
        return hienTai.ValueKind == JsonValueKind.Null ? null : hienTai;
    }

    public static string Chuoi(this JsonElement goc, params string[] duongDan)
    {
        var o = goc.Nhanh(duongDan);
        if (o == null) return null;

        return o.Value.ValueKind switch
        {
            JsonValueKind.String => o.Value.GetString(),
            JsonValueKind.Number => o.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static int? So(this JsonElement goc, params string[] duongDan)
    {
        var o = goc.Nhanh(duongDan);
        if (o == null) return null;

        if (o.Value.ValueKind == JsonValueKind.Number && o.Value.TryGetInt32(out var n)) return n;

        var chuoi = o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString() : null;
        if (string.IsNullOrWhiteSpace(chuoi)) return null;

        var so = new string(chuoi.TakeWhile(x => char.IsDigit(x) || x == '-').ToArray());
        return int.TryParse(so, out var k) ? k : null;
    }

    public static double? SoThuc(this JsonElement goc, params string[] duongDan)
    {
        var o = goc.Nhanh(duongDan);
        if (o == null) return null;

        if (o.Value.ValueKind == JsonValueKind.Number) return o.Value.GetDouble();

        var chuoi = o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString() : null;
        return double.TryParse(chuoi, NumberStyles.Any, CultureInfo.InvariantCulture, out var k) ? k : null;
    }

    public static bool LaDung(this JsonElement goc, params string[] duongDan)
    {
        var o = goc.Nhanh(duongDan);
        return o != null && o.Value.ValueKind == JsonValueKind.True;
    }

    public static DateTime? Ngay(this JsonElement goc, params string[] duongDan)
    {
        var chuoi = goc.Chuoi(duongDan);
        if (string.IsNullOrWhiteSpace(chuoi)) return null;

        return DateTime.TryParse(chuoi, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ngay)
            ? ngay.Date
            : null;
    }

    public static List<JsonElement> Mang(this JsonElement goc, params string[] duongDan)
    {
        var o = goc.Nhanh(duongDan);
        if (o == null || o.Value.ValueKind != JsonValueKind.Array) return new List<JsonElement>();
        return o.Value.EnumerateArray().ToList();
    }
}
