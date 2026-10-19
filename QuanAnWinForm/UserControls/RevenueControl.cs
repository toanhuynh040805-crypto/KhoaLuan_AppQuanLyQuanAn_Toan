using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh "Thong ke & Bao cao" - danh cho QuanLy, ThuNgan. Gom 4 loai bao cao
// dung yeu cau de cuong (khoan 5): Doanh thu, Mon an ban chay, Theo nhan vien,
// Khuyen mai - dung CHUNG 1 bo loc thoi gian (cbPeriod) o goc tren ben phai.
public partial class RevenueControl : UserControl
{
    private readonly ApiService _api = new();
    private DateTime _from = DateTime.Today;
    private DateTime _to = DateTime.Today;

    private static readonly string[] InvoiceStatusLabels = { "Chưa thanh toán", "Đã thanh toán", "Đã hủy" };

    public RevenueControl()
    {
        InitializeComponent();
        WireUpEvents();
        SetupGridColumns();

        cbPeriod.SelectedIndex = 0;
        cbPeriod.SelectedIndexChanged += async (s, e) => await ReloadAllAsync();
        btnRefresh.Click += async (s, e) => await ReloadAllAsync();

        Load += async (s, e) => await ReloadAllAsync();
    }

    // Xem giai thich chi tiet trong DashboardControl.cs: lambda khong duoc dat trong .Designer.cs.
    private void WireUpEvents()
    {
        panelChartCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelChartCard.Width - 1, panelChartCard.Height - 1);
        panelInvoicesCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelInvoicesCard.Width - 1, panelInvoicesCard.Height - 1);
    }

    // Cau hinh cot cho 3 grid moi (Mon an / Nhan vien / Khuyen mai) bang code thay
    // vi Designer.cs cho gon - grid "Hoa don gan day" (gridInvoices) van giu khai
    // bao cot san trong Designer.cs nhu ban goc.
    private void SetupGridColumns()
    {
        gridTopItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridTopItems.Columns.Add("colRank", "Hạng");
        gridTopItems.Columns.Add("colItemName", "Tên món");
        gridTopItems.Columns.Add("colQty", "Số lượng bán");
        gridTopItems.Columns.Add("colItemRevenue", "Doanh thu");
        foreach (DataGridViewColumn c in gridTopItems.Columns) c.ReadOnly = true;

        gridByEmployee.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridByEmployee.Columns.Add("colEmpName", "Nhân viên");
        gridByEmployee.Columns.Add("colEmpInvoiceCount", "Số hóa đơn");
        gridByEmployee.Columns.Add("colEmpRevenue", "Doanh thu");
        gridByEmployee.Columns.Add("colEmpAvg", "Giá trị TB/hóa đơn");
        foreach (DataGridViewColumn c in gridByEmployee.Columns) c.ReadOnly = true;

        gridCampaigns.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridCampaigns.Columns.Add("colCampaignName", "Chiến dịch");
        gridCampaigns.Columns.Add("colCampaignInvoiceCount", "Số hóa đơn áp dụng");
        gridCampaigns.Columns.Add("colCampaignDiscount", "Tổng tiền đã giảm");
        foreach (DataGridViewColumn c in gridCampaigns.Columns) c.ReadOnly = true;
    }

    // Quy doi lua chon trong cbPeriod thanh khoang ngay [_from, _to] (2 dau bao gom).
    private void ResolvePeriod()
    {
        var today = DateTime.Today;
        switch (cbPeriod.SelectedIndex)
        {
            case 1: // 7 ngày qua
                _from = today.AddDays(-6);
                _to = today;
                break;
            case 2: // 30 ngày qua
                _from = today.AddDays(-29);
                _to = today;
                break;
            case 3: // Tháng này
                _from = new DateTime(today.Year, today.Month, 1);
                _to = today;
                break;
            default: // Hôm nay
                _from = today;
                _to = today;
                break;
        }
    }

    private async Task ReloadAllAsync()
    {
        ResolvePeriod();
        lblSubtitle.Text = _from == _to
            ? $"Ngày {_from:dd/MM/yyyy}"
            : $"Từ {_from:dd/MM/yyyy} đến {_to:dd/MM/yyyy}";

        try
        {
            await Task.WhenAll(
                LoadSummaryAsync(),
                LoadChartAsync(),
                LoadRecentInvoicesAsync(),
                LoadTopItemsAsync(),
                LoadByEmployeeAsync(),
                LoadCampaignsAsync()
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được báo cáo: {ex.Message}");
        }
    }

    private async Task LoadSummaryAsync()
    {
        var summary = await _api.GetReportSummaryAsync(_from, _to) ?? new RevenueSummaryDto();

        var trendText = summary.TrendPercent.HasValue ? $"{Math.Abs(summary.TrendPercent.Value):0.#}% so với kỳ trước" : null;
        var trendUp = summary.TrendPercent is null or >= 0;

        cardRevenue.Configure("💰", AppColors.Primary, AppColors.ErrorContainer, "DOANH THU", $"{summary.Revenue:N0}đ", trendText, trendUp);
        cardOrders.Configure("🧾", AppColors.Tertiary, Color.FromArgb(224, 242, 241), "SỐ HÓA ĐƠN", summary.OrderCount.ToString("N0"));
        cardCost.Configure("📊", AppColors.Secondary, AppColors.SecondaryContainer, "GIÁ TRỊ TB/HÓA ĐƠN", $"{summary.AvgOrderValue:N0}đ");
        cardProfit.Configure("🏷️", AppColors.Warning, Color.FromArgb(255, 243, 224), "TỔNG TIỀN ĐÃ GIẢM", $"{summary.TotalDiscount:N0}đ");
    }

    private async Task LoadChartAsync()
    {
        var points = await _api.GetRevenueByPeriodAsync(_from, _to);
        chartPanel.Data = points.Select(p => (p.Label, (double)p.Revenue)).ToList();
        chartPanel.HighlightIndex = points.Count - 1;
        chartPanel.Invalidate();
    }

    private async Task LoadRecentInvoicesAsync()
    {
        var invoices = await _api.GetRecentInvoicesReportAsync(_from, _to, take: 15);
        gridInvoices.Rows.Clear();
        foreach (var inv in invoices)
        {
            var statusIndex = Math.Clamp(inv.Status, 0, InvoiceStatusLabels.Length - 1);
            gridInvoices.Rows.Add(
                $"#INV-{inv.Id}",
                string.IsNullOrEmpty(inv.TableName) ? "Mang về" : inv.TableName,
                inv.InvoiceDate.ToString("HH:mm, dd/MM"),
                $"{inv.TotalAmount:N0} đ",
                InvoiceStatusLabels[statusIndex]
            );
        }
    }

    private async Task LoadTopItemsAsync()
    {
        var items = await _api.GetTopItemsAsync(_from, _to, take: 10);
        gridTopItems.Rows.Clear();
        var rank = 1;
        foreach (var item in items)
        {
            gridTopItems.Rows.Add(rank++, item.MenuItemName, item.QuantitySold.ToString("N0"), $"{item.Revenue:N0}đ");
        }
        if (items.Count == 0)
            gridTopItems.Rows.Add("", "(Chưa có dữ liệu trong khoảng thời gian này)", "", "");
    }

    private async Task LoadByEmployeeAsync()
    {
        var rows = await _api.GetRevenueByEmployeeAsync(_from, _to);
        gridByEmployee.Rows.Clear();
        foreach (var r in rows)
        {
            var avg = r.InvoiceCount > 0 ? r.Revenue / r.InvoiceCount : 0;
            gridByEmployee.Rows.Add(r.EmployeeName, r.InvoiceCount.ToString("N0"), $"{r.Revenue:N0}đ", $"{avg:N0}đ");
        }
        if (rows.Count == 0)
            gridByEmployee.Rows.Add("(Chưa có dữ liệu trong khoảng thời gian này)", "", "", "");
    }

    private async Task LoadCampaignsAsync()
    {
        var rows = await _api.GetCampaignReportAsync(_from, _to);
        gridCampaigns.Rows.Clear();
        foreach (var r in rows)
        {
            gridCampaigns.Rows.Add(r.CampaignName, r.InvoiceCount.ToString("N0"), $"{r.TotalDiscount:N0}đ");
        }
        if (rows.Count == 0)
            gridCampaigns.Rows.Add("(Chưa có chiến dịch nào được áp dụng trong khoảng thời gian này)", "", "");
    }
}