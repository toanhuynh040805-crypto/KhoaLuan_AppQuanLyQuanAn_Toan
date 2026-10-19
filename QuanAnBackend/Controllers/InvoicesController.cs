using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        var rows = await _db.Invoices
            .Include(i => i.Order)!.ThenInclude(o => o!.Table)
            .Where(i => i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1))
            .OrderByDescending(i => i.InvoiceDate)
            .AsNoTracking()
            .Select(i => new
            {
                Id = i.InvoiceId,
                i.InvoiceDate,
                BaseTableName = i.Order != null && i.Order.Table != null ? i.Order.Table.TableName : "",
                TakeawayNumber = i.Order != null ? i.Order.TakeawayNumber : null,
                i.TotalAmount,
                Status = i.Status
            })
            .ToListAsync();

        var invoices = rows.Select(r => new
        {
            r.Id,
            r.InvoiceDate,
            TableName = r.TakeawayNumber.HasValue ? $"Mang về MV-{r.TakeawayNumber.Value:000}" : r.BaseTableName,
            r.TotalAmount,
            r.Status
        }).ToList();

        return Ok(invoices);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Order)!.ThenInclude(o => o!.Table)
            .Include(i => i.Order)!.ThenInclude(o => o!.Employee)!.ThenInclude(e => e!.User)
            .Include(i => i.Order)!.ThenInclude(o => o!.OrderItems)!.ThenInclude(oi => oi.MenuItem)
            .Include(i => i.Payments)
            .Include(i => i.AppliedCampaign)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice is null) return NotFound();

        return Ok(new
        {
            Id = invoice.InvoiceId,
            invoice.InvoiceDate,
            TableName = invoice.Order?.Table?.TableName
                        ?? (invoice.Order?.TakeawayCode is { } code ? $"Mang về {code}" : ""),
            OrderType = invoice.Order?.OrderType,
            CashierName = invoice.Order != null && invoice.Order.Employee != null && invoice.Order.Employee.User != null
                ? invoice.Order.Employee.User.FullName : "",
            Items = (invoice.Order?.OrderItems ?? new List<OrderItem>())
                .Where(oi => oi.Status != KitchenStatus.DaHuy)
                .Select(oi => new
                {
                    Name = oi.MenuItem != null ? oi.MenuItem.ItemName : "(món đã bị xóa)",
                    oi.Quantity,
                    oi.UnitPrice,
                    LineTotal = oi.Quantity * oi.UnitPrice,
                    IsTakeaway = oi.IsTakeaway || invoice.Order?.OrderType == OrderTypes.MangVe
                })
                .ToList(),
            invoice.SubTotal,
            invoice.DiscountAmount,
            CampaignName = invoice.AppliedCampaign != null ? invoice.AppliedCampaign.CampaignName : null,
            invoice.TaxAmount,
            invoice.TotalAmount,
            Status = invoice.Status,
            Payments = invoice.Payments.Select(p => new { Method = p.PaymentMethod, p.Amount, p.PaymentTime })
        });
    }
}