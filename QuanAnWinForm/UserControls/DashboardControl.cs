using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Tong quan (Dashboard) - chi danh cho Role QuanLy. Lay du lieu THAT
// tu ReportsController (doanh thu, don hang, mon ban chay, bieu do theo gio),
// TablesController (so ban dang phuc vu) va CustomersController (khach moi).
public partial class DashboardControl : UserControl
{
    private readonly ApiService _api = new();

    public DashboardControl()
    {
        InitializeComponent();
        WireUpEvents();

        Load += async (s, e) => await LoadAllAsync();
    }

    private void WireUpEvents()
    {
        // Mỗi dòng món bán chạy luôn rộng bằng khung -> giá tiền không bị cắt.
        // (Đặt ở đây, không đặt trong InitializeComponent vì Designer không đọc được lambda.)
        flowTopItems.SizeChanged += (s, e) =>
        {
            foreach (Control c in flowTopItems.Controls)
                if (c is Panel) c.Width = flowTopItems.ClientSize.Width;
        };

        panelChartCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelChartCard.Width - 1, panelChartCard.Height - 1);
        panelTopItemsCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelTopItemsCard.Width - 1, panelTopItemsCard.Height - 1);
    }

    private async Task LoadAllAsync()
    {
        lblSubtitle.Text = $"Cập nhật lúc {DateTime.Now:HH:mm, dd/MM/yyyy}";

        try
        {
            await Task.WhenAll(
                LoadSummaryAsync(),
                LoadTablesAsync(),
                LoadNewCustomersAsync(),
                LoadChartAsync(),
                LoadTopItemsAsync()
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được dữ liệu tổng quan: {ex.Message}");
        }
    }

    private async Task LoadSummaryAsync()
    {
        var summary = await _api.GetReportSummaryAsync(DateTime.Today, DateTime.Today) ?? new RevenueSummaryDto();

        var trendText = summary.TrendPercent.HasValue ? $"{Math.Abs(summary.TrendPercent.Value):0.#}% so với hôm qua" : null;
        var trendUp = summary.TrendPercent is null or >= 0;

        cardRevenue.Configure("💰", AppColors.Primary, AppColors.ErrorContainer, "DOANH THU HÔM NAY", $"{summary.Revenue:N0}đ", trendText, trendUp);
        cardOrders.Configure("🧾", AppColors.Tertiary, Color.FromArgb(224, 242, 241), "SỐ ĐƠN HÀNG", summary.OrderCount.ToString("N0"));
    }

    private async Task LoadTablesAsync()
    {
        // "Bàn mang về" (khu vực hệ thống "Mang về") không phải bàn ăn tại chỗ -> không tính vào thống kê bàn
        var tables = (await _api.GetTablesAsync())
            .Where(t => !string.Equals(t.AreaName?.Trim(), "Mang về", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var busy = tables.Count(t => t.Status == (int)TableStatusEnum.DangPhucVu);
        cardTables.Configure("🪑", AppColors.Secondary, AppColors.SecondaryContainer, "BÀN ĐANG PHỤC VỤ", $"{busy}/{tables.Count}");
    }

    private async Task LoadNewCustomersAsync()
    {
        var customers = await _api.GetCustomersAsync();
        var newToday = customers.Count(c => c.CreatedAt.Date == DateTime.Today);
        cardNewCustomers.Configure("⭐", AppColors.Warning, Color.FromArgb(255, 243, 224), "KHÁCH MỚI HÔM NAY", newToday.ToString("N0"));
    }

    private async Task LoadChartAsync()
    {
        var points = await _api.GetRevenueByHourAsync(DateTime.Today);
        chartPanel.Data = points.Select(p => (p.Label, (double)p.Revenue)).ToList();
        chartPanel.HighlightIndex = points.Count > 0
            ? points.Select((p, i) => (p.Revenue, i)).OrderByDescending(x => x.Revenue).First().i
            : -1;
        chartPanel.Invalidate();
    }

    private async Task LoadTopItemsAsync()
    {
        foreach (var old in flowTopItems.Controls.Cast<Control>().ToList())
        {
            flowTopItems.Controls.Remove(old);
            old.Dispose();
        }

        var topTask = _api.GetTopItemsAsync(DateTime.Today, DateTime.Today, take: 5);
        var menuTask = _api.GetMenuAsync();
        await Task.WhenAll(topTask, menuTask);

        var items = topTask.Result;
        // Báo cáo chỉ trả MenuItemId -> tra ImageUrl từ danh sách thực đơn.
        var imageById = menuTask.Result
            .Where(m => !string.IsNullOrWhiteSpace(m.ImageUrl))
            .ToDictionary(m => m.Id, m => m.ImageUrl!);

        if (items.Count == 0)
        {
            flowTopItems.Controls.Add(new Label { Text = "Chưa có dữ liệu bán hàng hôm nay.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            return;
        }

        foreach (var item in items)
        {
            imageById.TryGetValue(item.MenuItemId, out var imageUrl);
            BuildTopItemRow(flowTopItems, item.MenuItemName, $"Đã bán: {item.QuantitySold}", $"{item.Revenue:N0}đ", imageUrl);
        }
    }

    private async Task LoadTopItemThumbAsync(Panel thumb, Label placeholder, string imageUrl)
    {
        var img = await _api.LoadMenuImageAsync(imageUrl);
        if (img is null) return;
        using (img)
        {
            if (thumb.IsDisposed) return;
            var old = thumb.BackgroundImage;
            thumb.BackgroundImage = CropToFill(img, thumb.Size);
            thumb.BackgroundImageLayout = ImageLayout.None;
            old?.Dispose();
            placeholder.Visible = false;
        }
    }

    // Cắt ảnh cho đầy vừa khung (kiểu "cover"), giữ tỷ lệ, không bị méo.
    private static Bitmap CropToFill(Image src, Size target)
    {
        var bmp = new Bitmap(target.Width, target.Height);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale = Math.Max((float)target.Width / src.Width, (float)target.Height / src.Height);
        float w = target.Width / scale, h = target.Height / scale;
        g.DrawImage(src, new Rectangle(Point.Empty, target),
            (src.Width - w) / 2, (src.Height - h) / 2, w, h, GraphicsUnit.Pixel);
        return bmp;
    }
}