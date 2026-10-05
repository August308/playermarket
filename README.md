# Hệ thống theo dõi giá trị chuyển nhượng cầu thủ (PlayerMarket)

Đồ án Quản lý dự án phần mềm — ASP.NET Core 8, Entity Framework Core, giao diện ASP.NET Core MVC.

## 1. Cấu trúc solution

| Dự án | Vai trò | Mục WBS |
|---|---|---|
| `PlayerTransfer.Domain` | Thực thể, enum | 3.3 |
| `PlayerTransfer.Infrastructure` | `ApplicationDbContext`, cấu hình EF, dữ liệu mẫu, băm mật khẩu | 4.1, 4.2 |
| `PlayerTransfer.Application` | DTO, dịch vụ nghiệp vụ, JWT, nhập bộ dữ liệu Transfermarkt, đồng bộ API-Football, dọn dữ liệu | 5.1 |
| `PlayerTransfer.Api` | Web API (78 điểm cuối) + Swagger + kênh thời gian thực SignalR | 5.1 |
| `PlayerTransfer.Web` | Giao diện MVC, gọi sang API bằng `HttpClient` | 5.2 |
| `PlayerTransfer.Tests` | 67 kiểm thử xUnit (đơn vị + tích hợp API) | 6.x |

## 2. Yêu cầu môi trường

- .NET SDK 8.0 trở lên.
- Cơ sở dữ liệu: **mặc định là SQLite** (tệp `src/PlayerTransfer.Api/playertransfer.db`, không cần cài gì thêm).
  Muốn dùng SQL Server thì xem mục 6.

## 3. Chạy lần đầu

```bash
dotnet restore
dotnet build

# Cửa sổ 1 — API (tự tạo cơ sở dữ liệu, nạp dữ liệu mẫu, dọn dữ liệu cũ)
dotnet run --project src/PlayerTransfer.Api      # https://localhost:7011/swagger

# Cửa sổ 2 — giao diện
dotnet run --project src/PlayerTransfer.Web      # https://localhost:7211
```

Trong Visual Studio: chuột phải solution → **Configure Startup Projects** → **Multiple startup projects** →
đặt `PlayerTransfer.Api` và `PlayerTransfer.Web` là **Start**.

Chạy bằng Docker: sao chép `.env.example` thành `.env`, đặt `JWT_KEY`, rồi `docker compose up --build`
(giao diện ở `http://localhost:8081`).

## 4. Tài khoản mặc định

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Quản trị viên | `admin@playertransfer.local` | `Admin@123` |
| Thành viên | `member@playertransfer.local` | `Member@123` |

Hai tài khoản này chỉ được gợi ý trên trang đăng nhập khi chạy ở môi trường `Development`.
Hãy đổi mật khẩu trước khi triển khai thật.

## 5. Dữ liệu

Hệ thống dùng hai nguồn:

1. **Bộ dữ liệu Transfermarkt** (`davidcariboo/player-scores` trên Kaggle, giấy phép CC0) — nguồn chính:
   cầu thủ, câu lạc bộ, giá trị thị trường và lịch sử giá, chuyển nhượng, số liệu thi đấu.
   Nạp tại **Quản trị → Nạp bộ dữ liệu** (dán đường dẫn thư mục chứa các tệp `.csv`).
   Cơ sở dữ liệu hiện có 2.851 cầu thủ của 96 câu lạc bộ ở 5 giải hàng đầu châu Âu, mùa 2025/2026.
2. **API-Football** — tùy chọn, để cập nhật đội hình và chấn thương (xem mục 7).

Khi chưa nạp nguồn nào, hệ thống chạy bằng 149 cầu thủ mẫu có sẵn trong mã nguồn.

Những điều cần biết về dữ liệu:

- **Sáu chỉ số của cầu thủ** được tính từ số liệu thi đấu thật (số phút, bàn thắng, kiến tạo, thẻ phạt), nên
  mang tên đúng với thứ chúng đo: *Ra sân, Ghi bàn, Kiến tạo, Tấn công, Kỷ luật, Thể lực*. Bộ dữ liệu không đo
  được tốc độ hay khả năng phòng ngự. Cầu thủ thi đấu quá ít phút sẽ hiện "chưa đủ số liệu" thay vì số 0.
- **Chấn thương:** bộ Transfermarkt không có dữ liệu chấn thương. Trang hồ sơ ghi rõ "chưa có dữ liệu";
  thông tin chỉ xuất hiện khi đồng bộ được từ API-Football.
- **Loại chuyển nhượng:** thương vụ có phí bằng 0 hiển thị là "Không mất phí" vì nguồn không phân biệt
  chuyển nhượng tự do với cho mượn; thương vụ không công bố phí hiển thị "Chưa công bố phí".
