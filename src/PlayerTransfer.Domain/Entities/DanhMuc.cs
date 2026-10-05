using PlayerTransfer.Domain.Enums;

namespace PlayerTransfer.Domain.Entities;

public class QuocGia
{
    public int MaQuocGia { get; set; }
    public string TenQuocGia { get; set; }
    public string MaISO { get; set; }
    public ICollection<CauThu> CauThus { get; set; } = new List<CauThu>();
}

public class GiaiDau
{
    /// <summary>Mã giải trên Transfermarkt, ví dụ GB1, ES1.</summary>
    public string MaTransfermarkt { get; set; }

    public int MaGiaiDau { get; set; }
    public string MaNgoaiApi { get; set; }
    public string TenGiaiDau { get; set; }
    public string Logo { get; set; }
    public int? MaQuocGia { get; set; }
    public QuocGia QuocGia { get; set; }
    public ICollection<CauLacBo> CauLacBos { get; set; } = new List<CauLacBo>();
}

public class CauLacBo
{
    /// <summary>Mã câu lạc bộ trên Transfermarkt.</summary>
    public string MaTransfermarkt { get; set; }

    public int MaCLB { get; set; }
    public string MaNgoaiApi { get; set; }
    public string TenCLB { get; set; }
    public string Logo { get; set; }
    public int? NamThanhLap { get; set; }
    public string SanNha { get; set; }
    public int MaGiaiDau { get; set; }
    public GiaiDau GiaiDau { get; set; }
    public ICollection<CauThu> CauThus { get; set; } = new List<CauThu>();
}

public class ViTriThiDau
{
    public int MaViTri { get; set; }
    public string TenViTri { get; set; }
    public string VietTat { get; set; }
    public NhomViTri NhomViTri { get; set; }
    public ICollection<CauThu> CauThus { get; set; } = new List<CauThu>();
}
