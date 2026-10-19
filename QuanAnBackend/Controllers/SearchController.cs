using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

public record SearchResultItem(string Type, int Id, string Title, string Subtitle);

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;
    public SearchController(AppDbContext db) => _db = db;

    // GET /api/search?keyword=...&role=QuanLy -> gop ket qua tu nhieu bang, moi
    // loai lay toi da 5 ket qua. "role" dung de AN BOT nhung loai khong lien
    // quan/khong duoc phep xem cua Role dang dang nhap (khop dung bang phan
    // quyen da ap dung o tung Controller rieng le - vd Bep khong thay Khach
    // hang, chi QuanLy moi thay Nhan vien).
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] string role)
    {
        var results = new List<SearchResultItem>();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length < 2)
            return Ok(results);

        var kw = keyword.Trim();
        const int take = 5;

        // Món ăn - mọi Role đều được xem
        var menuItems = await _db.MenuItems.AsNoTracking()
            .Where(m => m.ItemName.Contains(kw))
            .OrderBy(m => m.ItemName)
            .Take(take)
            .Select(m => new SearchResultItem("MenuItem", m.MenuItemId, m.ItemName, $"{m.Price:N0}đ"))
            .ToListAsync();
        results.AddRange(menuItems);

        // Bàn - mọi Role đều được xem
        var tables = await _db.Tables.AsNoTracking()
            .Include(t => t.Area)
            .Where(t => t.TableName.Contains(kw))
            .OrderBy(t => t.TableName)
            .Take(take)
            .Select(t => new SearchResultItem("Table", t.TableId, t.TableName, t.Area != null ? t.Area.AreaName : ""))
            .ToListAsync();
        results.AddRange(tables);

        // Khách hàng - ẩn với Bếp
        if (role != "Bep")
        {
            var customers = await _db.Customers.AsNoTracking()
                .Where(c => c.FullName.Contains(kw) || c.Phone.Contains(kw))
                .OrderBy(c => c.FullName)
                .Take(take)
                .Select(c => new SearchResultItem("Customer", c.CustomerId, c.FullName, c.Phone))
                .ToListAsync();
            results.AddRange(customers);
        }

        // Nhân viên - chỉ QuanLy (khớp UsersController)
        if (role == "QuanLy")
        {
            var employees = await _db.Employees.AsNoTracking()
                .Include(e => e.User)
                .Where(e => e.User != null && (e.User.FullName.Contains(kw) || e.EmployeeCode.Contains(kw)))
                .OrderBy(e => e.User!.FullName)
                .Take(take)
                .Select(e => new SearchResultItem("Employee", e.EmployeeId, e.User!.FullName, e.EmployeeCode))
                .ToListAsync();
            results.AddRange(employees);
        }

        // Đơn hàng - chỉ QuanLy, ThuNgan (khớp OrdersController.GetAll); tìm theo
        // mã số (vd gõ "30" -> Đơn #30)
        if ((role == "QuanLy" || role == "ThuNgan") && int.TryParse(kw, out var orderId))
        {
            var orders = await _db.Orders.AsNoTracking()
                .Include(o => o.Table)
                .Where(o => o.OrderId == orderId)
                .Take(take)
                .Select(o => new SearchResultItem("Order", o.OrderId, $"Đơn #{o.OrderId}", o.Table != null ? o.Table.TableName : ""))
                .ToListAsync();
            results.AddRange(orders);
        }

        // Đặt bàn - ẩn với Bếp
        if (role != "Bep")
        {
            var reservations = await _db.Reservations.AsNoTracking()
                .Where(r => (r.CustomerName != null && r.CustomerName.Contains(kw)) || (r.CustomerPhone != null && r.CustomerPhone.Contains(kw)))
                .OrderByDescending(r => r.ReservationTime)
                .Take(take)
                .Select(r => new SearchResultItem("Reservation", r.ReservationId, r.CustomerName ?? "Khách đặt bàn", $"{r.ReservationTime:HH:mm dd/MM} - {r.CustomerPhone}"))
                .ToListAsync();
            results.AddRange(reservations);
        }

        return Ok(results);
    }
}