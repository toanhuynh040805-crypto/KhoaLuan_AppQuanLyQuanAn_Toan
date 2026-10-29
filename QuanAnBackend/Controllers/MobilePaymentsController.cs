using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

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
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o =>
                o.TableId == tableId &&
                o.Status == OrderStatus.DangMo)
            .OrderByDescending(o => o.OrderTime)
            .Select(o => new
            {
                o.OrderId,
                o.PaymentRequestedByUserId,
                o.PaymentRequestedAt
            })
            .FirstOrDefaultAsync();

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

        var pending = await _db.Orders
            .AsNoTracking()
            .Where(o =>
                requestedOrderIds.Contains(o.OrderId) &&
                o.Status == OrderStatus.DangMo)
            .OrderBy(o => o.OrderTime)
            .Select(o => new
            {
                orderId = o.OrderId,
                tableId = o.TableId,
                tableName = o.Table != null ? o.Table.TableName : $"Bàn #{o.TableId}",
                orderTime = o.OrderTime
            })
            .ToListAsync();

        return Ok(pending);
    }
}
