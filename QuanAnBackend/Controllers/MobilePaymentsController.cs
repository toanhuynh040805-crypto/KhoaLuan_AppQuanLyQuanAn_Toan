using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

internal sealed class PendingOrderTableLinkRow
{
    public int OrderId { get; set; }
    public int TableId { get; set; }
}

/// <summary>
/// Các API trạng thái thanh toán dành cho Mobile.
/// Không tạo thêm bảng/CSDL: trạng thái "chờ thanh toán" được suy ra từ
/// Notification yêu cầu thanh toán + Order vẫn còn DangMo.
/// </summary>
[ApiController]
[Route("api/mobile/payments")]
public class MobilePaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobilePaymentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("request-lock/{tableId:int}")]
    [Authorize]
    public async Task<IActionResult> GetRequestLock(int tableId)
    {
        var orderId = await _db.Database.SqlQuery<int>($"""
            SELECT TOP 1 o.OrderId AS [Value]
            FROM Orders o
            LEFT JOIN OrderTables ot ON ot.OrderId = o.OrderId
            WHERE o.Status = N'DangMo'
              AND (o.TableId = {tableId} OR ot.TableId = {tableId})
            ORDER BY o.OrderTime DESC, o.OrderId DESC;
            """).FirstOrDefaultAsync();

        var order = orderId > 0
            ? await _db.Orders
                .AsNoTracking()
                .Where(o => o.OrderId == orderId)
                .Select(o => new
                {
                    o.OrderId,
                    o.PaymentRequestedByUserId,
                    o.PaymentRequestedAt
                })
                .FirstOrDefaultAsync()
            : null;

        if (order is null)
        {
            return Ok(new
            {
                isLocked = false,
                remainingSeconds = 0,
                requestedByUserId = (int?)null,
                requestedAt = (DateTime?)null
            });
        }

        var now = DateTime.Now;
        var lockUntil = order.PaymentRequestedAt?.AddMinutes(5);
        var isLocked =
            lockUntil.HasValue &&
            now < lockUntil.Value;

        var remainingSeconds = isLocked
            ? Math.Max(
                1,
                (int)Math.Ceiling(
                    (lockUntil!.Value - now).TotalSeconds))
            : 0;

        return Ok(new
        {
            orderId = order.OrderId,
            isLocked,
            remainingSeconds,
            requestedByUserId =
                order.PaymentRequestedByUserId,
            requestedAt = order.PaymentRequestedAt,
            lockUntil
        });
    }

    /// <summary>
    /// Thu ngân/Quản lý lấy danh sách bàn đang có yêu cầu thanh toán.
    /// Việc đã đọc thông báo không làm mất trạng thái chờ; trạng thái chỉ hết
    /// khi Order được thanh toán/đóng.
    /// </summary>
    [HttpGet("pending")]
    [RequireMobilePermission("payment.complete")]
    public async Task<IActionResult> GetPending()
    {
        var requestedOrderIds = await _db.Notifications
            .AsNoTracking()
            .Where(n =>
                n.Title == "Yêu cầu thanh toán" &&
                n.RelatedEntityType == "Order" &&
                n.RelatedEntityId.HasValue)
            .Select(n => n.RelatedEntityId!.Value)
            .Distinct()
            .ToListAsync();

        if (requestedOrderIds.Count == 0)
            return Ok(Array.Empty<object>());

        var pendingOrders = await _db.Orders
            .AsNoTracking()
            .Where(o =>
                requestedOrderIds.Contains(o.OrderId) &&
                o.Status == OrderStatus.DangMo)
            .OrderBy(o => o.OrderTime)
            .Select(o => new
            {
                o.OrderId,
                o.TableId,
                o.OrderTime
            })
            .ToListAsync();

        if (pendingOrders.Count == 0)
            return Ok(Array.Empty<object>());

        var pendingOrderIdSet = pendingOrders
            .Select(order => order.OrderId)
            .ToHashSet();

        var linkedRows = await _db.Database
            .SqlQuery<PendingOrderTableLinkRow>($"""
                SELECT ot.OrderId, ot.TableId
                FROM OrderTables ot
                INNER JOIN Orders o ON o.OrderId = ot.OrderId
                WHERE o.Status = N'DangMo';
                """)
            .ToListAsync();

        linkedRows = linkedRows
            .Where(row => pendingOrderIdSet.Contains(row.OrderId))
            .ToList();

        var tableIds = pendingOrders
            .Where(order => order.TableId.HasValue)
            .Select(order => order.TableId!.Value)
            .Concat(linkedRows.Select(row => row.TableId))
            .Distinct()
            .ToList();

        var tableNames = await _db.Tables
            .AsNoTracking()
            .Where(table => tableIds.Contains(table.TableId))
            .ToDictionaryAsync(table => table.TableId, table => table.TableName);

        var pending = pendingOrders
            .SelectMany(order =>
            {
                var ids = linkedRows
                    .Where(row => row.OrderId == order.OrderId)
                    .Select(row => row.TableId)
                    .Concat(order.TableId.HasValue
                        ? new[] { order.TableId.Value }
                        : Array.Empty<int>())
                    .Distinct();

                return ids.Select(tableId => new
                {
                    orderId = order.OrderId,
                    tableId,
                    tableName = tableNames.TryGetValue(tableId, out var name)
                        ? name
                        : $"Bàn #{tableId}",
                    orderTime = order.OrderTime
                });
            })
            .OrderBy(row => row.orderTime)
            .ThenBy(row => row.tableName)
            .ToList();

        return Ok(pending);
    }
}
