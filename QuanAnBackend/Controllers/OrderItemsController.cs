using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record UpdateOrderItemStatusDto(
    KitchenStatus NewStatus,
    int? UserId,
    string? CancelReason,
    byte[]? RowVer
);

[ApiController]
[Route("api/[controller]")]
public class OrderItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    private readonly ILogger<OrderItemsController> _logger;

    public OrderItemsController(
        AppDbContext db,
        IHubContext<OrderHub> hub,
        NotificationService notifications,
        ILogger<OrderItemsController> logger)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? departmentId, [FromQuery] KitchenStatus? status, [FromQuery] int? tableId)
    {
        int? effectiveDepartmentId = departmentId;

        // Cùng role Bep nhưng mỗi tài khoản chỉ được thao tác Station được phân công.
        if (User.IsInRole("Bep") && !User.IsInRole("QuanLy"))
        {
            var userId = CurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "Không xác định được tài khoản chế biến" });

            var employeeStation = await _db.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Select(e => new
                {
                    e.StationId,
                    StationName = e.Station != null ? e.Station.StationName : ""
                })
                .FirstOrDefaultAsync();

            if (employeeStation?.StationId is null)
                return Conflict(new { message = "Tài khoản chế biến chưa được phân công Bếp hoặc Quầy nước" });

            if (departmentId.HasValue && departmentId.Value != employeeStation.StationId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = $"Tài khoản này chỉ được thao tác bộ phận {employeeStation.StationName}" });

            effectiveDepartmentId = employeeStation.StationId.Value;
        }

        var query = _db.OrderItems
            .Include(oi => oi.MenuItem)
            .Include(oi => oi.Order)!.ThenInclude(o => o!.Table)
            .AsNoTracking().AsQueryable();

        if (effectiveDepartmentId.HasValue)
            query = query.Where(oi => oi.MenuItem!.StationId == effectiveDepartmentId.Value);

        if (tableId.HasValue)
            query = query.Where(oi => oi.Order != null && oi.Order.TableId == tableId.Value && oi.Order.Status == OrderStatus.DangMo);

        if (status.HasValue)
            query = query.Where(oi => oi.Status == status.Value);
        else if (effectiveDepartmentId.HasValue && !tableId.HasValue)
            query = query.Where(oi => oi.Status == KitchenStatus.MoiGoi || oi.Status == KitchenStatus.DangCheBien);
        else
            query = query.Where(oi => oi.Status != KitchenStatus.DaHuy && (tableId.HasValue || oi.Status != KitchenStatus.DaPhucVu));

        var rows = await query.OrderBy(oi => oi.OrderItemId).Select(oi => new
        {
            Id = oi.OrderItemId,
            oi.OrderId,
            TableId = oi.Order != null ? oi.Order.TableId : null,
            BaseTableName = oi.Order != null && oi.Order.Table != null ? oi.Order.Table.TableName : "",
            TakeawayNumber = oi.Order != null ? oi.Order.TakeawayNumber : null,
            OrderType = oi.Order != null ? oi.Order.OrderType : OrderTypes.TaiBan,
            ItemIsTakeaway = oi.IsTakeaway,
            oi.MenuItemId,
            MenuItemName = oi.MenuItem != null ? oi.MenuItem.ItemName : "",
            oi.Quantity,
            oi.Note,
            KitchenStatus = oi.Status,
            oi.CancelReason,
            oi.RowVer,
            oi.CreatedAt
        }).ToListAsync();

        var items = rows.Select(r => new
        {
            r.Id,
            r.OrderId,
            r.TableId,
            TableName = r.TakeawayNumber.HasValue ? $"Mang về MV-{r.TakeawayNumber.Value:000}" : r.BaseTableName,
            IsTakeaway = r.ItemIsTakeaway || r.OrderType == OrderTypes.MangVe,
            r.MenuItemId,
            r.MenuItemName,
            r.Quantity,
            r.Note,
            r.KitchenStatus,
            r.CancelReason,
            r.RowVer,
            r.CreatedAt
        }).ToList();

        return Ok(items);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "QuanLy,Bep")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderItemStatusDto request)
    {
        var item = await _db.OrderItems
            .Include(oi => oi.MenuItem)
            .Include(oi => oi.Order)!.ThenInclude(o => o!.Table)
            .Include(oi => oi.Order)!.ThenInclude(o => o!.Employee)
            .FirstOrDefaultAsync(oi => oi.OrderItemId == id);

        if (item is null) return NotFound(new { message = "Món ăn không tồn tại." });

        var actorUserId = CurrentUserId() ?? request.UserId;

        if (User.IsInRole("Bep") && !User.IsInRole("QuanLy"))
        {
            if (!actorUserId.HasValue)
                return Unauthorized(new { message = "Không xác định được tài khoản chế biến" });

            var employeeStation = await _db.Employees
                .AsNoTracking()
                .Where(e => e.UserId == actorUserId.Value)
                .Select(e => new
                {
                    e.StationId,
                    StationName = e.Station != null ? e.Station.StationName : ""
                })
                .FirstOrDefaultAsync();

            if (employeeStation?.StationId is null)
                return Conflict(new { message = "Tài khoản chế biến chưa được phân công Bếp hoặc Quầy nước" });

            if (item.MenuItem?.StationId != employeeStation.StationId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = $"Món này không thuộc bộ phận {employeeStation.StationName} của tài khoản đang đăng nhập" });
        }

        if (item.Status == KitchenStatus.DaHuy)
            return BadRequest(new { message = "Món ăn này đã bị hủy trước đó." });

        if (item.Order != null && item.Order.Status != OrderStatus.DangMo && request.NewStatus == KitchenStatus.DaHuy)
            return BadRequest(new { message = "Đơn hàng đã thanh toán/đóng, không thể hủy món này." });

        if (request.NewStatus == item.Status)
            return Ok(new { Id = item.OrderItemId, item.OrderId, KitchenStatus = item.Status, RowVer = item.RowVer });

        if (request.NewStatus == KitchenStatus.DaHuy)
        {
            if (item.Status != KitchenStatus.MoiGoi)
                return BadRequest(new { message = "Chỉ có thể hủy món khi món ở trạng thái 'Mới gọi'." });
            item.CancelReason = request.CancelReason;
            item.CancelledByUserId = actorUserId;
        }
        else
        {
            bool isValid = (item.Status == KitchenStatus.MoiGoi && request.NewStatus == KitchenStatus.DangCheBien) ||
                           (item.Status == KitchenStatus.DangCheBien && request.NewStatus == KitchenStatus.DaCheBien) ||
                           (item.Status == KitchenStatus.DaCheBien && request.NewStatus == KitchenStatus.DaPhucVu);
            if (!isValid)
                return BadRequest(new { message = $"Không thể chuyển từ trạng thái '{item.Status}' sang '{request.NewStatus}'." });
        }

        item.Status = request.NewStatus;

        var ticket = await _db.KitchenTickets.FirstOrDefaultAsync(k => k.OrderItemId == item.OrderItemId);
        if (ticket != null)
        {
            if (request.NewStatus == KitchenStatus.DangCheBien)
            {
                ticket.Status = KitchenTicketStatus.DangCheBien;
                if (actorUserId.HasValue)
                {
                    var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == actorUserId.Value);
                    if (emp != null) ticket.ChefId = emp.EmployeeId;
                }
            }
            else if (request.NewStatus == KitchenStatus.DaCheBien)
            {
                ticket.Status = KitchenTicketStatus.HoanThanh;
                ticket.CompletedTime = DateTime.Now;
            }
        }

        _db.DishStatusHistories.Add(new DishStatusHistory
        {
            OrderItemId = item.OrderItemId,
            Status = request.NewStatus,
            ChangedAt = DateTime.Now,
            ChangedBy = actorUserId
        });
        // Đơn đã thanh toán trước (thanh toán sớm / đơn mang về): món vừa chế biến xong
        // coi như đã giao khách, tự chuyển sang "Đã phục vụ".
        if (request.NewStatus == KitchenStatus.DaCheBien &&
            item.Order?.Status == OrderStatus.DaThanhToan)
        {
            item.Status = KitchenStatus.DaPhucVu;
            _db.DishStatusHistories.Add(new DishStatusHistory
            {
                OrderItemId = item.OrderItemId,
                Status = KitchenStatus.DaPhucVu,
                ChangedAt = DateTime.Now,
                ChangedBy = actorUserId
            });
        }
        if (request.RowVer != null && request.RowVer.Length > 0)
            _db.Entry(item).Property(x => x.RowVer).OriginalValue = request.RowVer;

        try
        {
            await _db.SaveChangesAsync();

            if (item.Order?.CustomerId is int customerId)
            {
                var statusLabel = request.NewStatus switch
                {
                    KitchenStatus.DangCheBien => "đang được chế biến",
                    KitchenStatus.DaCheBien => "đã chế biến xong",
                    KitchenStatus.DaPhucVu => "đã được phục vụ",
                    KitchenStatus.DaHuy => "đã bị hủy",
                    _ => "đã được cập nhật"
                };
                await _notifications.CreateForCustomerAsync(
                    customerId,
                    "Cập nhật món trong đơn hàng",
                    $"Món {item.MenuItem?.ItemName ?? "ăn"} trong đơn #{item.OrderId} {statusLabel}.",
                    NotificationType.DonHang,
                    $"order-item-status:{item.OrderItemId}:{(int)request.NewStatus}:{DateTime.Now.Ticks}",
                    "Order",
                    item.OrderId);
            }

            try
            {
                await _hub.Clients.Group("order-management").SendAsync("OrderUpdated", new
                {
                    orderId = item.OrderId
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Order {OrderId} item status was saved but realtime notification failed.", item.OrderId);
            }

            if (request.NewStatus == KitchenStatus.DaHuy)
            {
                await _hub.Clients.Group("internal-users").SendAsync("OrderItemCancelled", new
                {
                    Id = item.OrderItemId,
                    item.OrderId,
                    TableId = item.Order?.TableId ?? 0,
                    TableName = OrderLabel(item.Order),
                    MenuItemName = item.MenuItem?.ItemName ?? "",
                    Reason = item.CancelReason
                });
                await BroadcastItemStatusAsync(item);
            }
            else
            {
                await BroadcastItemStatusAsync(item);
                if (request.NewStatus == KitchenStatus.DaCheBien)
                {
                    await _notifications.CreateForPermissionAsync(
                        "Món đã chế biến xong",
                        $"{item.MenuItem?.ItemName} - {OrderLabel(item.Order)} đã sẵn sàng phục vụ",
                        NotificationType.DonHang,
                        "order.serve",
                        relatedEntityType: "OrderItem",
                        relatedEntityId: item.OrderItemId);
                }
            }

            return Ok(new { Id = item.OrderItemId, item.OrderId, KitchenStatus = item.Status, RowVer = item.RowVer });
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(409, new { message = "Món ăn đã được cập nhật hoặc hủy bởi người khác. Vui lòng tải lại trang." });
        }
    }

    [HttpPut("order/{orderId}/status")]
    [Authorize(Roles = "QuanLy,Bep")]
    public async Task<IActionResult> UpdateStatusForOrder(int orderId, [FromBody] KitchenStatus status)
    {
        var items = await _db.OrderItems
            .Include(oi => oi.MenuItem)
            .Include(oi => oi.Order)!.ThenInclude(o => o!.Table)
            .Include(oi => oi.Order)!.ThenInclude(o => o!.Employee)
            .Where(oi => oi.OrderId == orderId && oi.Status != KitchenStatus.DaHuy)
            .ToListAsync();

        var actorUserId = CurrentUserId();
        if (User.IsInRole("Bep") && !User.IsInRole("QuanLy"))
        {
            if (!actorUserId.HasValue)
                return Unauthorized(new { message = "Không xác định được tài khoản chế biến" });

            var stationId = await _db.Employees
                .AsNoTracking()
                .Where(e => e.UserId == actorUserId.Value)
                .Select(e => e.StationId)
                .FirstOrDefaultAsync();

            if (!stationId.HasValue)
                return Conflict(new { message = "Tài khoản chế biến chưa được phân công Bếp hoặc Quầy nước" });

            items = items.Where(item => item.MenuItem?.StationId == stationId.Value).ToList();
        }

        if (!items.Any()) return NotFound();

        foreach (var item in items)
        {
            item.Status = status;
            _db.DishStatusHistories.Add(new DishStatusHistory
            {
                OrderItemId = item.OrderItemId,
                Status = status,
                ChangedAt = DateTime.Now,
                ChangedBy = actorUserId
            });
        }
        await _db.SaveChangesAsync();

        var customerId = items.Select(item => item.Order?.CustomerId).FirstOrDefault(id => id.HasValue);
        if (customerId.HasValue)
        {
            var statusLabel = status switch
            {
                KitchenStatus.DangCheBien => "đang được chế biến",
                KitchenStatus.DaCheBien => "đã chế biến xong",
                KitchenStatus.DaPhucVu => "đã được phục vụ",
                _ => "đã được cập nhật"
            };
            await _notifications.CreateForCustomerAsync(
                customerId.Value,
                "Cập nhật các món trong đơn hàng",
                $"{items.Count} món trong đơn #{orderId} {statusLabel}.",
                NotificationType.DonHang,
                $"order-items-status:{orderId}:{(int)status}:{DateTime.UtcNow.Ticks}",
                "Order",
                orderId);
        }

        try
        {
            await _hub.Clients.Group("order-management").SendAsync("OrderUpdated", new
            {
                orderId
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Order {OrderId} items status was saved but realtime notification failed.", orderId);
        }

        foreach (var item in items) await BroadcastItemStatusAsync(item);
        return Ok(new { updated = items.Count });
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var userId) ? userId : null;
    }

    private static string OrderLabel(Order? order)
    {
        if (order is null) return "";
        if (order.Table is not null) return order.Table.TableName;
        return order.TakeawayCode is not null ? $"Mang về {order.TakeawayCode}" : "";
    }

    private async Task BroadcastItemStatusAsync(OrderItem item)
    {
        var label = OrderLabel(item.Order);
        await _hub.Clients.Group("internal-users").SendAsync("ItemStatusUpdated", new
        {
            Id = item.OrderItemId,
            item.OrderId,
            TableId = item.Order?.TableId ?? 0,
            TableName = label,
            MenuItemName = item.MenuItem?.ItemName ?? "",
            Status = item.Status
        });

        var employeeCode = item.Order?.Employee?.EmployeeCode;
        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            await _hub.Clients.Group($"employee-{employeeCode}").SendAsync("DishStatusChanged", new
            {
                orderItemId = item.OrderItemId,
                tableId = item.Order?.TableId ?? 0,
                tableName = label,
                itemName = item.MenuItem?.ItemName ?? "Món",
                status = item.Status switch
                {
                    KitchenStatus.DangCheBien => "Đang chế biến",
                    KitchenStatus.DaCheBien => "Đã xong",
                    KitchenStatus.DaPhucVu => "Đã phục vụ",
                    KitchenStatus.DaHuy => "Đã hủy",
                    _ => "Chờ chế biến"
                }
            });
        }
    }
}