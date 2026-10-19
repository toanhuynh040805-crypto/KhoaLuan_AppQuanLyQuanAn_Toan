using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Middleware;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

// Hồ sơ + lịch sử đơn của khách web đang đăng nhập.
// CustomerId luôn lấy từ JWT, không nhận từ client.
[ApiController]
[Route("api/customers/me")]
[Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
[CustomerAccess]
public class CustomerProfileController : ControllerBase
{
    private const int MaxOrders = 50;

    private readonly AppDbContext _db;

    public CustomerProfileController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/customers/me
    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var customerId = CustomerTokens.GetCustomerId(User);
        if (customerId is null) return Unauthorized();

        var c = await _db.Customers.AsNoTracking()
            .Where(x => x.CustomerId == customerId.Value)
            .Select(x => new
            {
                x.CustomerId,
                x.FullName,
                x.Phone,
                x.Email,
                x.DateOfBirth,
                x.LoyaltyPoint,
                x.MemberLevel,
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (c is null)
            return NotFound(new { message = "Không tìm thấy khách hàng." });

        return Ok(new
        {
            Id = c.CustomerId,
            c.CustomerId,
            c.FullName,
            c.Phone,
            c.Email,
            c.DateOfBirth,
            c.LoyaltyPoint,
            MemberLevel = c.MemberLevel.ToString(),
            c.CreatedAt
        });
    }

    // GET api/customers/me/orders  (tối đa 50 đơn gần nhất)
    [HttpGet("orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var customerId = CustomerTokens.GetCustomerId(User);
        if (customerId is null) return Unauthorized();

        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId.Value)
            .OrderByDescending(o => o.OrderTime)
            .Take(MaxOrders)
            .Select(o => new
            {
                o.OrderId,
                o.OrderTime,
                o.TotalAmount,
                o.Status,
                o.OrderType,
                o.TakeawayNumber,
                TableName = o.Table != null ? o.Table.TableName : null,
                PaymentStatus = o.Invoices
                    .SelectMany(i => i.Payments)
                    .OrderByDescending(p => p.PaymentTime)
                    .Select(p => (PaymentStatus?)p.Status)
                    .FirstOrDefault(),
                Items = o.OrderItems
                    .Where(oi => oi.Status != KitchenStatus.DaHuy)
                    .Select(oi => new
                    {
                        oi.Quantity,
                        oi.UnitPrice,
                        oi.Note,
                        Name = oi.MenuItem != null ? oi.MenuItem.ItemName : null
                    })
                    .ToList()
            })
            .ToListAsync();

        var result = rows.Select(o => new
        {
            o.OrderId,
            o.OrderTime,
            o.TotalAmount,
            Status = o.Status.ToString(),
            PaymentStatus = o.PaymentStatus?.ToString(),
            o.OrderType,
            TakeawayCode = o.TakeawayNumber.HasValue ? $"MV-{o.TakeawayNumber.Value:000}" : null,
            o.TableName,
            OrderItems = o.Items.Select(i => new
            {
                i.Quantity,
                i.UnitPrice,
                i.Note,
                MenuItem = new { Name = i.Name ?? "Món ăn" }
            })
        });

        return Ok(result);
    }
}