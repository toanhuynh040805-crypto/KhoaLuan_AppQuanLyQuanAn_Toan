using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record SetRoleMobilePermissionsRequest(List<string> PermissionKeys);

[ApiController]
[Route("api/mobile/access")]
public class MobileAccessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MobilePermissionService _permissions;
    private readonly AreaAccessService _areaAccess;

    public MobileAccessController(
        AppDbContext db,
        MobilePermissionService permissions,
        AreaAccessService areaAccess)
    {
        _db = db;
        _permissions = permissions;
        _areaAccess = areaAccess;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        var permissions = await _permissions.GetPermissionsAsync(User);
        var area = await _areaAccess.GetAccessAsync(User);
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new
        {
            roles,
            permissions = permissions.OrderBy(x => x).ToList(),
            area = new
            {
                restricted = area.Restricted,
                areaIds = area.AreaIds.OrderBy(x => x).ToList()
            }
        });
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
    {
        if (!await _permissions.HasPermissionAsync(User, "access.configure"))
            return Forbid();

        var permissions = await _db.Database.SqlQuery<PermissionCatalogRow>($"""
            SELECT PermissionKey, DisplayName, Description
            FROM dbo.MobilePermissions
            WHERE IsActive = 1
            ORDER BY DisplayName;
            """).ToListAsync();

        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new { r.RoleId, r.RoleName, r.Description })
            .ToListAsync();

        var assignments = await _db.Database.SqlQuery<RolePermissionRow>($"""
            SELECT RoleId, PermissionKey
            FROM dbo.RoleMobilePermissions
            WHERE IsAllowed = 1;
            """).ToListAsync();

        return Ok(new
        {
            permissions,
            roles = roles.Select(role => new
            {
                role.RoleId,
                role.RoleName,
                role.Description,
                permissionKeys = assignments
                    .Where(x => x.RoleId == role.RoleId)
                    .Select(x => x.PermissionKey)
                    .OrderBy(x => x)
                    .ToList()
            })
        });
    }

    [HttpPut("roles/{roleId:int}")]
    public async Task<IActionResult> SetRolePermissions(
        int roleId,
        [FromBody] SetRoleMobilePermissionsRequest request)
    {
        if (!await _permissions.HasPermissionAsync(User, "access.configure"))
            return Forbid();

        var roleExists = await _db.Roles.AsNoTracking().AnyAsync(r => r.RoleId == roleId);
        if (!roleExists)
            return NotFound(new { message = "Không tìm thấy vai trò" });

        var requested = (request.PermissionKeys ?? new List<string>())
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var valid = await _db.Database.SqlQuery<string>($"""
            SELECT PermissionKey AS Value
            FROM dbo.MobilePermissions
            WHERE IsActive = 1;
            """).ToListAsync();
        var validSet = valid.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = requested.Where(x => !validSet.Contains(x)).ToList();
        if (invalid.Count > 0)
        {
            return BadRequest(new
            {
                message = "Có permission không tồn tại",
                invalidPermissionKeys = invalid
            });
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        try
        {
            await using (var disable = connection.CreateCommand())
            {
                disable.Transaction = tx;
                disable.CommandText = """
                    UPDATE dbo.RoleMobilePermissions
                    SET IsAllowed = 0, UpdatedAt = GETDATE()
                    WHERE RoleId = @RoleId;
                    """;
                AddParameter(disable, "@RoleId", roleId);
                await disable.ExecuteNonQueryAsync();
            }

            foreach (var key in requested)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = tx;
                command.CommandText = """
                    IF EXISTS (
                        SELECT 1 FROM dbo.RoleMobilePermissions
                        WHERE RoleId = @RoleId AND PermissionKey = @PermissionKey
                    )
                    BEGIN
                        UPDATE dbo.RoleMobilePermissions
                        SET IsAllowed = 1, UpdatedAt = GETDATE()
                        WHERE RoleId = @RoleId AND PermissionKey = @PermissionKey;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO dbo.RoleMobilePermissions(RoleId, PermissionKey, IsAllowed, UpdatedAt)
                        VALUES(@RoleId, @PermissionKey, 1, GETDATE());
                    END;
                    """;
                AddParameter(command, "@RoleId", roleId);
                AddParameter(command, "@PermissionKey", key);
                await command.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { roleId, permissionKeys = requested.OrderBy(x => x).ToList() });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed class PermissionCatalogRow
    {
        public string PermissionKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class RolePermissionRow
    {
        public int RoleId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
    }
}
