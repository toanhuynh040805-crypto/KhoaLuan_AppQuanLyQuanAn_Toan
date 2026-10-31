using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QuanAnWinForm.Utils;
using System.Drawing.Drawing2D;

namespace QuanAnWinForm.UserControls;

// Màn hình Đơn hàng.
//  - Trên cùng: tiêu đề + 4 thẻ thống kê của ngày đang xem.
//  - Thanh lọc: ngày, trạng thái (lọc trạng thái làm ngay trên máy, không gọi lại API).
//  - Thân trang: bảng danh sách đơn (co giãn theo cửa sổ) + khung chi tiết đơn rộng cố định.
// Các dòng đơn / dòng món được tự vẽ (OnPaint) nên nhẹ và luôn thẳng cột.
public partial class OrderManagementControl : UserControl, IRefreshableContent
{
    private readonly ApiService _api = new();
    private readonly int _userId;

    private List<OrderSummaryDto> _allOrders = new();   // toàn bộ đơn của ngày
    private List<OrderSummaryDto> _orders = new();      // sau khi lọc theo trạng thái
    private readonly List<OrderRowPanel> _orderRows = new();
    private readonly List<ItemRowPanel> _itemRows = new();

    private int? _selectedOrderId;
    private int _loadVersion;     // chống kết quả cũ ghi đè kết quả mới khi đổi ngày liên tục
    private int _detailVersion;   // chống chi tiết cũ ghi đè khi bấm nhanh nhiều đơn
    private int _orderScrollBar;  // bề rộng thanh cuộn đang chiếm ở danh sách (để tiêu đề cột thẳng hàng)
    private readonly System.Windows.Forms.Timer _realtimeRefreshTimer =
        new() { Interval = 350 };
    private bool _realtimeDirty;
    private bool _realtimeRefreshRunning;

    // Trạng thái nhãn bo tròn ở phần đầu chi tiết
    private string _pillText = "";
    private Color _pillFg = Color.Black;
    private Color _pillBg = Color.White;

    private static readonly string[] OrderStatusLabels = { "Đang mở", "Đã thanh toán", "Đã hủy" };
    private static readonly string[] KitchenStatusLabels = { "Mới gọi", "Đang chế biến", "Đã chế biến", "Đã phục vụ", "Đã hủy" };

    // ---- Font dùng chung (AppFonts.* tạo Font mới mỗi lần gọi nên phải cache khi vẽ tay) ----
    private static readonly Font FontTitle = new("Segoe UI", 11F, FontStyle.Bold);
    private static readonly Font FontTotal = new("Segoe UI", 12F, FontStyle.Bold);
    private static readonly Font FontBody = new("Segoe UI", 10F);
    private static readonly Font FontBodyBold = new("Segoe UI", 10F, FontStyle.Bold);
    private static readonly Font FontStrike = new("Segoe UI", 10F, FontStyle.Bold | FontStyle.Strikeout);
    private static readonly Font FontSmall = new("Segoe UI", 9F);
    private static readonly Font FontCaption = new("Segoe UI", 8.5F, FontStyle.Bold);

    private static readonly Color SelectedBg = Color.FromArgb(253, 236, 231);

    private const TextFormatFlags Tf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
    private const TextFormatFlags TfRight = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
    private const TextFormatFlags TfCenter = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    // Hình học các cột trong dòng đơn
    private const int ColPadL = 22;        // lề trái nội dung (sau dải màu trạng thái)
    private const int ColPadR = 20;        // lề phải
    private const int RightBlockW = 150;   // khối "tổng tiền + trạng thái" bên phải
    private const int LeftColW = 250;      // cột "Đơn #.. / vị trí" ở chế độ rộng
    private const int WideMin = 900;       // dòng rộng từ mức này mới hiện thêm các cột Nhân viên / Thời gian / Nguồn

    public OrderManagementControl() : this(0) { }

