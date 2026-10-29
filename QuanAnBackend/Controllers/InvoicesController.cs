using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public InvoicesController(AppDbContext db)
    {
        _db = db;
    }

    private bool IsManager =>
        User.IsInRole("QuanLy") ||
        User.IsInRole("Admin");

    private bool IsCashier =>
        User.IsInRole("ThuNgan");

    private int? CurrentUserId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId)
            ? userId
            : null;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? date)
    {
        var currentUserId = CurrentUserId;
        if (!currentUserId.HasValue)
            return Unauthorized();

        if (!IsManager && !IsCashier)
            return Forbid();

        var day = (date ?? DateTime.Today).Date;

        var query = _db.Invoices
            .AsNoTracking()
            .Where(i =>
                i.InvoiceDate >= day &&
                i.InvoiceDate < day.AddDays(1));

        // Thu ngân chỉ được xem các hóa đơn do chính tài khoản đó
        // xác nhận thanh toán. Quản lý được xem toàn bộ.
        if (!IsManager)
        {
            query = query.Where(
                i => i.CreatedBy == currentUserId.Value);
        }

        var rows = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new
            {
                Id = i.InvoiceId,
                i.InvoiceDate,
                BaseTableName =
                    i.Order != null &&
                    i.Order.Table != null
                        ? i.Order.Table.TableName
                        : "",
                TakeawayNumber =
                    i.Order != null
                        ? i.Order.TakeawayNumber
                        : null,
                i.TotalAmount,
                Status = i.Status,
                CashierName =
                    i.CreatedByUser != null
                        ? i.CreatedByUser.FullName
                        : "",
                WaiterName =
                    i.RequestedByUser != null
                        ? i.RequestedByUser.FullName
                        : i.Order != null &&
                          i.Order.Employee != null &&
                          i.Order.Employee.User != null
                            ? i.Order.Employee.User.FullName
                            : ""
            })
            .ToListAsync();

        var invoices = rows.Select(r => new
        {
            r.Id,
            r.InvoiceDate,
            TableName = r.TakeawayNumber.HasValue
                ? $"Mang về MV-{r.TakeawayNumber.Value:000}"
                : r.BaseTableName,
            r.TotalAmount,
            r.Status,
            r.WaiterName,
            r.CashierName
        }).ToList();

        return Ok(invoices);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUserId = CurrentUserId;
        if (!currentUserId.HasValue)
            return Unauthorized();

        if (!IsManager && !IsCashier)
            return Forbid();

        var invoice = await _db.Invoices
            .Include(i => i.CreatedByUser)
            .Include(i => i.RequestedByUser)
            .Include(i => i.Order)!
                .ThenInclude(o => o!.Table)
            .Include(i => i.Order)!
                .ThenInclude(o => o!.Employee)!
                .ThenInclude(e => e!.User)
            .Include(i => i.Order)!
                .ThenInclude(o => o!.OrderItems)!
                .ThenInclude(oi => oi.MenuItem)
            .Include(i => i.Payments)
            .Include(i => i.AppliedCampaign)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice is null)
            return NotFound();

        if (!IsManager &&
            invoice.CreatedBy != currentUserId.Value)
        {
            return Forbid();
        }

        var orderId = invoice.OrderId;
        var tableName =
            invoice.Order?.Table?.TableName
            ?? (invoice.Order?.TakeawayCode is { } code
                ? $"Mang về {code}"
                : "");

        var waiterName =
            invoice.RequestedByUser?.FullName
            ?? invoice.Order?.Employee?.User?.FullName
            ?? "";

        var cashierName =
            invoice.CreatedByUser?.FullName
            ?? "";

        return Ok(new
        {
            Id = invoice.InvoiceId,
            invoice.OrderId,
            invoice.InvoiceDate,
            OrderTime = invoice.Order?.OrderTime,
            TableName = tableName,
            OrderType = invoice.Order?.OrderType,
            WaiterName = waiterName,
            CashierName = cashierName,
            Items =
                (invoice.Order?.OrderItems
                    ?? new List<OrderItem>())
                .Where(oi =>
                    oi.Status != KitchenStatus.DaHuy)
                .Select(oi => new
                {
                    Name =
                        oi.MenuItem != null
                            ? oi.MenuItem.ItemName
                            : "(món đã bị xóa)",
                    oi.Quantity,
                    oi.UnitPrice,
                    LineTotal =
                        oi.Quantity * oi.UnitPrice,
                    IsTakeaway =
                        oi.IsTakeaway ||
                        invoice.Order?.OrderType ==
                            OrderTypes.MangVe
                })
                .ToList(),
            invoice.SubTotal,
            invoice.DiscountAmount,
            CampaignName =
                invoice.AppliedCampaign != null
                    ? invoice.AppliedCampaign.CampaignName
                    : null,
            invoice.TaxAmount,
            invoice.TotalAmount,
            Status = invoice.Status,
            Payments = invoice.Payments
                .OrderBy(p => p.PaymentTime)
                .Select(p => new
                {
                    Method = (int)p.PaymentMethod,
                    p.Amount,
                    p.PaymentTime,
                    p.TransactionRef
                })
                .ToList(),
            ShopName = ReceiptProfile.ShopName,
            ShopAddress = ReceiptProfile.ShopAddress,
            BankName = ReceiptProfile.BankCode,
            BankAccountNo =
                ReceiptProfile.BankAccountNo,
            BankAccountName =
                ReceiptProfile.BankAccountName,
            BankBranch =
                ReceiptProfile.BankBranch,
            TransferContent =
                ReceiptProfile.TransferContent(orderId),
            QrImageUrl =
                ReceiptProfile.BuildVietQrImageUrl(
                    invoice.TotalAmount,
                    orderId,
                    "qr_only")
        });
    }
}
