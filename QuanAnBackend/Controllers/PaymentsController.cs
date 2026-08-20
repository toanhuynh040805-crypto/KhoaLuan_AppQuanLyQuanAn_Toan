using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

public record CreatePaymentRequest(int OrderId, PaymentMethod Method);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PaymentsController(AppDbContext db) => _db = db;

    // POST api/payments -> Thu ngân tạo hóa đơn, sinh VietQR động nếu chọn QR
    // TODO: tích hợp thật với VietQR.io hoặc API ngân hàng ở đây
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)!.ThenInclude(oi => oi.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId);

        if (order is null) return NotFound();

        var total = order.OrderItems.Sum(oi => oi.Quantity * (oi.MenuItem?.Price ?? 0));

        var payment = new Payment
        {
            OrderId = order.Id,
            TotalAmount = total,
            Method = request.Method,
            QRCodeContent = request.Method == PaymentMethod.VietQR
                ? GenerateVietQrPayload(total, order.Id)
                : null
        };

        _db.Payments.Add(payment);
        order.Status = OrderStatus.HoanTat;
        await _db.SaveChangesAsync();

        return Ok(payment);
    }

    private string GenerateVietQrPayload(decimal amount, int orderId)
    {
        // Placeholder: thay bằng gọi API VietQR.io thật, truyền số tiền + nội dung "DH{orderId}"
        return $"VIETQR|AMOUNT={amount}|CONTENT=DH{orderId}";
    }
}