    public OrderManagementControl(int userId)
    {
        _userId = userId;
        InitializeComponent();
        StyleControls();

        cbFilterStatus.Items.Add("Tất cả trạng thái");
        cbFilterStatus.Items.AddRange(OrderStatusLabels.Cast<object>().ToArray());
        cbFilterStatus.SelectedIndex = 0;
        dtpFilterDate.Value = DateTime.Today;

        ShowDetailEmpty();
        UpdateStats();

        _realtimeRefreshTimer.Tick += async (s, e) =>
        {
            _realtimeRefreshTimer.Stop();
            if (_realtimeRefreshRunning)
            {
                _realtimeDirty = true;
                return;
            }

            _realtimeRefreshRunning = true;
            try
            {
                _realtimeDirty = false;
                await LoadOrdersAsync();
            }
            finally
            {
                _realtimeRefreshRunning = false;
                if (_realtimeDirty && !IsDisposed && Visible)
                    _realtimeRefreshTimer.Start();
            }
        };

        Load += async (s, e) =>
        {
            SubscribeRealtime();
            await LoadOrdersAsync();
        };

        VisibleChanged += (s, e) =>
        {
            if (Visible && _realtimeDirty)
                QueueRealtimeRefresh();
        };

        Disposed += (s, e) =>
        {
            UnsubscribeRealtime();
            _realtimeRefreshTimer.Stop();
            _realtimeRefreshTimer.Dispose();
        };
        dtpFilterDate.ValueChanged += async (s, e) =>
        {
            _selectedOrderId = null;
            ShowDetailEmpty();
            await LoadOrdersAsync();
        };
        cbFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();
        btnToday.Click += (s, e) =>
        {
            if (dtpFilterDate.Value.Date == DateTime.Today) _ = LoadOrdersAsync();
            else dtpFilterDate.Value = DateTime.Today;
        };
        btnRefresh.Click += async (s, e) => await LoadOrdersAsync();
        btnCancelOrder.Click += async (s, e) => await CancelCurrentOrderAsync();

        panelOrderScroll.Resize += (s, e) => LayoutOrderRows(reposition: false);
        panelItems.Resize += (s, e) => LayoutItemRows(reposition: false);
    }

    public async Task RefreshDataAsync()
    {
        _realtimeDirty = false;
        await LoadOrdersAsync();
    }

    private void SubscribeRealtime()
    {
        UnsubscribeRealtime();
        NotificationHubClient.OrderUpdated += OnOrderUpdated;
        NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
        NotificationHubClient.ItemStatusUpdated += OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled += OnOrderItemCancelled;
        NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
        NotificationHubClient.ConnectionRestored += OnConnectionRestored;
    }

    private void UnsubscribeRealtime()
    {
        NotificationHubClient.OrderUpdated -= OnOrderUpdated;
        NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
        NotificationHubClient.ItemStatusUpdated -= OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled -= OnOrderItemCancelled;
        NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
        NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
    }

