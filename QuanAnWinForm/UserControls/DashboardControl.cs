using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Tong quan (Dashboard) - chi danh cho Role QuanLy. Lay du lieu THAT
// tu ReportsController (doanh thu, don hang, mon ban chay, bieu do theo gio),
// TablesController (so ban dang phuc vu) va CustomersController (khach moi).
public partial class DashboardControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => LoadAllAsync(silent: true);

    private readonly ApiService _api = new();
    private readonly System.Windows.Forms.Timer _realtimeTimer =
        new() { Interval = 700 };
    private bool _realtimeRunning;
    private bool _realtimeDirty;

    public DashboardControl()
    {
        InitializeComponent();
        WireUpEvents();

        _realtimeTimer.Tick += async (s, e) =>
        {
            _realtimeTimer.Stop();
            if (_realtimeRunning)
            {
                _realtimeDirty = true;
                return;
            }

            _realtimeRunning = true;
            try
            {
                _realtimeDirty = false;
                await LoadAllAsync(silent: true);
            }
            finally
            {
                _realtimeRunning = false;
                if (_realtimeDirty && !IsDisposed && Visible)
                    _realtimeTimer.Start();
            }
        };

        Load += async (s, e) =>
        {
            SubscribeRealtime();
            await LoadAllAsync();
        };

        VisibleChanged += (s, e) =>
        {
            if (Visible && _realtimeDirty)
                QueueRealtimeRefresh();
        };

        Disposed += (s, e) =>
        {
            UnsubscribeRealtime();
            _realtimeTimer.Stop();
            _realtimeTimer.Dispose();
        };
    }

    private void SubscribeRealtime()
    {
        UnsubscribeRealtime();
        NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
        NotificationHubClient.OrderUpdated += OnOrderUpdated;
        NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
        NotificationHubClient.ConnectionRestored += OnConnectionRestored;
    }

    private void UnsubscribeRealtime()
    {
        NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
        NotificationHubClient.OrderUpdated -= OnOrderUpdated;
        NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
        NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
    }

    private void OnPaymentCompleted(PaymentCompletedPayload _) => QueueRealtimeRefresh();
    private void OnOrderUpdated(OrderUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnTableStatusUpdated(TableStatusUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnConnectionRestored() => QueueRealtimeRefresh();

    private void QueueRealtimeRefresh()
    {
        if (IsDisposed) return;
        _realtimeDirty = true;
        if (!Visible || !IsHandleCreated) return;

        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed || !Visible) return;
                _realtimeTimer.Stop();
                _realtimeTimer.Start();
            });
        }
        catch (InvalidOperationException) { }
    }

    private void WireUpEvents()
    {
        // Mỗi dòng món bán chạy luôn rộng bằng khung -> giá tiền không bị cắt.
        // (Đặt ở đây, không đặt trong InitializeComponent vì Designer không đọc được lambda.)
        flowTopItems.SizeChanged += (s, e) => ApplyTopRowSizes();

        panelChartCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelChartCard.Width - 1, panelChartCard.Height - 1);
        panelTopItemsCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelTopItemsCard.Width - 1, panelTopItemsCard.Height - 1);

        BuildTopItemsToggle();
    }

    private async Task LoadAllAsync(bool silent = false)
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
            if (!silent && !IsDisposed)
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

    private const int TopTake = 5;      // số món hiển thị
    private int _topItemsVersion;   // chống 2 lần tải chồng nhau làm trùng dòng

    // Chiều cao mỗi dòng = chia đều chiều cao khung cho TopTake dòng => lấp đầy khung, không dư trống.
    private int TopRowHeight() => Math.Clamp(flowTopItems.ClientSize.Height / TopTake, 64, 108);

    private void ApplyTopRowSizes()
    {
        var size = new Size(flowTopItems.ClientSize.Width, TopRowHeight());
        foreach (Control c in flowTopItems.Controls)
            if (c is Panel) c.Size = size;
    }

    private void BuildTopItemRowSized(FlowLayoutPanel parent, string name, string sold, string revenue, string? imageUrl)
    {
        var row = new Panel
        {
            Size = new Size(Math.Max(200, parent.ClientSize.Width), TopRowHeight()),
            Margin = Padding.Empty,
        };
        var thumb = new Panel { BackColor = AppColors.SurfaceContainer };
        var lblPlaceholder = new Label
        {
            Text = "🍽",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 14),
            ForeColor = AppColors.Outline,
        };
        thumb.Controls.Add(lblPlaceholder);
        thumb.Disposed += (s, e) => thumb.BackgroundImage?.Dispose();

        var lblName = new Label
        {
            Text = name,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var lblSold = new Label
        {
            Text = sold,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var lblRevenue = new Label
        {
            Text = revenue,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = AppColors.Primary,
            AutoSize = true,
        };

        void LayoutRow()
        {
            int rh = row.Height;
            int ts = Math.Clamp(rh - 20, 48, 80);
            thumb.SetBounds(0, (rh - ts) / 2, ts, ts);

            int textLeft = ts + 14;
            lblRevenue.Location = new Point(
                Math.Max(textLeft, row.Width - lblRevenue.PreferredWidth),
                (rh - lblRevenue.PreferredHeight) / 2);

            int nameW = Math.Max(40, lblRevenue.Left - textLeft - 8);
            int top = (rh - 46) / 2;
            lblName.SetBounds(textLeft, top, nameW, 24);
            lblSold.SetBounds(textLeft, top + 24, nameW, 22);
            row.Invalidate();
        }

        row.Controls.Add(thumb);
        row.Controls.Add(lblName);
        row.Controls.Add(lblSold);
        row.Controls.Add(lblRevenue);
        row.Resize += (s, e) => LayoutRow();
        row.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };
        LayoutRow();

        parent.Controls.Add(row);

        if (!string.IsNullOrWhiteSpace(imageUrl))
            _ = LoadTopItemThumbAsync(thumb, lblPlaceholder, imageUrl);
    }

    // ---- Bộ chọn kỳ cho "Món bán chạy": Hôm nay | Tháng này ----
    private bool _topByMonth = true;   // mặc định: Tháng này
    private readonly Button _btnTopToday = new() { Text = "Hôm nay" };
    private readonly Button _btnTopMonth = new() { Text = "Tháng này" };

    private void BuildTopItemsToggle()
    {
        foreach (var b in new[] { _btnTopToday, _btnTopMonth })
        {
            b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            b.Padding = Padding.Empty;
            b.Size = new Size(TextRenderer.MeasureText(b.Text, b.Font).Width + 36, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            panelTopItemsCard.Controls.Add(b);
            b.BringToFront();
        }

        _btnTopToday.Click += async (s, e) => await SetTopPeriodAsync(false);
        _btnTopMonth.Click += async (s, e) => await SetTopPeriodAsync(true);
        panelTopItemsCard.Resize += (s, e) => PositionTopToggle();

        ApplyTopToggleStyle();
        PositionTopToggle();
    }

    private void PositionTopToggle()
    {
        int right = panelTopItemsCard.ClientSize.Width - 20;
        _btnTopMonth.Location = new Point(right - _btnTopMonth.Width, 14);
        _btnTopToday.Location = new Point(_btnTopMonth.Left - _btnTopToday.Width + 1, 14);
    }

    private void ApplyTopToggleStyle()
    {
        static void Style(Button b, bool active)
        {
            b.BackColor = active ? AppColors.Primary : Color.White;
            b.ForeColor = active ? Color.White : AppColors.OnSurface;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = active ? AppColors.Primary : AppColors.OutlineVariant;
        }

        Style(_btnTopToday, !_topByMonth);
        Style(_btnTopMonth, _topByMonth);
        lblTopItemsTitle.Text = _topByMonth ? "Món bán chạy tháng này" : "Món bán chạy hôm nay";
    }

    private async Task SetTopPeriodAsync(bool byMonth)
    {
        if (_topByMonth == byMonth) return;
        _topByMonth = byMonth;
        ApplyTopToggleStyle();

        try { await LoadTopItemsAsync(); }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show($"Không tải được món bán chạy: {ex.Message}");
        }
    }

    private async Task LoadTopItemsAsync()
    {
        var version = ++_topItemsVersion;

        // Tải dữ liệu TRƯỚC, chưa đụng tới giao diện.
        var today = DateTime.Today;
        var from = _topByMonth ? new DateTime(today.Year, today.Month, 1) : today;
        var topTask = _api.GetTopItemsAsync(from, today, take: TopTake);
        var menuTask = _api.GetMenuAsync();
        await Task.WhenAll(topTask, menuTask);

        // Đã có lần tải mới hơn (hoặc control đã đóng) -> bỏ kết quả cũ.
        if (version != _topItemsVersion || IsDisposed) return;

        var items = topTask.Result;
        // Báo cáo chỉ trả MenuItemId -> tra ImageUrl từ danh sách thực đơn.
        var imageById = menuTask.Result
            .Where(m => !string.IsNullOrWhiteSpace(m.ImageUrl))
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => g.First().ImageUrl!);

        // Xóa + dựng lại liền mạch, KHÔNG có await ở giữa => không thể bị trùng dòng.
        flowTopItems.SuspendLayout();
        foreach (var old in flowTopItems.Controls.Cast<Control>().ToList())
        {
            flowTopItems.Controls.Remove(old);
            old.Dispose();
        }

        if (items.Count == 0)
        {
            flowTopItems.Controls.Add(new Label { Text = _topByMonth ? "Chưa có dữ liệu bán hàng tháng này." : "Chưa có dữ liệu bán hàng hôm nay.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        }
        else
        {
            foreach (var item in items)
            {
                imageById.TryGetValue(item.MenuItemId, out var imageUrl);
                BuildTopItemRowSized(flowTopItems, item.MenuItemName, $"Đã bán: {item.QuantitySold}", $"{item.Revenue:N0}đ", imageUrl);
            }
        }
        flowTopItems.ResumeLayout(true);
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
            thumb.BackgroundImageLayout = ImageLayout.Zoom;
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