using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    private readonly AppDbContext _db;

    public IntegrationController(AppDbContext db)
    {
        _db = db;
    }

    // Mobile/WinForms dùng endpoint này để kiểm tra backend dùng chung.
    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync();

            if (shouldClose)
                await connection.CloseAsync();

            return Ok(new
            {
                status = "ok",
                database = "connected",
                sharedBackend = true,
                clients = new[] { "WinForms", "Mobile" },
                signalRHub = "/hubs/order",
                capabilities = new
                {
                    dineIn = true,
                    takeaway = true,
                    reservations = true,
                    kitchenAndBarRouting = true,
                    payments = true,
                    invoices = true,
                    notifications = true,
                    tableLocks = true,
                    shiftsAndAreas = true,
                    mobilePermissions = true,
                    menuImages = true
                },
                serverTime = DateTime.Now
            });
        }
        catch
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    status = "error",
                    database = "unavailable",
                    sharedBackend = true
                }
            );
        }
    }

    // Bếp/Quầy nước gọi khi khởi động hoặc reconnect để lấy ticket bỏ lỡ.
    // LEFT JOIN DiningTables để đơn Mang về (TableId = NULL) không bị mất.
    [HttpGet("kitchen/pending")]
    public async Task<IActionResult> GetPendingKitchenTickets(
        [FromQuery] string? station = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    kt.KitchenTicketId,
                    oi.OrderItemId,
                    o.OrderId,
                    t.TableId,
                    COALESCE(
                        t.TableName,
                        CASE
                            WHEN o.TakeawayNumber IS NOT NULL
                                THEN CONCAT(N'Mang về #', o.TakeawayNumber)
                            ELSE N'Mang về'
                        END
                    ) AS TableName,
                    ISNULL(s.StationName, N''),
                    COALESCE(mi.ItemName, c.ComboName, N'Món') AS ItemName,
                    oi.Quantity,
                    oi.Note,
                    oi.Status,
                    kt.Status,
                    kt.SentTime,
                    ISNULL(o.OrderType, N'TaiBan') AS OrderType,
                    o.TakeawayNumber
                FROM KitchenTickets kt
                INNER JOIN OrderItems oi
                    ON oi.OrderItemId = kt.OrderItemId
                INNER JOIN Orders o
                    ON o.OrderId = oi.OrderId
                LEFT JOIN DiningTables t
                    ON t.TableId = o.TableId
                LEFT JOIN MenuItems mi
                    ON mi.MenuItemId = oi.MenuItemId
                LEFT JOIN Combos c
                    ON c.ComboId = oi.ComboId
                LEFT JOIN Stations s
                    ON s.StationId = kt.StationId
                WHERE o.Status = N'DangMo'
                  AND oi.Status NOT IN (N'DaHuy', N'DaPhucVu')
                  AND kt.Status <> N'HoanThanh'
                  AND (
                        @Station IS NULL
                        OR @Station = N''
                        OR s.StationName = @Station
                  )
                ORDER BY kt.SentTime, kt.KitchenTicketId;
                """;

            AddParameter(
                command,
                "@Station",
                string.IsNullOrWhiteSpace(station)
                    ? null
                    : station.Trim()
            );

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    kitchenTicketId = reader.GetInt32(0),
                    orderItemId = reader.GetInt32(1),
                    orderId = reader.GetInt32(2),
                    tableId = reader.IsDBNull(3)
                        ? (int?)null
                        : reader.GetInt32(3),
                    tableName = reader.GetString(4),
                    station = reader.GetString(5),
                    itemName = reader.GetString(6),
                    quantity = reader.GetInt32(7),
                    note = reader.IsDBNull(8)
                        ? null
                        : reader.GetString(8),
                    status = DisplayDishStatus(reader.GetString(9)),
                    ticketStatus = reader.GetString(10),
                    sentTime = reader.GetDateTime(11),
                    orderType = reader.GetString(12),
                    takeawayNumber = reader.IsDBNull(13)
                        ? (int?)null
                        : reader.GetInt32(13)
                });
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // Thu ngân gọi khi khởi động/reconnect để phục hồi yêu cầu thanh toán.
    // Đơn Mang về không có DiningTable nên phải LEFT JOIN.
    [HttpGet("payments/pending")]
    public async Task<IActionResult> GetPendingPayments()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                ;WITH LatestRequest AS
                (
                    SELECT
                        n.NotificationId,
                        n.RelatedEntityId AS OrderId,
                        n.Content,
                        n.CreatedAt,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY n.RelatedEntityId
                            ORDER BY n.CreatedAt DESC, n.NotificationId DESC
                        ) AS RowNumber
                    FROM Notifications n
                    WHERE n.TargetRole = N'ThuNgan'
                      AND n.RelatedEntityType = N'Order'
                      AND n.Title = N'Yêu cầu thanh toán'
                )
                SELECT
                    lr.NotificationId,
                    o.OrderId,
                    t.TableId,
                    COALESCE(
                        t.TableName,
                        CASE
                            WHEN o.TakeawayNumber IS NOT NULL
                                THEN CONCAT(N'Mang về #', o.TakeawayNumber)
                            ELSE N'Mang về'
                        END
                    ) AS TableName,
                    o.TotalAmount,
                    lr.Content,
                    lr.CreatedAt,
                    ISNULL(o.OrderType, N'TaiBan') AS OrderType,
                    o.TakeawayNumber
                FROM LatestRequest lr
                INNER JOIN Orders o
                    ON o.OrderId = lr.OrderId
                LEFT JOIN DiningTables t
                    ON t.TableId = o.TableId
                WHERE lr.RowNumber = 1
                  AND o.Status = N'DangMo'
                ORDER BY lr.CreatedAt;
                """;

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    notificationId = reader.GetInt32(0),
                    orderId = reader.GetInt32(1),
                    tableId = reader.IsDBNull(2)
                        ? (int?)null
                        : reader.GetInt32(2),
                    tableName = reader.GetString(3),
                    totalAmount = reader.GetDecimal(4),
                    message = reader.GetString(5),
                    requestedAt = reader.GetDateTime(6),
                    orderType = reader.GetString(7),
                    takeawayNumber = reader.IsDBNull(8)
                        ? (int?)null
                        : reader.GetInt32(8)
                });
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // WinForms/Mobile dùng endpoint này để biết tên group station SignalR.
    [AllowAnonymous]
    [HttpGet("stations")]
    public async Task<IActionResult> GetStations()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT StationId, StationName
                FROM Stations
                ORDER BY StationId;
                """;

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var stationName = reader.GetString(1);
                result.Add(new
                {
                    stationId = reader.GetInt32(0),
                    stationName,
                    signalRGroup = $"dept-{stationName}"
                });
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string DisplayDishStatus(string status)
    {
        return status switch
        {
            "MoiGoi" => "Chờ chế biến",
            "DangCheBien" => "Đang chế biến",
            "DaCheBien" => "Đã xong",
            "DaPhucVu" => "Đã phục vụ",
            "DaHuy" => "Đã hủy",
            _ => status
        };
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
