using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Controllers;

// Cac API thong ke, bao cao (doanh thu / mon an / nhan vien / khuyen mai) - dung
// cho man hinh "Doanh thu" (QuanLy, ThuNgan). Tat ca deu loc theo Invoice.InvoiceDate
// va CHI tinh tren hoa don Status = DaThanhToan (tru rieng recent-invoices liet ke
// ca hoa don chua/da huy de xem duoc dung tinh trang).
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "QuanLy,ThuNgan")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    // Quy uoc chung: "from"/"to" la ngay (khong gio), loc theo InvoiceDate trong
    // khoang [from 00:00, to 23:59:59]. Khong truyen -> mac dinh HOM NAY.
    private static (DateTime From, DateTime ToExclusive) ResolveRange(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.Today).Date;
        var t = (to ?? DateTime.Today).Date.AddDays(1); // moc ket thuc dang EXCLUSIVE
        return (f, t);
    }

    // GET /api/reports/summary?from=&to= -> so lieu 4 the StatCard dau trang: Doanh
    // thu, So hoa don, Gia tri TB/hoa don, Tong tien da giam gia - kem % tang/giam
    // so voi ky truoc lien ke (cung do dai voi ky dang xem).
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = ResolveRange(from, to);
        var days = (t - f).Days;
        var prevFrom = f.AddDays(-days);
        var prevTo = f; // exclusive, noi tiep ngay ngay truoc do

        var current = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t)
            .GroupBy(i => 1)
            .Select(g => new
            {
                Revenue = g.Sum(i => i.TotalAmount),
                OrderCount = g.Count(),
                TotalDiscount = g.Sum(i => i.DiscountAmount)
            })
            .FirstOrDefaultAsync();

        var previousRevenue = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= prevFrom && i.InvoiceDate < prevTo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        var revenue = current?.Revenue ?? 0;
        var orderCount = current?.OrderCount ?? 0;
        var totalDiscount = current?.TotalDiscount ?? 0;
        var avgOrderValue = orderCount > 0 ? revenue / orderCount : 0;
        double? trendPercent = previousRevenue > 0 ? (double)((revenue - previousRevenue) / previousRevenue * 100) : null;

        return Ok(new
        {
            Revenue = revenue,
            OrderCount = orderCount,
            AvgOrderValue = avgOrderValue,
            TotalDiscount = totalDiscount,
            TrendPercent = trendPercent
        });
    }

    // GET /api/reports/revenue-by-period?from=&to= -> du lieu ve bieu do cot. Khoang
    // ngay <= 31 -> nhom theo TUNG NGAY (dien du ca ngay khong ban duoc gi = 0d de
    // bieu do khong bi "hut" cot); nguoc lai nhom theo THANG.
    [HttpGet("revenue-by-period")]
    public async Task<IActionResult> GetRevenueByPeriod([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = ResolveRange(from, to);
        var groupByMonth = (t - f).Days > 31;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t)
            .Select(i => new { i.InvoiceDate, i.TotalAmount })
            .ToListAsync();

        List<object> points;
        if (groupByMonth)
        {
            points = invoices
                .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => (object)new { Label = $"{g.Key.Month:00}/{g.Key.Year}", Revenue = g.Sum(x => x.TotalAmount) })
                .ToList();
        }
        else
        {
            var byDate = invoices.GroupBy(i => i.InvoiceDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
            points = new List<object>();
            for (var d = f; d < t; d = d.AddDays(1))
            {
                points.Add(new { Label = d.ToString("dd/MM"), Revenue = byDate.TryGetValue(d, out var rev) ? rev : 0 });
            }
        }

        return Ok(points);
    }

    // GET /api/reports/revenue-by-hour?date=yyyy-MM-dd -> doanh thu theo tung
    // khung 2 gio (08:00, 10:00, 12:00...) trong 1 NGAY CU THE - dung rieng cho
    // bieu do o man Tong quan (khac voi revenue-by-period la theo NGAY/THANG cho
    // khoang nhieu ngay).
    [HttpGet("revenue-by-hour")]
    public async Task<IActionResult> GetRevenueByHour([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1))
            .Select(i => new { i.InvoiceDate, i.TotalAmount })
            .ToListAsync();

        var buckets = new[] { 8, 10, 12, 14, 16, 18, 20 };
        var points = buckets.Select(startHour =>
        {
            var endHour = startHour + 2;
            var revenue = invoices.Where(i => i.InvoiceDate.Hour >= startHour && i.InvoiceDate.Hour < endHour).Sum(i => i.TotalAmount);
            return new { Label = $"{startHour:00}:00", Revenue = revenue };
        }).ToList();

        return Ok(points);
    }

    // GET /api/reports/recent-invoices?from=&to=&take=10 -> hoa don gan day trong ky (moi nhat truoc)
    [HttpGet("recent-invoices")]
    public async Task<IActionResult> GetRecentInvoices([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 10)
    {
        var (f, t) = ResolveRange(from, to);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= f && i.InvoiceDate < t)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(take)
            .Select(i => new
            {
                Id = i.InvoiceId,
                TableName = i.Order != null && i.Order.Table != null ? i.Order.Table.TableName : "",
                i.InvoiceDate,
                i.TotalAmount,
                Status = i.Status
            })
            .ToListAsync();

        return Ok(invoices);
    }

    // GET /api/reports/top-items?from=&to=&take=10 -> mon an/thuc uong ban chay
    // nhat trong ky, xep theo SO LUONG ban ra (khong tinh mon da bi huy).
    [HttpGet("top-items")]
    public async Task<IActionResult> GetTopItems([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 10)
    {
        var (f, t) = ResolveRange(from, to);

        var paidOrderIds = _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t)
            .Select(i => i.OrderId);

        var topItems = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Status != KitchenStatus.DaHuy && paidOrderIds.Contains(oi.OrderId))
            .GroupBy(oi => new { oi.MenuItemId, oi.MenuItem!.ItemName })
            .Select(g => new
            {
                MenuItemId = g.Key.MenuItemId,
                MenuItemName = g.Key.ItemName,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(take)
            .ToListAsync();

        return Ok(topItems);
    }

    // GET /api/reports/by-order-type?from=&to= -> tach hoa don / doanh thu AN TAI CHO va MANG VE.
    // Hoa don mang ve = don OrderType = MangVe, hoac don cua Mobile gan vao "ban mang ve"
    // (khu vuc "Mang ve"). TakeawayItemsRevenue = tong tien cac MON mang ve (ke ca mon mang ve
    // nam trong don an tai cho).
    [HttpGet("by-order-type")]
    public async Task<IActionResult> GetByOrderType([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = ResolveRange(from, to);

        var paid = _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t);

        var takeawayInvoices = paid.Where(i => i.Order != null
            && (i.Order.OrderType == OrderTypes.MangVe
                || (i.Order.Table != null && i.Order.Table.Area != null && i.Order.Table.Area.AreaName == "Mang về")));

        var totalCount = await paid.CountAsync();
        var totalRevenue = await paid.SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
        var takeawayCount = await takeawayInvoices.CountAsync();
        var takeawayRevenue = await takeawayInvoices.SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        var paidOrderIds = paid.Select(i => i.OrderId);
        var takeawayItemsRevenue = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Status != KitchenStatus.DaHuy
                && paidOrderIds.Contains(oi.OrderId)
                && (oi.IsTakeaway
                    || oi.Order!.OrderType == OrderTypes.MangVe
                    || (oi.Order.Table != null && oi.Order.Table.Area != null && oi.Order.Table.Area.AreaName == "Mang về")))
            .SumAsync(oi => (decimal?)(oi.UnitPrice * oi.Quantity)) ?? 0;

        return Ok(new
        {
            DineIn = new { InvoiceCount = totalCount - takeawayCount, Revenue = totalRevenue - takeawayRevenue },
            Takeaway = new { InvoiceCount = takeawayCount, Revenue = takeawayRevenue },
            TakeawayItemsRevenue = takeawayItemsRevenue
        });
    }

    // GET /api/reports/by-employee?from=&to= -> doanh thu theo tung nhan vien
    // (nguoi tao/phuc vu Order - Order.EmployeeId), sap xep doanh thu giam dan.
    [HttpGet("by-employee")]
    public async Task<IActionResult> GetByEmployee([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = ResolveRange(from, to);

        var result = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t)
            .GroupBy(i => new
            {
                EmployeeId = i.Order!.EmployeeId,
                EmployeeName = i.Order!.Employee != null && i.Order.Employee.User != null ? i.Order.Employee.User.FullName : "(Không rõ)"
            })
            .Select(g => new
            {
                g.Key.EmployeeId,
                g.Key.EmployeeName,
                InvoiceCount = g.Count(),
                Revenue = g.Sum(i => i.TotalAmount)
            })
            .OrderByDescending(x => x.Revenue)
            .ToListAsync();

        return Ok(result);
    }

    // GET /api/reports/campaigns?from=&to= -> hieu qua tung chien dich khuyen mai
    // (so hoa don ap dung, tong tien da giam) trong ky - CHI tinh hoa don co
    // AppliedCampaignId (bo qua hoa don khong dung khuyen mai).
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaignReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = ResolveRange(from, to);

        var result = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.DaThanhToan && i.InvoiceDate >= f && i.InvoiceDate < t && i.AppliedCampaignId != null)
            .GroupBy(i => new { i.AppliedCampaignId, CampaignName = i.AppliedCampaign!.CampaignName })
            .Select(g => new
            {
                CampaignId = g.Key.AppliedCampaignId,
                g.Key.CampaignName,
                InvoiceCount = g.Count(),
                TotalDiscount = g.Sum(i => i.DiscountAmount)
            })
            .OrderByDescending(x => x.TotalDiscount)
            .ToListAsync();

        return Ok(result);
    }
}