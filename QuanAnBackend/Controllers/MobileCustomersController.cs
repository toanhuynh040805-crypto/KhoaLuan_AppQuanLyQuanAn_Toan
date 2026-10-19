using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

public record SetMobileTableCustomerRequest(int CustomerId, string? EmployeeCode);

[ApiController]
[Route("api/mobile/customers")]
[RequireMobilePermission("customer.manage")]
public class MobileCustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileCustomersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("table/{tableId}")]
    public async Task<IActionResult> GetForTable(int tableId)
    {
        var orderId = await FindOpenOrderIdAsync(tableId);
        if (!orderId.HasValue) return Ok(new { customer = (object?)null });

        var customer = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderId == orderId.Value && o.CustomerId.HasValue)
            .Select(o => new
            {
                Id = o.Customer!.CustomerId,
                o.Customer.FullName,
                o.Customer.Phone,
                o.Customer.LoyaltyPoint,
                MemberLevel = o.Customer.MemberLevel.ToString()
            })
            .FirstOrDefaultAsync();

        return Ok(new { customer });
    }

    [HttpPut("table/{tableId}")]
    public async Task<IActionResult> SetForTable(
        int tableId,
        [FromBody] SetMobileTableCustomerRequest request)
    {
        var customer = await _db.Customers.FindAsync(request.CustomerId);
        if (customer is null)
            return BadRequest(new { message = "Khách hàng không tồn tại" });

        var orderId = await FindOpenOrderIdAsync(tableId);
        if (!orderId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.EmployeeCode))
                return BadRequest(new { message = "Hãy gọi món hoặc xác định nhân viên trước khi gắn thành viên" });

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeCode == request.EmployeeCode.Trim());
            if (employee is null)
                return BadRequest(new { message = "Không tìm thấy nhân viên đang đăng nhập" });

            var table = await _db.Tables.FindAsync(tableId);
            if (table is null) return NotFound(new { message = "Không tìm thấy bàn" });

            var order = new Order
            {
                TableId = tableId,
                EmployeeId = employee.EmployeeId,
                CustomerId = customer.CustomerId,
                OrderTime = DateTime.Now,
                Status = OrderStatus.DangMo,
                TotalAmount = 0
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            orderId = order.OrderId;

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                IF NOT EXISTS (
                    SELECT 1 FROM OrderTables
                    WHERE OrderId = {order.OrderId} AND TableId = {tableId}
                )
                INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable)
                VALUES ({order.OrderId}, {tableId}, 1);
            ");
        }
        else
        {
            var order = await _db.Orders.FindAsync(orderId.Value);
            if (order is null) return NotFound(new { message = "Không tìm thấy đơn đang mở" });
            order.CustomerId = customer.CustomerId;
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            orderId,
            customer = new
            {
                Id = customer.CustomerId,
                customer.FullName,
                customer.Phone,
                customer.LoyaltyPoint,
                MemberLevel = customer.MemberLevel.ToString()
            }
        });
    }

    private async Task<int?> FindOpenOrderIdAsync(int tableId)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP 1 o.OrderId
                FROM Orders o
                LEFT JOIN OrderTables ot ON ot.OrderId = o.OrderId
                WHERE o.Status = N'DangMo'
                  AND (o.TableId = @TableId OR ot.TableId = @TableId)
                ORDER BY o.OrderTime DESC, o.OrderId DESC;
                """;
            var p = command.CreateParameter();
            p.ParameterName = "@TableId";
            p.Value = tableId;
            command.Parameters.Add(p);
            var value = await command.ExecuteScalarAsync();
            return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
