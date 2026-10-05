using Microsoft.AspNetCore.DataProtection;
using PlayerTransfer.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Mọi phép đọc/ghi số của máy (liên kết biểu mẫu, JSON, CSV) dùng định dạng bất biến "1234.5",
// bất kể Windows cài tiếng Việt hay tiếng Anh. Nếu để theo máy, trên máy tiếng Việt ô nhập "0.5"
// sẽ bị hiểu thành 5. Phần HIỂN THỊ kiểu Việt Nam (1.234,5) do DinhDangSo đảm nhiệm.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;


builder.Host.UseSerilog((context, cauHinhLog) => cauHinhLog
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/web-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<PhienHetHanFilter>();
    // Mọi lệnh POST/PUT/DELETE phải kèm mã chống giả mạo (CSRF). Trước đây chỉ vài action của
    // AccountController kiểm tra; các lệnh xóa cầu thủ, đổi trạng thái tài khoản, chạy đồng bộ… thì không.
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Khóa mã hóa cookie (phiên, chống giả mạo, "ghi nhớ đăng nhập") lưu vào thư mục của ứng dụng để
// không đổi sau mỗi lần khởi động lại — nếu đổi, mọi cookie cũ thành vô hiệu (log từng báo
// "The payload was invalid").
builder.Services.AddDataProtection()
    .SetApplicationName("PlayerTransfer.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

builder.Services.AddSingleton<BoLamMoiToken>();
builder.Services.AddSingleton<GhiNhoDangNhap>();
builder.Services.AddScoped<TheoDoiCuaToi>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7011/";

// Chỉ bỏ qua kiểm tra chứng chỉ TLS khi phát triển cục bộ (chứng chỉ tự ký của `dotnet dev-certs`).
// Trước đây bật ở MỌI môi trường — khi triển khai thật, kẻ đứng giữa có thể giả làm máy chủ API.
// Nếu buộc phải dùng chứng chỉ tự ký ngoài môi trường phát triển, đặt Api:BoQuaKiemTraChungChi = true.
var boQuaChungChi = builder.Environment.IsDevelopment()
                    || builder.Configuration.GetValue("Api:BoQuaKiemTraChungChi", false);

HttpClientHandler TaoBoXuLyHttp()
{
    var boXuLy = new HttpClientHandler();
    if (boQuaChungChi)
        boXuLy.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return boXuLy;
}
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(TaoBoXuLyHttp);

// Client riêng cho thao tác chạy lâu (nạp bộ dữ liệu Transfermarkt vài chục nghìn dòng).
// Tách khỏi client chung để một lần nạp chậm không làm các trang khác chờ theo.
builder.Services.AddHttpClient("ApiDaiHan", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(30);
})
.ConfigurePrimaryHttpMessageHandler(TaoBoXuLyHttp);

// Chạy sau proxy của nền tảng (Render, Railway, nginx…): proxy kết thúc TLS rồi gọi vào bằng HTTP.
// Bật ForwardedHeaders:TinProxy để ứng dụng biết yêu cầu gốc là HTTPS (cookie Secure, không chuyển hướng lặp)
// và biết IP thật của người dùng (giới hạn tần suất, danh sách thiết bị). CHỈ bật khi chắc chắn có proxy đứng trước.
var tinProxy = builder.Configuration.GetValue("ForwardedHeaders:TinProxy", false);
if (tinProxy)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = null;          // lấy địa chỉ đầu chuỗi = trình duyệt của người dùng
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (tinProxy) app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Loi");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// "Ghi nhớ đăng nhập": phiên trống nhưng còn cookie ghi nhớ thì đăng nhập lại ngầm trước khi
// trang được dựng, để thanh điều hướng và các trang cần đăng nhập thấy đúng trạng thái.
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.ContainsKey(GhiNhoDangNhap.TenCookie)
        && string.IsNullOrEmpty(context.Session.GetString("AccessToken")))
    {
        await context.RequestServices.GetRequiredService<ApiClient>().KhoiPhucTuGhiNhoAsync();
    }

    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
