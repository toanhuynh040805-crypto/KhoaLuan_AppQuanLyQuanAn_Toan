using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Lưu lịch sử các thao tác ghi từ Mobile/WinForms mà không làm thay đổi
/// QuanAnManagementDB_Final.sql. Bảng phụ được tạo idempotent khi Backend chạy.
/// </summary>
public class MobileOperationAuditService
{
    private readonly AppDbContext _db;

    public MobileOperationAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.MobileOperationLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobileOperationLogs
                (
                    LogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NULL,
                    EmployeeCode NVARCHAR(50) NULL,
                    HttpMethod NVARCHAR(12) NOT NULL,
                    RequestPath NVARCHAR(300) NOT NULL,
                    TableIds NVARCHAR(500) NULL,
                    AreaNames NVARCHAR(500) NULL,
                    OutsideAssignedArea BIT NOT NULL
                        CONSTRAINT DF_MobileOperationLogs_Outside DEFAULT(0),
                    StatusCode INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL
                        CONSTRAINT DF_MobileOperationLogs_CreatedAt DEFAULT(GETDATE())
                );

                CREATE INDEX IX_MobileOperationLogs_CreatedAt
                    ON dbo.MobileOperationLogs(CreatedAt DESC);
                CREATE INDEX IX_MobileOperationLogs_EmployeeCode
                    ON dbo.MobileOperationLogs(EmployeeCode, CreatedAt DESC);
            END;
            """);
    }

    public async Task LogAsync(
        HttpContext context,
        IReadOnlyCollection<int> tableIds,
        bool outsideAssignedArea,
        int statusCode)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) ||
            HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method))
            return;

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/mobile/table-locks", StringComparison.OrdinalIgnoreCase))
            return;

        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdRaw, out var parsedUserId)
            ? parsedUserId
            : null;

        string? employeeCode = null;
        if (userId.HasValue)
        {
            employeeCode = await _db.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Select(e => e.EmployeeCode)
                .FirstOrDefaultAsync();
        }

        var ids = tableIds.Where(id => id > 0).Distinct().ToList();
        var tableIdsText = ids.Count == 0 ? null : string.Join(',', ids);

        string? areaNames = null;
        if (ids.Count > 0)
        {
            var names = await _db.Tables
                .AsNoTracking()
                .Where(t => ids.Contains(t.TableId))
                .Select(t => t.Area != null ? t.Area.AreaName : null)
                .Where(name => name != null && name != string.Empty)
                .Distinct()
                .ToListAsync();

            if (names.Count > 0)
                areaNames = string.Join(" | ", names!);
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.MobileOperationLogs
                (UserId, EmployeeCode, HttpMethod, RequestPath,
                 TableIds, AreaNames, OutsideAssignedArea, StatusCode, CreatedAt)
            VALUES
                ({userId}, {employeeCode}, {method}, {path},
                 {tableIdsText}, {areaNames}, {outsideAssignedArea}, {statusCode}, GETDATE());
            """);
    }
}
