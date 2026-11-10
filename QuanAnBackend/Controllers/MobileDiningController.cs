using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

public record UpdateMobileGuestCountRequest(int GuestCount);

internal sealed class CurrentWalkInGuestCountRow
{
    public int TableId { get; set; }
    public int GuestCount { get; set; }
}

internal sealed class LinkedOpenOrderTableRow
{
    public int OrderId { get; set; }
    public int TableId { get; set; }
}

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

    // Mobile dashboard: dữ liệu lõi chỉ dùng các bảng EF chắc chắn tồn tại.
    // Không phụ thuộc OrderTables/WaitingQueue/Combos để tránh một bảng phụ lệch
    // schema làm trắng toàn bộ màn hình bàn.
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _db.Tables
            .AsNoTracking()
            .Include(table => table.Area)
            .OrderBy(table => table.Area != null ? table.Area.AreaName : "")
            .ThenBy(table => table.TableName)
            .ToListAsync();

        if (tables.Count == 0)
            return Ok(Array.Empty<object>());

        var tableIds = tables
            .Select(table => table.TableId)
            .ToArray();

        var openOrders = await _db.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.DangMo &&
                order.TableId.HasValue &&
                tableIds.Contains(order.TableId.Value))
            .Select(order => new
            {
                order.OrderId,
                TableId = order.TableId!.Value,
                order.OrderTime
            })
            .ToListAsync();

        var openOrderIds = openOrders
            .Select(order => order.OrderId)
            .ToArray();

        var amountsByOrder = openOrderIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await _db.OrderItems
                .AsNoTracking()
                .Where(item =>
                    openOrderIds.Contains(item.OrderId) &&
                    item.Status != KitchenStatus.DaHuy)
                .GroupBy(item => item.OrderId)
                .Select(group => new
                {
                    OrderId = group.Key,
                    Amount = group.Sum(item =>
                        item.Quantity * item.UnitPrice)
                })
                .ToDictionaryAsync(
                    item => item.OrderId,
                    item => item.Amount);

        var linkedOrderRows = new List<LinkedOpenOrderTableRow>();
        try
        {
            linkedOrderRows = await _db.Database
                .SqlQuery<LinkedOpenOrderTableRow>($"""
                    SELECT ot.OrderId, ot.TableId
                    FROM OrderTables ot
                    INNER JOIN Orders o ON o.OrderId = ot.OrderId
                    WHERE o.Status = N'DangMo';
                    """)
                .ToListAsync();
        }
        catch (DbException)
        {
            // DB cũ chưa có OrderTables: bàn đơn vẫn hoạt động bình thường.
        }

        var linkedOrderIdByTable = linkedOrderRows
            .GroupBy(row => row.TableId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.OrderId)
                    .First().OrderId);

        var linkedTableIdsByOrder = linkedOrderRows
            .GroupBy(row => row.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.TableId).ToHashSet());

        var currentWalkInGuests = new Dictionary<int, int>();
        try
        {
            var walkInRows = await _db.Database
                .SqlQuery<CurrentWalkInGuestCountRow>($"""
                    WITH RankedWalkIns AS
                    (
                        SELECT
                            w.AssignedTableId AS TableId,
                            w.NumberOfGuests AS GuestCount,
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY w.AssignedTableId
                                ORDER BY COALESCE(w.SeatedTime, w.CheckInTime) DESC,
                                         w.QueueId DESC
                            ) AS RowNumber
                        FROM WaitingQueue w
                        WHERE w.AssignedTableId IS NOT NULL
                          AND CONVERT(NVARCHAR(50), w.Status) IN
                              (N'DaNhanBan', N'Đã nhận bàn', N'Da Nhan Ban')
                    )
                    SELECT TableId, GuestCount
                    FROM RankedWalkIns
                    WHERE RowNumber = 1;
                    """)
                .ToListAsync();

            currentWalkInGuests = walkInRows
                .GroupBy(row => row.TableId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().GuestCount);
        }
        catch (DbException)
        {
            // DB cũ có thể chưa có WaitingQueue. Giữ màn hình bàn hoạt động,
            // chỉ bỏ qua số khách walk-in thay vì làm hỏng toàn bộ dashboard.
        }

        var now = DateTime.Now;
        var reservations = await _db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                tableIds.Contains(reservation.TableId) &&
                reservation.ReservationTime >= now.AddDays(-1) &&
                (reservation.Status == ReservationStatus.DaXacNhan ||
                 reservation.Status == ReservationStatus.DaNhanBan))
            .ToListAsync();

        var physicalGroups = tables
            .GroupBy(table => new
            {
                table.AreaId,
                Name = (table.TableName ?? "")
                    .Trim()
                    .ToUpper()
            })
            .Select(group => group
                .OrderByDescending(table => table.Status switch
                {
                    TableStatus.DangPhucVu => 400,
                    TableStatus.DaDat => 300,
                    TableStatus.DangDonDep => 200,
                    _ => 100
                })
                .ThenByDescending(table => table.TableId)
                .First())
            .ToList();

        var result = physicalGroups
            .Select(table =>
            {
                var physicalTableIds = tables
                    .Where(candidate =>
                        candidate.AreaId == table.AreaId &&
                        string.Equals(
                            candidate.TableName?.Trim(),
                            table.TableName?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.TableId)
                    .ToHashSet();

                var sessionTableIds = physicalTableIds.ToHashSet();
                int? linkedOrderId = null;

                foreach (var physicalTableId in physicalTableIds)
                {
                    if (linkedOrderIdByTable.TryGetValue(
                        physicalTableId,
                        out var resolvedOrderId))
                    {
                        linkedOrderId = resolvedOrderId;
                        break;
                    }
                }

                if (linkedOrderId.HasValue &&
                    linkedTableIdsByOrder.TryGetValue(
                        linkedOrderId.Value,
                        out var linkedTableIds))
                {
                    sessionTableIds.UnionWith(linkedTableIds);
                }

                var order = linkedOrderId.HasValue
                    ? openOrders.FirstOrDefault(item =>
                        item.OrderId == linkedOrderId.Value)
                    : openOrders
                        .Where(item => sessionTableIds.Contains(item.TableId))
                        .OrderByDescending(item => item.OrderTime)
                        .FirstOrDefault();

                var currentWalkInGuestCount = sessionTableIds
                    .Select(sessionTableId =>
                        currentWalkInGuests.TryGetValue(
                            sessionTableId,
                            out var guestCount)
                            ? (int?)guestCount
                            : null)
                    .FirstOrDefault(value => value.HasValue);

                var currentReservation = reservations
                    .Where(item =>
                        sessionTableIds.Contains(item.TableId) &&
                        item.Status == ReservationStatus.DaNhanBan)
                    .OrderByDescending(item => item.ReservationTime)
                    .ThenByDescending(item => item.ReservationId)
                    .FirstOrDefault();

                var nextReservation = reservations
                    .Where(item =>
                        sessionTableIds.Contains(item.TableId) &&
                        item.Status == ReservationStatus.DaXacNhan &&
                        item.ReservationTime.AddMinutes(item.DurationMinutes) >= now)
                    .OrderBy(item => item.ReservationTime)
                    .ThenBy(item => item.ReservationId)
                    .FirstOrDefault();

                var amount = order is not null &&
                             amountsByOrder.TryGetValue(
                                 order.OrderId,
                                 out var orderAmount)
                    ? orderAmount
                    : 0m;

                decimal ReservationDepositAmount(Reservation? reservation)
                {
                    if (reservation is null)
                        return 0m;

                    if (!reservation.BookingGroupId.HasValue)
                        return reservation.DepositAmount;

                    return reservations
                        .Where(item =>
                            item.BookingGroupId ==
                            reservation.BookingGroupId)
                        .Sum(item => item.DepositAmount);
                }

                bool ReservationDepositPaid(Reservation? reservation)
                {
                    if (reservation is null)
                        return false;

                    if (!reservation.BookingGroupId.HasValue)
                        return reservation.DepositPaid;

                    return reservations.Any(item =>
                        item.BookingGroupId ==
                            reservation.BookingGroupId &&
                        item.DepositPaid &&
                        item.DepositAmount > 0);
                }

                var displayReservation =
                    currentReservation ?? nextReservation;
                var displayReservationDeposit =
                    ReservationDepositAmount(displayReservation);
                var displayReservationDepositPaid =
                    ReservationDepositPaid(displayReservation);

                var startedAt = order?.OrderTime ??
                                currentReservation?.ReservationTime;

                var displayStatus = table.Status;
                var reservationIsSoon =
                    nextReservation is not null &&
                    nextReservation.ReservationTime <= now.AddHours(1);

                if (displayStatus == TableStatus.DaDat &&
                    currentReservation is null &&
                    !reservationIsSoon)
                {
                    // Reservation còn hơn 1 giờ: vẫn giữ thông tin lịch đặt,
                    // nhưng bàn được dùng như bàn thường cho đến khi gần giờ.
                    displayStatus = TableStatus.Trong;
                }

                if (displayStatus == TableStatus.Trong &&
                    reservationIsSoon)
                {
                    // Chỉ đánh dấu/cảnh báo Đã đặt trong vòng 1 giờ.
                    // Đây là cảnh báo mềm, các API nhận/chuyển bàn thường vẫn cho phép.
                    displayStatus = TableStatus.DaDat;
                }

                var currentSessionType =
                    table.Status == TableStatus.DangPhucVu
                        ? currentReservation is not null
                            ? "Reservation"
                            : "WalkIn"
                        : null;

                return new
                {
                    id = table.TableId,
                    tableName = table.TableName,
                    capacity = table.Capacity,
                    status = displayStatus.ToString(),
                    areaId = table.AreaId,
                    areaName = table.Area?.AreaName ?? "",
                    tableType = table.TableType.ToString(),
                    amount = table.Status == TableStatus.DangPhucVu
                        ? amount
                        : (decimal?)null,
                    customerId = currentReservation?.CustomerId ??
                        (table.Status == TableStatus.DangPhucVu
                            ? (int?)null
                            : nextReservation?.CustomerId),
                    customerName = currentReservation?.CustomerName ??
                        (table.Status == TableStatus.DangPhucVu
                            ? null
                            : nextReservation?.CustomerName),
                    customerPhone = currentReservation?.CustomerPhone ??
                        (table.Status == TableStatus.DangPhucVu
                            ? null
                            : nextReservation?.CustomerPhone),
                    guestCount = currentReservation?.NumberOfGuests ??
                        (table.Status == TableStatus.DangPhucVu
                            ? currentWalkInGuestCount
                            : nextReservation?.NumberOfGuests),
                    time = startedAt?.ToString("HH:mm"),
                    currentSessionType,
                    reservationId = currentReservation?.ReservationId ??
                        nextReservation?.ReservationId,
                    reservationTime = currentReservation?.ReservationTime ??
                        nextReservation?.ReservationTime,
                    reservationStatus = currentReservation?.Status.ToString() ??
                        nextReservation?.Status.ToString(),
                    reservationReceived = currentReservation is not null,
                    reservationCustomerName = currentReservation?.CustomerName ??
                        nextReservation?.CustomerName,
                    reservationCustomerPhone = currentReservation?.CustomerPhone ??
                        nextReservation?.CustomerPhone,
                    reservationGuestCount = currentReservation?.NumberOfGuests ??
                        nextReservation?.NumberOfGuests,
                    reservationDurationMinutes =
                        currentReservation?.DurationMinutes ??
                        nextReservation?.DurationMinutes,
                    reservationDepositAmount =
                        displayReservationDeposit,
                    reservationDepositPaid =
                        displayReservationDepositPaid,
                    reservationNote = currentReservation?.Note ??
                        nextReservation?.Note,
                    nextReservationId = nextReservation?.ReservationId,
                    nextReservationTime = nextReservation?.ReservationTime,
                    nextReservationCustomerName = nextReservation?.CustomerName,
                    nextReservationCustomerPhone = nextReservation?.CustomerPhone,
                    nextReservationGuestCount = nextReservation?.NumberOfGuests
                };
            })
            .ToList();

        return Ok(result);
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