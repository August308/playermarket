namespace PlayerTransfer.Application.Services;

/// <summary>
/// D3 — tìm kiếm chịu lỗi chính tả và xếp hạng độ liên quan.
/// Dùng khoảng cách Levenshtein có giới hạn để tránh duyệt toàn bộ ma trận khi chuỗi quá khác nhau.
/// </summary>
public static class TimKiemHelper
{
    /// <summary>
    /// Khoảng cách Levenshtein giữa hai chuỗi (số phép thêm, xóa, thay thế tối thiểu).
    /// Trả về nguongToiDa + 1 ngay khi chắc chắn vượt ngưỡng, để dừng sớm.
    /// </summary>
    public static int KhoangCach(string a, string b, int nguongToiDa = 3)
    {
        a ??= string.Empty;
        b ??= string.Empty;

        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (Math.Abs(a.Length - b.Length) > nguongToiDa) return nguongToiDa + 1;

        var truoc = new int[b.Length + 1];
        var hienTai = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) truoc[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            hienTai[0] = i;
            var nhoNhatHang = hienTai[0];

            for (var j = 1; j <= b.Length; j++)
            {
                var chiPhi = a[i - 1] == b[j - 1] ? 0 : 1;

                hienTai[j] = Math.Min(
                    Math.Min(hienTai[j - 1] + 1, truoc[j] + 1),
                    truoc[j - 1] + chiPhi);

                if (hienTai[j] < nhoNhatHang) nhoNhatHang = hienTai[j];
            }

            // Cả hàng đã vượt ngưỡng thì không cần tính tiếp.
            if (nhoNhatHang > nguongToiDa) return nguongToiDa + 1;

            (truoc, hienTai) = (hienTai, truoc);
        }

        return truoc[b.Length];
    }

    /// <summary>
    /// Chấm điểm độ liên quan giữa từ khóa và tên cầu thủ (cả hai đã bỏ dấu, viết thường).
    /// Điểm càng cao càng khớp; 0 nghĩa là không nên hiển thị.
    /// </summary>
    public static int ChamDiem(string tuKhoa, string ten)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa) || string.IsNullOrWhiteSpace(ten)) return 0;

        if (ten == tuKhoa) return 1000;
        if (ten.StartsWith(tuKhoa, StringComparison.Ordinal)) return 900 - Math.Min(ten.Length - tuKhoa.Length, 80);

        var cacTu = ten.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Khớp đầu một từ trong tên: "haa" khớp "Erling Haaland".
        foreach (var tu in cacTu)
        {
            if (tu.StartsWith(tuKhoa, StringComparison.Ordinal))
                return 800 - Math.Min(tu.Length - tuKhoa.Length, 80);
        }

        if (ten.Contains(tuKhoa, StringComparison.Ordinal)) return 700;

        // Gõ sai vài ký tự: cho phép sai nhiều hơn với từ khóa dài hơn.
        var nguong = tuKhoa.Length <= 4 ? 1 : tuKhoa.Length <= 7 ? 2 : 3;

        var totNhat = nguong + 1;
        foreach (var tu in cacTu)
        {
            var kc = KhoangCach(tuKhoa, tu, nguong);
            if (kc < totNhat) totNhat = kc;
        }

        if (totNhat <= nguong) return 600 - totNhat * 100;

        var kcCaTen = KhoangCach(tuKhoa, ten, nguong);
        return kcCaTen <= nguong ? 500 - kcCaTen * 100 : 0;
    }

    /// <summary>Cắt từ khóa thành đoạn đầu dùng để lọc thô ở tầng cơ sở dữ liệu.</summary>
    public static string DoanDauDeLoc(string tuKhoaKhongDau)
    {
        if (string.IsNullOrWhiteSpace(tuKhoaKhongDau)) return string.Empty;

        // Lấy tối đa 3 ký tự đầu: đủ hẹp để truy vấn nhanh, đủ rộng để không bỏ sót lỗi gõ ở cuối.
        return tuKhoaKhongDau.Length <= 3 ? tuKhoaKhongDau : tuKhoaKhongDau[..3];
    }
}
