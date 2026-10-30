using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using QuanAnBackend.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace QuanAnBackend.Controllers;

public record CreateOrderItemDto(int MenuItemId, int Quantity, string? Note, bool IsTakeaway = false);
public record AddItemsRequest(
    int? TableId,
    int? UserId = null,
    string? EmployeeCode = null,
    List<CreateOrderItemDto>? Items = null,
    bool IsTakeaway = false
);
public record MobileUpdateOrderItemStatusRequest(string Status);
public record AddedOrderItemDto(
    int Id,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? Note,
    bool IsTakeaway = false
);
public record CheckInRequest(int TableId, int UserId, int? ReservationId, string? Note);
public record CancelOrderDto(int UserId, string? Reason);
public record SetOrderCustomerRequest(int? CustomerId);

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OrderRoutingService _routingService;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AppDbContext db,
        OrderRoutingService routingService,
        IHubContext<OrderHub> hub,
        NotificationService notifications,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _routingService = routingService;
        _hub = hub;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpPost("add-items")]
    [RequireMobilePermission("order.create")]
    public async Task<IActionResult> AddItems([FromBody] AddItemsRequest request)
    {
        var items = request.Items ?? new List<CreateOrderItemDto>();

        if (items.Count == 0)
            return BadRequest(new { message = "Danh sách món đang trống" });

        if (items.Any(x => x.MenuItemId <= 0 || x.Quantity <= 0))
            return BadRequest(new { message = "Món hoặc số lượng không hợp lệ" });

        if (request.IsTakeaway)
            return await AddTakeawayItemsAsync(request, items);

        var tableId = request.TableId.GetValueOrDefault();
        if (tableId <= 0)
            return BadRequest(new { message = "Bàn không hợp lệ" });

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            int? employeeId = null;
            int? userId = null;

            if (!string.IsNullOrWhiteSpace(request.EmployeeCode))
            {
                await using var employeeCommand = connection.CreateCommand();
                employeeCommand.Transaction = transaction;
                employeeCommand.CommandText = """
                    SELECT TOP 1 EmployeeId, UserId
                    FROM Employees
                    WHERE EmployeeCode = @EmployeeCode;
                    """;
                AddDbParameter(employeeCommand, "@EmployeeCode", request.EmployeeCode.Trim());

                await using var reader = await employeeCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    employeeId = reader.GetInt32(0);
                    userId = reader.GetInt32(1);
                }
            }

            if (!employeeId.HasValue && request.UserId.HasValue)
            {
                await using var employeeCommand = connection.CreateCommand();
                employeeCommand.Transaction = transaction;
                employeeCommand.CommandText = """
                    SELECT TOP 1 EmployeeId, UserId
                    FROM Employees
                    WHERE UserId = @UserId;
                    """;
                AddDbParameter(employeeCommand, "@UserId", request.UserId.Value);

                await using var reader = await employeeCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    employeeId = reader.GetInt32(0);
                    userId = reader.GetInt32(1);
                }
            }

            if (!employeeId.HasValue || !userId.HasValue)
                return BadRequest(new { message = "Không tìm thấy hồ sơ nhân viên đăng nhập" });

            var tableExists = await ExecuteScalarIntDbAsync(
                connection,
                transaction,
                "SELECT COUNT(*) FROM DiningTables WHERE TableId = @TableId;",
                ("@TableId", tableId)
            );

            if (tableExists == 0)
                return NotFound(new { message = "Không tìm thấy bàn" });

            int? orderId = null;

            await using (var orderCommand = connection.CreateCommand())
            {
                orderCommand.Transaction = transaction;
                orderCommand.CommandText = """
                    SELECT TOP 1 o.OrderId
                    FROM Orders o
                    LEFT JOIN OrderTables ot ON ot.OrderId = o.OrderId
                    WHERE o.Status = N'DangMo'
                      AND (o.TableId = @TableId OR ot.TableId = @TableId)
                    ORDER BY o.OrderTime DESC, o.OrderId DESC;
                    """;
                AddDbParameter(orderCommand, "@TableId", tableId);

                var value = await orderCommand.ExecuteScalarAsync();
                if (value is not null && value != DBNull.Value)
                    orderId = Convert.ToInt32(value);
            }

            if (!orderId.HasValue)
            {
                await using var createOrder = connection.CreateCommand();
                createOrder.Transaction = transaction;
                createOrder.CommandText = """
                    SET NOCOUNT ON;

                    INSERT INTO Orders
                        (TableId, EmployeeId, OrderTime, Status, TotalAmount)
                    VALUES
                        (@TableId, @EmployeeId, GETDATE(), N'DangMo', 0);

                    SELECT CAST(SCOPE_IDENTITY() AS int);
                    """;
                AddDbParameter(createOrder, "@TableId", tableId);
                AddDbParameter(createOrder, "@EmployeeId", employeeId.Value);

                var value = await createOrder.ExecuteScalarAsync();
                orderId = Convert.ToInt32(value);

                // OrderTables la bang lien ket duoc dung cho chuyen/gop/tach ban.
                await ExecuteDbAsync(
                    connection,
                    transaction,
                    """
                    IF OBJECT_ID(N'OrderTables', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM OrderTables
                        WHERE OrderId = @OrderId
                          AND TableId = @TableId
                    )
                    BEGIN
                        INSERT INTO OrderTables
                            (OrderId, TableId, IsPrimaryTable)
                        VALUES
                            (@OrderId, @TableId, 1);
                    END
                    """,
                    ("@OrderId", orderId.Value),
                    ("@TableId", tableId)
                );
            }

            // Mỗi lần nhân viên bấm xác nhận gọi món là một đợt (batch).
            // Toàn bộ món trong cùng request dùng chung CreatedAt để Bếp/Quầy nước
            // gom đúng một phiếu. Gọi thêm lần sau sẽ có CreatedAt mới và xếp sau.
            var batchCreatedAt = DateTime.Now;

            var addedItems = new List<AddedOrderItemDto>();
            var realtimeTickets = new List<(string StationName, int Id, string Name, int Quantity, string? Note, bool IsTakeaway)>();

            foreach (var dto in items)
            {
                string? itemName = null;
                decimal price = 0;
                bool isAvailable = false;
                int? stationId = null;
                string? stationName = null;

                await using (var menuCommand = connection.CreateCommand())
                {
                    menuCommand.Transaction = transaction;
                    menuCommand.CommandText = """
                        SELECT TOP 1
                            mi.ItemName,
                            mi.Price,
                            mi.IsAvailable,
                            mi.StationId,
                            s.StationName
                        FROM MenuItems mi
                        LEFT JOIN Stations s
                            ON s.StationId = mi.StationId
                        WHERE mi.MenuItemId = @MenuItemId;
                        """;
                    AddDbParameter(menuCommand, "@MenuItemId", dto.MenuItemId);

                    await using var reader = await menuCommand.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        itemName = reader.IsDBNull(0) ? null : reader.GetString(0);
                        price = reader.GetDecimal(1);
                        isAvailable = reader.GetBoolean(2);
                        stationId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                        stationName = reader.IsDBNull(4) ? null : reader.GetString(4);
                    }
                }

                if (string.IsNullOrWhiteSpace(itemName))
                    return BadRequest(new { message = $"Món #{dto.MenuItemId} không tồn tại" });

                if (!isAvailable)
                    return Conflict(new { message = $"Món {itemName} hiện đã hết" });

                if (price <= 0)
                    return Conflict(new { message = $"Giá món {itemName} chưa được cấu hình hợp lệ" });

                int newOrderItemId;

                await using (var insertItem = connection.CreateCommand())
                {
                    insertItem.Transaction = transaction;
                    insertItem.CommandText = """
                        SET NOCOUNT ON;

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
                                IsTakeaway,
                                CreatedAt
                            )
                        VALUES
                            (
                                @OrderId,
                                @MenuItemId,
                                NULL,
                                @Quantity,
                                @UnitPrice,
                                @Note,
                                N'MoiGoi',
                                0,
                                @IsTakeaway,
                                @CreatedAt
                            );

                        SELECT CAST(SCOPE_IDENTITY() AS int);
                        """;

                    AddDbParameter(insertItem, "@OrderId", orderId.Value);
                    AddDbParameter(insertItem, "@MenuItemId", dto.MenuItemId);
                    AddDbParameter(insertItem, "@Quantity", dto.Quantity);
                    AddDbParameter(insertItem, "@UnitPrice", price);
                    AddDbParameter(insertItem, "@Note", dto.Note);
                    AddDbParameter(insertItem, "@IsTakeaway", dto.IsTakeaway);
                    AddDbParameter(insertItem, "@CreatedAt", batchCreatedAt);

                    var value = await insertItem.ExecuteScalarAsync();
                    newOrderItemId = Convert.ToInt32(value);
                }

                await ExecuteDbAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO DishStatusHistory
                        (OrderItemId, Status, ChangedAt, ChangedBy)
                    VALUES
                        (@OrderItemId, N'MoiGoi', GETDATE(), @UserId);
                    """,
                    ("@OrderItemId", newOrderItemId),
                    ("@UserId", userId.Value)
                );

                if (stationId.HasValue)
                {
                    await ExecuteDbAsync(
                        connection,
                        transaction,
                        """
                        INSERT INTO KitchenTickets
                            (OrderItemId, StationId, SentTime, Status)
                        VALUES
                            (@OrderItemId, @StationId, GETDATE(), N'ChoXuLy');
                        """,
                        ("@OrderItemId", newOrderItemId),
                        ("@StationId", stationId.Value)
                    );
                }

                addedItems.Add(
                    new AddedOrderItemDto(
                        newOrderItemId,
                        itemName,
                        dto.Quantity,
                        price,
                        "Chờ chế biến",
                        dto.Note,
                        dto.IsTakeaway
                    )
                );

                if (!string.IsNullOrWhiteSpace(stationName))
                {
                    realtimeTickets.Add(
                        (stationName!, newOrderItemId, itemName, dto.Quantity, dto.Note, dto.IsTakeaway)
                    );
                }
            }

            await ExecuteDbAsync(
                connection,
                transaction,
                """
                UPDATE DiningTables
                SET Status = N'DangPhucVu'
                WHERE TableId = @TableId;
                """,
                ("@TableId", tableId)
            );

            await transaction.CommitAsync();

            var tableName = await _db.Tables
                .AsNoTracking()
                .Where(t => t.TableId == tableId)
                .Select(t => t.TableName)
                .FirstOrDefaultAsync() ?? "";

            // Status gửi dạng số (enum) để WinForm deserialize được; Mobile parse cả số lẫn chuỗi.
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = tableId,
                TableName = tableName,
                Status = TableStatus.DangPhucVu
            });

            foreach (var stationGroup in realtimeTickets.GroupBy(x => x.StationName))
            {
                await _hub.Clients
                    .Group($"dept-{stationGroup.Key}")
                    .SendAsync("NewTicket", new
                    {
                        OrderId = orderId.Value,
                        TableId = tableId,
                        TableName = tableName,
                        Department = stationGroup.Key,
                        Items = stationGroup.Select(x => new
                        {
                            x.Id,
                            MenuItemName = x.Name,
                            x.Quantity,
                            x.Note,
                            x.IsTakeaway,
                            CreatedAt = DateTime.Now
                        }).ToList()
                    });
            }

            return Ok(new
            {
                orderId = orderId.Value,
                items = addedItems
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[Mobile AddItems] {ex}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Không thể lưu món vào đơn. Vui lòng thử lại sau.",
                    detail = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
                        ? ex.Message
                        : null
                }
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }


    // Latest WinForms contract: true takeaway order has no physical table and
    // receives a daily MV-xxx number. Existing Mobile takeaway-via-area remains
    // untouched because it continues to send IsTakeaway=false with a TableId.
    private async Task<IActionResult> AddTakeawayItemsAsync(
        AddItemsRequest request,
        List<CreateOrderItemDto> items)
    {
        Employee? employee = null;
        if (!string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeCode == request.EmployeeCode.Trim());
        }

        var actorUserId = request.UserId ?? CurrentUserId();
        if (employee is null && actorUserId.HasValue)
        {
            employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == actorUserId.Value);
        }

        if (employee is null)
            return BadRequest(new { message = "Không tìm thấy hồ sơ nhân viên đăng nhập" });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var lastNumber = await _db.Orders
                .Where(o => o.TakeawayNumber != null && o.OrderTime >= today && o.OrderTime < tomorrow)
                .MaxAsync(o => o.TakeawayNumber) ?? 0;

            var order = new Order
            {
                TableId = null,
                OrderType = OrderTypes.MangVe,
                TakeawayNumber = lastNumber + 1,
                EmployeeId = employee.EmployeeId,
                OrderTime = DateTime.Now,
                Status = OrderStatus.DangMo,
                TotalAmount = 0
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var newItemIds = new List<int>();
            var addedItems = new List<AddedOrderItemDto>();
            var batchCreatedAt = DateTime.Now;

            foreach (var dto in items)
            {
                var menuItem = await _db.MenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MenuItemId == dto.MenuItemId);

                if (menuItem is null)
                    return BadRequest(new { message = $"Món #{dto.MenuItemId} không tồn tại" });
                if (!menuItem.IsAvailable)
                    return Conflict(new { message = $"Món {menuItem.ItemName} hiện đã hết" });
                if (menuItem.Price <= 0)
                    return Conflict(new { message = $"Giá món {menuItem.ItemName} chưa được cấu hình hợp lệ" });

                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    MenuItemId = menuItem.MenuItemId,
                    Quantity = dto.Quantity,
                    UnitPrice = menuItem.Price,
                    Note = dto.Note,
                    Status = KitchenStatus.MoiGoi,
                    IsRefillRequest = false,
                    IsTakeaway = true,
                    CreatedAt = batchCreatedAt
                };
                _db.OrderItems.Add(orderItem);
                await _db.SaveChangesAsync();

                _db.DishStatusHistories.Add(new DishStatusHistory
                {
                    OrderItemId = orderItem.OrderItemId,
                    Status = KitchenStatus.MoiGoi,
                    ChangedAt = DateTime.Now,
                    ChangedBy = actorUserId ?? employee.UserId
                });

                newItemIds.Add(orderItem.OrderItemId);
                addedItems.Add(new AddedOrderItemDto(
                    orderItem.OrderItemId,
                    menuItem.ItemName,
                    dto.Quantity,
                    menuItem.Price,
                    "Chờ chế biến",
                    dto.Note,
                    true));
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

                        // Đơn đã commit: lỗi chuyển phiếu sang bếp không được làm hỏng đơn.
            if (newItemIds.Count > 0)
            {
                try
                {
                    await _routingService.RouteNewOrderItemsAsync(order.OrderId, newItemIds);
                }
                catch (Exception routeEx)
                {
                    Console.WriteLine($"[Takeaway Routing] {routeEx}");
                }
            }

            return Ok(new
            {
                orderId = order.OrderId,
                order.TakeawayNumber,
                order.TakeawayCode,
                items = addedItems
            });
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { /* transaction đã commit/đóng */ }
            Console.WriteLine($"[Takeaway AddItems] {ex}");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Không thể tạo đơn mang về. Vui lòng thử lại sau." });
        }
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == request.UserId);
        if (employee is null)
            return BadRequest(new { message = "Tài khoản này chưa được gắn với hồ sơ nhân viên (Employees)" });

        var table = await _db.Tables.FindAsync(request.TableId);
        if (table is null) return NotFound(new { message = "Bàn không tồn tại" });

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.TableId == request.TableId && o.Status == OrderStatus.DangMo);

        if (order is null)
        {
            order = new Order { TableId = request.TableId, EmployeeId = employee.EmployeeId, Note = request.Note };
            _db.Orders.Add(order);
        }

        table.Status = TableStatus.DangPhucVu;

        if (request.ReservationId.HasValue)
        {
            var reservation = await _db.Reservations.FindAsync(request.ReservationId.Value);
            if (reservation is not null)
            {
                reservation.Status = ReservationStatus.DaNhanBan;

                // Gắn khách thành viên của lượt đặt bàn vào đơn ngay khi nhận bàn
                // để màn Thanh toán tự lấy lại SĐT/email, không phải nhập tay.
                if (order.CustomerId is null)
                {
                    var reservedCustomerId = await ResolveReservationCustomerIdAsync(reservation);
                    if (reservedCustomerId.HasValue) order.CustomerId = reservedCustomerId;
                }
            }
        }

        await _db.SaveChangesAsync();

        if (request.ReservationId.HasValue)
        {
            var checkedInReservation = await _db.Reservations.FindAsync(request.ReservationId.Value);
            if (checkedInReservation?.CustomerId is int reservationCustomerId)
            {
                await _notifications.CreateForCustomerAsync(
                    reservationCustomerId,
                    "Đã nhận bàn",
                    "Nhà hàng đã ghi nhận bạn đến nhận bàn.",
                    NotificationType.DatBan,
                    $"reservation-status:{checkedInReservation.ReservationId}:{(int)ReservationStatus.DaXacNhan}:{(int)ReservationStatus.DaNhanBan}",
                    "Reservation",
                    checkedInReservation.ReservationId);
            }
        }

        await _hub.Clients.All.SendAsync("TableStatusUpdated", new
        {
            TableId = table.TableId,
            table.TableName,
            Status = table.Status
        });

        return Ok(new { OrderId = order.OrderId, TableId = table.TableId, Status = table.Status });
    }

    // PUT /api/orders/{id}/customer -> gắn (hoặc bỏ gắn nếu CustomerId = null) 1
    // khách hàng thành viên vào đơn đang mở. Dùng khi Thu ngân/nhân viên order
    // muốn gắn khách NGAY khi order thay vì đợi tới lúc thanh toán mới chọn.
    [HttpPut("{id}/customer")]
    public async Task<IActionResult> SetCustomer(int id, [FromBody] SetOrderCustomerRequest request)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
        if (order is null) return NotFound();
        if (order.Status != OrderStatus.DangMo)
            return BadRequest(new { message = "Chỉ có thể gắn khách hàng cho đơn đang mở" });

        if (request.CustomerId.HasValue)
        {
            var exists = await _db.Customers.AnyAsync(c => c.CustomerId == request.CustomerId.Value);
            if (!exists) return BadRequest(new { message = "Khách hàng không tồn tại" });
        }

        order.CustomerId = request.CustomerId;
        await _db.SaveChangesAsync();

        return Ok(new { order.OrderId, order.CustomerId });
    }

    [HttpGet]
    [Authorize(Roles = "QuanLy,ThuNgan")]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] OrderStatus? status)
    {
        var query = _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Employee)!.ThenInclude(e => e!.User)
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .AsQueryable();

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(o => o.OrderTime >= day && o.OrderTime < day.AddDays(1));
        }
        else
        {
            query = query.Where(o => o.OrderTime >= DateTime.Today);
        }

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        var rows = await query
            .OrderByDescending(o => o.OrderTime)
            .Select(o => new
            {
                Id = o.OrderId,
                o.TableId,
                o.OrderType,
                o.TakeawayNumber,
                o.Source,
                BaseTableName = o.Table != null ? o.Table.TableName : "",
                EmployeeName = o.Employee != null && o.Employee.User != null ? o.Employee.User.FullName : "",
                o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.FullName : null,
                o.OrderTime,
                o.TotalAmount,
                Status = o.Status,
                ItemCount = o.OrderItems.Count(i => i.Status != KitchenStatus.DaHuy)
            })
            .ToListAsync();

        var orders = rows.Select(r => new
        {
            r.Id,
            r.TableId,
            r.OrderType,
            r.TakeawayNumber,
            r.Source,
            TakeawayCode = r.TakeawayNumber.HasValue ? $"MV-{r.TakeawayNumber.Value:000}" : null,
            TableName = r.TakeawayNumber.HasValue ? $"Mang về MV-{r.TakeawayNumber.Value:000}" : r.BaseTableName,
            r.EmployeeName,
            r.CustomerId,
            r.CustomerName,
            r.OrderTime,
            r.TotalAmount,
            r.Status,
            r.ItemCount
        }).ToList();

        return Ok(orders);
    }

    [HttpPut("{id}/cancel")]
    [Authorize(Roles = "QuanLy,ThuNgan")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderDto? request)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (order.Status != OrderStatus.DangMo)
            return BadRequest(new { message = "Chỉ có thể hủy đơn hàng đang ở trạng thái Đang mở." });

                bool hasCookingOrCookedItems = order.OrderItems.Any(i => i.Status == KitchenStatus.DangCheBien || i.Status == KitchenStatus.DaCheBien);
        if (hasCookingOrCookedItems && !User.IsInRole("QuanLy"))
        {
            // Đơn đã có món chế biến: chỉ Quản lý được hủy cả đơn.
            return BadRequest(new { message = "Đơn hàng đã có món đang chế biến hoặc hoàn thành. Chỉ Quản lý mới được hủy cả đơn!" });
        }

        var cancelReason = string.IsNullOrWhiteSpace(request?.Reason) && hasCookingOrCookedItems
            ? "Quản lý hủy đơn đã chế biến"
            : request?.Reason;

        foreach (var item in order.OrderItems.Where(i => i.Status != KitchenStatus.DaHuy))
        {
            item.Status = KitchenStatus.DaHuy;
            item.CancelReason = cancelReason;
            item.CancelledByUserId = request?.UserId;

            _db.DishStatusHistories.Add(new DishStatusHistory
            {
                OrderItemId = item.OrderItemId,
                Status = KitchenStatus.DaHuy,
                ChangedAt = DateTime.Now,
                ChangedBy = request?.UserId
            });
        }

        var finishedReservations = new List<Reservation>();
        if (order.Table is not null && order.Table.Status == TableStatus.DangPhucVu)
        {
            order.Table.Status = TableStatus.Trong;

            // FIX: giong PaymentsController.Create - huy don giua chung cung phai
            // dong luot dat ban tuong ung, khong de no ket dinh o DaNhanBan mai.
            finishedReservations = await _db.Reservations
                .Where(r => r.TableId == order.Table.TableId && r.Status == ReservationStatus.DaNhanBan)
                .ToListAsync();
            foreach (var r in finishedReservations) r.Status = ReservationStatus.HoanTat;
        }

                // Lưu các món trước (trigger SQL ghi lại dòng Orders làm RowVer đổi), rồi nạp lại
        // RowVer mới của đơn trước khi đổi trạng thái đơn, tránh lỗi concurrency.
        await using var cancelTx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _db.Entry(order).ReloadAsync();
        order.Status = OrderStatus.DaHuy;
        await _db.SaveChangesAsync();
        await cancelTx.CommitAsync();

        if (order.Table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = order.Table.TableId,
                order.Table.TableName,
                Status = order.Table.Status
            });
        }

        await _notifications.CreateForRolesAsync(
            "Đơn hàng bị hủy",
            $"Đơn #{order.OrderId} ({order.Table?.TableName ?? order.TakeawayCode ?? "Mang về"}) đã bị hủy" + (string.IsNullOrWhiteSpace(cancelReason) ? "" : $" - Lý do: {cancelReason}"),
            NotificationType.DonHang,
            new[] { "QuanLy", "Bep" },
            relatedEntityType: "Order",
            relatedEntityId: order.OrderId);

        if (order.CustomerId.HasValue)
        {
            await _notifications.CreateForCustomerAsync(
                order.CustomerId.Value,
                "Đơn hàng đã bị hủy",
                $"Đơn hàng #{order.OrderId} đã được nhà hàng hủy.",
                NotificationType.DonHang,
                $"order-cancelled:{order.OrderId}",
                "Order",
                order.OrderId);
        }
        foreach (var reservation in finishedReservations.Where(reservation => reservation.CustomerId.HasValue))
        {
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId!.Value,
                "Đặt bàn đã hoàn tất",
                "Lượt đặt bàn của bạn đã hoàn tất.",
                NotificationType.DatBan,
                $"reservation-status:{reservation.ReservationId}:{(int)ReservationStatus.DaNhanBan}:{(int)ReservationStatus.HoanTat}",
                "Reservation",
                reservation.ReservationId);
        }

        try
        {
            await _hub.Clients.Group("order-management").SendAsync("OrderUpdated", new
            {
                orderId = order.OrderId
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to broadcast order cancellation to order-management group for order {OrderId}", order.OrderId);
        }

        return Ok(new { Id = order.OrderId, Status = order.Status });
    }

    // Mobile compatibility: endpoint cũ dùng chuỗi trạng thái tiếng Việt.
    [HttpPut("items/{id}/status")]
    public async Task<IActionResult> UpdateMobileItemStatus(
        int id,
        [FromBody] MobileUpdateOrderItemStatusRequest request)
    {
        var target = NormalizeMobileStatus(request.Status);
        if (!target.HasValue || target.Value == KitchenStatus.DaHuy)
            return BadRequest(new { message = "Trạng thái món không hợp lệ" });

        var isManager = User.IsInRole("QuanLy");
        var isKitchen = User.IsInRole("Bep");
        var isServer = User.IsInRole("PhucVu");

        if ((target.Value == KitchenStatus.DangCheBien ||
             target.Value == KitchenStatus.DaCheBien) &&
            !isKitchen && !isManager)
        {
            return Forbid();
        }

        if (target.Value == KitchenStatus.DaPhucVu &&
            !isServer && !isManager)
        {
            return Forbid();
        }

        var targetDb = target.Value.ToString();
        var targetDisplay = DisplayMobileStatus(target.Value);

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            string? currentStatus = null;
            int tableId = 0;
            string tableName = "";
            string itemName = "Món";
            string? employeeCode = null;
            int? employeeUserId = null;

            await using (var readCommand = connection.CreateCommand())
            {
                readCommand.Transaction = transaction;
                readCommand.CommandText = """
                    SELECT TOP 1
                        oi.Status,
                        o.TableId,
                        COALESCE(
                            t.TableName,
                            CASE WHEN o.TakeawayNumber IS NOT NULL
                                 THEN N'Mang về MV-' + RIGHT(N'000' + CONVERT(NVARCHAR(10), o.TakeawayNumber), 3)
                                 ELSE N'' END
                        ),
                        COALESCE(mi.ItemName, cb.ComboName, N'Món'),
                        e.EmployeeCode,
                        e.UserId
                    FROM OrderItems oi
                    INNER JOIN Orders o ON o.OrderId = oi.OrderId
                    LEFT JOIN DiningTables t ON t.TableId = o.TableId
                    LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                    LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
                    LEFT JOIN Employees e ON e.EmployeeId = o.EmployeeId
                    WHERE oi.OrderItemId = @OrderItemId;
                    """;
                AddDbParameter(readCommand, "@OrderItemId", id);

                await using var reader = await readCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Không tìm thấy món" });

                currentStatus = reader.GetString(0);
                tableId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                tableName = reader.GetString(2);
                itemName = reader.GetString(3);
                employeeCode = reader.IsDBNull(4) ? null : reader.GetString(4);
                employeeUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            }

            if (!IsValidMobileTransitionDb(currentStatus!, targetDb))
            {
                return Conflict(new
                {
                    message = $"Không thể chuyển món từ {DisplayMobileStatusDb(currentStatus!)} sang {targetDisplay}"
                });
            }

            await ExecuteDbAsync(
                connection,
                transaction,
                """
                UPDATE OrderItems
                SET Status = @Status
                WHERE OrderItemId = @OrderItemId;
                """,
                ("@Status", targetDb),
                ("@OrderItemId", id)
            );

            if (target.Value == KitchenStatus.DangCheBien)
            {
                await ExecuteDbAsync(
                    connection,
                    transaction,
                    """
                    UPDATE KitchenTickets
                    SET Status = N'DangCheBien'
                    WHERE OrderItemId = @OrderItemId;
                    """,
                    ("@OrderItemId", id)
                );
            }
            else if (target.Value == KitchenStatus.DaCheBien)
            {
                await ExecuteDbAsync(
                    connection,
                    transaction,
                    """
                    UPDATE KitchenTickets
                    SET Status = N'HoanThanh',
                        CompletedTime = GETDATE()
                    WHERE OrderItemId = @OrderItemId;
                    """,
                    ("@OrderItemId", id)
                );
            }

            await ExecuteDbAsync(
                connection,
                transaction,
                """
                INSERT INTO DishStatusHistory
                    (OrderItemId, Status, ChangedAt, ChangedBy)
                VALUES
                    (@OrderItemId, @Status, GETDATE(), @ChangedBy);
                """,
                ("@OrderItemId", id),
                ("@Status", targetDb),
                ("@ChangedBy", CurrentUserId() ?? employeeUserId)
            );

            await transaction.CommitAsync();

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                await _hub.Clients
                    .Group($"employee-{employeeCode}")
                    .SendAsync("DishStatusChanged", new
                    {
                        orderItemId = id,
                        tableId,
                        tableName,
                        itemName,
                        status = targetDisplay
                    });
            }

            if (target.Value == KitchenStatus.DaCheBien && employeeUserId.HasValue)
            {
                await _notifications.CreateAsync(
                    "Món đã xong",
                    $"{tableName} - {itemName} đã chế biến xong.",
                    NotificationType.DonHang,
                    targetUserId: employeeUserId.Value,
                    relatedEntityType: "OrderItem",
                    relatedEntityId: id
                );
            }

            return Ok(new { id, status = targetDisplay });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[Mobile UpdateItemStatus] {ex}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Không thể cập nhật trạng thái món. Vui lòng thử lại." }
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [HttpPut("items/{id}/cancel")]
    public async Task<IActionResult> CancelMobileItem(int id)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            string? currentStatus = null;
            int tableId = 0;
            string tableName = "";
            string itemName = "Món";
            string? employeeCode = null;
            int? employeeUserId = null;

            await using (var readCommand = connection.CreateCommand())
            {
                readCommand.Transaction = transaction;
                readCommand.CommandText = """
                    SELECT TOP 1
                        oi.Status,
                        o.TableId,
                        COALESCE(
                            t.TableName,
                            CASE WHEN o.TakeawayNumber IS NOT NULL
                                 THEN N'Mang về MV-' + RIGHT(N'000' + CONVERT(NVARCHAR(10), o.TakeawayNumber), 3)
                                 ELSE N'' END
                        ),
                        COALESCE(mi.ItemName, cb.ComboName, N'Món'),
                        e.EmployeeCode,
                        e.UserId
                    FROM OrderItems oi
                    INNER JOIN Orders o ON o.OrderId = oi.OrderId
                    LEFT JOIN DiningTables t ON t.TableId = o.TableId
                    LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                    LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
                    LEFT JOIN Employees e ON e.EmployeeId = o.EmployeeId
                    WHERE oi.OrderItemId = @OrderItemId;
                    """;
                AddDbParameter(readCommand, "@OrderItemId", id);

                await using var reader = await readCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Không tìm thấy món" });

                currentStatus = reader.GetString(0);
                tableId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                tableName = reader.GetString(2);
                itemName = reader.GetString(3);
                employeeCode = reader.IsDBNull(4) ? null : reader.GetString(4);
                employeeUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            }

            if (!string.Equals(currentStatus, "MoiGoi", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    message = "Chỉ có thể hủy món khi bếp chưa bắt đầu chế biến"
                });
            }

            await ExecuteDbAsync(
                connection,
                transaction,
                """
                UPDATE OrderItems
                SET Status = N'DaHuy'
                WHERE OrderItemId = @OrderItemId
                  AND Status = N'MoiGoi';

                UPDATE KitchenTickets
                SET Status = N'HoanThanh',
                    CompletedTime = COALESCE(CompletedTime, GETDATE())
                WHERE OrderItemId = @OrderItemId;
                """,
                ("@OrderItemId", id)
            );

            await ExecuteDbAsync(
                connection,
                transaction,
                """
                INSERT INTO DishStatusHistory
                    (OrderItemId, Status, ChangedAt, ChangedBy)
                VALUES
                    (@OrderItemId, N'DaHuy', GETDATE(), @ChangedBy);
                """,
                ("@OrderItemId", id),
                ("@ChangedBy", CurrentUserId() ?? employeeUserId)
            );

            await transaction.CommitAsync();

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                await _hub.Clients
                    .Group($"employee-{employeeCode}")
                    .SendAsync("DishStatusChanged", new
                    {
                        orderItemId = id,
                        tableId,
                        tableName,
                        itemName,
                        status = "Đã hủy"
                    });
            }

            return Ok(new { id, status = "Đã hủy" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[Mobile CancelItem] {ex}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Không thể hủy món. Vui lòng thử lại." }
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // Xác định khách thành viên của 1 lượt đặt bàn:
    //  1) Reservation.CustomerId (đặt từ web khi đã đăng nhập), nếu không có thì
    //  2) khách có SĐT trùng Reservation.CustomerPhone (đặt hộ tại quầy, hoặc khách
    //     đăng ký thành viên SAU khi đặt bàn).
    private async Task<int?> ResolveReservationCustomerIdAsync(Reservation reservation)
    {
        if (reservation.CustomerId.HasValue)
        {
            var exists = await _db.Customers.AsNoTracking()
                .AnyAsync(c => c.CustomerId == reservation.CustomerId.Value);
            if (exists) return reservation.CustomerId;
        }

        var phone = reservation.CustomerPhone?.Trim();
        if (string.IsNullOrEmpty(phone)) return null;

        return await _db.Customers.AsNoTracking()
            .Where(c => c.Phone == phone)
            .Select(c => (int?)c.CustomerId)
            .FirstOrDefaultAsync();
    }

    // GET api/orders/table/{tableId} -> đơn đang mở của bàn, dùng bởi
    // WinForms khi mở màn thanh toán sau khi món được gọi từ Mobile.
    // Có trả thêm "Customer" (khách thành viên gắn với đơn / lượt đặt bàn) để
    // màn Thanh toán tự điền SĐT và email gửi hóa đơn.
    [HttpGet("table/{tableId:int}")]
    public async Task<IActionResult> GetOpenByTable(int tableId)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .AsNoTracking()
            .Where(o => o.TableId == tableId && o.Status == OrderStatus.DangMo)
            .OrderByDescending(o => o.OrderTime)
            .FirstOrDefaultAsync();

        if (order is null)
            return NotFound(new { message = "Bàn này chưa có đơn đang mở" });

        // Khách của đơn; nếu đơn chưa gắn khách thì tra theo lượt đặt bàn đã nhận
        // của bàn này (đơn tạo trước khi khách đăng ký thành viên, hoặc nhận bàn từ Mobile).
        var customerId = order.CustomerId;
        if (customerId is null)
        {
            var windowStart = order.OrderTime.AddHours(-12);
            var windowEnd = order.OrderTime.AddHours(12);
            var reservation = await _db.Reservations.AsNoTracking()
                .Where(r => r.TableId == tableId
                            && r.Status == ReservationStatus.DaNhanBan
                            && r.ReservationTime >= windowStart && r.ReservationTime <= windowEnd)
                .OrderByDescending(r => r.ReservationTime)
                .FirstOrDefaultAsync();
            if (reservation is not null)
                customerId = await ResolveReservationCustomerIdAsync(reservation);
        }

        object? customerInfo = null;
        if (customerId.HasValue)
        {
            customerInfo = await _db.Customers.AsNoTracking()
                .Where(c => c.CustomerId == customerId.Value)
                .Select(c => new
                {
                    Id = c.CustomerId,
                    c.FullName,
                    c.Phone,
                    c.Email,
                    c.LoyaltyPoint,
                    MemberLevel = c.MemberLevel
                })
                .FirstOrDefaultAsync();
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    oi.OrderItemId,
                    oi.MenuItemId,
                    COALESCE(mi.ItemName, cb.ComboName, N'Món') AS ItemName,
                    oi.Quantity,
                    COALESCE(NULLIF(oi.UnitPrice, 0), mi.Price, cb.Price, 0) AS UnitPrice,
                    oi.Status,
                    oi.Note,
                    oi.IsTakeaway
                FROM OrderItems oi
                LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
                WHERE oi.OrderId = @OrderId
                  AND oi.Status <> N'DaHuy'
                ORDER BY oi.OrderItemId;
                """;
            AddDbParameter(command, "@OrderId", order.OrderId);

            var items = new List<object>();
            decimal totalAmount = 0;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var quantity = reader.GetInt32(3);
                var unitPrice = reader.GetDecimal(4);
                var statusText = reader.GetString(5);
                var status = Enum.TryParse<KitchenStatus>(statusText, out var parsedStatus)
                    ? parsedStatus
                    : KitchenStatus.MoiGoi;

                totalAmount += quantity * unitPrice;

                items.Add(new
                {
                    Id = reader.GetInt32(0),
                    MenuItemId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Status = status,
                    Note = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsTakeaway = !reader.IsDBNull(7) && reader.GetBoolean(7)
                });
            }

            return Ok(new
            {
                OrderId = order.OrderId,
                order.TableId,
                TableName = order.Table?.TableName ?? "",
                TotalAmount = totalAmount,
                Items = items,
                Customer = customerInfo
            });
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // GET api/orders/takeaway/open -> các đơn mang về đang mở, dùng cho tab "Mang về" của WinForm:
    //  - đơn OrderType = MangVe (không gắn bàn, có số MV-xxx), và
    //  - đơn tạo từ Mobile gắn vào "bàn mang về" thuộc khu vực "Mang về" (cho tới khi Mobile
    //    chuyển sang đơn không bàn).
        [HttpGet("takeaway/open")]
    public async Task<IActionResult> GetOpenTakeaway()
    {
        var rows = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.DangMo
                        && (o.OrderType == OrderTypes.MangVe
                            || (o.Table != null && o.Table.Area != null && o.Table.Area.AreaName == "Mang về")))
            .OrderBy(o => o.OrderTime)
            .Select(o => new
            {
                Id = o.OrderId,
                o.TakeawayNumber,
                SlotName = o.Table != null ? o.Table.TableName : null,
                o.OrderTime,
                EmployeeName = o.Employee != null && o.Employee.User != null ? o.Employee.User.FullName : "",
                o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.FullName : null,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : null,
                ItemCount = o.OrderItems
                    .Where(i => i.Status != KitchenStatus.DaHuy)
                    .Sum(i => (int?)i.Quantity) ?? 0,
                PendingItemCount = o.OrderItems
                    .Count(i => i.Status != KitchenStatus.DaHuy && i.Status != KitchenStatus.DaPhucVu),
                TotalAmount = o.OrderItems
                    .Where(i => i.Status != KitchenStatus.DaHuy)
                    .Sum(i => (decimal?)(i.Quantity * (i.UnitPrice > 0 ? i.UnitPrice : (i.MenuItem != null ? i.MenuItem.Price : 0)))) ?? 0,
                Items = o.OrderItems
                    .Where(i => i.Status != KitchenStatus.DaHuy)
                    .OrderBy(i => i.OrderItemId)
                    .Select(i => new
                    {
                        Id = i.OrderItemId,
                        Name = i.MenuItem != null ? i.MenuItem.ItemName : "(món đã bị xóa)",
                        i.Quantity,
                        UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : (i.MenuItem != null ? i.MenuItem.Price : 0),
                        i.Note,
                        KitchenStatus = i.Status
                    })
                    .ToList()
            })
            .ToListAsync();

        var result = rows.Select(r => new
        {
            r.Id,
            r.TakeawayNumber,
            TakeawayCode = r.TakeawayNumber.HasValue ? $"MV-{r.TakeawayNumber.Value:000}" : r.SlotName,
            r.OrderTime,
            r.EmployeeName,
            r.CustomerId,
            r.CustomerName,
            r.CustomerPhone,
            r.ItemCount,
            r.PendingItemCount,
            r.TotalAmount,
            r.Items
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)!.ThenInclude(oi => oi.MenuItem)
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null) return NotFound();

        var paymentStatus = await _db.Payments
            .Where(payment => payment.Invoice != null && payment.Invoice.OrderId == order.OrderId)
            .OrderByDescending(payment => payment.PaymentTime)
            .Select(payment => payment.Status.ToString())
            .FirstOrDefaultAsync();

        return Ok(new
        {
            Id = order.OrderId,
            order.TableId,
            order.OrderType,
            order.TakeawayNumber,
            order.TakeawayCode,
            order.Source,
            TableName = order.Table?.TableName ?? (order.TakeawayCode is not null ? $"Mang về {order.TakeawayCode}" : ""),
            Table = order.Table is null ? null : new { Id = order.Table.TableId, order.Table.TableName, order.Table.Capacity, Status = order.Table.Status },
            Customer = order.Customer is null ? null : new
            {
                Id = order.Customer.CustomerId,
                order.Customer.FullName,
                order.Customer.Phone,
                order.Customer.LoyaltyPoint,
                MemberLevel = order.Customer.MemberLevel
            },
            Status = order.Status,
            PaymentStatus = paymentStatus,
            CreatedAt = order.OrderTime,
            order.TotalAmount,
            OrderItems = order.OrderItems.Select(oi => new
            {
                Id = oi.OrderItemId,
                oi.MenuItemId,
                MenuItem = oi.MenuItem is null ? null : new { Id = oi.MenuItem.MenuItemId, Name = oi.MenuItem.ItemName, oi.MenuItem.Price },
                oi.Quantity,
                oi.Note,
                oi.IsTakeaway,
                KitchenStatus = oi.Status,
                oi.CancelReason,
                oi.RowVer
            })
        });
    }
    private int? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static KitchenStatus? NormalizeMobileStatus(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "chờ chế biến" or "moigoi" => KitchenStatus.MoiGoi,
            "đang chế biến" or "dangchebien" => KitchenStatus.DangCheBien,
            "đã xong" or "dachebien" => KitchenStatus.DaCheBien,
            "đã phục vụ" or "daphucvu" => KitchenStatus.DaPhucVu,
            "đã hủy" or "dahuy" => KitchenStatus.DaHuy,
            _ => null
        };
    }

    private static string DisplayMobileStatus(KitchenStatus value)
    {
        return value switch
        {
            KitchenStatus.DangCheBien => "Đang chế biến",
            KitchenStatus.DaCheBien => "Đã xong",
            KitchenStatus.DaPhucVu => "Đã phục vụ",
            KitchenStatus.DaHuy => "Đã hủy",
            _ => "Chờ chế biến"
        };
    }

    private static bool IsValidMobileTransition(
        KitchenStatus current,
        KitchenStatus target)
    {
        if (current == target)
            return true;

        return (current, target) switch
        {
            (KitchenStatus.MoiGoi, KitchenStatus.DangCheBien) => true,
            (KitchenStatus.DangCheBien, KitchenStatus.DaCheBien) => true,
            (KitchenStatus.DaCheBien, KitchenStatus.DaPhucVu) => true,
            _ => false
        };
    }

    private static void AddDbParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<int> ExecuteDbAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var parameter in parameters)
            AddDbParameter(command, parameter.Name, parameter.Value);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ExecuteScalarIntDbAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var parameter in parameters)
            AddDbParameter(command, parameter.Name, parameter.Value);

        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static bool IsValidMobileTransitionDb(
        string current,
        string target)
    {
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            return true;

        return (current, target) switch
        {
            ("MoiGoi", "DangCheBien") => true,
            ("DangCheBien", "DaCheBien") => true,
            ("DaCheBien", "DaPhucVu") => true,
            _ => false
        };
    }

    private static string DisplayMobileStatusDb(string value)
    {
        return value switch
        {
            "DangCheBien" => "Đang chế biến",
            "DaCheBien" => "Đã xong",
            "DaPhucVu" => "Đã phục vụ",
            "DaHuy" => "Đã hủy",
            _ => "Chờ chế biến"
        };
    }

}