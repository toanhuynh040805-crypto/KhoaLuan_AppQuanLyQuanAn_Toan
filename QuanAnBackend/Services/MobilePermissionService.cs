using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Đọc quyền hiệu lực từ DB. Quyền của User là hợp của tất cả Role đang active.
/// Không chứa switch/case theo tên vai trò.
/// </summary>
public class MobilePermissionService
{
    private readonly AppDbContext _db;

    public MobilePermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<string>> GetPermissionsForUserIdAsync(int userId)
    {
        if (userId <= 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var permissions = await _db.Database.SqlQuery<string>($"""
            SELECT DISTINCT rp.PermissionKey AS Value
            FROM dbo.UserRoles ur
            INNER JOIN dbo.RoleMobilePermissions rp
                ON rp.RoleId = ur.RoleId
               AND rp.IsAllowed = 1
            INNER JOIN dbo.MobilePermissions p
                ON p.PermissionKey = rp.PermissionKey
               AND p.IsActive = 1
            WHERE ur.UserId = {userId}
              AND ur.IsActive = 1;
            """).ToListAsync();

        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HashSet<string>> GetPermissionsAsync(ClaimsPrincipal user)
    {
        var userId = GetUserId(user);
        return userId.HasValue
            ? await GetPermissionsForUserIdAsync(userId.Value)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return false;

        var permissions = await GetPermissionsAsync(user);
        return permissions.Contains(permissionKey.Trim());
    }

    public async Task<bool> HasPermissionForUserIdAsync(
        int userId,
        string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return false;

        var permissions = await GetPermissionsForUserIdAsync(userId);
        return permissions.Contains(permissionKey.Trim());
    }

    public static int? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var userId) && userId > 0
            ? userId
            : null;
    }
}
