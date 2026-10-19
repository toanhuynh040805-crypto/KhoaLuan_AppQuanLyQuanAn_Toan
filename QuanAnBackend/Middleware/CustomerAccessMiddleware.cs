using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Middleware;

/// <summary>
/// Đánh dấu endpoint mà tài khoản khách hàng web (role "KhachHang") được phép gọi.
/// Mọi endpoint khác (trừ [AllowAnonymous]) sẽ bị chặn với khách hàng.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CustomerAccessAttribute : Attribute
{
}

/// <summary>
/// Chặn token khách hàng gọi vào API nhân viên (orders, invoices, customers,
/// hubs...). Khách chỉ được vào endpoint có [CustomerAccess] hoặc [AllowAnonymous].
/// Phải đặt SAU app.UseAuthentication().
/// </summary>
public sealed class CustomerAccessMiddleware
{
    public const string CustomerRole = "KhachHang";

    private readonly RequestDelegate _next;

    public CustomerAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is not null &&
            context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole(CustomerRole))
        {
            var allowed =
                endpoint.Metadata.GetMetadata<CustomerAccessAttribute>() is not null ||
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Tài khoản khách hàng không có quyền truy cập chức năng này."
                });
                return;
            }
        }

        await _next(context);
    }
}