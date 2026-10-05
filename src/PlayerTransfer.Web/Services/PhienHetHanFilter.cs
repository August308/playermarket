using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PlayerTransfer.Web.Services;

/// <summary>
/// Khi phiên đăng nhập hết hạn hẳn GIỮA CHỪNG (refresh token cũng không dùng được), ApiClient đánh dấu
/// vào HttpContext.Items. Bộ lọc này đổi kết quả của trang đang mở thành chuyển sang đăng nhập kèm
/// thông báo, thay vì hiện ra một trang rỗng như thể người dùng không có dữ liệu.
/// </summary>
public class PhienHetHanFilter : IAsyncResultFilter
{
    public const string Khoa = "PhienVuaHetHan";

    private readonly ITempDataDictionaryFactory _tempData;

    public PhienHetHanFilter(ITempDataDictionaryFactory tempData) => _tempData = tempData;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http = context.HttpContext;
        var laTrangThuong = HttpMethods.IsGet(http.Request.Method)
                            && !http.Request.Headers.ContainsKey("HX-Request")
                            && context.Result is ViewResult;

        if (laTrangThuong && http.Items.ContainsKey(Khoa))
        {
            _tempData.GetTempData(http)["LoiThongBao"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
            context.Result = new RedirectToActionResult("DangNhap", "Account",
                new { returnUrl = http.Request.Path + http.Request.QueryString });
        }

        await next();
    }
}
