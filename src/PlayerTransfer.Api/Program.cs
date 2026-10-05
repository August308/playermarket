using Microsoft.AspNetCore.HttpOverrides;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlayerTransfer.Api.Hubs;
using PlayerTransfer.Api.Middleware;
using PlayerTransfer.Application;
using PlayerTransfer.Application.Services;
using PlayerTransfer.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Mọi phép đọc/ghi số của máy (liên kết biểu mẫu, JSON, CSV) dùng định dạng bất biến "1234.5",
// bất kể Windows cài tiếng Việt hay tiếng Anh. Nếu để theo máy, trên máy tiếng Việt ô nhập "0.5"
// sẽ bị hiểu thành 5. Phần HIỂN THỊ kiểu Việt Nam (1.234,5) do DinhDangSo đảm nhiệm.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;


// Ghi nhật ký ra màn hình và ra tệp theo ngày (yêu cầu NFR-10).
builder.Host.UseSerilog((context, cauHinhLog) => cauHinhLog
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

builder.Services.ThemDichVuUngDung(builder.Configuration);
builder.Services.AddControllers();

// D1 — kênh thời gian thực. Đăng ký SAU ThemDichVuUngDung để thay thế bản rỗng.
builder.Services.AddSignalR();
builder.Services.AddSingleton<PlayerTransfer.Application.Interfaces.IThongBaoRealtime, ThongBaoSignalR>();

// E7 — QuestPDF dùng giấy phép Community (miễn phí cho dự án phi thương mại).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

// D2 — bộ đệm phản hồi cho các điểm cuối công khai, đọc nhiều và ít đổi.
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(x => x.NoCache());
    options.AddPolicy("DanhMuc", x => x.Expire(TimeSpan.FromMinutes(30)).SetVaryByQuery("*"));
    options.AddPolicy("BangXepHang", x => x.Expire(TimeSpan.FromMinutes(10)).SetVaryByQuery("*"));
});

if (builder.Configuration.GetValue("FootballApi:BatDongBoDinhKy", false))
{
    builder.Services.AddHostedService<DongBoNenService>();
}

// E2 + E3 — chụp ảnh giá hằng ngày và gửi cảnh báo khi chạm ngưỡng.
if (builder.Configuration.GetValue("ChupAnhGia:Bat", true))
{
    builder.Services.AddHostedService<ChupAnhGiaNenService>();
}

// Tính năng 9 — thư tổng hợp biến động giá mỗi tuần.
if (builder.Configuration.GetValue("ThuTuan:Bat", false))
{
    builder.Services.AddHostedService<ThuTuanNenService>();
}

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Hệ thống theo dõi giá trị chuyển nhượng cầu thủ",
        Version = "v1",
        Description = "Tài liệu API cho đồ án Quản lý dự án phần mềm."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập access token nhận được từ /api/auth/login."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Đọc khóa JWT lúc dịch vụ được dựng chứ không phải lúc builder vừa khởi tạo:
// TokenService cũng đọc cấu hình lúc chạy, nên hai phía luôn dùng cùng một khóa
// (kể cả khi cấu hình bị ghi đè sau, như trong kiểm thử tích hợp).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, cauHinh) =>
    {
        var khoaJwt = cauHinh["Jwt:Key"] ?? "khoa-mac-dinh-chi-dung-cho-moi-truong-phat-trien-2026";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cauHinh["Jwt:Issuer"] ?? "PlayerTransfer",
            ValidAudience = cauHinh["Jwt:Audience"] ?? "PlayerTransferClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(khoaJwt)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // WebSocket không gắn được header Authorization nên SignalR truyền token
        // qua query string; chỉ chấp nhận cách này trên đúng đường dẫn hub.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments(ThongBaoHub.DuongDan))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// Giao diện Web gọi API thay mặt người dùng và chuyển tiếp IP thật qua X-Forwarded-For.
// Chỉ tin tiêu đề này từ máy cục bộ (mặc định) hoặc các mạng khai báo trong
// ForwardedHeaders:MangTinCay (ví dụ mạng nội bộ Docker), để kẻ khác không giả IP được.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;

    foreach (var cidr in builder.Configuration.GetSection("ForwardedHeaders:MangTinCay").Get<string[]>() ?? Array.Empty<string>())
    {
        var phan = cidr.Split('/');
        if (phan.Length == 2 && System.Net.IPAddress.TryParse(phan[0], out var diaChi) && int.TryParse(phan[1], out var doDai))
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(diaChi, doDai));
    }
});

// Khóa phân vùng giới hạn tần suất: người đã đăng nhập tính theo tài khoản, khách tính theo IP
// thật của họ (không phải IP của máy chủ Web như trước — khi đó cả hệ thống dùng chung một hạn mức).
static string KhoaGioiHan(HttpContext context) =>
    context.User.Identity?.IsAuthenticated == true
        ? "nd:" + context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro");

