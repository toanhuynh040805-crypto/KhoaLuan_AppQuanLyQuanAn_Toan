using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Controllers;

public record CreateCustomerRequest(string FullName, string Phone, string? Email, DateTime? DateOfBirth);
public record UpdateCustomerRequest(string FullName, string Phone, string? Email, DateTime? DateOfBirth);
public record AdjustPointsRequest(int Points);

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }
        private sealed class WebAccountRow
    {
        public int CustomerId { get; set; }
        public bool IsActive { get; set; }
    }

    // CustomerAccounts không map EF nên đọc bằng SQL: CustomerId -> IsActive
    private async Task<Dictionary<int, bool>> LoadWebAccountsAsync(int? customerId = null)
    {
        var rows = customerId is null
            ? await _db.Database.SqlQuery<WebAccountRow>(
                $"SELECT CustomerId, IsActive FROM dbo.CustomerAccounts").ToListAsync()
            : await _db.Database.SqlQuery<WebAccountRow>(
                $"SELECT CustomerId, IsActive FROM dbo.CustomerAccounts WHERE CustomerId = {customerId.Value}").ToListAsync();

        return rows.ToDictionary(r => r.CustomerId, r => r.IsActive);
    }
    // GET /api/customers?keyword=... -> danh sách khách hàng (tìm theo tên hoặc
    // SĐT), kèm số đơn đã thanh toán + tổng chi tiêu để nhận diện nhanh khách
    // "thân thiết" ngay trên lưới danh sách.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? keyword)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c => c.FullName.Contains(kw) || c.Phone.Contains(kw));
        }

        var customers = await query
            .OrderByDescending(c => c.LoyaltyPoint)
            .Select(c => new
            {
                Id = c.CustomerId,
                c.FullName,
                c.Phone,
                c.Email,
                c.DateOfBirth,
                c.LoyaltyPoint,
                MemberLevel = c.MemberLevel,
                c.CreatedAt,
                OrderCount = c.Orders.Count(o => o.Status == OrderStatus.DaThanhToan),
                TotalSpent = c.Orders
                    .Where(o => o.Status == OrderStatus.DaThanhToan)
                    .SelectMany(o => o.Invoices)
                    .Where(i => i.Status == InvoiceStatus.DaThanhToan)
                    .Sum(i => (decimal?)i.TotalAmount) ?? 0
            })
        .ToListAsync();

        var accounts = await LoadWebAccountsAsync();

        return Ok(customers.Select(c => new
        {
            c.Id,
            c.FullName,
            c.Phone,
            c.Email,
            c.DateOfBirth,
            c.LoyaltyPoint,
            c.MemberLevel,
            c.CreatedAt,
            c.OrderCount,
            c.TotalSpent,
            HasWebAccount = accounts.ContainsKey(c.Id),
            WebAccountActive = accounts.TryGetValue(c.Id, out var active) && active
        }));
    }

    // GET /api/customers/search?phone=... -> tra cứu nhanh cho màn Bán hàng (POS)
    // gắn khách vào hóa đơn. Cho phép tìm gần đúng (Contains) để gõ vài số cuối
    // cũng ra kết quả, giới hạn 10 kết quả để không tràn danh sách gợi ý.
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return Ok(new List<object>());

        var results = await _db.Customers.AsNoTracking()
            .Where(c => c.Phone.Contains(phone))
            .OrderBy(c => c.Phone)
            .Take(10)
            .Select(c => new { Id = c.CustomerId, c.FullName, c.Phone, c.Email, c.LoyaltyPoint, MemberLevel = c.MemberLevel })
            .ToListAsync();

        return Ok(results);
    }

    // GET /api/customers/{id} -> chi tiết 1 khách hàng: thông tin cơ bản + lịch
    // sử đơn hàng gần đây + lịch sử tích/đổi điểm.
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null) return NotFound();
        var accounts = await LoadWebAccountsAsync(id);
        var recentOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.OrderTime)
            .Take(20)
            .Select(o => new
            {
                Id = o.OrderId,
                o.OrderTime,
                o.TotalAmount,
                Status = o.Status,
                TableName = o.Table != null ? o.Table.TableName : ""
            })
            .ToListAsync();

        var history = await _db.LoyaltyPointHistories.AsNoTracking()
            .Where(h => h.CustomerId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(50)
            .Select(h => new { Id = h.HistoryId, h.Points, Type = h.Type, h.RelatedInvoiceId, h.CreatedAt })
            .ToListAsync();

        return Ok(new
        {
            Id = customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.DateOfBirth,
            customer.LoyaltyPoint,
            MemberLevel = customer.MemberLevel,
            customer.CreatedAt,
            HasWebAccount = accounts.ContainsKey(id),
            WebAccountActive = accounts.TryGetValue(id, out var webActive) && webActive,
            RecentOrders = recentOrders,
            LoyaltyHistory = history
        });
    }

    [HttpGet("{id}/loyalty-history")]
    public async Task<IActionResult> GetLoyaltyHistory(int id)
    {
        var exists = await _db.Customers.AnyAsync(c => c.CustomerId == id);
        if (!exists) return NotFound();

        var history = await _db.LoyaltyPointHistories.AsNoTracking()
            .Where(h => h.CustomerId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new { Id = h.HistoryId, h.Points, Type = h.Type, h.RelatedInvoiceId, h.CreatedAt })
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Họ tên và số điện thoại là bắt buộc" });

        var phone = request.Phone.Trim();
        if (await _db.Customers.AnyAsync(c => c.Phone == phone))
            return BadRequest(new { message = "Số điện thoại này đã được đăng ký cho khách hàng khác" });

        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            DateOfBirth = request.DateOfBirth,
            LoyaltyPoint = 0,
            MemberLevel = MemberLevel.ThanhVien
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return Ok(new { Id = customer.CustomerId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Họ tên và số điện thoại là bắt buộc" });

        var phone = request.Phone.Trim();
        if (await _db.Customers.AnyAsync(c => c.Phone == phone && c.CustomerId != id))
            return BadRequest(new { message = "Số điện thoại này đã được đăng ký cho khách hàng khác" });

        customer.FullName = request.FullName.Trim();
        customer.Phone = phone;
        customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.DateOfBirth = request.DateOfBirth;

        await _db.SaveChangesAsync();
        return Ok(new { Id = customer.CustomerId });
    }

    // PUT /api/customers/{id}/web-account/active  (body: true | false)
    // Khóa/mở tài khoản đăng nhập web của khách. Khóa sẽ đăng xuất mọi phiên đang có.
    [HttpPut("{id}/web-account/active")]
    [Authorize(Roles = "QuanLy,ThuNgan")]
    public async Task<IActionResult> SetWebAccountActive(int id, [FromBody] bool isActive)
    {
        var updated = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerAccounts
            SET IsActive = {isActive}, FailedLoginCount = 0, LockedUntil = NULL
            WHERE CustomerId = {id}
            """);

        if (updated == 0)
            return NotFound(new { message = "Khách hàng này chưa có tài khoản web." });

        if (!isActive)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.CustomerRefreshTokens SET RevokedAt = GETDATE()
                WHERE CustomerId = {id} AND RevokedAt IS NULL
                """);
        }

        return Ok(new { Id = id, WebAccountActive = isActive });
    }

    // Chỉ cho xóa khi khách CHƯA có đơn hàng/lịch sử điểm nào (tránh mất dấu vết
    // dữ liệu đã phát sinh giao dịch thật) - đúng nguyên tắc DeleteBehavior.Restrict
    // đang áp dụng chung toàn hệ thống.
    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy,ThuNgan")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        var hasOrders = await _db.Orders.AnyAsync(o => o.CustomerId == id);
        var hasHistory = await _db.LoyaltyPointHistories.AnyAsync(h => h.CustomerId == id);
        if (hasOrders || hasHistory)
            return BadRequest(new { message = "Khách hàng đã có giao dịch/lịch sử điểm, không thể xóa" });

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/customers/{id}/adjust-points -> Quản lý cộng/trừ điểm thủ công
    // (vd tặng điểm sinh nhật, trừ điểm do nhập sai trước đó). Points có thể âm.
    [HttpPost("{id}/adjust-points")]
    [Authorize(Roles = "QuanLy,ThuNgan")]
    public async Task<IActionResult> AdjustPoints(int id, [FromBody] AdjustPointsRequest request)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        if (request.Points == 0) return BadRequest(new { message = "Số điểm điều chỉnh phải khác 0" });

        var newBalance = customer.LoyaltyPoint + request.Points;
        if (newBalance < 0) return BadRequest(new { message = "Số điểm hiện có không đủ để trừ" });

        customer.LoyaltyPoint = newBalance;
        customer.MemberLevel = CustomerLoyaltyHelper.CalculateMemberLevel(newBalance);

        _db.LoyaltyPointHistories.Add(new LoyaltyPointHistory
        {
            CustomerId = id,
            Points = request.Points,
            Type = request.Points > 0 ? LoyaltyPointType.TichDiem : LoyaltyPointType.DoiDiem,
            RelatedInvoiceId = null,
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
        return Ok(new { Id = customer.CustomerId, customer.LoyaltyPoint, MemberLevel = customer.MemberLevel });
    }
}