- **Mốc dữ liệu** (ngày cập nhật giá và số liệu thi đấu) khai báo ở khóa `MocDuLieu` trong
  `src/PlayerTransfer.Web/appsettings.json` và được in trên trang chủ, trang hồ sơ.
- Mỗi lần khởi động, API tự dọn dữ liệu sai do phiên bản cũ để lại (mốc giá trùng, câu lạc bộ và giải đấu
  trùng tên, tên quốc gia chưa Việt hóa, nhật ký đồng bộ ghi sai trạng thái). Các bước này chạy lại không gây hại.

## 6. Dùng SQL Server thay cho SQLite

Trong `src/PlayerTransfer.Api/appsettings.json`:

```json
{
  "ConnectionStrings": { "Default": "Server=(localdb)\\MSSQLLocalDB;Database=PlayerTransfer;Trusted_Connection=True;TrustServerCertificate=True" },
  "Database": { "Provider": "SqlServer", "TuDongTao": true }
}
```

## 7. Cắm khóa API-Football (tùy chọn)

1. Đăng ký tại `dashboard.api-football.com/register`, lấy khóa ở mục **Account → My Access**.
2. Mở `src/PlayerTransfer.Api/appsettings.Development.json` (tệp này không đưa lên Git), điền:

```json
{
  "FootballApi": {
    "Key": "KHOA_CUA_BAN",
    "BatDongBoDinhKy": true,
    "MuaGiai": 2024
  }
}
```

3. Khởi động lại API. Vào **Quản trị → Đồng bộ → Đồng bộ ngay** để chạy thủ công.

**Gói miễn phí** chỉ cho 100 lượt gọi mỗi ngày và **chỉ truy cập được các mùa 2022–2024**. Nếu để trống
`MuaGiai`, hệ thống gọi mùa hiện tại và API sẽ từ chối; khi đó nhật ký đồng bộ ghi **Thất bại** kèm lý do.
Hệ thống tự dừng khi dùng tới 90% hạn mức.

Lưu ý: số liệu mùa 2024 cũ hơn bộ Transfermarkt 2025/2026 đang có. Chỉ nên bật đồng bộ với gói miễn phí khi
chưa nạp bộ dữ liệu Transfermarkt, hoặc khi cần dữ liệu chấn thương để trình diễn.

Bản ghi do quản trị viên tạo hoặc sửa được đánh cờ `ChinhSuaThuCong = true` và không bị đồng bộ ghi đè.

## 8. Chạy kiểm thử

```bash
dotnet test
```

67 kiểm thử: đăng ký/đăng nhập/khóa tài khoản/thu hồi token, tìm kiếm và lọc, so sánh, đồng bộ, nhập CSV,
dọn dữ liệu, định dạng số và giờ, quản trị cầu thủ, cùng các kiểm thử tích hợp gọi thẳng vào API.

## 9. Chức năng

- **Xác thực:** đăng ký, đăng nhập, đăng xuất, ghi nhớ đăng nhập 30 ngày, làm mới token, đổi mật khẩu,
  quên/đặt lại mật khẩu, quản lý thiết bị đăng nhập, khóa tạm sau 5 lần sai, băm mật khẩu BCrypt.
- **Tra cứu:** tìm kiếm không dấu có gợi ý tức thời, bộ lọc đa tiêu chí, sắp xếp, phân trang, bảng lệnh `Ctrl+K`.
- **Hồ sơ cầu thủ:** thông tin cơ bản, sáu chỉ số, số liệu mùa giải, biểu đồ biến động giá, lịch sử chuyển nhượng,
  lịch sử chấn thương, cầu thủ tương đồng, dự báo giá, xuất PDF.
- **So sánh:** 2–4 cầu thủ, tô đậm giá trị tốt nhất, biểu đồ radar, xuất CSV và PNG.
- **Thống kê và phân tích** (dành cho thành viên): theo mùa, giải đấu, vị trí, độ tuổi; top thương vụ; cân đối
  câu lạc bộ; mạng chuyển nhượng; mô hình định giá; biến động giá; tuyển trạch; hỏi bằng tiếng Việt.
- **Cá nhân:** danh sách theo dõi kèm ngưỡng cảnh báo giá, đội hình ảo, trò chơi đoán giá.
- **Quản trị:** thêm/sửa/xóa cầu thủ, quản lý người dùng, đồng bộ, nạp bộ dữ liệu, nhập Excel, nhật ký thao tác,
  kiểm chứng chất lượng dữ liệu.
- **Giao diện:** chế độ sáng/tối, bố cục đáp ứng từ 360px (có menu đầy đủ trên điện thoại), chế độ trình chiếu.

## 10. Quy ước định dạng

