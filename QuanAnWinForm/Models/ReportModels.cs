namespace QuanAnWinForm.Models;

public class RevenueSummaryDto
{
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
    public decimal AvgOrderValue { get; set; }
    public decimal TotalDiscount { get; set; }
    public double? TrendPercent { get; set; }
}

public class RevenuePointDto
{
    public string Label { get; set; } = "";
    public decimal Revenue { get; set; }
}

public class RecentInvoiceReportDto
{
    public int Id { get; set; }
    public string TableName { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
}

public class TopItemDto
{
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class EmployeeRevenueDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal Revenue { get; set; }
}

public class CampaignReportDto
{
    public int? CampaignId { get; set; }
    public string CampaignName { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal TotalDiscount { get; set; }
}