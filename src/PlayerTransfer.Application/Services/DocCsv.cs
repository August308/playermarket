using System.Globalization;
using System.Text;

namespace PlayerTransfer.Application.Services;

/// <summary>
/// Bộ đọc CSV theo chuẩn RFC 4180, đọc theo dòng (streaming) nên không nạp cả
/// tệp vào bộ nhớ — cần thiết vì appearances.csv của dataset Transfermarkt có
/// hàng triệu dòng. Tự viết thay vì thêm thư viện ngoài để giảm phụ thuộc.
/// </summary>
public sealed class DocCsv : IDisposable
{
    private readonly StreamReader _doc;
    private readonly Dictionary<string, int> _viTriCot;

    /// <summary>Tên các cột đọc được ở dòng đầu, giữ nguyên thứ tự.</summary>
    public IReadOnlyList<string> TieuDe { get; }

    public DocCsv(string duongDan)
    {
        _doc = new StreamReader(duongDan, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var dongDau = DocMotDong();
        if (dongDau == null)
        {
            TieuDe = Array.Empty<string>();
            _viTriCot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        TieuDe = dongDau.Select(x => x.Trim()).ToList();

        _viTriCot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < TieuDe.Count; i++)
            _viTriCot.TryAdd(TieuDe[i], i);
    }

    /// <summary>Tệp có đủ tất cả các cột này không.</summary>
    public bool CoCot(params string[] ten) => ten.All(x => _viTriCot.ContainsKey(x));

    /// <summary>Trả về tên cột đầu tiên có mặt trong danh sách tên gọi thay thế.</summary>
    public string CotDauTienCo(params string[] tenThayThe) =>
        tenThayThe.FirstOrDefault(x => _viTriCot.ContainsKey(x));

    /// <summary>Duyệt từng dòng dữ liệu; mỗi dòng là một mảng ô theo thứ tự cột.</summary>
    public IEnumerable<string[]> DuyetDong()
    {
        while (true)
        {
            var dong = DocMotDong();
            if (dong == null) yield break;

            // Bỏ qua dòng trống hoàn toàn.
            if (dong.Length == 1 && string.IsNullOrWhiteSpace(dong[0])) continue;

            yield return dong;
        }
    }

    public string O(string[] dong, string tenCot)
    {
        if (tenCot == null || !_viTriCot.TryGetValue(tenCot, out var i)) return null;
        if (i >= dong.Length) return null;

        var giaTri = dong[i];
        return string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();
    }

    public int? So(string[] dong, string tenCot)
    {
        var chuoi = O(dong, tenCot);
        if (chuoi == null) return null;
        return int.TryParse(chuoi, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public decimal? SoThuc(string[] dong, string tenCot)
    {
        var chuoi = O(dong, tenCot);
        if (chuoi == null) return null;
        return decimal.TryParse(chuoi, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public DateTime? Ngay(string[] dong, string tenCot)
    {
        var chuoi = O(dong, tenCot);
        if (chuoi == null) return null;

        // Dataset dùng yyyy-MM-dd, đôi khi kèm giờ.
        string[] dinhDang = { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(chuoi, dinhDang, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var v))
            return v;

        return DateTime.TryParse(chuoi, CultureInfo.InvariantCulture, DateTimeStyles.None, out v) ? v : null;
    }

    // ------------------------------------------------------------ phân tích

    /// <summary>
    /// Đọc một bản ghi CSV. Một bản ghi có thể trải trên nhiều dòng vật lý nếu
    /// ô được bọc trong dấu nháy kép và chứa ký tự xuống dòng.
    /// </summary>
    private string[] DocMotDong()
    {
        if (_doc.EndOfStream) return null;

        var cacO = new List<string>();
        var dem = new StringBuilder();
        var trongNhay = false;

        while (true)
        {
            var kyTuInt = _doc.Read();

            if (kyTuInt < 0)
            {
                // Hết tệp: chốt ô cuối cùng.
                cacO.Add(dem.ToString());
                return cacO.ToArray();
            }

            var kyTu = (char)kyTuInt;

            if (trongNhay)
            {
                if (kyTu == '"')
                {
                    // Hai dấu nháy liền nhau là một dấu nháy thật bên trong ô.
                    if (_doc.Peek() == '"') { _doc.Read(); dem.Append('"'); }
                    else trongNhay = false;
                }
                else
                {
                    dem.Append(kyTu);
                }
                continue;
            }

            switch (kyTu)
            {
                case '"':
                    trongNhay = true;
                    break;

                case ',':
                    cacO.Add(dem.ToString());
                    dem.Clear();
                    break;

                case '\r':
                    // Nuốt luôn \n của cặp CRLF.
                    if (_doc.Peek() == '\n') _doc.Read();
                    cacO.Add(dem.ToString());
                    return cacO.ToArray();

                case '\n':
                    cacO.Add(dem.ToString());
                    return cacO.ToArray();

                default:
                    dem.Append(kyTu);
                    break;
            }
        }
    }

    public void Dispose() => _doc?.Dispose();
}
