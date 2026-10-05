using System.Net;
using System.Text.Json;

namespace PlayerTransfer.Api.Middleware;

/// <summary>Bắt mọi lỗi chưa xử lý và trả về cấu trúc lỗi thống nhất.</summary>
public class XuLyLoiMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<XuLyLoiMiddleware> _log;

    public XuLyLoiMiddleware(RequestDelegate next, ILogger<XuLyLoiMiddleware> log)
    {
        _next = next; _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Lỗi chưa xử lý tại {Duong}", context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            var noiDung = JsonSerializer.Serialize(new
            {
                code = "LOI_HE_THONG",
                message = "Đã xảy ra lỗi phía máy chủ. Vui lòng thử lại sau."
            });

            await context.Response.WriteAsync(noiDung);
        }
    }
}
