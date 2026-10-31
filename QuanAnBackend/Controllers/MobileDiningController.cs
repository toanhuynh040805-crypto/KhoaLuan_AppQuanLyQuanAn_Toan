using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

public record UpdateMobileGuestCountRequest(int GuestCount);

[ApiController]
[Route("api/mobile/dining")]
public class MobileDiningController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public MobileDiningController(
        AppDbContext db,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // Mobile dashboard: tra them khu vuc + so khach, nhung KHONG doi schema DB.
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
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
                    t.TableId,
                    t.TableName,
                    t.Capacity,
                    t.Status,
                    t.AreaId,
                    ISNULL(a.AreaName, N'') AS AreaName,
                    t.TableType,
                    COALESCE((
                        SELECT SUM(
                            oi.Quantity * COALESCE(
                                NULLIF(oi.UnitPrice, 0),
                                mi.Price,
                                cb.Price,
                                0
                            )
                        )
                        FROM Orders o
                        INNER JOIN OrderItems oi ON oi.OrderId = o.OrderId
                        LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                        LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
                        WHERE o.Status = N'DangMo'
                          AND (
                              o.TableId = t.TableId
                              OR EXISTS (
                                  SELECT 1 FROM OrderTables ot
                                  WHERE ot.OrderId = o.OrderId
                                    AND ot.TableId = t.TableId
                              )
                          )
                          AND oi.Status <> N'DaHuy'
                    ), 0) AS Amount,
                    COALESCE(res.CustomerName, walkin.CustomerName) AS CustomerName,
                    CASE
                        WHEN t.Status IN (N'DangPhucVu', N'DaDat') THEN
                            NULLIF(COALESCE(res.NumberOfGuests, walkin.NumberOfGuests, 0), 0)
                        ELSE NULL
                    END AS GuestCount,
                    COALESCE(openOrder.OrderTime, walkin.SeatedTime, res.ReservationTime) AS StartedAt
                FROM DiningTables t
                LEFT JOIN Areas a ON a.AreaId = t.AreaId
                OUTER APPLY (
                    SELECT TOP 1
                        r.CustomerName,
                        r.NumberOfGuests,
                        r.ReservationTime
                    FROM Reservations r
                    WHERE r.TableId = t.TableId
                      AND (
                            (t.Status = N'DaDat' AND r.Status = N'DaXacNhan')
                            OR
                            (t.Status = N'DangPhucVu' AND r.Status = N'DaNhanBan')
                          )
                    ORDER BY r.ReservationTime DESC, r.ReservationId DESC
                ) res
                OUTER APPLY (
                    SELECT TOP 1
                        w.CustomerName,
                        w.NumberOfGuests,
                        w.SeatedTime
                    FROM WaitingQueue w
                    WHERE w.AssignedTableId = t.TableId
                      AND w.Status = N'DaNhanBan'
                    ORDER BY COALESCE(w.SeatedTime, w.CheckInTime) DESC, w.QueueId DESC
                ) walkin
                OUTER APPLY (
                    SELECT TOP 1 o.OrderTime
                    FROM Orders o
                    WHERE o.Status = N'DangMo'
                      AND (
                          o.TableId = t.TableId
                          OR EXISTS (
                              SELECT 1 FROM OrderTables ot
                              WHERE ot.OrderId = o.OrderId
                                AND ot.TableId = t.TableId
                          )
                      )
                    ORDER BY o.OrderTime DESC, o.OrderId DESC
                ) openOrder
                ORDER BY a.AreaName, t.TableName;
                """;

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var status = reader.GetString(3);
                var startedAt = reader.IsDBNull(10)
                    ? (DateTime?)null
                    : reader.GetDateTime(10);

                result.Add(new
                {
                    id = reader.GetInt32(0),
                    tableName = reader.GetString(1),
                    capacity = reader.GetInt32(2),
                    status,
                    areaId = reader.GetInt32(4),
                    areaName = reader.GetString(5),
                    tableType = reader.GetString(6),
                    amount = status == "DangPhucVu"
                        ? reader.GetDecimal(7)
                        : (decimal?)null,
                    customerName = reader.IsDBNull(8)
                        ? null
                        : reader.GetString(8),
                    guestCount = reader.IsDBNull(9)
                        ? (int?)null
                        : reader.GetInt32(9),
                    time = startedAt?.ToString("HH:mm")
                });
            }

            return Ok(result);
        }
        catch (DbException)
        {
            // Một số DB cũ có thể chưa có đầy đủ các bảng phụ như
            // OrderTables/WaitingQueue/Combos. Không để lỗi dữ liệu bổ sung
            // làm Mobile mất toàn bộ sơ đồ bàn.
            var fallback = await _db.Tables
                .AsNoTracking()
                .Include(table => table.Area)
                .OrderBy(table => table.Area != null ? table.Area.AreaName : "")
                .ThenBy(table => table.TableName)
                .ToListAsync();

            return Ok(fallback.Select(table => new
            {
                id = table.TableId,
                tableName = table.TableName,
                capacity = table.Capacity,
                status = table.Status.ToString(),
                areaId = table.AreaId,
                areaName = table.Area?.AreaName ?? "",
                tableType = table.TableType.ToString(),
                amount = (decimal?)null,
                customerName = (string?)null,
                guestCount = (int?)null,
                time = (string?)null
            }));
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // So khach la thong tin tuy chon: 0 = chua nhap.
    [HttpPut("tables/{id}/guest-count")]
    [RequireMobilePermission("table.manage")]
    public async Task<IActionResult> UpdateGuestCount(
        int id,
        [FromBody] UpdateMobileGuestCountRequest request)
    {
        if (request.GuestCount < 0)
            return BadRequest(new { message = "Số khách không được âm" });

        var table = await _db.Tables
            .AsNoTracking()
            .Where(t => t.TableId == id)
            .Select(t => new
            {
                t.TableId,
                t.TableName,
                t.Capacity,
                Status = t.Status.ToString()
            })
            .FirstOrDefaultAsync();

        if (table is null)
            return NotFound(new { message = "Không tìm thấy bàn" });

        if (table.Status != "DangPhucVu")
            return Conflict(new { message = "Chỉ cập nhật số khách khi bàn đang phục vụ" });

        if (request.GuestCount > 0 && request.GuestCount > table.Capacity)
        {
            return BadRequest(new
            {
                message = $"Bàn chỉ chứa tối đa {table.Capacity} khách"
            });
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var updatedReservation = await ExecuteScalarIntAsync(
                connection,
                transaction,
                """
                ;WITH CurrentReservation AS (
                    SELECT TOP 1 *
                    FROM Reservations
                    WHERE TableId = @TableId
                      AND Status = N'DaNhanBan'
                    ORDER BY ReservationTime DESC, ReservationId DESC
                )
                UPDATE CurrentReservation
                SET NumberOfGuests = @GuestCount;

                SELECT @@ROWCOUNT;
                """,
                ("@TableId", id),
                ("@GuestCount", request.GuestCount)
            );

            if (updatedReservation == 0)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    IF EXISTS (
                        SELECT 1
                        FROM WaitingQueue
                        WHERE AssignedTableId = @TableId
                          AND Status = N'DaNhanBan'
                    )
                    BEGIN
                        ;WITH CurrentWalkIn AS (
                            SELECT TOP 1 *
                            FROM WaitingQueue
                            WHERE AssignedTableId = @TableId
                              AND Status = N'DaNhanBan'
                            ORDER BY COALESCE(SeatedTime, CheckInTime) DESC, QueueId DESC
                        )
                        UPDATE CurrentWalkIn
                        SET NumberOfGuests = @GuestCount;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO WaitingQueue
                            (
                                CustomerName,
                                NumberOfGuests,
                                Status,
                                CheckInTime,
                                EstimatedWaitMinutes
                            )
                        VALUES
                            (
                                NULL,
                                @GuestCount,
                                N'DaNhanBan',
                                GETDATE(),
                                0
                            );

                        ;WITH LatestWalkIn AS (
                            SELECT TOP 1 *
                            FROM WaitingQueue
                            WHERE AssignedTableId IS NULL
                              AND Status = N'DaNhanBan'
                              AND NumberOfGuests = @GuestCount
                            ORDER BY CheckInTime DESC, QueueId DESC
                        )
                        UPDATE LatestWalkIn
                        SET AssignedTableId = @TableId,
                            SeatedTime = GETDATE();
                    END
                    """,
                    ("@TableId", id),
                    ("@GuestCount", request.GuestCount)
                );
            }

            await transaction.CommitAsync();

            await _hub.Clients.All.SendAsync(
                "TableStatusUpdated",
                new
                {
                    TableId = id,
                    table.TableName,
                    Status = 1 // 1 = DangPhucVu (số, khớp enum WinForm; Mobile parse được cả số lẫn chuỗi)
                });

            return Ok(new
            {
                id,
                guestCount = request.GuestCount > 0
                    ? request.GuestCount
                    : (int?)null
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // Khong sua schema: "Mang ve" dung 1 pool DiningTable trong khu vuc Mang ve.
    // TableType giu dung schema goc (Thuong/VIP); Mobile nhan biet mang ve bang AreaName.
    // Moi don lay 1 slot trong; thanh toan xong slot tro ve Trong va duoc tai su dung.
    [HttpPost("takeaway/open")]
    [RequireMobilePermission("order.create")]
    public async Task<IActionResult> OpenTakeaway()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var areaId = await ExecuteScalarNullableIntAsync(
                connection,
                transaction,
                """
                SELECT TOP 1 AreaId
                FROM Areas
                WHERE AreaName = N'Mang về'
                ORDER BY AreaId;
                """
            );

            if (!areaId.HasValue)
            {
                areaId = await ExecuteScalarNullableIntAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO Areas (AreaName, Description)
                    VALUES (N'Mang về', N'Đơn mang về - khu vực hệ thống');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """
                );
            }

            if (!areaId.HasValue)
                throw new InvalidOperationException("Không thể tạo khu vực Mang về");

            var tableId = await ExecuteScalarNullableIntAsync(
                connection,
                transaction,
                """
                SELECT TOP 1 t.TableId
                FROM DiningTables t WITH (UPDLOCK, ROWLOCK)
                WHERE t.AreaId = @AreaId
                  AND t.Status = N'Trong'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Orders o
                      WHERE o.TableId = t.TableId
                        AND o.Status = N'DangMo'
                  )
                ORDER BY t.TableId;
                """,
                ("@AreaId", areaId.Value)
            );

            if (!tableId.HasValue)
            {
                var sequence = await ExecuteScalarIntAsync(
                    connection,
                    transaction,
                    """
                    SELECT COUNT(*) + 1
                    FROM DiningTables
                    WHERE AreaId = @AreaId;
                    """,
                    ("@AreaId", areaId.Value)
                );

                tableId = await ExecuteScalarNullableIntAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO DiningTables
                        (TableName, AreaId, Capacity, TableType, Status)
                    VALUES
                        (@TableName, @AreaId, 1, N'Thuong', N'DangPhucVu');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """,
                    ("@TableName", $"MV-{sequence:000}"),
                    ("@AreaId", areaId.Value)
                );
            }
            else
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE DiningTables
                    SET Status = N'DangPhucVu'
                    WHERE TableId = @TableId;
                    """,
                    ("@TableId", tableId.Value)
                );
            }

            if (!tableId.HasValue)
                throw new InvalidOperationException("Không thể mở đơn mang về");

            var tableName = await ExecuteScalarStringAsync(
                connection,
                transaction,
                """
                SELECT TableName
                FROM DiningTables
                WHERE TableId = @TableId;
                """,
                ("@TableId", tableId.Value)
            ) ?? $"MV-{tableId.Value:000}";

            await transaction.CommitAsync();

            await _hub.Clients.All.SendAsync(
                "TableStatusUpdated",
                new
                {
                    TableId = tableId.Value,
                    TableName = tableName,
                    Status = 1 // 1 = DangPhucVu (số, khớp enum WinForm; Mobile parse được cả số lẫn chuỗi)
                });

            return Ok(new
            {
                id = tableId.Value,
                tableName,
                capacity = 1,
                status = "DangPhucVu",
                areaId = areaId.Value,
                areaName = "Mang về",
                tableType = "Thuong",
                amount = 0,
                customerName = (string?)null,
                guestCount = (int?)null,
                time = DateTime.Now.ToString("HH:mm")
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ExecuteScalarIntAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value
            ? 0
            : Convert.ToInt32(value);
    }

    private static async Task<int?> ExecuteScalarNullableIntAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value
            ? null
            : Convert.ToInt32(value);
    }

    private static async Task<string?> ExecuteScalarStringAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value
            ? null
            : value.ToString();
    }

    private static void AddParameters(
        DbCommand command,
        params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}