using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QuanAnBackend.Services;

namespace QuanAnBackend.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireMobilePermissionAttribute : TypeFilterAttribute
{
    public RequireMobilePermissionAttribute(string permissionKey)
        : base(typeof(MobilePermissionAuthorizationFilter))
    {
        Arguments = new object[] { permissionKey };
    }
}

public sealed class MobilePermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string _permissionKey;
    private readonly MobilePermissionService _permissions;

    public MobilePermissionAuthorizationFilter(
        string permissionKey,
        MobilePermissionService permissions)
    {
        _permissionKey = permissionKey;
        _permissions = permissions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!await _permissions.HasPermissionAsync(user, _permissionKey))
            context.Result = new ForbidResult();
    }
}
