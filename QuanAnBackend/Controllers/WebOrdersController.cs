using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

[Route("api/orders")]
[ApiController]
[Authorize(Roles = "Customer")]
public class WebOrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<WebOrdersController> _logger;
    private readonly OrderRoutingService _orderRoutingService;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;

    public WebOrdersController(
        AppDbContext context,
        ILogger<WebOrdersController> logger,
        OrderRoutingService orderRoutingService,
        IHubContext<OrderHub> hub,
        NotificationService notifications)
    {
        _context = context;
        _logger = logger;
        _orderRoutingService = orderRoutingService;
        _hub = hub;
        _notifications = notifications;
    }

    private int GetCurrentCustomerId()
        => CustomerTokens.GetCustomerId(User) ?? 0;

    [HttpPost("customer-order")]
    public async Task<IActionResult> CreateCustomerOrder([FromBody] WebOrderRequestDto request)
    {
        var customerId = GetCurrentCustomerId();
        if (customerId <= 0)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        if (request.ClientRequestId is null || request.ClientRequestId == Guid.Empty)
            return BadRequest(new { message = "Thiếu mã yêu cầu đặt món. Vui lòng tải lại trang và thử lại." });

        if (request.TableId is null or <= 0)
            return BadRequest(new { message = "Vui lòng chọn bàn hợp lệ." });

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Giỏ hàng đang trống." });

        if (request.Items.Any(item =>
                item is null ||
                item.MenuItemId is null or <= 0 ||
                item.ComboId.HasValue ||
                item.Quantity is < 1 or > 99 ||
                (item.Note?.Length ?? 0) > 255))
        {
            return BadRequest(new { message = "Món ăn, số lượng hoặc ghi chú không hợp lệ." });
        }

        var payloadHash = CalculateRequestHash(request);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            if (!await _context.Customers.AnyAsync(customer => customer.CustomerId == customerId))
                return Unauthorized(new { message = "Không tìm thấy tài khoản khách hàng. Vui lòng đăng nhập lại." });

            var previousOrder = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(order =>
                    order.CustomerId == customerId &&
                    order.ClientRequestId == request.ClientRequestId);

            if (previousOrder is not null)
            {
                if (!string.Equals(previousOrder.ClientRequestHash, payloadHash, StringComparison.Ordinal))
                    return Conflict(new
                    {
                        code = "IDEMPOTENCY_KEY_REUSED",
                        message = "Mã yêu cầu đã được dùng cho nội dung đơn hàng khác. Vui lòng tải lại checkout."
                    });

                var existingItemIds = await _context.OrderItems
                    .Where(item => item.OrderId == previousOrder.OrderId)
                    .Select(item => item.OrderItemId)
                    .ToListAsync();
                await transaction.CommitAsync();
                return await RouteAndRespondAsync(previousOrder, existingItemIds);
            }

            var table = await _context.Tables
                .FirstOrDefaultAsync(candidate => candidate.TableId == request.TableId.Value);
            if (table is null)
                return NotFound(new { message = "Không tìm thấy bàn đã chọn." });
            if (table.Status != TableStatus.Trong)
                return Conflict(new { message = "Bàn vừa được sử dụng hoặc không còn khả dụng. Vui lòng chọn bàn khác." });

            var webEmployeeId = await _context.Employees
                .Where(employee => employee.EmployeeCode == OrderSources.WebEmployeeCode)
                .Select(employee => employee.EmployeeId)
                .SingleOrDefaultAsync();

            if (webEmployeeId <= 0)
                return Conflict(new { message = "Nhà hàng chưa cấu hình nhân viên hệ thống nhận đơn web. Vui lòng liên hệ nhân viên." });

            var menuItemIds = request.Items.Select(item => item.MenuItemId!.Value).Distinct().ToArray();
            var menuItems = await _context.MenuItems
                .Where(menuItem => menuItemIds.Contains(menuItem.MenuItemId))
                .ToDictionaryAsync(menuItem => menuItem.MenuItemId);

            foreach (var item in request.Items)
            {
                var menuItemId = item.MenuItemId!.Value;
                if (!menuItems.TryGetValue(menuItemId, out var menuItem))
                    return NotFound(new { message = $"Món #{menuItemId} không còn trong thực đơn." });
                if (!menuItem.IsAvailable)
                    return Conflict(new { message = $"Món {menuItem.ItemName} hiện không còn bán." });
                if (menuItem.Price <= 0)
                    return Conflict(new { message = $"Giá món {menuItem.ItemName} chưa được cấu hình hợp lệ." });
            }

            var totalAmount = request.Items.Sum(item =>
                menuItems[item.MenuItemId!.Value].Price * item.Quantity);
            var order = new Order
            {
                TableId = table.TableId,
                OrderType = OrderTypes.TaiBan,
                EmployeeId = webEmployeeId,
                CustomerId = customerId,
                ClientRequestId = request.ClientRequestId,
                ClientRequestHash = payloadHash,
                Source = OrderSources.Web,
                OrderTime = DateTime.Now,
                Status = OrderStatus.DangMo,
                TotalAmount = totalAmount
            };

            foreach (var item in request.Items)
            {
                var menuItem = menuItems[item.MenuItemId!.Value];
                order.OrderItems.Add(new OrderItem
                {
                    MenuItemId = menuItem.MenuItemId,
                    Quantity = item.Quantity,
                    UnitPrice = menuItem.Price,
                    Note = item.Note?.Trim(),
                    Status = KitchenStatus.MoiGoi,
                    IsRefillRequest = false
                });
            }

            table.Status = TableStatus.DangPhucVu;
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _hub.Clients.All.SendAsync("TableStatusUpdated", new
                {
                    TableId = table.TableId,
                    table.TableName,
                    Status = table.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast TableStatusUpdated for TableId {TableId}", table.TableId);
            }

            return await RouteAndRespondAsync(
                order,
                order.OrderItems.Select(item => item.OrderItemId).ToList());
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Failed to create customer web order for customer {CustomerId}.", customerId);
            return StatusCode(500, new { message = "Không thể tạo đơn hàng lúc này. Giỏ hàng của bạn vẫn được giữ lại." });
        }
    }

    [HttpGet("customer-order/{clientRequestId:guid}")]
    public async Task<IActionResult> GetCustomerOrderByRequestId(Guid clientRequestId)
    {
        var customerId = GetCurrentCustomerId();
        if (customerId <= 0)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await _context.Orders
            .AsNoTracking()
            .Where(candidate =>
                candidate.CustomerId == customerId &&
                candidate.ClientRequestId == clientRequestId)
            .Select(candidate => new
            {
                candidate.OrderId,
                candidate.TableId,
                TableName = candidate.Table != null ? candidate.Table.TableName : null,
                candidate.TotalAmount,
                candidate.Status,
                Items = candidate.OrderItems.Select(item => new
                {
                    item.MenuItemId,
                    MenuItemName = item.MenuItem != null ? item.MenuItem.ItemName : "Món ăn",
                    item.Quantity,
                    item.UnitPrice,
                    item.Note
                }).ToList()
            })
            .FirstOrDefaultAsync();

        await transaction.CommitAsync();
        if (order is null)
            return NotFound(new
            {
                code = "ORDER_NOT_FOUND",
                message = "Chưa tìm thấy đơn hàng đã ghi nhận cho mã yêu cầu này."
            });

        return Ok(new
        {
            orderId = order.OrderId,
            tableId = order.TableId,
            tableName = order.TableName,
            totalAmount = order.TotalAmount,
            status = order.Status.ToString(),
            items = order.Items,
            message = "Đơn hàng đã được ghi nhận."
        });
    }

    private async Task<IActionResult> RouteAndRespondAsync(Order order, List<int> itemIds)
    {
        try
        {
            await _orderRoutingService.RouteNewOrderItemsAsync(order.OrderId, itemIds);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Customer order {OrderId} was saved but kitchen routing failed.", order.OrderId);
            return StatusCode(500, new
            {
                message = $"Đơn hàng #{order.OrderId} đã được ghi nhận nhưng chưa thể xác nhận chuyển đến bếp. Vui lòng thử lại cùng yêu cầu; không tạo đơn mới."
            });
        }

        var realtimeNotificationSent = await NotifyOrderManagementAsync(order.OrderId);
        if (order.CustomerId.HasValue)
        {
            await _notifications.CreateForCustomerAsync(
                order.CustomerId.Value,
                "Đơn hàng đã được tiếp nhận",
                $"Đơn hàng #{order.OrderId} của bạn đã được tiếp nhận và gửi đến nhà bếp.",
                NotificationType.DonHang,
                $"order-created:{order.OrderId}",
                "Order",
                order.OrderId);
        }
        return Ok(CreateOrderResponse(order, realtimeNotificationSent));
    }

    private async Task<bool> NotifyOrderManagementAsync(int orderId)
    {
        try
        {
            await _hub.Clients.Group("order-management").SendAsync("OrderCreated", new
            {
                orderId
            });
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Customer order {OrderId} was saved but its realtime notification failed.", orderId);
            return false;
        }
    }

    private static string CalculateRequestHash(WebOrderRequestDto request)
    {
        var content = new StringBuilder()
            .Append(request.TableId)
            .Append('\n');

        foreach (var item in request.Items!)
        {
            var encodedNote = Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Note?.Trim() ?? string.Empty));
            content.Append(item.MenuItemId)
                .Append(':')
                .Append(item.Quantity)
                .Append(':')
                .Append(encodedNote)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString())));
    }

    private static object CreateOrderResponse(Order order, bool realtimeNotificationSent) => new
    {
        orderId = order.OrderId,
        totalAmount = order.TotalAmount,
        status = order.Status.ToString(),
        realtimeDispatchSucceeded = realtimeNotificationSent,
        message = realtimeNotificationSent
            ? "Đơn hàng đã được tạo."
            : "Đơn hàng đã được tạo; thông báo trực tiếp chưa gửi được. Vui lòng tải lại danh sách đơn."
    };
}

public class WebOrderRequestDto
{
    public int? TableId { get; set; }
    public Guid? ClientRequestId { get; set; }
    public List<WebOrderItemDto>? Items { get; set; }
}

public class WebOrderItemDto
{
    public int? MenuItemId { get; set; }
    public int? ComboId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}
