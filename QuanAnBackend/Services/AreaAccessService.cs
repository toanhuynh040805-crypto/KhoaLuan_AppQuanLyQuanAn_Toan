using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

public record AreaAccessSnapshot(bool Restricted, HashSet<int> AreaIds);

/// <summary>
/// Giới hạn khu vực dựa trên permission cấu hình trong DB thay vì tên Role.
/// User có permission area.all được thao tác mọi khu; các User còn lại chỉ
/// thao tác các khu được phân công trong EmployeeAreaAssignments hoặc khu được
/// xếp theo ca làm (ShiftAssignments.AreaId) trong ngày.
/// </summary>
public class AreaAccessService
{
    private readonly AppDbContext _db;
    private readonly MobilePermissionService _permissions;

    public AreaAccessService(
        AppDbContext db,
        MobilePermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<bool> RequiresAreaRestrictionAsync(ClaimsPrincipal user)
    {
        return !await _permissions.HasPermissionAsync(user, "area.all");
    }

    public async Task<AreaAccessSnapshot> GetAccessAsync(
        ClaimsPrincipal user,
        DateTime? workDate = null)
    {
        if (!await RequiresAreaRestrictionAsync(user))
            return new AreaAccessSnapshot(false, new HashSet<int>());

        var userId = MobilePermissionService.GetUserId(user);
        if (!userId.HasValue)
            return new AreaAccessSnapshot(true, new HashSet<int>());

        var day = (workDate ?? DateTime.Today).Date;

        // Ưu tiên khu vực của ca làm trong ngày. Trước đây UNION cả
        // EmployeeAreaAssignments (có thể là phân công cũ/toàn cục) với
        // ShiftAssignments khiến nhân viên vừa được đổi từ T1 A sang T1 B vẫn
        // được xem là có quyền ở cả T1 A và T1 B. Quy tắc mới:
        //   1) Có khu theo ca hôm nay -> chỉ dùng khu theo ca hôm nay.
        //   2) Không có -> dùng phân khu riêng đúng ngày.
        //   3) Vẫn không có -> mới fallback phân khu toàn cục.
        var areaIds = await _db.Database
            .SqlQuery<int>($"""
                WITH ShiftAreas AS (
                    SELECT DISTINCT saa.AreaId
                    FROM ShiftAssignments sa
                    INNER JOIN Employees e
                        ON e.EmployeeId = sa.EmployeeId
                    INNER JOIN ShiftAssignmentAreas saa
                        ON saa.ShiftAssignmentId = sa.ShiftAssignmentId
                    WHERE e.UserId = {userId.Value}
                      AND sa.WorkDate = {day}
                      AND sa.Status <> N'VangMat'

                    UNION

                    SELECT DISTINCT sa.AreaId
                    FROM ShiftAssignments sa
                    INNER JOIN Employees e
                        ON e.EmployeeId = sa.EmployeeId
                    WHERE e.UserId = {userId.Value}
                      AND sa.AreaId IS NOT NULL
                      AND sa.WorkDate = {day}
                      AND sa.Status <> N'VangMat'
                ),
                DatedAreas AS (
                    SELECT DISTINCT eaa.AreaId
                    FROM EmployeeAreaAssignments eaa
                    INNER JOIN Employees e ON e.EmployeeId = eaa.EmployeeId
                    WHERE e.UserId = {userId.Value}
                      AND eaa.IsActive = 1
                      AND eaa.WorkDate = {day}
                ),
                GlobalAreas AS (
                    SELECT DISTINCT eaa.AreaId
                    FROM EmployeeAreaAssignments eaa
                    INNER JOIN Employees e ON e.EmployeeId = eaa.EmployeeId
                    WHERE e.UserId = {userId.Value}
                      AND eaa.IsActive = 1
                      AND eaa.WorkDate IS NULL
                )
                SELECT AreaId AS Value
                FROM ShiftAreas

                UNION

                SELECT AreaId AS Value
                FROM DatedAreas
                WHERE NOT EXISTS (SELECT 1 FROM ShiftAreas)

                UNION

                SELECT AreaId AS Value
                FROM GlobalAreas
                WHERE NOT EXISTS (SELECT 1 FROM ShiftAreas)
                  AND NOT EXISTS (SELECT 1 FROM DatedAreas);
                """)
            .ToListAsync();

        return new AreaAccessSnapshot(true, areaIds.ToHashSet());
    }

    public async Task<bool> CanOperateAreaAsync(
        ClaimsPrincipal user,
        int areaId,
        DateTime? workDate = null)
    {
        var access = await GetAccessAsync(user, workDate);
        return !access.Restricted || access.AreaIds.Contains(areaId);
    }

    public async Task<bool> CanOperateTableAsync(
        ClaimsPrincipal user,
        int tableId,
        DateTime? workDate = null)
    {
        if (!await RequiresAreaRestrictionAsync(user))
            return true;

        var areaId = await _db.Tables
            .AsNoTracking()
            .Where(t => t.TableId == tableId)
            .Select(t => (int?)t.AreaId)
            .FirstOrDefaultAsync();

        if (!areaId.HasValue)
            return false;

        return await CanOperateAreaAsync(user, areaId.Value, workDate);
    }

    public async Task<bool> CanOperateOrderItemAsync(
        ClaimsPrincipal user,
        int orderItemId,
        DateTime? workDate = null)
    {
        if (!await RequiresAreaRestrictionAsync(user))
            return true;

        var tableId = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.OrderItemId == orderItemId)
            .Select(oi => (int?)oi.Order!.TableId)
            .FirstOrDefaultAsync();

        if (!tableId.HasValue)
            return false;

        return await CanOperateTableAsync(user, tableId.Value, workDate);
    }
}