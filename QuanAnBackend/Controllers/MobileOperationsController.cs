using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

public record MobilePriorityRequest(bool Active, string? EmployeeCode);

[ApiController]
[Route("api/mobile/ops")]
[Authorize]
public class MobileOperationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileOperationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("recent-notes")]
    public async Task<IActionResult> GetRecentNotes([FromQuery] int take = 8)
    {
        take = Math.Clamp(take, 1, 20);
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (@Take)
                    LTRIM(RTRIM(Note)) AS Note,
                    MAX(CreatedAt) AS LastUsedAt
                FROM OrderItems
                WHERE Note IS NOT NULL
                  AND LEN(LTRIM(RTRIM(Note))) > 0
                GROUP BY LTRIM(RTRIM(Note))
                ORDER BY MAX(CreatedAt) DESC;
                """;
            AddParameter(command, "@Take", take);

            var result = new List<object>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    note = reader.GetString(0),
                    lastUsedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1)
                });
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    [HttpGet("tables/{tableId:int}/order-log")]
    public async Task<IActionResult> GetOrderLog(int tableId, [FromQuery] int take = 150)
    {
        take = Math.Clamp(take, 1, 300);
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            int? orderId = null;
            await using (var orderCommand = connection.CreateCommand())
            {
                orderCommand.CommandText = """
                    SELECT TOP 1 OrderId
                    FROM Orders
                    WHERE TableId = @TableId
                    ORDER BY OrderTime DESC, OrderId DESC;
                    """;
                AddParameter(orderCommand, "@TableId", tableId);
                var value = await orderCommand.ExecuteScalarAsync();
                if (value is not null && value != DBNull.Value)
                    orderId = Convert.ToInt32(value);
            }

            if (!orderId.HasValue)
                return Ok(new { tableId, orderId = (int?)null, events = Array.Empty<object>() });

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (@Take)
                    h.HistoryId,
                    oi.OrderItemId,
                    COALESCE(mi.ItemName, N'(Món)') AS ItemName,
                    oi.Quantity,
                    CASE CONVERT(nvarchar(50), h.Status)
                        WHEN N'MoiGoi' THEN N'Chờ chế biến'
                        WHEN N'DangCheBien' THEN N'Đang chế biến'
                        WHEN N'DaXong' THEN N'Đã xong'
                        WHEN N'DaPhucVu' THEN N'Đã phục vụ'
                        WHEN N'DaHuy' THEN N'Đã hủy'
                        ELSE CONVERT(nvarchar(50), h.Status)
                    END AS StatusLabel,
                    h.ChangedAt,
                    COALESCE(u.FullName, N'Hệ thống') AS ChangedBy,
                    oi.Note
                FROM DishStatusHistory h
                INNER JOIN OrderItems oi ON oi.OrderItemId = h.OrderItemId
                LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                LEFT JOIN Users u ON u.UserId = h.ChangedBy
                WHERE oi.OrderId = @OrderId
                ORDER BY h.ChangedAt DESC, h.HistoryId DESC;
                """;
            AddParameter(command, "@Take", take);
            AddParameter(command, "@OrderId", orderId.Value);

            var events = new List<object>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new
                {
                    historyId = reader.GetInt32(0),
                    orderItemId = reader.GetInt32(1),
                    itemName = reader.GetString(2),
                    quantity = reader.GetInt32(3),
                    status = reader.GetString(4),
                    changedAt = reader.GetDateTime(5),
                    changedBy = reader.GetString(6),
                    note = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            return Ok(new { tableId, orderId, events });
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    [HttpGet("kitchen-priorities")]
    public async Task<IActionResult> GetKitchenPriorities()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            await EnsurePriorityTableAsync(connection);

            await using (var cleanup = connection.CreateCommand())
            {
                cleanup.CommandText = """
                    UPDATE p
                    SET IsActive = 0, UpdatedAt = GETDATE()
                    FROM MobileKitchenPriorities p
                    WHERE p.IsActive = 1
                      AND p.PriorityScope = N'Item'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM OrderItems oi
                          WHERE oi.OrderItemId = p.OrderItemId
                            AND CONVERT(nvarchar(50), oi.Status) IN (N'MoiGoi', N'DangCheBien')
                      );

                    UPDATE p
                    SET IsActive = 0, UpdatedAt = GETDATE()
                    FROM MobileKitchenPriorities p
                    WHERE p.IsActive = 1
                      AND p.PriorityScope = N'Table'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM Orders o
                          INNER JOIN OrderItems oi ON oi.OrderId = o.OrderId
                          WHERE o.TableId = p.TableId
                            AND CONVERT(nvarchar(50), o.Status) = N'DangMo'
                            AND CONVERT(nvarchar(50), oi.Status) IN (N'MoiGoi', N'DangCheBien')
                      );
                    """;
                await cleanup.ExecuteNonQueryAsync();
            }

            var itemIds = new List<int>();
            var tableIds = new List<int>();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT PriorityScope, TableId, OrderItemId
                FROM MobileKitchenPriorities
                WHERE IsActive = 1
                ORDER BY UpdatedAt DESC, PriorityId DESC;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var scope = reader.GetString(0);
                if (scope == "Item" && !reader.IsDBNull(2))
                    itemIds.Add(reader.GetInt32(2));
                else if (scope == "Table" && !reader.IsDBNull(1))
                    tableIds.Add(reader.GetInt32(1));
            }

            return Ok(new { itemIds = itemIds.Distinct(), tableIds = tableIds.Distinct() });
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    [HttpPost("priority/item/{orderItemId:int}")]
    [Authorize(Roles = "PhucVu,ThuNgan,QuanLy")]
    public async Task<IActionResult> SetItemPriority(int orderItemId, [FromBody] MobilePriorityRequest request)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            await EnsurePriorityTableAsync(connection);

            int? tableId = null;
            await using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT TOP 1 o.TableId
                    FROM OrderItems oi
                    INNER JOIN Orders o ON o.OrderId = oi.OrderId
                    WHERE oi.OrderItemId = @OrderItemId
                      AND CONVERT(nvarchar(50), o.Status) = N'DangMo';
                    """;
                AddParameter(lookup, "@OrderItemId", orderItemId);
                var value = await lookup.ExecuteScalarAsync();
                if (value is not null && value != DBNull.Value)
                    tableId = Convert.ToInt32(value);
            }

            if (!tableId.HasValue)
                return NotFound(new { message = "Không tìm thấy món trong đơn đang mở" });

            var userId = await ResolveUserIdAsync(connection, request.EmployeeCode);
            await UpsertPriorityAsync(
                connection,
                scope: "Item",
                tableId: tableId,
                orderItemId: orderItemId,
                active: request.Active,
                userId: userId
            );

            return Ok(new { orderItemId, tableId, active = request.Active });
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    [HttpPost("priority/table/{tableId:int}")]
    [Authorize(Roles = "PhucVu,ThuNgan,QuanLy")]
    public async Task<IActionResult> SetTablePriority(int tableId, [FromBody] MobilePriorityRequest request)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            await EnsurePriorityTableAsync(connection);

            await using (var validate = connection.CreateCommand())
            {
                validate.CommandText = """
                    SELECT COUNT(*)
                    FROM Orders
                    WHERE TableId = @TableId
                      AND CONVERT(nvarchar(50), Status) = N'DangMo';
                    """;
                AddParameter(validate, "@TableId", tableId);
                var count = Convert.ToInt32(await validate.ExecuteScalarAsync() ?? 0);
                if (count <= 0)
                    return BadRequest(new { message = "Bàn chưa có đơn đang mở để ưu tiên" });
            }

            var userId = await ResolveUserIdAsync(connection, request.EmployeeCode);
            await UpsertPriorityAsync(
                connection,
                scope: "Table",
                tableId: tableId,
                orderItemId: null,
                active: request.Active,
                userId: userId
            );

            return Ok(new { tableId, active = request.Active });
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task EnsurePriorityTableAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'MobileKitchenPriorities', N'U') IS NULL
            BEGIN
                CREATE TABLE MobileKitchenPriorities
                (
                    PriorityId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PriorityScope nvarchar(20) NOT NULL,
                    TableId int NULL,
                    OrderItemId int NULL,
                    IsActive bit NOT NULL CONSTRAINT DF_MobileKitchenPriorities_IsActive DEFAULT (1),
                    RequestedByUserId int NULL,
                    RequestedAt datetime2 NOT NULL CONSTRAINT DF_MobileKitchenPriorities_RequestedAt DEFAULT (GETDATE()),
                    UpdatedAt datetime2 NOT NULL CONSTRAINT DF_MobileKitchenPriorities_UpdatedAt DEFAULT (GETDATE())
                );

                CREATE INDEX IX_MobileKitchenPriorities_Table
                    ON MobileKitchenPriorities(TableId, IsActive);
                CREATE INDEX IX_MobileKitchenPriorities_Item
                    ON MobileKitchenPriorities(OrderItemId, IsActive);
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int?> ResolveUserIdAsync(DbConnection connection, string? employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return null;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 1 UserId
            FROM Employees
            WHERE EmployeeCode = @EmployeeCode;
            """;
        AddParameter(command, "@EmployeeCode", employeeCode.Trim());
        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private static async Task UpsertPriorityAsync(
        DbConnection connection,
        string scope,
        int? tableId,
        int? orderItemId,
        bool active,
        int? userId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE MobileKitchenPriorities
            SET IsActive = @IsActive,
                RequestedByUserId = @RequestedByUserId,
                UpdatedAt = GETDATE()
            WHERE PriorityScope = @PriorityScope
              AND ((@OrderItemId IS NOT NULL AND OrderItemId = @OrderItemId)
                   OR (@OrderItemId IS NULL AND TableId = @TableId));

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO MobileKitchenPriorities
                    (PriorityScope, TableId, OrderItemId, IsActive, RequestedByUserId, RequestedAt, UpdatedAt)
                VALUES
                    (@PriorityScope, @TableId, @OrderItemId, @IsActive, @RequestedByUserId, GETDATE(), GETDATE());
            END;
            """;
        AddParameter(command, "@PriorityScope", scope);
        AddParameter(command, "@TableId", tableId);
        AddParameter(command, "@OrderItemId", orderItemId);
        AddParameter(command, "@IsActive", active);
        AddParameter(command, "@RequestedByUserId", userId);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