// Chống dò mật khẩu ở tầng mạng: giới hạn số lần gọi nhóm điểm cuối xác thực.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("XacThuc", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro"),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: KhoaGioiHan(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ChiQuanTriVien", policy => policy.RequireRole("QuanTriVien"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ChoPhepWeb", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                     ?? new[] { "https://localhost:7211", "http://localhost:5211" })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Khóa ký JWT: ngoài môi trường phát triển, KHÔNG cho chạy với khóa mẫu trong appsettings.json hay khóa
// mặc định trong mã nguồn — ai đọc được kho mã cũng tự ký được token quản trị viên.
{
    var khoaJwtCauHinh = app.Configuration["Jwt:Key"];
    var laKhoaMau = string.IsNullOrWhiteSpace(khoaJwtCauHinh)
                    || khoaJwtCauHinh.StartsWith("hay-thay-bang", StringComparison.OrdinalIgnoreCase)
                    || khoaJwtCauHinh.StartsWith("khoa-mac-dinh", StringComparison.OrdinalIgnoreCase)
                    || Encoding.UTF8.GetByteCount(khoaJwtCauHinh) < 32;

    if (laKhoaMau)
    {
        const string huongDan =
            "Jwt:Key đang là khóa mẫu hoặc ngắn hơn 32 ký tự. Hãy đặt một chuỗi bí mật ngẫu nhiên qua biến môi trường " +
            "Jwt__Key (Docker: JWT_KEY trong tệp .env) hoặc `dotnet user-secrets set Jwt:Key ...`.";

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            app.Logger.LogWarning("CẢNH BÁO BẢO MẬT: {HuongDan} (Chỉ được bỏ qua ở môi trường phát triển.)", huongDan);
        else
            throw new InvalidOperationException(huongDan);
    }
}

// Tạo cơ sở dữ liệu và nạp dữ liệu mẫu khi khởi động lần đầu.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (app.Configuration.GetValue("Database:TuDongTao", true))
        {
            await db.Database.EnsureCreatedAsync();
        }
        // Cơ sở dữ liệu tạo từ phiên bản cũ chưa có các cột mới — bổ sung trước khi truy vấn.
        await DonDepDuLieuService.NangCapLuocDoAsync(db, log);
        await SeedData.SeedAsync(db);

        // E12 — nâng tập dữ liệu lên quy mô lớn để kiểm thử hiệu năng.
        // Đặt Seed:TongCauThu = 0 trong appsettings nếu chỉ muốn 149 cầu thủ thật.
        var tongCauThu = app.Configuration.GetValue("Seed:TongCauThu", 2000);
        if (tongCauThu > 0)
        {
            await SeedData.SinhThemCauThuAsync(db, tongCauThu);
        }

        // Sửa dữ liệu sai do các phiên bản trước để lại (idempotent).
        await DonDepDuLieuService.ChayAsync(db, log);

        // Triển khai công khai: mật khẩu quản trị mặc định (Admin@123) nằm ngay trong mã nguồn và README,
        // nên cho phép đặt mật khẩu khác qua cấu hình QuanTri:MatKhau (biến môi trường QuanTri__MatKhau).
        // Chạy mỗi lần khởi động vì trên máy chủ không có ổ đĩa bền, cơ sở dữ liệu được dựng lại từ bản gốc.
        var matKhauQuanTri = app.Configuration["QuanTri:MatKhau"];
        if (!string.IsNullOrWhiteSpace(matKhauQuanTri))
        {
            var emailQuanTri = app.Configuration["QuanTri:Email"] ?? "admin@playertransfer.local";
            var quanTri = await db.NguoiDungs.FirstOrDefaultAsync(x => x.Email == emailQuanTri);
            if (quanTri == null)
            {
                log.LogWarning("QuanTri:MatKhau được đặt nhưng không tìm thấy tài khoản {Email}.", emailQuanTri);
            }
            else if (!PlayerTransfer.Infrastructure.Security.MatKhauHelper.KiemTra(matKhauQuanTri, quanTri.MatKhauHash))
            {
                quanTri.MatKhauHash = PlayerTransfer.Infrastructure.Security.MatKhauHelper.BamMatKhau(matKhauQuanTri);
                await db.SaveChangesAsync();
                log.LogInformation("Đã đặt lại mật khẩu quản trị viên theo cấu hình QuanTri:MatKhau.");
            }
        }
    }
    catch (Exception ex)
    {
        log.LogError(ex,
            "KHÔNG KẾT NỐI ĐƯỢC CƠ SỞ DỮ LIỆU. Hãy kiểm tra chuỗi kết nối ConnectionStrings:Default " +
            "trong appsettings.json (hiện tại: {ChuoiKetNoi}). Nếu máy không có LocalDB, hãy đổi sang tên " +
            "máy chủ SQL Server thật, hoặc đặt Database:Provider = \"Sqlite\" và ConnectionStrings:Default " +
            "= \"Data Source=playertransfer.db\" để chạy thử không cần SQL Server.",
            builder.Configuration.GetConnectionString("Default"));
        throw;
    }
}

// Phải chạy đầu tiên để mọi bước sau (giới hạn tần suất, ghi phiên đăng nhập) thấy IP thật.
app.UseForwardedHeaders();
app.UseMiddleware<XuLyLoiMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("BatSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlayerTransfer API v1"));
}

app.UseSerilogRequestLogging();

// E11 — máy chủ kiểm thử chạy trên HTTP nên không chuyển hướng, nếu không mọi
// yêu cầu trong kiểm thử tích hợp đều trả về 307 thay vì nội dung thật.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseOutputCache();
app.UseCors("ChoPhepWeb");
// Xác thực TRƯỚC giới hạn tần suất để có thể phân vùng theo tài khoản.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ThongBaoHub>(ThongBaoHub.DuongDan);

app.Run();

// E11 — để WebApplicationFactory<Program> trong dự án kiểm thử nhìn thấy lớp này.
public partial class Program { }