- Số hiển thị theo kiểu Việt Nam (`1.234,5`) trên mọi máy, qua `DinhDangSo` (máy chủ) và `PMSo`/`PMGia` (trình duyệt).
- Thời điểm lưu ở UTC, hiển thị theo giờ Việt Nam qua `DinhDangSo.GioVN`.
- Máy chủ đọc số theo định dạng bất biến (`1234.5`) nên biểu mẫu hoạt động giống nhau dù Windows cài tiếng Việt hay tiếng Anh.

## 11. Bảo mật khi triển khai

- **Khóa JWT:** ngoài môi trường `Development`, API **từ chối khởi động** nếu `Jwt:Key` còn là chuỗi mẫu hoặc
  ngắn hơn 32 ký tự. Đặt qua biến môi trường `Jwt__Key` (Docker: `JWT_KEY` trong `.env`).
- **Chứng chỉ TLS:** giao diện chỉ bỏ qua kiểm tra chứng chỉ của API khi chạy `Development`. Nếu buộc phải dùng
  chứng chỉ tự ký ở môi trường khác, đặt `Api:BoQuaKiemTraChungChi = true` trong cấu hình của dự án Web.
- **CSRF:** mọi lệnh POST của giao diện đều phải kèm mã chống giả mạo (biểu mẫu tự có; JavaScript lấy qua `PMCsrf()`).
- **Token:** refresh token không bao giờ nằm trên URL; access token cho kênh thời gian thực được trình duyệt
  lấy qua `/Account/TokenRealtime` thay vì in sẵn trong mã HTML.
- Không đưa lên Git: `appsettings.Development.json`, `.env`, tệp `*.db`, thư mục `keys/` và `logs/`.

## 12. Triển khai lên Render (gói miễn phí)

Render chỉ cấp **một** cổng công khai cho mỗi dịch vụ và dịch vụ miễn phí không gọi nội bộ lẫn nhau được,
nên API và giao diện chạy chung một container: giao diện nghe cổng công khai, API chỉ nghe ở `127.0.0.1:8081`.

| Tệp | Vai trò |
|---|---|
| `Dockerfile.render` | Dựng ảnh chứa cả API lẫn giao diện |
| `deploy/khoi-dong.sh` | Chép cơ sở dữ liệu ban đầu, chạy API rồi giao diện, giới hạn bộ nhớ từng tiến trình |
| `deploy/du-lieu-khoi-tao.sqlite` | Cơ sở dữ liệu ban đầu (2.851 cầu thủ) được đóng sẵn vào ảnh |
| `render.yaml` | Bản thiết kế để Render tự tạo dịch vụ và biến môi trường |

Các bước: đẩy mã lên GitHub → Render → **New → Blueprint** → chọn kho mã → nhập `QuanTri__MatKhau` → **Apply**.

Biến môi trường:

| Biến | Ý nghĩa |
|---|---|
| `Jwt__Key` | Khóa ký JWT, Render tự sinh. Thiếu biến này API không khởi động. |
| `QuanTri__MatKhau` | Mật khẩu của `admin@playertransfer.local`, đặt lại mỗi lần khởi động. |
| `PORT` | Cổng giao diện nghe (10000). |
| `FootballApi__Key`, `FootballApi__BatDongBoDinhKy` | Tùy chọn, xem mục 7. |
| `HEAP_API`, `HEAP_WEB` | Tùy chọn: trần heap của từng tiến trình khi dùng gói nhiều RAM hơn. |

Giới hạn của gói miễn phí cần biết:

- **Dữ liệu không bền.** Dịch vụ ngủ sau khoảng 15 phút không có truy cập và mỗi lần thức dậy, triển khai lại
  hay khởi động lại, cơ sở dữ liệu quay về bản gốc trong ảnh: tài khoản mới đăng ký, danh sách theo dõi, đội hình ảo
  đều mất. Muốn giữ dữ liệu phải dùng gói trả phí và gắn ổ đĩa bền vào `/app/dulieu`.
- **Lần truy cập đầu sau khi ngủ mất khoảng một phút** để container khởi động.
- **Kênh thời gian thực (SignalR) tắt** vì trình duyệt không nối thẳng tới API được; mọi chức năng khác vẫn chạy.
- **Bộ nhớ 512 MB:** khi chạy thử toàn bộ chức năng, hai tiến trình dùng tổng cộng khoảng 430 MB lúc cao nhất.
- Trang "Nạp bộ dữ liệu" cần đường dẫn thư mục trên máy chủ nên không dùng được trên Render. Muốn đổi dữ liệu:
  nạp trên máy mình, chép `src/PlayerTransfer.Api/playertransfer.db` đè lên `deploy/du-lieu-khoi-tao.sqlite` rồi đẩy lên lại.
