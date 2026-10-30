using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Middleware;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record CustomerOrderItemRequest(int MenuItemId, int Quantity, string? Note);
public record CustomerOrderRequest(int TableId, Guid? ClientRequestId, List<CustomerOrderItemRequest>? Items);

// Endpoint cũ được giữ để tương thích/debug. Luồng Web hiện tại dùng
// WebOrdersController tại /api/orders/customer-order; tách route để tránh
// hai action cùng bắt một request và gây AmbiguousMatchException.
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/orders/customer-order-legacy")]
[Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
[CustomerAccess]
public class CustomerOrdersController : ControllerBase
{
    private const int MaxLines = 30;
    private const int MaxQuantity = 99;
    private const int MaxNoteLength = 200;
    private const int MaxOpenWebOrders = 3;

    private readonly AppDbContext _db;
    private readonly OrderRoutingService _routing;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    private readonly ILogger<CustomerOrdersController> _logger;

    public CustomerOrdersController(
        AppDbContext db,
        OrderRoutingService routing,
        IHubContext<OrderHub> hub,
        NotificationService notifications,
        ILogger<CustomerOrdersController> logger)
    {
        _db = db;
        _routing = routing;
        _hub = hub;
        _notifications = notifications;
        _logger = logger;
    }

    private sealed record Line(int MenuItemId, int Quantity, string? Note);

    // GET api/orders/customer-order/tables -> bàn ăn tại chỗ + trạng thái (web chỉ cho chọn bàn Trống)
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _db.Tables.AsNoTracking()
            .Include(t => t.Area)
            .Where(t => !t.TableName.StartsWith("MV-") &&
                        (t.Area == null || t.Area.AreaName != "Mang về"))
            .OrderBy(t => t.AreaId).ThenBy(t => t.TableId)
            .ToListAsync();

        return Ok(tables.Select(t => new
        {
            Id = t.TableId,
            t.TableId,
            t.TableName,
            t.Capacity,
            t.AreaId,
            AreaName = t.Area?.AreaName ?? "",
            TableType = t.TableType.ToString(),
            Status = t.Status.ToString()
        }));
    }

    // POST api/orders/customer-order  { tableId, clientRequestId, items: [{ menuItemId, quantity, note }] }
    [HttpPost]
    [EnableRateLimiting("web-booking")]
    public async Task<IActionResult> Create([FromBody] CustomerOrderRequest request)
    {
        var customerId = CustomerTokens.GetCustomerId(User);
        if (customerId is null) return Unauthorized();

        var key = request.ClientRequestId.GetValueOrDefault();
        if (key == Guid.Empty)
            return Error(400, "INVALID_REQUEST_ID", "Mã yêu cầu không hợp lệ.");
        if (request.TableId <= 0)
            return Error(400, "INVALID_TABLE", "Vui lòng chọn bàn.");

        var lines = NormalizeLines(request.Items);
        if (lines is null)
            return Error(400, "INVALID_ITEMS",
                $"Danh sách món không hợp lệ (tối đa {MaxLines} món, số lượng 1–{MaxQuantity}, ghi chú tối đa {MaxNoteLength} ký tự).");

        var accountActive = await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.CustomerAccounts
            WHERE CustomerId = {customerId.Value} AND IsActive = 1
            """).SingleAsync();
        if (accountActive == 0)
            return Error(403, "ACCOUNT_DISABLED", "Tài khoản không còn hiệu lực.");

        // Idempotent: cùng khóa + cùng nội dung -> trả lại đơn cũ, không tạo đơn mới.
        var existing = await LoadOrderAsync(key);
        if (existing is not null)
            return ReplayOrConflict(existing, customerId.Value, request.TableId, lines);

        var menuIds = lines.Select(l => l.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems.AsNoTracking()
            .Where(m => menuIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId);

        foreach (var l in lines)
        {
            if (!menuItems.TryGetValue(l.MenuItemId, out var m))
                return Error(404, "MENU_ITEM_NOT_FOUND", $"Món #{l.MenuItemId} không tồn tại.");
            if (!m.IsAvailable)
                return Error(409, "MENU_ITEM_UNAVAILABLE", $"Món {m.ItemName} hiện đã hết.");
            if (m.Price <= 0)
                return Error(409, "MENU_ITEM_PRICE_INVALID", $"Giá món {m.ItemName} chưa được cấu hình hợp lệ.");
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId.Value);
        if (customer is null) return Unauthorized();

        var webEmployee = await _db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeCode == OrderSources.WebEmployeeCode);
        if (webEmployee is null)
            return Error(500, "WEB_EMPLOYEE_MISSING", "Hệ thống chưa sẵn sàng nhận đơn từ web.");

        var openWebOrders = await _db.Orders.CountAsync(o =>
            o.CustomerId == customerId.Value &&
            o.Source == OrderSources.Web &&
            o.Status == OrderStatus.DangMo);
        if (openWebOrders >= MaxOpenWebOrders)
            return Error(409, "TOO_MANY_OPEN_ORDERS",
                $"Bạn đang có {openWebOrders} đơn chưa thanh toán. Vui lòng thanh toán hoặc liên hệ nhân viên trước khi gọi thêm.");

        int orderId;
        string tableName;

        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            try
            {
                var table = await _db.Tables.Include(t => t.Area)
                    .FirstOrDefaultAsync(t => t.TableId == request.TableId);
                if (table is null)
                    return Error(404, "TABLE_NOT_FOUND", "Không tìm thấy bàn.");
                if (table.TableName.StartsWith("MV-") || table.Area?.AreaName == "Mang về")
                    return Error(400, "TABLE_NOT_DINE_IN", "Bàn này không nhận đặt món online.");
                if (table.Status != TableStatus.Trong ||
                    await _db.Orders.AnyAsync(o => o.TableId == table.TableId && o.Status == OrderStatus.DangMo))
                    return Error(409, "TABLE_UNAVAILABLE", "Bàn này không còn trống. Vui lòng chọn bàn khác.");

                var now = DateTime.Now;
                var order = new Order
                {
                    TableId = table.TableId,
                    OrderType = OrderTypes.TaiBan,
                    EmployeeId = webEmployee.EmployeeId,
                    CustomerId = customerId.Value,
                    OrderTime = now,
                    Status = OrderStatus.DangMo,
                    Source = OrderSources.Web,
                    ClientRequestId = key,
                    TotalAmount = lines.Sum(l => l.Quantity * menuItems[l.MenuItemId].Price)
                };

                foreach (var l in lines)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        MenuItemId = l.MenuItemId,
                        Quantity = l.Quantity,
                        UnitPrice = menuItems[l.MenuItemId].Price,   // giá lấy từ DB, không tin client
                        Note = l.Note,
                        Status = KitchenStatus.MoiGoi,
                        IsRefillRequest = false,
                        IsTakeaway = false,
                        CreatedAt = now
                    });
                }

                _db.Orders.Add(order);
                table.Status = TableStatus.DangPhucVu;
                await _db.SaveChangesAsync();

                foreach (var oi in order.OrderItems)
                {
                    _db.DishStatusHistories.Add(new DishStatusHistory
                    {
                        OrderItemId = oi.OrderItemId,
                        Status = KitchenStatus.MoiGoi,
                        ChangedAt = now,
                        ChangedBy = webEmployee.UserId
                    });
                }
                await _db.SaveChangesAsync();

                // OrderTables là bảng liên kết dùng cho chuyển/gộp/tách bàn (giống luồng add-items).
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    IF OBJECT_ID(N'OrderTables', N'U') IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM OrderTables
                                       WHERE OrderId = {order.OrderId} AND TableId = {table.TableId})
                        INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable)
                        VALUES ({order.OrderId}, {table.TableId}, 1);
                    """);

                await tx.CommitAsync();

                orderId = order.OrderId;
                tableName = table.TableName;
            }
            catch (DbUpdateException)
            {
                // Hai yêu cầu cùng khóa chạy song song: chỉ một cái được ghi (unique index).
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                var raced = await LoadOrderAsync(key);
                if (raced is not null)
                    return ReplayOrConflict(raced, customerId.Value, request.TableId, lines);
                throw;
            }
        }

        var created = await LoadOrderAsync(key);
        if (created is null)
            return Error(500, "ORDER_NOT_FOUND", "Đơn đã được lưu nhưng chưa đọc lại được. Vui lòng kiểm tra trong lịch sử đơn.");

        // Đơn đã commit: lỗi ở các bước dưới chỉ ghi log, không làm hỏng đơn.
        try
        {
            await _routing.RouteNewOrderItemsAsync(orderId, created.OrderItems.Select(i => i.OrderItemId).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chuyển phiếu bếp cho đơn web #{OrderId} thất bại", orderId);
        }

        try
        {
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = request.TableId,
                TableName = tableName,
                Status = TableStatus.DangPhucVu
            });

            await _notifications.CreateForRolesAsync(
                "Đơn đặt món mới từ web",
                $"{customer.FullName} gọi món tại {tableName} ({created.OrderItems.Count} món, đơn #{orderId})",
                NotificationType.DonHang,
                new[] { "ThuNgan", "PhucVu" },
                relatedEntityType: "Order",
                relatedEntityId: orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi thông báo đơn web #{OrderId} thất bại", orderId);
        }

        return StatusCode(StatusCodes.Status201Created, ToDto(created));
    }

    // GET api/orders/customer-order/{clientRequestId} -> tra lại đơn theo mã chống trùng (của chính khách)
    [HttpGet("{clientRequestId:guid}")]
    public async Task<IActionResult> GetByRequestId(Guid clientRequestId)
    {
        var customerId = CustomerTokens.GetCustomerId(User);
        if (customerId is null) return Unauthorized();

        if (clientRequestId == Guid.Empty)
            return Error(404, "ORDER_NOT_FOUND", "Không tìm thấy đơn hàng.");

        var order = await LoadOrderAsync(clientRequestId);

        if (order is null || order.CustomerId != customerId.Value)
            return Error(404, "ORDER_NOT_FOUND", "Không tìm thấy đơn hàng.");

        return Ok(ToDto(order));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private ObjectResult Error(int status, string code, string message)
        => StatusCode(status, new { code, message });

    private Task<Order?> LoadOrderAsync(Guid key)
        => _db.Orders.AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .FirstOrDefaultAsync(o => o.ClientRequestId == key);

    // Cùng khóa: đúng chủ + cùng bàn + cùng món -> trả lại đơn cũ; khác -> 409 IDEMPOTENCY_KEY_REUSED.
    private IActionResult ReplayOrConflict(Order existing, int customerId, int tableId, List<Line> lines)
    {
        var sameRequest =
            existing.CustomerId == customerId &&
            existing.TableId == tableId &&
            Fingerprint(existing.OrderItems.Select(i => (i.MenuItemId ?? 0, i.Quantity, i.Note))) ==
            Fingerprint(lines.Select(l => (l.MenuItemId, l.Quantity, l.Note)));

        return sameRequest
            ? Ok(ToDto(existing))
            : Error(409, "IDEMPOTENCY_KEY_REUSED", "Mã yêu cầu này đã được dùng cho một nội dung khác.");
    }

    private static string Fingerprint(IEnumerable<(int MenuItemId, int Quantity, string? Note)> lines)
        => string.Join("|", lines
            .Select(l => $"{l.MenuItemId}x{l.Quantity}:{l.Note}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private static List<Line>? NormalizeLines(List<CustomerOrderItemRequest>? items)
    {
        if (items is null || items.Count == 0 || items.Count > MaxLines) return null;

        var result = new List<Line>();
        foreach (var i in items)
        {
            if (i.MenuItemId <= 0 || i.Quantity < 1 || i.Quantity > MaxQuantity) return null;
            var note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim();
            if (note is { Length: > MaxNoteLength }) return null;
            result.Add(new Line(i.MenuItemId, i.Quantity, note));
        }
        return result;
    }

    private static object ToDto(Order o) => new
    {
        orderId = o.OrderId,
        clientRequestId = o.ClientRequestId,
        status = o.Status.ToString(),
        totalAmount = o.TotalAmount,
        tableId = o.TableId,
        tableName = o.Table?.TableName ?? $"Bàn #{o.TableId}",
        orderTime = o.OrderTime,
        source = o.Source,
        items = o.OrderItems
            .OrderBy(i => i.OrderItemId)
            .Select(i => new
            {
                menuItemId = i.MenuItemId,
                menuItemName = i.MenuItem?.ItemName ?? "Món ăn",
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                note = i.Note,
                status = i.Status.ToString()
            })
    };
}