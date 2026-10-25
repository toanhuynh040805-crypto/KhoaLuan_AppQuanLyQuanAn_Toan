using ClosedXML.Excel;
using QuanAnWinForm.Models;

namespace QuanAnWinForm.Services;

// Du lieu dau vao de xuat bao cao doanh thu ra Excel.
public sealed class ReportExportData
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string PeriodLabel { get; set; } = "";
    public string ChartUnit { get; set; } = "Ngày";
    public RevenueSummaryDto Summary { get; set; } = new();
    public List<RevenuePointDto> Points { get; set; } = new();
    public List<RecentInvoiceReportDto> Invoices { get; set; } = new();
    public List<TopItemDto> TopItems { get; set; } = new();
    public List<EmployeeRevenueDto> Employees { get; set; } = new();
    public List<CampaignReportDto> Campaigns { get; set; } = new();
}

// Xuat bao cao ra file .xlsx (6 sheet) bang ClosedXML.
public static class ExcelExportService
{
    private static readonly string[] InvoiceStatusLabels = { "Chưa thanh toán", "Đã thanh toán", "Đã hủy" };
    private static readonly XLColor HeaderBg = XLColor.FromHtml("#AC2D00");
    private const string MoneyFormat = "#,##0\" đ\"";
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    public static void Export(string path, ReportExportData d)
    {
        using var wb = new XLWorkbook();
        BuildSummarySheet(wb, d);
        BuildRevenueSheet(wb, d);
        BuildInvoicesSheet(wb, d);
        BuildTopItemsSheet(wb, d);
        BuildEmployeesSheet(wb, d);
        BuildCampaignsSheet(wb, d);
        wb.SaveAs(path);
    }

    private static void BuildSummarySheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Tổng quan");
        ws.Column(1).Width = 30;
        ws.Column(2).Width = 34;

        ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU - GUSTOPOS";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = HeaderBg;

