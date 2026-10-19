using Microsoft.AspNetCore.StaticFiles;

namespace QuanAnBackend.Middleware;

/// <summary>
/// Giữ ảnh món dùng chung ổn định cho cả WinForms và Mobile.
/// Nếu database còn trỏ tới một ảnh lịch sử chưa có trên máy mới,
/// backend trả về PNG placeholder thay vì 404. Khi ảnh thật được upload
/// lại vào wwwroot/images/menu thì StaticFileMiddleware tự phục vụ ảnh thật.
/// </summary>
public sealed class MenuImageFallbackMiddleware
{
    private static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
    );

    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public MenuImageFallbackMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsMenuImageRequest(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var relativePath = context.Request.Path.Value!
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot"
            );
        }

        var fullPath = Path.GetFullPath(
            Path.Combine(webRoot, relativePath)
        );

        var menuRoot = Path.GetFullPath(
            Path.Combine(webRoot, "images", "menu")
        );

        // Chặn path traversal và chỉ can thiệp đúng thư mục ảnh món.
        if (!fullPath.StartsWith(
                menuRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                fullPath,
                menuRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (File.Exists(fullPath))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "image/png";
        context.Response.Headers.CacheControl = "public,max-age=300";
        context.Response.Headers["X-Menu-Image-Fallback"] = "1";
        context.Response.ContentLength = PlaceholderPng.Length;
        await context.Response.Body.WriteAsync(PlaceholderPng);
    }

    private static bool IsMenuImageRequest(PathString path)
    {
        if (!path.StartsWithSegments(
                "/images/menu",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(path.Value ?? string.Empty);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