    private void OnOrderUpdated(OrderUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnPaymentCompleted(PaymentCompletedPayload _) => QueueRealtimeRefresh();
    private void OnItemStatusUpdated(ItemStatusUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnOrderItemCancelled(OrderItemCancelledPayload _) => QueueRealtimeRefresh();
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
                _realtimeRefreshTimer.Stop();
                _realtimeRefreshTimer.Start();
            });
        }
        catch (InvalidOperationException)
        {
            // Control đang đóng.
        }
    }

    // =====================================================================
    // GIAO DIỆN TĨNH (viền khung, tiêu đề cột, nhãn trạng thái)
    // =====================================================================

    private void StyleControls()
    {
        static void BorderOnPaint(Panel p) => p.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        BorderOnPaint(panelListCard);
        BorderOnPaint(panelDetail);

        panelListHeader.Paint += PanelListHeader_Paint;

        panelItemsHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(pen, 0, panelItemsHeader.Height - 1, panelItemsHeader.Width, panelItemsHeader.Height - 1);
        };

        panelDetailFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(pen, 0, 0, panelDetailFooter.Width, 0);
        };

        pillDetailStatus.Paint += (s, e) =>
        {
            if (_pillText.Length == 0) return;
            var r = new Rectangle(0, 0, pillDetailStatus.Width - 1, pillDetailStatus.Height - 1);
            FillPill(e.Graphics, r, _pillBg);
            TextRenderer.DrawText(e.Graphics, _pillText, FontCaption, pillDetailStatus.ClientRectangle, _pillFg, TfCenter);
        };
    }

    // Tiêu đề cột của bảng danh sách (dùng chung hình học với OrderRowPanel nên luôn thẳng cột)
    private void PanelListHeader_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        int w = panelListHeader.Width - _orderScrollBar;
        var (wide, midX, colW) = GetColumns(w);
        int h = panelListHeader.Height;

        using (var pen = new Pen(AppColors.OutlineVariant))
            g.DrawLine(pen, 0, h - 1, panelListHeader.Width, h - 1);

        TextRenderer.DrawText(g, "ĐƠN HÀNG", FontCaption, new Rectangle(ColPadL, 0, LeftColW, h), AppColors.OnSurfaceVariant, Tf);
        if (wide)
        {
            TextRenderer.DrawText(g, "NHÂN VIÊN", FontCaption, new Rectangle(midX, 0, colW - 8, h), AppColors.OnSurfaceVariant, Tf);
            TextRenderer.DrawText(g, "THỜI GIAN", FontCaption, new Rectangle(midX + colW, 0, colW - 8, h), AppColors.OnSurfaceVariant, Tf);
            TextRenderer.DrawText(g, "NGUỒN", FontCaption, new Rectangle(midX + colW * 2, 0, colW - 8, h), AppColors.OnSurfaceVariant, Tf);
        }
        TextRenderer.DrawText(g, "TỔNG TIỀN / TRẠNG THÁI", FontCaption,
            new Rectangle(w - ColPadR - RightBlockW - 30, 0, RightBlockW + 30, h), AppColors.OnSurfaceVariant, TfRight);
    }

    // =====================================================================
    // TẢI & LỌC DANH SÁCH
    // =====================================================================

    private async Task LoadOrdersAsync()
    {
        var version = ++_loadVersion;
        btnRefresh.Enabled = false;
        try
        {
            // Luôn tải CẢ ngày (không lọc trạng thái trên server) để thẻ thống kê luôn đủ số liệu;
            // lọc theo trạng thái làm tại chỗ trong ApplyFilter().
            var list = await _api.GetOrdersAsync(dtpFilterDate.Value.Date, null);
            if (version != _loadVersion) return;

            _allOrders = list;
            ApplyFilter();

            // Đơn đang xem còn trong danh sách -> tải lại chi tiết để thấy trạng thái mới nhất
            if (_selectedOrderId is int selected) await ShowOrderDetailAsync(selected);
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            _allOrders = new();
            _orders = new();
            UpdateStats();
            RenderOrderList();
            lblEmptyList.Text = $"Không tải được danh sách đơn: {ex.Message}";
            lblEmptyList.ForeColor = AppColors.Error;
            lblEmptyList.Visible = true;
        }
        finally
        {
            if (version == _loadVersion) btnRefresh.Enabled = true;
        }
    }

    private void ApplyFilter()
    {
        int idx = cbFilterStatus.SelectedIndex;
        _orders = idx > 0
            ? _allOrders.Where(o => o.Status == idx - 1).ToList()
            : _allOrders.ToList();

        UpdateStats();
        lblCount.Text = $"{_orders.Count} đơn hàng";

        // Đơn đang chọn không còn trong danh sách đã lọc -> bỏ chọn
        if (_selectedOrderId is int selected && !_orders.Any(o => o.Id == selected))
        {
            _selectedOrderId = null;
            ShowDetailEmpty();
        }

        RenderOrderList();
    }

    private void UpdateStats()
    {
        int total = _allOrders.Count;
        int open = _allOrders.Count(o => o.Status == 0);
        int paid = _allOrders.Count(o => o.Status == 1);
        decimal revenue = _allOrders.Where(o => o.Status == 1).Sum(o => o.TotalAmount);

        cardTotal.Configure("🧾", AppColors.Primary, AppColors.ErrorContainer, "TỔNG SỐ ĐƠN", total.ToString("N0"));
        cardOpen.Configure("⏳", Color.FromArgb(166, 106, 0), Color.FromArgb(255, 243, 224), "ĐANG MỞ", open.ToString("N0"));
        cardPaid.Configure("✅", AppColors.Tertiary, Color.FromArgb(224, 242, 241), "ĐÃ THANH TOÁN", paid.ToString("N0"));
        cardRevenue.Configure("💰", AppColors.Secondary, AppColors.SecondaryContainer, "DOANH THU", $"{revenue:N0}đ");
    }

    private void RenderOrderList()
    {
        panelOrderScroll.SuspendLayout();

        var old = _orderRows.ToList();
        _orderRows.Clear();
        panelOrderScroll.Controls.Clear();
        foreach (var r in old) r.Dispose();

        foreach (var order in _orders)
        {
            var id = order.Id;
            var row = new OrderRowPanel(order) { Selected = id == _selectedOrderId };
            row.Click += async (s, e) => await SelectOrderAsync(id);
            _orderRows.Add(row);
        }
        panelOrderScroll.Controls.AddRange(_orderRows.ToArray());

        if (_orders.Count == 0)
        {
            lblEmptyList.Text = _allOrders.Count == 0
                ? $"Chưa có đơn hàng nào trong ngày {dtpFilterDate.Value:dd/MM/yyyy}."
                : "Không có đơn hàng nào ở trạng thái này.";
            lblEmptyList.ForeColor = AppColors.OnSurfaceVariant;
        }
        lblEmptyList.Visible = _orders.Count == 0;

        panelOrderScroll.ResumeLayout(true);
        LayoutOrderRows(reposition: true);
    }

    private void LayoutOrderRows(bool reposition)
    {
        _orderScrollBar = LayoutStack(panelOrderScroll, _orderRows, reposition);
        panelListHeader.Invalidate();
    }

    private void LayoutItemRows(bool reposition) => LayoutStack(panelItems, _itemRows, reposition);

    // Xếp các dòng nối tiếp nhau, tràn hết bề ngang vùng chứa (trừ thanh cuộn nếu có).
    // Trả về bề rộng thanh cuộn đang chiếm chỗ.
    private static int LayoutStack(Panel host, IReadOnlyList<Control> rows, bool reposition)
    {
        if (rows.Count == 0) return 0;

        int totalHeight = rows.Sum(r => r.Height);
        bool needScroll = totalHeight > host.Height;
        int scrollBar = needScroll ? SystemInformation.VerticalScrollBarWidth : 0;
        int width = Math.Max(50, host.Width - scrollBar);

        if (reposition) host.AutoScrollPosition = Point.Empty;

        int y = 0;
        foreach (var r in rows)
        {
            if (reposition) r.Location = new Point(0, y);
            r.Width = width;
            y += r.Height;
        }
        return scrollBar;
    }

    // =====================================================================
    // CHI TIẾT ĐƠN
    // =====================================================================

    private async Task SelectOrderAsync(int orderId)
    {
        _selectedOrderId = orderId;
        foreach (var r in _orderRows) r.Selected = r.Order.Id == orderId;
        await ShowOrderDetailAsync(orderId);
    }

    private void ShowDetailEmpty()
    {
        _detailVersion++;
        ClearItemRows();
        panelDetailContent.Visible = false;
        panelDetailEmpty.Visible = true;
    }

    private void ClearItemRows()
    {
        var old = _itemRows.ToList();
        _itemRows.Clear();
        panelItems.Controls.Clear();
        foreach (var r in old) r.Dispose();
    }

    private async Task ShowOrderDetailAsync(int orderId)
    {
        var version = ++_detailVersion;
        var summary = _allOrders.FirstOrDefault(o => o.Id == orderId);

        panelDetailEmpty.Visible = false;
        panelDetailContent.Visible = true;

        // Hiện ngay phần đầu từ dữ liệu danh sách trong lúc chờ tải danh sách món
        FillDetailHead(orderId, summary, null);
        ClearItemRows();
        lblItemsCaption.Text = "ĐANG TẢI MÓN...";
        SetFooter(summary?.TotalAmount ?? 0, canCancel: false);

        OrderDto? order;
        try
        {
            order = await _api.GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            if (version != _detailVersion) return;
            lblItemsCaption.Text = $"KHÔNG TẢI ĐƯỢC CHI TIẾT: {ex.Message}";
            return;
        }

        if (version != _detailVersion) return;
        if (order is null)
        {
            lblItemsCaption.Text = "KHÔNG TÌM THẤY ĐƠN HÀNG NÀY";
            return;
        }

        FillDetailHead(orderId, summary, order);

        panelItems.SuspendLayout();
        ClearItemRows();
        foreach (var item in order.OrderItems)
            _itemRows.Add(BuildItemRow(item, order.Status));
        panelItems.Controls.AddRange(_itemRows.ToArray());
        panelItems.ResumeLayout(true);

        lblItemsCaption.Text = order.OrderItems.Count == 0
            ? "CHƯA CÓ MÓN NÀO"
            : $"MÓN ĐÃ GỌI ({order.OrderItems.Count})";
        LayoutItemRows(reposition: true);

        SetFooter(order.TotalAmount, canCancel: order.Status == 0);
    }

    private void FillDetailHead(int orderId, OrderSummaryDto? summary, OrderDto? order)
    {
        int status = order?.Status ?? summary?.Status ?? 0;
        int statusIndex = Math.Clamp(status, 0, OrderStatusLabels.Length - 1);
        var (fg, bg) = OrderStatusPalette(statusIndex);

        lblDetailTitle.Text = $"Đơn #{orderId}";
        SetDetailPill(OrderStatusLabels[statusIndex], fg, bg);

        // Đơn mang về không có bàn -> lấy tên hiển thị từ danh sách (VD "Mang về MV-003")
        string place = !string.IsNullOrWhiteSpace(summary?.TableName) ? summary!.TableName
            : order?.Table?.TableName is { Length: > 0 } t ? t
            : "Mang về";
        lblInfoPlace.Text = place;
        lblInfoStaff.Text = string.IsNullOrWhiteSpace(summary?.EmployeeName) ? "—" : summary!.EmployeeName;

        DateTime? time = order?.CreatedAt ?? summary?.OrderTime;
        lblInfoTime.Text = time.HasValue
            ? time.Value.ToString("HH:mm dd/MM/yyyy") +
              (status == 0 ? $" · {TimeDisplay.FormatElapsedCompact(time.Value)}" : "")
            : "—";

        string? source = order?.Source ?? summary?.Source;
        lblInfoSource.Text = DataSources.Label(source);
        lblInfoSource.ForeColor = string.Equals(source, DataSources.Web, StringComparison.OrdinalIgnoreCase)
            ? AppColors.Secondary : AppColors.OnSurface;
    }

    private void SetDetailPill(string text, Color fg, Color bg)
    {
        _pillText = text;
        _pillFg = fg;
        _pillBg = bg;

        int w = TextRenderer.MeasureText(text, FontCaption, Size.Empty, TextFormatFlags.NoPadding).Width + 26;
        pillDetailStatus.Size = new Size(w, 24);
        pillDetailStatus.Left = lblDetailTitle.Left + lblDetailTitle.PreferredWidth + 12;
        pillDetailStatus.Top = lblDetailTitle.Top + Math.Max(0, (lblDetailTitle.PreferredHeight - 24) / 2);
        pillDetailStatus.Invalidate();
    }

    private void SetFooter(decimal total, bool canCancel)
    {
        lblTotalValue.Text = $"{total:N0}đ";
        btnCancelOrder.Visible = canCancel;
        btnCancelOrder.Enabled = true;
        panelDetailFooter.Height = canCancel ? 112 : 60;
    }

    private ItemRowPanel BuildItemRow(OrderItemDto item, int orderStatus)
    {
        var statusIndex = Math.Clamp(item.KitchenStatus, 0, KitchenStatusLabels.Length - 1);
        var cancelled = statusIndex == 4;

        string note = cancelled && !string.IsNullOrWhiteSpace(item.CancelReason) ? $"Lý do hủy: {item.CancelReason}"
            : !string.IsNullOrWhiteSpace(item.Note) ? $"Ghi chú: {item.Note}"
            : "";

        var row = new ItemRowPanel
        {
            ItemName = item.MenuItem?.Name ?? "(Món đã xóa)",
            Qty = item.Quantity,
            PriceText = item.MenuItem is null ? "" : $"{item.MenuItem.Price * item.Quantity:N0}đ",
            StatusText = KitchenStatusLabels[statusIndex],
            StatusColor = KitchenStatusColor(statusIndex),
            Takeaway = item.IsTakeaway,
            Cancelled = cancelled,
            Note = note,
            TimeText = item.CreatedAt > DateTime.MinValue
                ? TimeDisplay.FormatClock(item.CreatedAt)
                : "",
        };
        row.Height = ItemRowPanel.HeightFor(note.Length > 0);

        // Món Mới gọi có thể mở hộp chỉnh số lượng rồi xác nhận.
        if (orderStatus == 0 && statusIndex == 0)
        {
            var btn = new Button
            {
                Text = "Sửa SL",
                Size = new Size(76, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = AppColors.Primary,
                Font = FontCaption,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = AppColors.Primary;
            btn.Click += async (s, e) => await EditItemQuantityAsync(item, btn);
            row.AttachCancelButton(btn);
        }
        return row;
    }

    // =====================================================================
    // HỦY ĐƠN / HỦY MÓN
    // =====================================================================

    private async Task CancelCurrentOrderAsync()
    {
        if (_selectedOrderId is not int orderId) return;

        string reason = PromptForCancelReason("Hủy toàn bộ đơn", "Nhập lý do hủy đơn hàng:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        btnCancelOrder.Enabled = false;
        try
        {
            await _api.CancelOrderAsync(orderId, _userId, reason);
            await LoadOrdersAsync(); // tải lại danh sách + chi tiết đơn đang chọn
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không hủy được đơn: {ex.Message}", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            btnCancelOrder.Enabled = true;
        }
    }

    private async Task EditItemQuantityAsync(
        OrderItemDto item,
        Button sourceButton)
    {
        using var dialog = new Form
        {
            Text = $"Chỉnh món - {item.MenuItem?.Name ?? "Món"}",
            Width = 380,
            Height = 230,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
        };

        var lblName = new Label
        {
            Text = item.MenuItem?.Name ?? "(Món đã xóa)",
            Font = AppFonts.HeadlineSm,
            Location = new Point(20, 18),
            Size = new Size(320, 30),
            AutoEllipsis = true,
        };

        var lblQty = new Label
        {
            Text = "Số lượng",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 64),
            AutoSize = true,
        };

        var quantity = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 99,
            Value = Math.Clamp(item.Quantity, 1, 99),
            Location = new Point(120, 60),
            Width = 110,
            Font = AppFonts.LabelLg,
            TextAlign = HorizontalAlignment.Center,
        };

        var btnCancelItem = new Button
        {
            Text = "Hủy món",
            Location = new Point(20, 120),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = AppColors.Error,
            Font = AppFonts.LabelLg,
        };
        btnCancelItem.FlatAppearance.BorderColor = AppColors.Error;

        var btnSave = new Button
        {
            Text = "Lưu",
            Location = new Point(220, 120),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
            Font = AppFonts.LabelLg,
        };
        btnSave.FlatAppearance.BorderSize = 0;

        dialog.Controls.AddRange(new Control[]
        {
            lblName,
            lblQty,
            quantity,
            btnCancelItem,
            btnSave,
        });

        btnSave.Click += async (s, e) =>
        {
            btnSave.Enabled = false;
            btnCancelItem.Enabled = false;

            try
            {
                await _api.UpdateOrderItemQuantityAsync(
                    item.Id,
                    (int)quantity.Value);

                dialog.Close();
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không cập nhật được số lượng món: {ex.Message}",
                    "Lỗi / Cảnh báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                btnSave.Enabled = true;
                btnCancelItem.Enabled = true;
            }
        };

        btnCancelItem.Click += async (s, e) =>
        {
            var reason = PromptForCancelReason(
                "Hủy món",
                $"Nhập lý do hủy món \"{item.MenuItem?.Name}\":");

            if (string.IsNullOrWhiteSpace(reason))
                return;

            btnSave.Enabled = false;
            btnCancelItem.Enabled = false;

            try
            {
                await _api.CancelOrderItemAsync(
                    item.Id,
                    _userId,
                    reason,
                    item.RowVer);

                dialog.Close();
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không hủy được món: {ex.Message}",
                    "Lỗi / Cảnh báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                btnSave.Enabled = true;
                btnCancelItem.Enabled = true;
            }
        };

        sourceButton.Enabled = false;
        dialog.ShowDialog(this);
        sourceButton.Enabled = true;
    }

    private async Task CancelItemAsync(OrderItemDto item, Button btn)
    {
        string reason = PromptForCancelReason("Hủy món", $"Nhập lý do hủy món \"{item.MenuItem?.Name}\":");
        if (string.IsNullOrWhiteSpace(reason)) return;

        btn.Enabled = false;
        try
        {
            await _api.CancelOrderItemAsync(item.Id, _userId, reason, item.RowVer);
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không hủy được món: {ex.Message}", "Lỗi / Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            btn.Enabled = true;
        }
    }

    private string PromptForCancelReason(string title, string prompt)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 380,
            Height = 190,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
        };

        var lblPrompt = new Label { Text = prompt, Font = AppFonts.BodyMd, Location = new Point(16, 14), AutoSize = true };
        var txtReason = new TextBox { Location = new Point(16, 42), Width = 330, Font = AppFonts.BodyMd, Text = "Khách đổi ý" };
        var btnOk = new Button
        {
            Text = "Đồng ý",
            Location = new Point(120, 88),
            Size = new Size(110, 34),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
            Font = AppFonts.LabelLg,
            Cursor = Cursors.Hand,
        };
        btnOk.FlatAppearance.BorderSize = 0;

        dialog.Controls.Add(lblPrompt);
        dialog.Controls.Add(txtReason);
        dialog.Controls.Add(btnOk);
        dialog.AcceptButton = btnOk;
        txtReason.SelectAll();

        return dialog.ShowDialog(this) == DialogResult.OK ? txtReason.Text.Trim() : string.Empty;
    }

    // =====================================================================
    // MÀU SẮC / HÌNH HỌC DÙNG CHUNG
    // =====================================================================

    // Đang mở = vàng, Đã thanh toán = xanh, Đã hủy = đỏ (trước đây "đang mở" và "đã thanh toán" cùng màu xanh nên khó phân biệt)
    private static (Color Fg, Color Bg) OrderStatusPalette(int status) => status switch
    {
        1 => (AppColors.Tertiary, Color.FromArgb(224, 242, 241)),
        2 => (AppColors.Error, AppColors.ErrorContainer),
        _ => (Color.FromArgb(166, 106, 0), Color.FromArgb(255, 243, 205)),
    };

    private static Color KitchenStatusColor(int status) => status switch
    {
        2 => AppColors.Tertiary,
        3 => AppColors.Success,
        4 => AppColors.Outline,
        _ => Color.FromArgb(200, 120, 0),
    };

    private static (bool Wide, int MidX, int ColW) GetColumns(int width)
    {
        bool wide = width >= WideMin;
        int midX = ColPadL + LeftColW + 16;
        int midEnd = width - ColPadR - RightBlockW - 16;
        int colW = wide ? Math.Max(60, (midEnd - midX) / 3) : 0;
        return (wide, midX, colW);
    }

    private static void FillPill(Graphics g, Rectangle r, Color back)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = new GraphicsPath())
        {
            int d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            using var brush = new SolidBrush(back);
            g.FillPath(brush, path);
        }
        g.SmoothingMode = old;
    }

    // =====================================================================
    // DÒNG ĐƠN (tự vẽ)
    // =====================================================================

    private sealed class OrderRowPanel : Panel
    {
        public const int RowHeight = 68;

        public OrderSummaryDto Order { get; }
        private bool _selected;
        private bool _hover;

        public bool Selected
        {
            get => _selected;
            set { if (_selected != value) { _selected = value; Invalidate(); } }
        }

        public OrderRowPanel(OrderSummaryDto order)
        {
            Order = order;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Hand;
            Height = RowHeight;
            BackColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var o = Order;
            int statusIndex = Math.Clamp(o.Status, 0, OrderStatusLabels.Length - 1);
            var (fg, bg) = OrderStatusPalette(statusIndex);

            // Nền + đường kẻ dưới + dải màu trạng thái bên trái
            var back = _selected ? SelectedBg : _hover ? AppColors.SurfaceLow : Color.White;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);
            using (var pen = new Pen(AppColors.OutlineVariant)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            using (var b = new SolidBrush(fg)) g.FillRectangle(b, 0, 0, 4, Height - 1);

            string place = string.IsNullOrWhiteSpace(o.TableName) ? "Mang về" : o.TableName;
            string staff = string.IsNullOrWhiteSpace(o.EmployeeName) ? "—" : o.EmployeeName;
            var (wide, midX, colW) = GetColumns(Width);

            if (wide)
            {
                TextRenderer.DrawText(g, $"Đơn #{o.Id}", FontTitle, new Rectangle(ColPadL, 10, LeftColW, 26), AppColors.OnSurface, Tf);
                TextRenderer.DrawText(g, $"{place} · {o.ItemCount} món", FontBody, new Rectangle(ColPadL, 36, LeftColW, 22), AppColors.OnSurfaceVariant, Tf);

                TextRenderer.DrawText(g, staff, FontBody, new Rectangle(midX, 0, colW - 8, Height), AppColors.OnSurface, Tf);
                TextRenderer.DrawText(g, o.OrderTime.ToString("HH:mm dd/MM/yyyy"), FontBody, new Rectangle(midX + colW, 0, colW - 8, Height), AppColors.OnSurface, Tf);
                TextRenderer.DrawText(g, DataSources.Label(o.Source), o.IsFromWeb ? FontBodyBold : FontBody,
                    new Rectangle(midX + colW * 2, 0, colW - 8, Height), o.IsFromWeb ? AppColors.Secondary : AppColors.OnSurface, Tf);
            }
            else
            {
                int leftW = Math.Max(60, Width - ColPadL - ColPadR - RightBlockW - 12);
                TextRenderer.DrawText(g, $"Đơn #{o.Id} · {place}", FontTitle, new Rectangle(ColPadL, 10, leftW, 26), AppColors.OnSurface, Tf);
                string meta = $"{staff} · {o.OrderTime:HH:mm dd/MM} · {o.ItemCount} món" + (o.IsFromWeb ? " · Web" : "");
                TextRenderer.DrawText(g, meta, FontBody, new Rectangle(ColPadL, 36, leftW, 22), AppColors.OnSurfaceVariant, Tf);
            }

            // Khối bên phải: tổng tiền + nhãn trạng thái
            int rightX = Width - ColPadR - RightBlockW;
            TextRenderer.DrawText(g, $"{o.TotalAmount:N0}đ", FontTotal, new Rectangle(rightX, 8, RightBlockW, 26), AppColors.Primary, TfRight);

            string statusText = OrderStatusLabels[statusIndex];
            int pillW = TextRenderer.MeasureText(g, statusText, FontCaption, Size.Empty, TextFormatFlags.NoPadding).Width + 24;
            var pill = new Rectangle(Width - ColPadR - pillW, 38, pillW, 22);
            FillPill(g, pill, bg);
            TextRenderer.DrawText(g, statusText, FontCaption, pill, fg, TfCenter);
        }
    }

    // =====================================================================
    // DÒNG MÓN TRONG CHI TIẾT (tự vẽ, riêng nút "Hủy món" là Button thật)
    // =====================================================================

    private sealed class ItemRowPanel : Panel
    {
        public string ItemName = "";
        public int Qty;
        public string PriceText = "";
        public string StatusText = "";
        public Color StatusColor = Color.Gray;
        public bool Takeaway;
        public bool Cancelled;
        public string Note = "";
        public string TimeText = "";
        private Button? _cancelButton;

        public static int HeightFor(bool hasNote) => hasNote ? 104 : 84;

        public ItemRowPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public void AttachCancelButton(Button btn)
        {
            _cancelButton = btn;
            Controls.Add(btn);
            PositionButton();
        }

        private void PositionButton()
        {
            if (_cancelButton is not null)
                _cancelButton.Location = new Point(Math.Max(0, Width - _cancelButton.Width - 2), 32);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PositionButton();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            const int priceW = 110;

            var nameColor = Cancelled ? AppColors.Outline : AppColors.OnSurface;
            TextRenderer.DrawText(g, $"{ItemName}  x{Qty}", Cancelled ? FontStrike : FontBodyBold,
                new Rectangle(0, 6, Math.Max(20, Width - priceW - 8), 24), nameColor, Tf);
            TextRenderer.DrawText(g, PriceText, FontBodyBold, new Rectangle(Width - priceW, 6, priceW, 24), nameColor, TfRight);

            string st = "● " + StatusText;
            int sw = TextRenderer.MeasureText(g, st, FontCaption, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, st, FontCaption, new Rectangle(0, 34, sw + 6, 22), StatusColor, Tf);

            if (Takeaway)
            {
                var chip = new Rectangle(sw + 16, 35, 66, 20);
                FillPill(g, chip, AppColors.ErrorContainer);
                TextRenderer.DrawText(g, "Mang về", FontCaption, chip, AppColors.Primary, TfCenter);
            }

            if (TimeText.Length > 0)
                TextRenderer.DrawText(g, TimeText, FontSmall, new Rectangle(0, 58, Width, 20), AppColors.OnSurfaceVariant, Tf);

            if (Note.Length > 0)
                TextRenderer.DrawText(g, Note, FontSmall, new Rectangle(0, 78, Width, 20), AppColors.OnSurfaceVariant, Tf);

            using var pen = new Pen(AppColors.OutlineVariant);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}