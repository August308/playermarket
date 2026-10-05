namespace PlayerTransfer.Application.Common;

/// <summary>Kết quả chung của một thao tác nghiệp vụ.</summary>
public class KetQua
{
    public bool ThanhCong { get; set; }
    public string Ma { get; set; }
    public string ThongDiep { get; set; }

    public static KetQua Ok(string thongDiep = null) =>
        new() { ThanhCong = true, ThongDiep = thongDiep };

    public static KetQua Loi(string ma, string thongDiep) =>
        new() { ThanhCong = false, Ma = ma, ThongDiep = thongDiep };
}

public class KetQua<T> : KetQua
{
    public T DuLieu { get; set; }

    public static KetQua<T> Ok(T duLieu, string thongDiep = null) =>
        new() { ThanhCong = true, DuLieu = duLieu, ThongDiep = thongDiep };

    public new static KetQua<T> Loi(string ma, string thongDiep) =>
        new() { ThanhCong = false, Ma = ma, ThongDiep = thongDiep };
}

/// <summary>Kết quả phân trang dùng chung cho mọi danh sách.</summary>
public class TrangDuLieu<T>
{
    public List<T> DanhSach { get; set; } = new();
    public int TongSo { get; set; }
    public int Trang { get; set; } = 1;
    public int KichThuocTrang { get; set; } = 20;
    public int TongSoTrang => KichThuocTrang <= 0 ? 0 : (int)Math.Ceiling(TongSo / (double)KichThuocTrang);
}
