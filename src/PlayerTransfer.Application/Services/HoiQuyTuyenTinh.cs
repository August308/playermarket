namespace PlayerTransfer.Application.Services;

/// <summary>
/// Hồi quy tuyến tính bội giải bằng phương trình chuẩn (normal equations):
///     beta = (XᵀX + λI)⁻¹ Xᵀy
///
/// Tự cài thay vì dùng thư viện ngoài để dự án không thêm phụ thuộc và để
/// toàn bộ phép tính đều kiểm chứng được khi bảo vệ đồ án.
/// λ (ridge) nhỏ giữ cho ma trận không suy biến khi hai đặc trưng gần cộng tuyến.
/// </summary>
public static class HoiQuyTuyenTinh
{
    /// <summary>
    /// Huấn luyện mô hình. X là ma trận đặc trưng (mỗi hàng một mẫu, CHƯA có cột hệ số chặn),
    /// y là vector nhãn. Trả về vector hệ số, phần tử đầu là hệ số chặn.
    /// </summary>
    public static double[] Huan(double[][] X, double[] y, double lamda = 1e-6)
    {
        if (X == null || y == null || X.Length == 0 || X.Length != y.Length)
            return null;

        var soMau = X.Length;
        var soDacTrung = X[0].Length + 1;   // +1 cho hệ số chặn
        if (soMau <= soDacTrung) return null;

        // Dựng XᵀX và Xᵀy trong một lượt duyệt.
        var A = new double[soDacTrung][];
        for (var i = 0; i < soDacTrung; i++) A[i] = new double[soDacTrung + 1];

        for (var m = 0; m < soMau; m++)
        {
            var hang = new double[soDacTrung];
            hang[0] = 1.0;
            for (var j = 0; j < X[m].Length; j++) hang[j + 1] = X[m][j];

            for (var i = 0; i < soDacTrung; i++)
            {
                for (var j = 0; j < soDacTrung; j++) A[i][j] += hang[i] * hang[j];
                A[i][soDacTrung] += hang[i] * y[m];
            }
        }

        // Ridge: cộng λ vào đường chéo, bỏ qua hệ số chặn.
        for (var i = 1; i < soDacTrung; i++) A[i][i] += lamda;

        return GiaiGauss(A, soDacTrung);
    }

    /// <summary>Khử Gauss có chọn trụ từng phần.</summary>
    private static double[] GiaiGauss(double[][] A, int n)
    {
        for (var cot = 0; cot < n; cot++)
        {
            // Chọn hàng có trị tuyệt đối lớn nhất làm trụ để ổn định số học.
            var hangTru = cot;
            for (var h = cot + 1; h < n; h++)
                if (Math.Abs(A[h][cot]) > Math.Abs(A[hangTru][cot])) hangTru = h;

            if (Math.Abs(A[hangTru][cot]) < 1e-12) return null;   // ma trận suy biến

            (A[cot], A[hangTru]) = (A[hangTru], A[cot]);

            var tru = A[cot][cot];
            for (var j = cot; j <= n; j++) A[cot][j] /= tru;

            for (var h = 0; h < n; h++)
            {
                if (h == cot) continue;
                var heSo = A[h][cot];
                if (Math.Abs(heSo) < 1e-15) continue;
                for (var j = cot; j <= n; j++) A[h][j] -= heSo * A[cot][j];
            }
        }

        var ketQua = new double[n];
        for (var i = 0; i < n; i++) ketQua[i] = A[i][n];
        return ketQua;
    }

    /// <summary>Dự đoán một mẫu: beta₀ + Σ betaᵢ·xᵢ.</summary>
    public static double DuDoan(double[] beta, double[] x)
    {
        if (beta == null || x == null || beta.Length != x.Length + 1) return 0;

        var tong = beta[0];
        for (var i = 0; i < x.Length; i++) tong += beta[i + 1] * x[i];
        return tong;
    }

    /// <summary>Hệ số xác định R² trên tập đưa vào.</summary>
    public static double TinhR2(double[] thuc, double[] duDoan)
    {
        if (thuc == null || duDoan == null || thuc.Length == 0 || thuc.Length != duDoan.Length)
            return 0;

        var trungBinh = thuc.Average();
        double tongBinhPhuongDu = 0, tongBinhPhuongTong = 0;

        for (var i = 0; i < thuc.Length; i++)
        {
            var du = thuc[i] - duDoan[i];
            tongBinhPhuongDu += du * du;
            var lech = thuc[i] - trungBinh;
            tongBinhPhuongTong += lech * lech;
        }

        return tongBinhPhuongTong < 1e-12 ? 0 : 1 - tongBinhPhuongDu / tongBinhPhuongTong;
    }
}
