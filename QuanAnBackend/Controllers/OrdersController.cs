using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record CreateOrderItemDto(int MenuItemId, int Quantity, string? Note);
public record AddItemsRequest(int TableId, int UserId, List<CreateOrderItemDto> Items);

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OrderRoutingService _routingService;

    public OrdersController(AppDbContext db, OrderRoutingService routingService)
    {
        _db = db;
        _routingService = routingService;
    }

    // POST api/orders/add-items
    // Đây là endpoint chính mà Mobile gọi khi nhân viên order món.
    // Sau khi lưu DB, kích hoạt thuật toán phân luồng (nghiệp vụ #2).
    [HttpPost("add-items")]
    public async Task<IActionResult> AddItems([FromBody] AddItemsRequest request)
    {
        // Tìm order đang mở của bàn này, nếu chưa có thì tạo mới
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.TableId == request.TableId && o.Status == OrderStatus.DangPhucVu);

        if (order is null)
        {
            order = new Order { TableId = request.TableId, UserId = request.UserId };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var table = await _db.Tables.FindAsync(request.TableId);
            if (table is not null) table.Status = TableStatus.DangPhucVu;
        }

        var newItemIds = new List<int>();
        foreach (var dto in request.Items)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                MenuItemId = dto.MenuItemId,
                Quantity = dto.Quantity,
                Note = dto.Note
            };
            _db.OrderItems.Add(orderItem);
            await _db.SaveChangesAsync();
            newItemIds.Add(orderItem.Id);
        }

        // Kích hoạt phân luồng ngay sau khi lưu món
        await _routingService.RouteNewOrderItemsAsync(order.Id, newItemIds);

        return Ok(new { OrderId = order.Id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)!.ThenInclude(oi => oi.MenuItem)
            .Include(o => o.Table)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? NotFound() : Ok(order);
    }
}
