using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Authorization;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

/// <summary>
/// Dashboard thống kê dành cho Mobile.
/// Dùng cùng dữ liệu/điều kiện với ReportsController để Mobile và WinForms
/// không lệch số liệu, nhưng trả về một payload duy nhất cho màn Thống kê.
/// </summary>
[ApiController]
[Route("api/mobile/statistics")]
public class MobileStatisticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileStatisticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequireMobilePermission("statistics.view")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today).Date;
        var toDate = (to ?? DateTime.Today).Date;

        if (toDate < fromDate)
        {
            return BadRequest(new
            {
                message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu"
            });
        }

        // Giới hạn hợp lý để tránh tải một khoảng quá lớn trên Mobile.
        if ((toDate - fromDate).TotalDays > 3660)
        {
            return BadRequest(new
            {
                message = "Khoảng thời gian thống kê quá lớn"
            });
        }

        var toExclusive = toDate.AddDays(1);
        var days = Math.Max(1, (toExclusive - fromDate).Days);
        var previousFrom = fromDate.AddDays(-days);
        var previousTo = fromDate;

        var paidInvoices = _db.Invoices
            .AsNoTracking()
            .Where(i =>
                i.Status == InvoiceStatus.DaThanhToan &&
                i.InvoiceDate >= fromDate &&
                i.InvoiceDate < toExclusive);

        var summary = await paidInvoices
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(i => i.TotalAmount),
                InvoiceCount = g.Count(),
                TotalDiscount = g.Sum(i => i.DiscountAmount)
            })
            .FirstOrDefaultAsync();

        var revenue = summary?.Revenue ?? 0m;
        var invoiceCount = summary?.InvoiceCount ?? 0;
        var totalDiscount = summary?.TotalDiscount ?? 0m;
        var avgInvoiceValue = invoiceCount > 0
            ? revenue / invoiceCount
            : 0m;

        var previousRevenue = await _db.Invoices
            .AsNoTracking()
            .Where(i =>
                i.Status == InvoiceStatus.DaThanhToan &&
                i.InvoiceDate >= previousFrom &&
                i.InvoiceDate < previousTo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        double? trendPercent = previousRevenue > 0
            ? (double)((revenue - previousRevenue) / previousRevenue * 100m)
            : null;

        var invoiceRows = await paidInvoices
            .Select(i => new
            {
                i.OrderId,
                i.InvoiceDate,
                i.TotalAmount
            })
            .ToListAsync();

        List<object> revenuePoints;
        if (days > 31)
        {
            revenuePoints = invoiceRows
                .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => (object)new
                {
                    label = $"{g.Key.Month:00}/{g.Key.Year}",
                    revenue = g.Sum(x => x.TotalAmount)
                })
                .ToList();
        }
        else
        {
            var byDate = invoiceRows
                .GroupBy(i => i.InvoiceDate.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));

            revenuePoints = new List<object>();
            for (var d = fromDate; d < toExclusive; d = d.AddDays(1))
            {
                revenuePoints.Add(new
                {
                    label = d.ToString("dd/MM"),
                    revenue = byDate.TryGetValue(d, out var value) ? value : 0m
                });
            }
        }

        var paidOrderIds = paidInvoices.Select(i => i.OrderId);

        var topItems = await _db.OrderItems
            .AsNoTracking()
            .Where(oi =>
                oi.Status != KitchenStatus.DaHuy &&
                paidOrderIds.Contains(oi.OrderId))
            .GroupBy(oi => new
            {
                oi.MenuItemId,
                Name = oi.MenuItem != null ? oi.MenuItem.ItemName : "(Không rõ)"
            })
            .Select(g => new
            {
                name = g.Key.Name,
                quantity = g.Sum(x => x.Quantity),
                revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.quantity)
            .ThenByDescending(x => x.revenue)
            .Take(10)
            .ToListAsync();

        var employees = await paidInvoices
            .Where(i => i.Order != null)
            .GroupBy(i => new
            {
                EmployeeId = i.Order!.EmployeeId,
                Name = i.Order.Employee != null &&
                       i.Order.Employee.User != null
                    ? i.Order.Employee.User.FullName
                    : "(Không rõ)"
            })
            .Select(g => new
            {
                name = g.Key.Name,
                invoiceCount = g.Count(),
                revenue = g.Sum(i => i.TotalAmount)
            })
            .OrderByDescending(x => x.revenue)
            .ToListAsync();

        var campaigns = await paidInvoices
            .Where(i => i.AppliedCampaignId != null)
            .GroupBy(i => new
            {
                i.AppliedCampaignId,
                Name = i.AppliedCampaign != null
                    ? i.AppliedCampaign.CampaignName
                    : "(Không rõ)"
            })
            .Select(g => new
            {
                name = g.Key.Name,
                invoiceCount = g.Count(),
                totalDiscount = g.Sum(i => i.DiscountAmount)
            })
            .OrderByDescending(x => x.totalDiscount)
            .ToListAsync();

        return Ok(new
        {
            revenue,
            invoiceCount,
            avgInvoiceValue,
            totalDiscount,
            trendPercent,
            revenuePoints,
            topItems,
            employees,
            campaigns
        });
    }
}