        ws.Cell(3, 1).Value = "Kỳ báo cáo";
        ws.Cell(3, 2).Value = d.PeriodLabel;
        ws.Cell(4, 1).Value = "Từ ngày";
        ws.Cell(4, 2).Value = d.From.ToString("dd/MM/yyyy");
        ws.Cell(5, 1).Value = "Đến ngày";
        ws.Cell(5, 2).Value = d.To.ToString("dd/MM/yyyy");
        ws.Cell(6, 1).Value = "Ngày xuất";
        ws.Cell(6, 2).Value = DateTime.Now.ToString(DateTimeFormat);
        ws.Range(3, 1, 6, 1).Style.Font.Bold = true;
        ws.Range(3, 2, 6, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        ws.Cell(8, 1).Value = "Chỉ tiêu";
        ws.Cell(8, 2).Value = "Giá trị";
        StyleHeader(ws.Range(8, 1, 8, 2));

        ws.Cell(9, 1).Value = "Doanh thu";
        ws.Cell(9, 2).Value = d.Summary.Revenue;
        ws.Cell(9, 2).Style.NumberFormat.Format = MoneyFormat;

        ws.Cell(10, 1).Value = "Số hóa đơn";
        ws.Cell(10, 2).Value = d.Summary.OrderCount;
        ws.Cell(10, 2).Style.NumberFormat.Format = "#,##0";

        ws.Cell(11, 1).Value = "Giá trị trung bình / hóa đơn";
        ws.Cell(11, 2).Value = d.Summary.AvgOrderValue;
        ws.Cell(11, 2).Style.NumberFormat.Format = MoneyFormat;

        ws.Cell(12, 1).Value = "Tổng tiền đã giảm";
        ws.Cell(12, 2).Value = d.Summary.TotalDiscount;
        ws.Cell(12, 2).Style.NumberFormat.Format = MoneyFormat;

        ws.Cell(13, 1).Value = "So với kỳ trước";
        ws.Cell(13, 2).Value = d.Summary.TrendPercent is double t
            ? $"{t:+0.0;-0.0;0.0}%"
            : "Không có dữ liệu kỳ trước";
        ws.Cell(13, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        StyleTable(ws.Range(8, 1, 13, 2));
    }

    private static void BuildRevenueSheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Doanh thu theo kỳ");
        ws.Cell(1, 1).Value = d.ChartUnit;
        ws.Cell(1, 2).Value = "Doanh thu";
        StyleHeader(ws.Range(1, 1, 1, 2));

        var r = 2;
        foreach (var p in d.Points)
        {
            ws.Cell(r, 1).Value = p.Label;
            ws.Cell(r, 2).Value = p.Revenue;
            ws.Cell(r, 2).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        if (d.Points.Count > 0)
        {
            ws.Cell(r, 1).Value = "Tổng cộng";
            ws.Cell(r, 2).FormulaA1 = $"SUM(B2:B{r - 1})";
            ws.Cell(r, 2).Style.NumberFormat.Format = MoneyFormat;
            ws.Range(r, 1, r, 2).Style.Font.Bold = true;
            ws.Range(r, 1, r, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F3F3");
        }

        StyleTable(ws.Range(1, 1, Math.Max(1, r), 2));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void BuildInvoicesSheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Hóa đơn");
        string[] headers = { "Mã hóa đơn", "Bàn / Kênh", "Thời gian", "Số tiền", "Trạng thái" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var r = 2;
        foreach (var inv in d.Invoices)
        {
            var status = InvoiceStatusLabels[Math.Clamp(inv.Status, 0, InvoiceStatusLabels.Length - 1)];
            ws.Cell(r, 1).Value = $"#INV-{inv.Id}";
            ws.Cell(r, 2).Value = string.IsNullOrEmpty(inv.TableName) ? "Mang về" : inv.TableName;
            ws.Cell(r, 3).Value = inv.InvoiceDate;
            ws.Cell(r, 3).Style.NumberFormat.Format = DateTimeFormat;
            ws.Cell(r, 4).Value = inv.TotalAmount;
            ws.Cell(r, 4).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 5).Value = status;
            r++;
        }

        StyleTable(ws.Range(1, 1, Math.Max(1, r - 1), headers.Length));
        ws.Range(1, 1, Math.Max(1, r - 1), headers.Length).SetAutoFilter();
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void BuildTopItemsSheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Món bán chạy");
        string[] headers = { "Hạng", "Tên món", "Số lượng bán", "Doanh thu" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var r = 2;
        var rank = 1;
        foreach (var it in d.TopItems)
        {
            ws.Cell(r, 1).Value = rank++;
            ws.Cell(r, 2).Value = it.MenuItemName;
            ws.Cell(r, 3).Value = it.QuantitySold;
            ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 4).Value = it.Revenue;
            ws.Cell(r, 4).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        StyleTable(ws.Range(1, 1, Math.Max(1, r - 1), headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void BuildEmployeesSheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Theo nhân viên");
        string[] headers = { "Nhân viên", "Số hóa đơn", "Doanh thu", "Giá trị TB / hóa đơn" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var r = 2;
        foreach (var e in d.Employees)
        {
            ws.Cell(r, 1).Value = e.EmployeeName;
            ws.Cell(r, 2).Value = e.InvoiceCount;
            ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 3).Value = e.Revenue;
            ws.Cell(r, 3).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 4).Value = e.InvoiceCount > 0 ? e.Revenue / e.InvoiceCount : 0m;
            ws.Cell(r, 4).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        StyleTable(ws.Range(1, 1, Math.Max(1, r - 1), headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void BuildCampaignsSheet(XLWorkbook wb, ReportExportData d)
    {
        var ws = wb.Worksheets.Add("Khuyến mãi");
        string[] headers = { "Chiến dịch", "Số hóa đơn áp dụng", "Tổng tiền đã giảm" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var r = 2;
        foreach (var cp in d.Campaigns)
        {
            ws.Cell(r, 1).Value = cp.CampaignName;
            ws.Cell(r, 2).Value = cp.InvoiceCount;
            ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 3).Value = cp.TotalDiscount;
            ws.Cell(r, 3).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        StyleTable(ws.Range(1, 1, Math.Max(1, r - 1), headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = HeaderBg;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StyleTable(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
    }
}