namespace PlayerTransfer.Domain.Enums;

public enum VaiTro { ThanhVien = 0, QuanTriVien = 1 }

public enum TrangThaiTaiKhoan { ChuaKichHoat = 0, DaKichHoat = 1, BiKhoa = 2 }

/// <summary>
/// KhongRo: nguồn không công bố phí (ô phí trống). Trước đây ô trống bị đọc thành 0 và gắn nhầm
/// "Chuyển nhượng tự do" — trong đó có cả các vụ cho mượn và các vụ giấu phí.
/// </summary>
public enum LoaiChuyenNhuong { MuaDut = 0, ChoMuon = 1, TuDo = 2, HetHan = 3, KetThucMuon = 4, KhongRo = 5 }

public enum ChanThuan { Trai = 0, Phai = 1, CaHai = 2 }

public enum NhomViTri { ThuMon = 0, HauVe = 1, TienVe = 2, TienDao = 3 }
