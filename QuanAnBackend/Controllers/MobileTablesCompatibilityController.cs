using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record ReceiveTableRequest(int CustomerCount);
public record TransferTableRequest(int SourceTableId, int TargetTableId);
public record MergeTableRequest(int SourceTableId, int TargetTableId);
public record LinkTablesRequest(int PrimaryTableId, List<int>? TableIds, string? EmployeeCode);
public record SplitTableItemRequest(int? OrderItemId, int Quantity);

public record SplitTableRequest(
    int SourceTableId,
    int TargetTableId,
    List<SplitTableItemRequest> Items
);

[ApiController]
[Route("api/tables")]
[RequireMobilePermission("table.manage")]
public class MobileTablesCompatibilityController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    public MobileTablesCompatibilityController(AppDbContext db, IHubContext<OrderHub> hub, NotificationService notifications)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
    }
    // PUT api/tables/{id}/receive
    // Nhan ban trong hoac nhan ban da dat.
    // =========================================================
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPut("compat/{id}/receive")]
    public async Task<IActionResult> Receive(
        int id,
        [FromBody] ReceiveTableRequest request)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var status = await GetTableStatusAsync(
                connection,
                transaction,
                id
            );

            if (status is null)
                return NotFound(new { message = "Không tìm thấy bàn" });

            if (status != "Trong" && status != "DaDat")
            {
                return Conflict(
                    new { message = "Bàn này hiện không thể nhận" }
                );
            }

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE DiningTables
                SET Status = N'DangPhucVu'
                WHERE TableId = @TableId;
                """,
                ("@TableId", id)
            );

            if (status == "Trong" && request.CustomerCount > 0)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
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
                            @CustomerCount,
                            N'DaNhanBan',
                            GETDATE(),
                            0
                        );

                    ;WITH LatestWalkIn AS (
                        SELECT TOP 1 *
                        FROM WaitingQueue
                        WHERE AssignedTableId IS NULL
                          AND Status = N'DaNhanBan'
                          AND NumberOfGuests = @CustomerCount
                        ORDER BY CheckInTime DESC, QueueId DESC
                    )
                    UPDATE LatestWalkIn
                    SET AssignedTableId = @TableId,
                        SeatedTime = GETDATE();
                    """,
                    ("@CustomerCount", request.CustomerCount),
                    ("@TableId", id)
                );
            }

            if (status == "DaDat")
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    ;WITH ReservationToReceive AS (
                        SELECT TOP 1 *
                        FROM Reservations
                        WHERE TableId = @TableId
                          AND Status = N'DaXacNhan'
                        ORDER BY ABS(
                            DATEDIFF(
                                MINUTE,
                                GETDATE(),
                                ReservationTime
                            )
                        )
                    )
                    UPDATE ReservationToReceive
                    SET Status = N'DaNhanBan';
                    """,
                    ("@TableId", id)
                );
            }

            await transaction.CommitAsync();

            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = id,
                TableName = "",
                Status = 1   // 1 = Đang phục vụ, khớp enum WinForm
            });

            return Ok(
                new
                {
                    id,
                    status = "occupied",
                    customerCount = request.CustomerCount
                }
            );
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

    // =========================================================
    // PUT api/tables/{id}/release
    // Tra ban. Neu order dang mo van con mon thi chan de tranh mat du lieu.
    // =========================================================
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPut("compat/{id}/release")]
    public async Task<IActionResult> Release(int id)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var status = await GetTableStatusAsync(
                connection,
                transaction,
                id
            );

            if (status is null)
                return NotFound(new { message = "Không tìm thấy bàn" });

            var activeItemCount = await ExecuteScalarIntAsync(
                connection,
                transaction,
                """
                SELECT COUNT(*)
                FROM Orders o
                INNER JOIN OrderItems oi
                    ON oi.OrderId = o.OrderId
                WHERE o.TableId = @TableId
                  AND o.Status = N'DangMo'
                  AND oi.Status <> N'DaHuy';
                """,
                ("@TableId", id)
            );

            var unservedItemCount = await ExecuteScalarIntAsync(
                connection,
                transaction,
                """
                SELECT COUNT(*)
                FROM Orders o
                INNER JOIN OrderItems oi
                    ON oi.OrderId = o.OrderId
                WHERE o.TableId = @TableId
                  AND o.Status = N'DangMo'
                  AND oi.Status NOT IN (N'DaPhucVu', N'DaHuy');
                """,
                ("@TableId", id)
            );

            if (activeItemCount > 0)
            {
                return Conflict(
                    new
                    {
                        message =
                            unservedItemCount > 0
                                ? "Bàn vẫn còn món chưa được phục vụ, chưa thể trả bàn"
                                : "Bàn đã có món. Hãy hoàn tất thanh toán trước khi trả bàn."
                    }
                );
            }

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE Orders
                SET Status = N'DaHuy'
                WHERE TableId = @TableId
                  AND Status = N'DangMo';

                UPDATE DiningTables
                SET Status = N'Trong'
                WHERE TableId = @TableId;
                """,
                ("@TableId", id)
            );

            await transaction.CommitAsync();

            return Ok(new { id, status = "empty" });
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

    // =========================================================
    // POST api/tables/transfer
    // Chuyen ban nguon sang ban trong dich.
    // =========================================================
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferTableRequest request)
    {
        if (request.SourceTableId == request.TargetTableId)
        {
            return BadRequest(
                new { message = "Bàn nguồn và bàn đích không được trùng nhau" }
            );
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var sourceStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.SourceTableId
            );

            var targetStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.TargetTableId
            );

            if (sourceStatus is null || targetStatus is null)
                return NotFound(new { message = "Không tìm thấy bàn" });

            if (sourceStatus != "DangPhucVu")
            {
                return Conflict(
                    new { message = "Bàn nguồn không ở trạng thái đang phục vụ" }
                );
            }

            if (targetStatus != "Trong")
            {
                return Conflict(
                    new { message = "Bàn đích không còn trống" }
                );
            }

            await ExecuteAsync(
                connection,
                transaction,
                """
                DELETE ot
                FROM OrderTables ot
                WHERE ot.TableId = @SourceTableId
                  AND EXISTS (
                      SELECT 1
                      FROM OrderTables existed
                      WHERE existed.OrderId = ot.OrderId
                        AND existed.TableId = @TargetTableId
                  );

                UPDATE OrderTables
                SET TableId = @TargetTableId
                WHERE TableId = @SourceTableId
                  AND OrderId IN (
                      SELECT OrderId
                      FROM Orders
                      WHERE Status = N'DangMo'
                  );

                UPDATE Orders
                SET TableId = @TargetTableId
                WHERE TableId = @SourceTableId
                  AND Status = N'DangMo';

                UPDATE Reservations
                SET TableId = @TargetTableId
                WHERE TableId = @SourceTableId
                  AND Status = N'DaNhanBan';

                ;WITH LatestWalkIn AS (
                    SELECT TOP 1 *
                    FROM WaitingQueue
                    WHERE AssignedTableId = @SourceTableId
                      AND Status = N'DaNhanBan'
                    ORDER BY
                        COALESCE(SeatedTime, CheckInTime) DESC,
                        QueueId DESC
                )
                UPDATE LatestWalkIn
                SET AssignedTableId = @TargetTableId;

                UPDATE DiningTables
                SET Status = N'Trong'
                WHERE TableId = @SourceTableId;

                UPDATE DiningTables
                SET Status = N'DangPhucVu'
                WHERE TableId = @TargetTableId;
                """,
                ("@SourceTableId", request.SourceTableId),
                ("@TargetTableId", request.TargetTableId)
            );

            await transaction.CommitAsync();
            await NotifyTableChangeAsync("Chuyển bàn", " → ", request.SourceTableId, request.TargetTableId);
            return Ok(
                new
                {
                    sourceTableId = request.SourceTableId,
                    targetTableId = request.TargetTableId
                }
            );
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

    // =========================================================
    // POST api/tables/merge
    // Gop ban nguon vao ban dich. Giu dung hanh vi Flutter hien tai:
    // ban nguon tro ve Trong, ban dich giu toan bo order.
    // =========================================================
    [HttpPost("merge")]
    public async Task<IActionResult> Merge(
        [FromBody] MergeTableRequest request)
    {
        if (request.SourceTableId == request.TargetTableId)
        {
            return BadRequest(
                new { message = "Bàn nguồn và bàn đích không được trùng nhau" }
            );
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var sourceStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.SourceTableId
            );

            var targetStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.TargetTableId
            );

            if (sourceStatus is null || targetStatus is null)
                return NotFound(new { message = "Không tìm thấy bàn" });

            if (sourceStatus != "DangPhucVu" ||
                targetStatus != "DangPhucVu")
            {
                return Conflict(
                    new
                    {
                        message =
                            "Chỉ có thể gộp hai bàn đang phục vụ"
                    }
                );
            }

            var sourceOrderId = await GetOpenOrderIdAsync(
                connection,
                transaction,
                request.SourceTableId
            );

            var targetOrderId = await GetOpenOrderIdAsync(
                connection,
                transaction,
                request.TargetTableId
            );

            if (sourceOrderId.HasValue)
            {
                if (targetOrderId.HasValue &&
                    targetOrderId.Value != sourceOrderId.Value)
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        UPDATE OrderItems
                        SET OrderId = @TargetOrderId
                        WHERE OrderId = @SourceOrderId;

                        DELETE FROM OrderTables
                        WHERE OrderId = @SourceOrderId;

                        UPDATE Orders
                        SET Status = N'DaHuy'
                        WHERE OrderId = @SourceOrderId;
                        """,
                        ("@TargetOrderId", targetOrderId.Value),
                        ("@SourceOrderId", sourceOrderId.Value)
                    );
                }
                else if (!targetOrderId.HasValue)
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        UPDATE Orders
                        SET TableId = @TargetTableId
                        WHERE OrderId = @SourceOrderId;

                        DELETE ot
                        FROM OrderTables ot
                        WHERE ot.OrderId = @SourceOrderId
                          AND EXISTS (
                              SELECT 1
                              FROM OrderTables existed
                              WHERE existed.OrderId = ot.OrderId
                                AND existed.TableId = @TargetTableId
                          );

                        UPDATE OrderTables
                        SET TableId = @TargetTableId,
                            IsPrimaryTable = 1
                        WHERE OrderId = @SourceOrderId
                          AND TableId = @SourceTableId;
                        """,
                        ("@TargetTableId", request.TargetTableId),
                        ("@SourceTableId", request.SourceTableId),
                        ("@SourceOrderId", sourceOrderId.Value)
                    );
                }
            }

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE Reservations
                SET TableId = @TargetTableId
                WHERE TableId = @SourceTableId
                  AND Status = N'DaNhanBan';

                UPDATE DiningTables
                SET Status = N'Trong'
                WHERE TableId = @SourceTableId;

                UPDATE DiningTables
                SET Status = N'DangPhucVu'
                WHERE TableId = @TargetTableId;
                """,
                ("@SourceTableId", request.SourceTableId),
                ("@TargetTableId", request.TargetTableId)
            );

            await transaction.CommitAsync();
            await NotifyTableChangeAsync("Gộp đơn", " → ", request.SourceTableId, request.TargetTableId);
            return Ok(
                new
                {
                    sourceTableId = request.SourceTableId,
                    targetTableId = request.TargetTableId
                }
            );
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

    // =========================================================
    // POST api/tables/link
    // Ghép nhiều bàn vật lý vào CÙNG MỘT order. Khác với /merge:
    // - merge = gộp 2 order thành 1 và trả bàn nguồn
    // - link = giữ nhiều bàn cùng DangPhucVu và cùng dùng một order
    // =========================================================
    [HttpPost("link")]
    public async Task<IActionResult> LinkTables([FromBody] LinkTablesRequest request)
    {
        var targetIds = (request.TableIds ?? new List<int>())
            .Where(id => id > 0 && id != request.PrimaryTableId)
            .Distinct()
            .ToList();

        if (request.PrimaryTableId <= 0 || targetIds.Count == 0)
            return BadRequest(new { message = "Chưa chọn bàn cần ghép" });

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var primaryStatus = await GetTableStatusAsync(
                connection, transaction, request.PrimaryTableId);
            if (primaryStatus is null)
                return NotFound(new { message = "Không tìm thấy bàn chính" });
            if (primaryStatus != "DangPhucVu")
                return Conflict(new { message = "Bàn chính phải đang phục vụ" });

            int? orderId = await GetOpenOrderIdAsync(
                connection, transaction, request.PrimaryTableId);

            if (!orderId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(request.EmployeeCode))
                    return BadRequest(new { message = "Không xác định được nhân viên để mở đơn chung" });

                var employeeId = await ExecuteScalarNullableIntAsync(
                    connection,
                    transaction,
                    "SELECT TOP 1 EmployeeId FROM Employees WHERE EmployeeCode = @EmployeeCode;",
                    ("@EmployeeCode", request.EmployeeCode.Trim())
                );
                if (!employeeId.HasValue)
                    return BadRequest(new { message = "Không tìm thấy nhân viên đang đăng nhập" });

                orderId = await ExecuteScalarNullableIntAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO Orders (TableId, EmployeeId, OrderTime, Status, TotalAmount)
                    VALUES (@TableId, @EmployeeId, GETDATE(), N'DangMo', 0);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """,
                    ("@TableId", request.PrimaryTableId),
                    ("@EmployeeId", employeeId.Value)
                );
            }

            if (!orderId.HasValue)
                throw new InvalidOperationException("Không thể tạo đơn chung cho nhóm bàn");

            await ExecuteAsync(
                connection,
                transaction,
                """
                IF NOT EXISTS (
                    SELECT 1 FROM OrderTables
                    WHERE OrderId = @OrderId AND TableId = @PrimaryTableId
                )
                INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable)
                VALUES (@OrderId, @PrimaryTableId, 1);
                """,
                ("@OrderId", orderId.Value),
                ("@PrimaryTableId", request.PrimaryTableId)
            );

            foreach (var tableId in targetIds)
            {
                var status = await ExecuteScalarStringAsync(
                    connection,
                    transaction,
                    "SELECT Status FROM DiningTables WITH (UPDLOCK, HOLDLOCK) WHERE TableId = @TableId;",
                    ("@TableId", tableId)
                );

                if (status is null)
                    return NotFound(new { message = $"Không tìm thấy bàn #{tableId}" });
                if (status != "Trong")
                    return Conflict(new { message = $"Có bàn vừa được người khác nhận. Vui lòng tải lại và chọn bàn trống khác." });

                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    IF NOT EXISTS (
                        SELECT 1 FROM OrderTables
                        WHERE OrderId = @OrderId AND TableId = @TableId
                    )
                    INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable)
                    VALUES (@OrderId, @TableId, 0);

                    UPDATE DiningTables
                    SET Status = N'DangPhucVu'
                    WHERE TableId = @TableId;
                    """,
                    ("@OrderId", orderId.Value),
                    ("@TableId", tableId)
                );
            }

            await transaction.CommitAsync();
            await NotifyTableChangeAsync("Ghép bàn", " + ", new[] { request.PrimaryTableId }.Concat(targetIds).ToArray());
            return Ok(new
            {
                orderId = orderId.Value,
                primaryTableId = request.PrimaryTableId,
                tableIds = targetIds
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    // =========================================================
    // POST api/tables/split
    // Tach mot phan mon sang ban trong.
    // Neu Flutter dang co mon local chua co OrderItemId (menu mock),
    // backend van luu trang thai ban dich; stage Menu/Order sau se
    // dong bo cac mon local vao DB.
    // =========================================================
    [HttpPost("split")]
    public async Task<IActionResult> Split(
        [FromBody] SplitTableRequest request)
    {
        if (request.SourceTableId == request.TargetTableId)
        {
            return BadRequest(
                new { message = "Bàn nguồn và bàn đích không được trùng nhau" }
            );
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var sourceStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.SourceTableId
            );

            var targetStatus = await GetTableStatusAsync(
                connection,
                transaction,
                request.TargetTableId
            );

            if (sourceStatus is null || targetStatus is null)
                return NotFound(new { message = "Không tìm thấy bàn" });

            if (sourceStatus != "DangPhucVu")
            {
                return Conflict(
                    new { message = "Bàn nguồn không ở trạng thái đang phục vụ" }
                );
            }

            if (targetStatus != "Trong")
            {
                return Conflict(
                    new { message = "Bàn đích không còn trống" }
                );
            }

            var sourceOrderId = await GetOpenOrderIdAsync(
                connection,
                transaction,
                request.SourceTableId
            );

            int? targetOrderId = null;
            var persistedItems = 0;

            var validRequests = request.Items
                .Where(x =>
                    x.OrderItemId.HasValue &&
                    x.OrderItemId.Value > 0 &&
                    x.Quantity > 0)
                .ToList();

            if (!sourceOrderId.HasValue || validRequests.Count == 0)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Các món của bàn này chưa được lưu trong database. Chưa thể tách bàn cho đến khi Order được đồng bộ API thật."
                    }
                );
            }

            if (sourceOrderId.HasValue && validRequests.Count > 0)
            {
                targetOrderId = await CreateSplitTargetOrderAsync(
                    connection,
                    transaction,
                    sourceOrderId.Value,
                    request.TargetTableId
                );

                foreach (var itemRequest in validRequests)
                {
                    var item = await GetSplitSourceItemAsync(
                        connection,
                        transaction,
                        sourceOrderId.Value,
                        itemRequest.OrderItemId!.Value
                    );

                    if (item is null)
                        continue;

                    if (itemRequest.Quantity > item.Quantity)
                    {
                        return BadRequest(
                            new
                            {
                                message =
                                    $"Số lượng tách của món {item.Name} vượt quá số lượng hiện có"
                            }
                        );
                    }

                    if (itemRequest.Quantity == item.Quantity)
                    {
                        await ExecuteAsync(
                            connection,
                            transaction,
                            """
                            UPDATE OrderItems
                            SET OrderId = @TargetOrderId
                            WHERE OrderItemId = @OrderItemId;
                            """,
                            ("@TargetOrderId", targetOrderId.Value),
                            ("@OrderItemId", item.OrderItemId)
                        );
                    }
                    else
                    {
                        await ExecuteAsync(
                            connection,
                            transaction,
                            """
                            UPDATE OrderItems
                            SET Quantity = Quantity - @SplitQuantity
                            WHERE OrderItemId = @OrderItemId;
                            """,
                            ("@SplitQuantity", itemRequest.Quantity),
                            ("@OrderItemId", item.OrderItemId)
                        );

                        var newOrderItemId =
                            await ClonePartialOrderItemAsync(
                                connection,
                                transaction,
                                item,
                                targetOrderId.Value,
                                itemRequest.Quantity
                            );

                        await RestoreStockAfterSplitCloneAsync(
                            connection,
                            transaction,
                            item,
                            newOrderItemId,
                            itemRequest.Quantity
                        );

                        await ExecuteAsync(
                            connection,
                            transaction,
                            """
                            INSERT INTO KitchenTickets
                                (
                                    OrderItemId,
                                    StationId,
                                    SentTime,
                                    ChefId,
                                    Status,
                                    CompletedTime
                                )
                            SELECT
                                @NewOrderItemId,
                                StationId,
                                SentTime,
                                ChefId,
                                Status,
                                CompletedTime
                            FROM KitchenTickets
                            WHERE OrderItemId = @OldOrderItemId;

                            INSERT INTO DishStatusHistory
                                (
                                    OrderItemId,
                                    Status,
                                    ChangedAt,
                                    ChangedBy
                                )
                            SELECT
                                @NewOrderItemId,
                                Status,
                                ChangedAt,
                                ChangedBy
                            FROM DishStatusHistory
                            WHERE OrderItemId = @OldOrderItemId;
                            """,
                            ("@NewOrderItemId", newOrderItemId),
                            ("@OldOrderItemId", item.OrderItemId)
                        );
                    }

                    persistedItems++;
                }
            }

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE DiningTables
                SET Status = N'DangPhucVu'
                WHERE TableId = @TargetTableId;
                """,
                ("@TargetTableId", request.TargetTableId)
            );

            await transaction.CommitAsync();
            await NotifyTableChangeAsync("Tách bàn", " → ", request.SourceTableId, request.TargetTableId);
            return Ok(
                new
                {
                    sourceTableId = request.SourceTableId,
                    targetTableId = request.TargetTableId,
                    targetOrderId,
                    persistedItems
                }
            );
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

        // Phát realtime cho WinForm/Mobile + tạo thông báo cho Thu ngân & Quản lý.
    // Lỗi ở bước này không được làm hỏng thao tác bàn đã commit.
    private async Task NotifyTableChangeAsync(
        string title,
        string separator,
        params int[] tableIds)
    {
        try
        {
            var ids = tableIds.Distinct().ToList();

            var tables = await _db.Tables
                .AsNoTracking()
                .Where(t => ids.Contains(t.TableId))
                .Select(t => new { t.TableId, t.TableName, t.Status })
                .ToListAsync();

            foreach (var t in tables)
            {
                await _hub.Clients.All.SendAsync("TableStatusUpdated", new
                {
                    TableId = t.TableId,
                    TableName = t.TableName,
                    Status = (int)t.Status
                });
            }

            var names = tableIds
                .Select(id => tables.FirstOrDefault(t => t.TableId == id)?.TableName
                              ?? $"#{id}");

            await _notifications.CreateForRolesAsync(
                title,
                $"{title}: {string.Join(separator, names)}",
                NotificationType.DonHang,
                new[] { "ThuNgan", "QuanLy" },
                "Table",
                tableIds.First());
        }
        catch
        {
            // bỏ qua: realtime/thông báo chỉ là phần phụ
        }
    }

    private static async Task<string?> GetTableStatusAsync(
        DbConnection connection,
        DbTransaction transaction,
        int tableId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Status
            FROM DiningTables WITH (UPDLOCK, ROWLOCK)
            WHERE TableId = @TableId;
            """;
        AddParameter(command, "@TableId", tableId);

        var value = await command.ExecuteScalarAsync();
        return value?.ToString();
    }

    private static async Task<int?> GetOpenOrderIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        int tableId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT TOP 1 OrderId
            FROM Orders
            WHERE TableId = @TableId
              AND Status = N'DangMo'
            ORDER BY OrderTime DESC;
            """;
        AddParameter(command, "@TableId", tableId);

        var value = await command.ExecuteScalarAsync();

        if (value is null || value == DBNull.Value)
            return null;

        return Convert.ToInt32(value);
    }

    private static async Task<int> CreateSplitTargetOrderAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sourceOrderId,
        int targetTableId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Orders
                (
                    TableId,
                    EmployeeId,
                    CustomerId,
                    OrderTime,
                    Status,
                    Note
                )
            OUTPUT INSERTED.OrderId
            SELECT
                @TargetTableId,
                EmployeeId,
                CustomerId,
                GETDATE(),
                N'DangMo',
                Note
            FROM Orders
            WHERE OrderId = @SourceOrderId;
            """;

        AddParameter(command, "@TargetTableId", targetTableId);
        AddParameter(command, "@SourceOrderId", sourceOrderId);

        var value = await command.ExecuteScalarAsync();

        if (value is null || value == DBNull.Value)
            throw new InvalidOperationException("Không thể tạo order cho bàn tách");

        var targetOrderId = Convert.ToInt32(value);

        await ExecuteAsync(
            connection,
            transaction,
            """
            IF NOT EXISTS (
                SELECT 1
                FROM OrderTables
                WHERE OrderId = @TargetOrderId
                  AND TableId = @TargetTableId
            )
            BEGIN
                INSERT INTO OrderTables
                    (OrderId, TableId, IsPrimaryTable)
                VALUES
                    (@TargetOrderId, @TargetTableId, 1);
            END
            """,
            ("@TargetOrderId", targetOrderId),
            ("@TargetTableId", targetTableId)
        );

        return targetOrderId;
    }

    private static async Task<SplitSourceItem?> GetSplitSourceItemAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sourceOrderId,
        int orderItemId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                oi.OrderItemId,
                oi.MenuItemId,
                oi.ComboId,
                oi.Quantity,
                oi.UnitPrice,
                oi.Note,
                oi.Status,
                oi.IsRefillRequest,
                oi.RefillSequenceNumber,
                COALESCE(mi.ItemName, cb.ComboName, N'Món'),
                oi.IsTakeaway
            FROM OrderItems oi
            LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
            LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
            WHERE oi.OrderItemId = @OrderItemId
              AND oi.OrderId = @SourceOrderId
              AND oi.Status <> N'DaHuy';
            """;

        AddParameter(command, "@OrderItemId", orderItemId);
        AddParameter(command, "@SourceOrderId", sourceOrderId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new SplitSourceItem(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetDecimal(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.GetString(9),
            !reader.IsDBNull(10) && reader.GetBoolean(10)
        );
    }

    private static async Task<int> ClonePartialOrderItemAsync(
        DbConnection connection,
        DbTransaction transaction,
        SplitSourceItem item,
        int targetOrderId,
        int splitQuantity)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO OrderItems
                (
                    OrderId,
                    MenuItemId,
                    ComboId,
                    Quantity,
                    UnitPrice,
                    Note,
                    Status,
                    IsRefillRequest,
                    RefillSequenceNumber,
                    IsTakeaway,
                    CreatedAt
                )
            VALUES
                (
                    @TargetOrderId,
                    @MenuItemId,
                    @ComboId,
                    @Quantity,
                    @UnitPrice,
                    @Note,
                    @Status,
                    @IsRefillRequest,
                    @RefillSequenceNumber,
                    @IsTakeaway,
                    GETDATE()
                );

            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        AddParameter(command, "@TargetOrderId", targetOrderId);
        AddParameter(command, "@MenuItemId", item.MenuItemId);
        AddParameter(command, "@ComboId", item.ComboId);
        AddParameter(command, "@Quantity", splitQuantity);
        AddParameter(command, "@UnitPrice", item.UnitPrice);
        AddParameter(command, "@Note", item.Note);
        AddParameter(command, "@Status", item.Status);
        AddParameter(command, "@IsRefillRequest", item.IsRefillRequest);
        AddParameter(
            command,
            "@RefillSequenceNumber",
            item.RefillSequenceNumber
        );
        AddParameter(command, "@IsTakeaway", item.IsTakeaway);

        var value = await command.ExecuteScalarAsync();

        if (value is null || value == DBNull.Value)
            throw new InvalidOperationException("Không thể tách món");

        return Convert.ToInt32(value);
    }

    private static async Task RestoreStockAfterSplitCloneAsync(
        DbConnection connection,
        DbTransaction transaction,
        SplitSourceItem item,
        int newOrderItemId,
        int splitQuantity)
    {
        if (item.MenuItemId.HasValue)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO StockTransactions
                    (
                        IngredientId,
                        ChangeType,
                        QuantityChange,
                        RelatedOrderItemId,
                        Note
                    )
                SELECT
                    IngredientId,
                    N'DieuChinh',
                    QuantityRequired * @SplitQuantity,
                    @NewOrderItemId,
                    N'Hoan lai kho do tach ban, khong phai goi mon moi'
                FROM MenuItemIngredients
                WHERE MenuItemId = @MenuItemId;
                """,
                ("@SplitQuantity", splitQuantity),
                ("@NewOrderItemId", newOrderItemId),
                ("@MenuItemId", item.MenuItemId.Value)
            );
        }
        else if (item.ComboId.HasValue)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO StockTransactions
                    (
                        IngredientId,
                        ChangeType,
                        QuantityChange,
                        RelatedOrderItemId,
                        Note
                    )
                SELECT
                    bom.IngredientId,
                    N'DieuChinh',
                    bom.QuantityRequired
                        * ci.Quantity
                        * @SplitQuantity,
                    @NewOrderItemId,
                    N'Hoan lai kho do tach combo sang ban khac'
                FROM ComboItems ci
                INNER JOIN MenuItemIngredients bom
                    ON bom.MenuItemId = ci.MenuItemId
                WHERE ci.ComboId = @ComboId;
                """,
                ("@SplitQuantity", splitQuantity),
                ("@NewOrderItemId", newOrderItemId),
                ("@ComboId", item.ComboId.Value)
            );
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

        foreach (var parameter in parameters)
        {
            AddParameter(
                command,
                parameter.Name,
                parameter.Value
            );
        }

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

        foreach (var parameter in parameters)
        {
            AddParameter(
                command,
                parameter.Name,
                parameter.Value
            );
        }

        var value = await command.ExecuteScalarAsync();

        if (value is null || value == DBNull.Value)
            return 0;

        return Convert.ToInt32(value);
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

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

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

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value
            ? null
            : value.ToString();
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

    private sealed record SplitSourceItem(
        int OrderItemId,
        int? MenuItemId,
        int? ComboId,
        int Quantity,
        decimal UnitPrice,
        string? Note,
        string Status,
        bool IsRefillRequest,
        int? RefillSequenceNumber,
        string Name,
        bool IsTakeaway
    );
}