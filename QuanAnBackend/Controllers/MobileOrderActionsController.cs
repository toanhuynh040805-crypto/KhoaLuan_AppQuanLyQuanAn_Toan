using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

public record MobileOrderActionRequest(
    List<int>? OrderItemIds,
    string? EmployeeCode = null
);

public record MobileClearTableRequest(
    int TableId,
    string? EmployeeCode = null
);

public record MobileOrderQuantityRequest(
    int Quantity,
    string? EmployeeCode = null
);

[ApiController]
[Route("api/mobile/order-actions")]
public class MobileOrderActionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public MobileOrderActionsController(
        AppDbContext db,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost("served")]
    [RequireMobilePermission("order.serve")]
    public async Task<IActionResult> MarkServed(
        [FromBody] MobileOrderActionRequest request)
    {
        var ids = NormalizeIds(request.OrderItemIds);
        if (ids.Count == 0)
            return BadRequest(new { message = "Chưa chọn món cần xác nhận phục vụ" });

        var userId = await ResolveUserIdAsync(request.EmployeeCode);

        var items = await _db.OrderItems
            .Include(x => x.Order)
                .ThenInclude(o => o!.Table)
            .Include(x => x.MenuItem)
            .Where(x => ids.Contains(x.OrderItemId))
            .ToListAsync();

        var updated = new List<OrderItem>();

        foreach (var item in items)
        {
            // Chỉ món Bếp đã hoàn thành mới được chuyển sang Đã phục vụ.
            if (item.Status != KitchenStatus.DaCheBien)
                continue;

            item.Status = KitchenStatus.DaPhucVu;
            _db.DishStatusHistories.Add(new DishStatusHistory
            {
                OrderItemId = item.OrderItemId,
                Status = KitchenStatus.DaPhucVu,
                ChangedAt = DateTime.Now,
                ChangedBy = userId
            });
            updated.Add(item);
        }

        await _db.SaveChangesAsync();
        await BroadcastAsync(updated, "Đã phục vụ");

        return Ok(new
        {
            status = "Đã phục vụ",
            updatedIds = updated.Select(x => x.OrderItemId).ToList(),
            skippedIds = ids.Except(updated.Select(x => x.OrderItemId)).ToList()
        });
    }

    [HttpPost("cancel")]
    [RequireMobilePermission("order.cancel")]
    public async Task<IActionResult> Cancel(
        [FromBody] MobileOrderActionRequest request)
    {
        var ids = NormalizeIds(request.OrderItemIds);
        if (ids.Count == 0)
            return BadRequest(new { message = "Chưa chọn món cần hủy" });

        var userId = await ResolveUserIdAsync(request.EmployeeCode);

        var items = await _db.OrderItems
            .Include(x => x.Order)
                .ThenInclude(o => o!.Table)
            .Include(x => x.MenuItem)
            .Include(x => x.KitchenTickets)
            .Where(x => ids.Contains(x.OrderItemId))
            .ToListAsync();

        var updated = new List<OrderItem>();

        foreach (var item in items)
        {
            // Giữ nguyên nghiệp vụ cũ: chỉ hủy khi Bếp chưa bắt đầu chế biến.
            if (item.Status != KitchenStatus.MoiGoi)
                continue;

            item.Status = KitchenStatus.DaHuy;
            item.CancelledByUserId = userId;

            foreach (var ticket in item.KitchenTickets)
            {
                ticket.Status = KitchenTicketStatus.HoanThanh;
                ticket.CompletedTime ??= DateTime.Now;
            }

            _db.DishStatusHistories.Add(new DishStatusHistory
            {
                OrderItemId = item.OrderItemId,
                Status = KitchenStatus.DaHuy,
                ChangedAt = DateTime.Now,
                ChangedBy = userId
            });
            updated.Add(item);
        }

        await _db.SaveChangesAsync();
        await BroadcastAsync(updated, "Đã hủy");

        return Ok(new
        {
            status = "Đã hủy",
            updatedIds = updated.Select(x => x.OrderItemId).ToList(),
            skippedIds = ids.Except(updated.Select(x => x.OrderItemId)).ToList()
        });
    }

    [HttpPut("items/{id:int}/quantity")]
    [RequireMobilePermission("order.cancel")]
    public async Task<IActionResult> UpdateQuantity(
        int id,
        [FromBody] MobileOrderQuantityRequest request)
    {
        if (id <= 0)
            return BadRequest(new { message = "Món không hợp lệ" });

        if (request.Quantity < 1 || request.Quantity > 99)
        {
            return BadRequest(new
            {
                message = "Số lượng món phải từ 1 đến 99"
            });
        }

        var userId = await ResolveUserIdAsync(request.EmployeeCode);

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var item = await _db.OrderItems
                .Include(x => x.Order)
                    .ThenInclude(o => o!.Table)
                .Include(x => x.MenuItem)
                .FirstOrDefaultAsync(x => x.OrderItemId == id);

            if (item is null)
            {
                await transaction.RollbackAsync();
                return NotFound(new { message = "Không tìm thấy món" });
            }

            if (item.Status != KitchenStatus.MoiGoi)
            {
                await transaction.RollbackAsync();
                return Conflict(new
                {
                    message =
                        "Chỉ có thể sửa số lượng khi bếp chưa bắt đầu chế biến"
                });
            }

            item.Quantity = request.Quantity;

            await _db.SaveChangesAsync();

            var totalAmount = await _db.OrderItems
                .Where(x =>
                    x.OrderId == item.OrderId &&
                    x.Status != KitchenStatus.DaHuy)
                .SumAsync(x =>
                    (decimal?)(x.Quantity * x.UnitPrice))
                ?? 0;

            if (item.Order is not null)
            {
                item.Order.TotalAmount = totalAmount;
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            var tableId = item.Order?.TableId ?? 0;
            var tableName =
                item.Order?.Table?.TableName ?? $"Bàn #{tableId}";
            var itemName =
                item.MenuItem?.ItemName ?? "Món";

            await _hub.Clients.All.SendAsync(
                "OrderItemQuantityUpdated",
                new
                {
                    id = item.OrderItemId,
                    orderId = item.OrderId,
                    tableId,
                    tableName,
                    itemName,
                    quantity = item.Quantity,
                    totalAmount
                });

            await _hub.Clients.All.SendAsync(
                "ItemStatusUpdated",
                new
                {
                    Id = item.OrderItemId,
                    item.OrderId,
                    TableId = tableId,
                    TableName = tableName,
                    MenuItemName = itemName,
                    Status = item.Status,
                    Quantity = item.Quantity
                });

            return Ok(new
            {
                id = item.OrderItemId,
                quantity = item.Quantity,
                totalAmount,
                changedBy = userId
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("items/{id:int}/decrement")]
    [RequireMobilePermission("order.cancel")]
    public async Task<IActionResult> DecrementQuantity(
        int id,
        [FromBody] MobileOrderActionRequest request)
    {
        if (id <= 0)
            return BadRequest(new { message = "Món không hợp lệ" });

        var userId = await ResolveUserIdAsync(request.EmployeeCode);

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var item = await _db.OrderItems
                .Include(x => x.Order)
                    .ThenInclude(o => o!.Table)
                .Include(x => x.MenuItem)
                .FirstOrDefaultAsync(
                    x => x.OrderItemId == id);

            if (item is null)
            {
                await transaction.RollbackAsync();
                return NotFound(new { message = "Không tìm thấy món" });
            }

            if (item.Status != KitchenStatus.MoiGoi)
            {
                await transaction.RollbackAsync();
                return Conflict(new
                {
                    message =
                        "Chỉ có thể giảm số lượng khi bếp chưa bắt đầu chế biến"
                });
            }

            if (item.Quantity <= 1)
            {
                await transaction.RollbackAsync();
                return Conflict(new
                {
                    message =
                        "Số lượng món đã là 1. Hãy hủy món nếu không còn gọi món này."
                });
            }

            item.Quantity -= 1;

            await _db.SaveChangesAsync();

            var totalAmount = await _db.OrderItems
                .Where(x =>
                    x.OrderId == item.OrderId &&
                    x.Status != KitchenStatus.DaHuy)
                .SumAsync(x =>
                    (decimal?)(x.Quantity * x.UnitPrice))
                ?? 0;

            if (item.Order is not null)
            {
                item.Order.TotalAmount = totalAmount;
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            var tableId = item.Order?.TableId ?? 0;
            var tableName =
                item.Order?.Table?.TableName ??
                $"Bàn #{tableId}";
            var itemName =
                item.MenuItem?.ItemName ?? "Món";

            // Mobile/Web có thể dùng event này khi cần cập nhật số lượng realtime.
            await _hub.Clients.All.SendAsync(
                "OrderItemQuantityUpdated",
                new
                {
                    id = item.OrderItemId,
                    orderId = item.OrderId,
                    tableId,
                    tableName,
                    itemName,
                    quantity = item.Quantity,
                    totalAmount
                });

            // WinForms Bếp đang nghe ItemStatusUpdated. Gửi cùng trạng thái hiện tại
            // và thêm Quantity để cập nhật 2x -> 1x ngay trên phiếu bếp.
            await _hub.Clients.All.SendAsync(
                "ItemStatusUpdated",
                new
                {
                    Id = item.OrderItemId,
                    item.OrderId,
                    TableId = tableId,
                    TableName = tableName,
                    MenuItemName = itemName,
                    Status = item.Status,
                    Quantity = item.Quantity
                });

            return Ok(new
            {
                id = item.OrderItemId,
                quantity = item.Quantity,
                totalAmount,
                changedBy = userId
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("clear-table")]
    [RequireMobilePermission("order.cancel")]
    [RequireMobilePermission("table.manage")]
    public async Task<IActionResult> ClearTable(
        [FromBody] MobileClearTableRequest request)
    {
        if (request.TableId <= 0)
            return BadRequest(new { message = "Bàn không hợp lệ" });

        var userId = await ResolveUserIdAsync(request.EmployeeCode);
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            string tableName;
            await using (var tableCommand = connection.CreateCommand())
            {
                tableCommand.Transaction = transaction;
                tableCommand.CommandText = "SELECT TableName FROM DiningTables WHERE TableId = @TableId";
                var p = tableCommand.CreateParameter();
                p.ParameterName = "@TableId";
                p.Value = request.TableId;
                tableCommand.Parameters.Add(p);

                var rawName = await tableCommand.ExecuteScalarAsync();
                if (rawName is null || rawName == DBNull.Value)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { message = "Không tìm thấy bàn" });
                }

                tableName = rawName.ToString() ?? $"Bàn #{request.TableId}";
            }

            var orderIds = new List<int>();
            await using (var orderCommand = connection.CreateCommand())
            {
                orderCommand.Transaction = transaction;
                orderCommand.CommandText = """
                    SELECT OrderId
                    FROM Orders
                    WHERE TableId = @TableId
                      AND Status = N'DangMo'
                    ORDER BY OrderId DESC;
                    """;
                var p = orderCommand.CreateParameter();
                p.ParameterName = "@TableId";
                p.Value = request.TableId;
                orderCommand.Parameters.Add(p);

                await using var reader = await orderCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    orderIds.Add(reader.GetInt32(0));
            }

            var hasOrderTables = false;
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.Transaction = transaction;
                existsCommand.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.OrderTables', N'U') IS NULL THEN 0 ELSE 1 END";
                hasOrderTables = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) == 1;
            }

            if (hasOrderTables)
            {
                await using var linkedOrderCommand = connection.CreateCommand();
                linkedOrderCommand.Transaction = transaction;
                linkedOrderCommand.CommandText = """
                    SELECT DISTINCT o.OrderId
                    FROM Orders o
                    INNER JOIN OrderTables ot ON ot.OrderId = o.OrderId
                    WHERE ot.TableId = @TableId
                      AND o.Status = N'DangMo';
                    """;
                var p = linkedOrderCommand.CreateParameter();
                p.ParameterName = "@TableId";
                p.Value = request.TableId;
                linkedOrderCommand.Parameters.Add(p);

                await using var reader = await linkedOrderCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    orderIds.Add(reader.GetInt32(0));
            }

            orderIds = orderIds.Distinct().ToList();

            if (orderIds.Count == 0)
            {
                await using var emptyTableCommand = connection.CreateCommand();
                emptyTableCommand.Transaction = transaction;
                emptyTableCommand.CommandText = "UPDATE DiningTables SET Status = N'Trong' WHERE TableId = @TableId";
                var p = emptyTableCommand.CreateParameter();
                p.ParameterName = "@TableId";
                p.Value = request.TableId;
                emptyTableCommand.Parameters.Add(p);
                await emptyTableCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                await _hub.Clients.All.SendAsync("TableStatusUpdated", new
                {
                    TableId = request.TableId,
                    Status = TableStatus.Trong
                });

                return Ok(new
                {
                    tableId = request.TableId,
                    status = "Trống",
                    cancelledCount = 0
                });
            }

            static void AddIds(System.Data.Common.DbCommand command, IReadOnlyList<int> ids, string prefix)
            {
                for (var i = 0; i < ids.Count; i++)
                {
                    var p = command.CreateParameter();
                    p.ParameterName = $"@{prefix}{i}";
                    p.Value = ids[i];
                    command.Parameters.Add(p);
                }
            }

            static string IdList(IReadOnlyList<int> ids, string prefix) =>
                string.Join(",", Enumerable.Range(0, ids.Count).Select(i => $"@{prefix}{i}"));

            var orderIn = IdList(orderIds, "OrderId");

            int blockedCount;
            await using (var blockedCommand = connection.CreateCommand())
            {
                blockedCommand.Transaction = transaction;
                blockedCommand.CommandText = $"""
                    SELECT COUNT(*)
                    FROM OrderItems
                    WHERE OrderId IN ({orderIn})
                      AND Status NOT IN (N'MoiGoi', N'DaHuy');
                    """;
                AddIds(blockedCommand, orderIds, "OrderId");
                blockedCount = Convert.ToInt32(await blockedCommand.ExecuteScalarAsync());
            }

            if (blockedCount > 0)
            {
                await transaction.RollbackAsync();
                return Conflict(new
                {
                    message = $"Có {blockedCount} món bếp đã bắt đầu xử lý hoặc đã phục vụ. Chỉ món Chờ chế biến mới có thể xóa."
                });
            }

            var itemIds = new List<int>();
            await using (var itemCommand = connection.CreateCommand())
            {
                itemCommand.Transaction = transaction;
                itemCommand.CommandText = $"""
                    SELECT OrderItemId
                    FROM OrderItems
                    WHERE OrderId IN ({orderIn})
                      AND Status = N'MoiGoi';
                    """;
                AddIds(itemCommand, orderIds, "OrderId");

                await using var reader = await itemCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    itemIds.Add(reader.GetInt32(0));
            }

            if (itemIds.Count > 0)
            {
                var itemIn = IdList(itemIds, "ItemId");

                await using (var ticketCommand = connection.CreateCommand())
                {
                    ticketCommand.Transaction = transaction;
                    ticketCommand.CommandText = $"""
                        UPDATE KitchenTickets
                        SET Status = N'HoanThanh',
                            CompletedTime = COALESCE(CompletedTime, GETDATE())
                        WHERE OrderItemId IN ({itemIn});
                        """;
                    AddIds(ticketCommand, itemIds, "ItemId");
                    await ticketCommand.ExecuteNonQueryAsync();
                }

                await using (var historyCommand = connection.CreateCommand())
                {
                    historyCommand.Transaction = transaction;
                    historyCommand.CommandText = $"""
                        INSERT INTO DishStatusHistory (OrderItemId, Status, ChangedAt, ChangedBy)
                        SELECT OrderItemId, N'DaHuy', GETDATE(), @ChangedBy
                        FROM OrderItems
                        WHERE OrderItemId IN ({itemIn});
                        """;
                    AddIds(historyCommand, itemIds, "ItemId");
                    var changedBy = historyCommand.CreateParameter();
                    changedBy.ParameterName = "@ChangedBy";
                    changedBy.Value = userId.HasValue ? userId.Value : DBNull.Value;
                    historyCommand.Parameters.Add(changedBy);
                    await historyCommand.ExecuteNonQueryAsync();
                }

                await using (var cancelCommand = connection.CreateCommand())
                {
                    cancelCommand.Transaction = transaction;
                    cancelCommand.CommandText = $"""
                        UPDATE OrderItems
                        SET Status = N'DaHuy'
                        WHERE OrderItemId IN ({itemIn})
                          AND Status = N'MoiGoi';
                        """;
                    AddIds(cancelCommand, itemIds, "ItemId");
                    await cancelCommand.ExecuteNonQueryAsync();
                }
            }

            await using (var closeOrderCommand = connection.CreateCommand())
            {
                closeOrderCommand.Transaction = transaction;
                closeOrderCommand.CommandText = $"""
                    UPDATE Orders
                    SET Status = N'DaHuy'
                    WHERE OrderId IN ({orderIn})
                      AND Status = N'DangMo';
                    """;
                AddIds(closeOrderCommand, orderIds, "OrderId");
                await closeOrderCommand.ExecuteNonQueryAsync();
            }

            var tableIds = new HashSet<int> { request.TableId };
            foreach (var orderId in orderIds)
            {
                await using var primaryTableCommand = connection.CreateCommand();
                primaryTableCommand.Transaction = transaction;
                primaryTableCommand.CommandText = "SELECT TableId FROM Orders WHERE OrderId = @OrderId";
                var p = primaryTableCommand.CreateParameter();
                p.ParameterName = "@OrderId";
                p.Value = orderId;
                primaryTableCommand.Parameters.Add(p);
                var raw = await primaryTableCommand.ExecuteScalarAsync();
                if (raw is not null && raw != DBNull.Value)
                    tableIds.Add(Convert.ToInt32(raw));
            }

            if (hasOrderTables)
            {
                await using var linkedTableCommand = connection.CreateCommand();
                linkedTableCommand.Transaction = transaction;
                linkedTableCommand.CommandText = $"""
                    SELECT DISTINCT TableId
                    FROM OrderTables
                    WHERE OrderId IN ({orderIn});
                    """;
                AddIds(linkedTableCommand, orderIds, "OrderId");
                await using var reader = await linkedTableCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tableIds.Add(reader.GetInt32(0));
            }

            var tableIdList = tableIds.ToList();
            var tableIn = IdList(tableIdList, "TableId");
            await using (var releaseCommand = connection.CreateCommand())
            {
                releaseCommand.Transaction = transaction;
                releaseCommand.CommandText = $"UPDATE DiningTables SET Status = N'Trong' WHERE TableId IN ({tableIn});";
                AddIds(releaseCommand, tableIdList, "TableId");
                await releaseCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            foreach (var itemId in itemIds)
            {
                await _hub.Clients.All.SendAsync("DishStatusChanged", new
                {
                    orderItemId = itemId,
                    tableId = request.TableId,
                    tableName,
                    itemName = "Món",
                    status = "Đã hủy"
                });
            }

            foreach (var releasedTableId in tableIds)
            {
                await _hub.Clients.All.SendAsync("TableStatusUpdated", new
                {
                    TableId = releasedTableId,
                    Status = TableStatus.Trong
                });
            }

            return Ok(new
            {
                tableId = request.TableId,
                status = "Trống",
                cancelledCount = itemIds.Count
            });
        }
        catch (Exception error)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch
            {
            }

            return StatusCode(500, new
            {
                message = $"Không thể xóa món và trả bàn: {error.Message}"
            });
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<int?> ResolveUserIdAsync(string? employeeCode)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (int.TryParse(claim, out var claimedUserId) && claimedUserId > 0)
            return claimedUserId;

        if (string.IsNullOrWhiteSpace(employeeCode))
            return null;

        return await _db.Employees
            .Where(e => e.EmployeeCode == employeeCode.Trim())
            .Select(e => (int?)e.UserId)
            .FirstOrDefaultAsync();
    }

        private async Task BroadcastAsync(
        IEnumerable<OrderItem> items,
        string status)
    {
        foreach (var item in items)
        {
            var tableId = item.Order?.TableId ?? 0;
            var tableName = item.Order?.Table?.TableName ?? $"Bàn #{tableId}";
            var itemName = item.MenuItem?.ItemName ?? "Món";

            // Cho mobile (giữ nguyên)
            await _hub.Clients.All.SendAsync("DishStatusChanged", new
            {
                orderItemId = item.OrderItemId,
                tableId,
                tableName,
                itemName,
                status
            });

            // Cho WinForm (Sơ đồ bàn, Bếp, Đơn mang về) - cùng contract với OrderItemsController
            await _hub.Clients.All.SendAsync("ItemStatusUpdated", new
            {
                Id = item.OrderItemId,
                item.OrderId,
                TableId = tableId,
                TableName = tableName,
                MenuItemName = itemName,
                Status = item.Status
            });
        }
    }

    private static List<int> NormalizeIds(List<int>? values)
    {
        return (values ?? new List<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }
}
