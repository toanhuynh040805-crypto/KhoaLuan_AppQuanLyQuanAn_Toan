using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Tab "Mang về": danh sách các đơn MANG VỀ đang mở (đã gửi bếp, chưa thanh toán).
// - "+ Tạo đơn mang về"  -> mở màn Thanh toán ở chế độ Mang về (SalesControl)
// - "Thanh toán"         -> mở đơn đó ở màn Thanh toán để thu tiền
// - "Hủy đơn"            -> hủy đơn mang về (khách không lấy)
// Control dựng hoàn toàn bằng code (không có Designer).
public partial class TakeawayOrdersControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => LoadOrdersAsync();


    private readonly ApiService _api = new();
    private readonly int _userId;

    private readonly Label _lblCount = new();
    private readonly FlowLayoutPanel _flowOrders = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15000 };
    private bool _loading;

    // Bấm "Tạo đơn mang về"
    public event Action? CreateRequested;

    // Bấm "Thanh toán" trên 1 đơn (tham số: OrderId)
    public event Action<int>? PayRequested;

    public TakeawayOrdersControl() : this(0) { }

    public TakeawayOrdersControl(int userId)
    {
        _userId = userId;
        BackColor = AppColors.Background;
        Dock = DockStyle.Fill;

        BuildLayout();

        Load += async (s, e) =>
        {
            NotificationHubClient.ItemStatusUpdated += OnItemChanged;
            NotificationHubClient.OrderItemCancelled += OnItemCancelled;
            NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
            NotificationHubClient.OrderUpdated += OnOrderUpdated;
            NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
            NotificationHubClient.ConnectionRestored += OnConnectionRestored;
            _timer.Start();
            await LoadOrdersAsync();
        };

        _timer.Tick += async (s, e) => await LoadOrdersAsync();

        // Sự kiện realtime là static -> bắt buộc hủy đăng ký khi control bị Dispose
        Disposed += (s, e) =>
        {
            NotificationHubClient.ItemStatusUpdated -= OnItemChanged;
            NotificationHubClient.OrderItemCancelled -= OnItemCancelled;
            NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
            NotificationHubClient.OrderUpdated -= OnOrderUpdated;
            NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
            NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
            _timer.Stop();
            _timer.Dispose();
        };
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(24, 16, 24, 8) };

        var lblTitle = new Label
        {
            Text = "Đơn mang về",
            Font = AppFonts.HeadlineMd,
            AutoSize = true,
            Location = new Point(24, 14)
        };

        _lblCount.Font = AppFonts.BodyMd;
        _lblCount.ForeColor = AppColors.OnSurfaceVariant;
        _lblCount.AutoSize = true;
        _lblCount.Location = new Point(26, 48);

        var btnCreate = new Button
        {
            Text = "+ Tạo đơn mang về",
            Size = new Size(170, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
            Font = AppFonts.LabelLg,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCreate.FlatAppearance.BorderSize = 0;
        btnCreate.Click += (s, e) => CreateRequested?.Invoke();

        var btnRefresh = new Button
        {
            Text = "Làm mới",
            Size = new Size(100, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = AppColors.OnSurface,
            Font = AppFonts.LabelLg,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnRefresh.FlatAppearance.BorderColor = AppColors.OutlineVariant;
        btnRefresh.Click += async (s, e) => await LoadOrdersAsync();

        header.Controls.Add(lblTitle);
        header.Controls.Add(_lblCount);
        header.Controls.Add(btnCreate);
        header.Controls.Add(btnRefresh);
        header.Resize += (s, e) =>
        {
            btnCreate.Location = new Point(header.ClientSize.Width - 24 - btnCreate.Width, 20);
            btnRefresh.Location = new Point(btnCreate.Left - 12 - btnRefresh.Width, 20);
        };

        _flowOrders.Dock = DockStyle.Fill;
        _flowOrders.AutoScroll = true;
        _flowOrders.Padding = new Padding(16, 8, 16, 16);
        _flowOrders.BackColor = AppColors.Background;

        Controls.Add(_flowOrders);
        Controls.Add(header);
    }

    // Các handler realtime chạy trên luồng nền của SignalR -> đẩy về luồng giao diện
    private void OnItemChanged(ItemStatusUpdatedPayload p) => ReloadOnUiThread();
    private void OnItemCancelled(OrderItemCancelledPayload p) => ReloadOnUiThread();
    private void OnPaymentCompleted(PaymentCompletedPayload p) => ReloadOnUiThread();
    private void OnOrderUpdated(OrderUpdatedPayload p) => ReloadOnUiThread();
    private void OnTableStatusUpdated(TableStatusUpdatedPayload p) => ReloadOnUiThread();
    private void OnConnectionRestored() => ReloadOnUiThread();

    private void ReloadOnUiThread()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(async () =>
            {
                if (!IsDisposed) await LoadOrdersAsync();
            });
        }
        catch (InvalidOperationException)
        {
            // Control đang bị đóng - bỏ qua
        }
    }

    private async Task LoadOrdersAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var orders = await _api.GetOpenTakeawayOrdersAsync();
            if (IsDisposed) return;
            RenderOrders(orders);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show($"Không tải được danh sách đơn mang về: {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RenderOrders(List<TakeawayOrderSummaryDto> orders)
    {
        _lblCount.Text = orders.Count == 0
            ? "Chưa có đơn mang về nào đang chờ."
            : $"{orders.Count} đơn đang chờ thanh toán";

        _flowOrders.SuspendLayout();
        foreach (Control old in _flowOrders.Controls.Cast<Control>().ToList())
        {
            _flowOrders.Controls.Remove(old);
            old.Dispose();
        }

        if (orders.Count == 0)
        {
            _flowOrders.Controls.Add(new Label
            {
                Text = "Bấm \"+ Tạo đơn mang về\" để bắt đầu gọi món cho khách mang về.",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurfaceVariant,
                AutoSize = true,
                Margin = new Padding(8, 12, 0, 0)
            });
        }
        else
        {
            foreach (var order in orders)
                _flowOrders.Controls.Add(BuildOrderCard(order));
        }

        _flowOrders.ResumeLayout();
    }

    private static readonly string[] KitchenStatusLabels = { "Mới gọi", "Đang chế biến", "Đã chế biến", "Đã phục vụ", "Đã hủy" };

    private static Color KitchenStatusColor(int status) => status switch
    {
        2 => AppColors.Tertiary,
        3 => AppColors.Success,
        4 => AppColors.Outline,
        _ => AppColors.Warning,
    };

    private Panel BuildOrderCard(TakeawayOrderSummaryDto order)
    {
        const int cardWidth = 360;
        const int pad = 14;
        const int inner = cardWidth - pad * 2;
        const int priceWidth = 90;

        var card = new Panel
        {
            Width = cardWidth,
            BackColor = Color.White,
            Margin = new Padding(8)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        Panel Separator(int top) => new Panel
        {
            Location = new Point(pad, top),
            Size = new Size(inner, 1),
            BackColor = AppColors.OutlineVariant
        };

        var code = string.IsNullOrEmpty(order.TakeawayCode) ? $"#{order.Id}" : order.TakeawayCode;
        var y = 10;

        // --- Mã đơn ---
        card.Controls.Add(new Label { Text = $"Mang về {code}", Font = AppFonts.HeadlineSm, Location = new Point(pad, y), AutoSize = true });
        if (order.IsFromWeb)
        {
            card.Controls.Add(new Label
            {
                Text = "🌐 Web",
                Font = AppFonts.LabelMd,
                ForeColor = Color.White,
                BackColor = AppColors.Secondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(60, 20),
                Location = new Point(cardWidth - pad - 60, y + 4)
            });
        }
        y += 28;

        // --- Giờ + nhân viên ---
        var who = string.IsNullOrWhiteSpace(order.EmployeeName) ? "" : $" · NV: {order.EmployeeName}";
        card.Controls.Add(new Label
        {
            // Đơn của ngày trước (còn tồn) hiện thêm ngày/tháng để không nhầm với mã MV trùng.
            Text = (order.OrderTime.Date == DateTime.Today
                        ? $"{order.OrderTime:HH:mm}"
                        : $"{order.OrderTime:dd/MM HH:mm}") + who,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(pad, y),
            Size = new Size(inner, 20),
            AutoEllipsis = true
        });
        y += 22;

        // --- Khách thành viên (nếu có) ---
        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            var phone = string.IsNullOrWhiteSpace(order.CustomerPhone) ? "" : $" · {order.CustomerPhone}";
            card.Controls.Add(new Label
            {
                Text = $"Khách: {order.CustomerName}{phone}",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurface,
                Location = new Point(pad, y),
                Size = new Size(inner, 20),
                AutoEllipsis = true
            });
            y += 22;
        }

        y += 4;
        card.Controls.Add(Separator(y));
        y += 8;

        // --- Danh sách món ---
        if (order.Items.Count == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "(Chưa có món nào)",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurfaceVariant,
                Location = new Point(pad, y),
                AutoSize = true
            });
            y += 26;
        }
        else
        {
            foreach (var item in order.Items)
            {
                card.Controls.Add(new Label
                {
                    Text = $"{item.Quantity}x {item.Name}",
                    Font = AppFonts.LabelLg,
                    Location = new Point(pad, y),
                    Size = new Size(inner - priceWidth - 4, 20),
                    AutoEllipsis = true
                });
                card.Controls.Add(new Label
                {
                    Text = $"{item.UnitPrice * item.Quantity:N0}đ",
                    Font = AppFonts.BodyMd,
                    Location = new Point(pad + inner - priceWidth, y),
                    Size = new Size(priceWidth, 20),
                    TextAlign = ContentAlignment.TopRight
                });
                y += 20;

                var statusIndex = Math.Clamp(item.KitchenStatus, 0, KitchenStatusLabels.Length - 1);
                card.Controls.Add(new Label
                {
                    Text = KitchenStatusLabels[statusIndex],
                    Font = AppFonts.BodyMd,
                    ForeColor = KitchenStatusColor(statusIndex),
                    Location = new Point(pad, y),
                    AutoSize = true
                });
                y += 20;

                if (!string.IsNullOrWhiteSpace(item.Note))
                {
                    card.Controls.Add(new Label
                    {
                        Text = $"Ghi chú: {item.Note}",
                        Font = AppFonts.BodyMd,
                        ForeColor = AppColors.OnSurfaceVariant,
                        Location = new Point(pad, y),
                        Size = new Size(inner, 20),
                        AutoEllipsis = true
                    });
                    y += 20;
                }

                y += 6;
            }
        }

        card.Controls.Add(Separator(y));
        y += 8;

        // --- Tình trạng phục vụ ---
        var served = order.PendingItemCount == 0;
        card.Controls.Add(new Label
        {
            Text = served
                ? $"{order.ItemCount} món · đã phục vụ đủ"
                : $"{order.ItemCount} món · còn {order.PendingItemCount} món chưa phục vụ",
            Font = AppFonts.BodyMd,
            ForeColor = served ? AppColors.Success : AppColors.Warning,
            Location = new Point(pad, y),
            Size = new Size(inner, 20),
            AutoEllipsis = true
        });
        y += 26;

        // --- Tổng cộng ---
        card.Controls.Add(new Label { Text = "Tổng cộng", Font = AppFonts.LabelLg, Location = new Point(pad, y + 2), AutoSize = true });
        card.Controls.Add(new Label
        {
            Text = $"{order.TotalAmount:N0}đ",
            Font = AppFonts.HeadlineSm,
            ForeColor = AppColors.Primary,
            Location = new Point(pad + inner - 160, y),
            Size = new Size(160, 26),
            TextAlign = ContentAlignment.TopRight
        });
        y += 34;

        // --- Điều kiện thanh toán ---
        // - Đơn khách tự gọi tại quầy WinForm: thanh toán trước như bình thường.
        // - Đơn nhân viên Mobile order hộ:
        //     + đã phục vụ đủ món      -> "Thanh toán" (tự mở, không cần chờ nhân viên bấm yêu cầu)
        //     + còn món chưa phục vụ   -> "Thanh toán sớm" (hỏi xác nhận trước khi thu tiền)
        var isEarlyPayment = order.IsMobileOrder && order.PendingItemCount > 0;

        if (isEarlyPayment)
        {
            card.Controls.Add(new Label
            {
                Text = $"Còn {order.PendingItemCount} món chưa phục vụ · có thể thanh toán sớm",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Warning,
                Location = new Point(pad, y),
                Size = new Size(inner, 20),
                AutoEllipsis = true
            });
            y += 24;
        }
        else if (order.IsMobileOrder && order.PaymentRequested)
        {
            card.Controls.Add(new Label
            {
                Text = "Nhân viên đã gửi yêu cầu thanh toán",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Success,
                Location = new Point(pad, y),
                Size = new Size(inner, 20),
                AutoEllipsis = true
            });
            y += 24;
        }

        // --- Nút ---
        var btnWidth = (inner - 8) / 2;
        var btnPay = new Button
        {
            Text = isEarlyPayment ? "Thanh toán sớm" : "Thanh toán",
            Location = new Point(pad, y),
            Size = new Size(btnWidth, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = isEarlyPayment ? AppColors.Warning : AppColors.Primary,
            ForeColor = Color.White,
            Font = AppFonts.LabelMd,
            Cursor = Cursors.Hand
        };
        btnPay.FlatAppearance.BorderSize = 0;
        btnPay.Click += (s, e) =>
        {
            if (isEarlyPayment)
            {
                var confirm = MessageBox.Show(
                    $"Đơn {code} còn {order.PendingItemCount} món chưa được phục vụ.\n\n" +
                    "Vẫn thanh toán trước? Các món này vẫn tiếp tục được bếp chế biến sau khi thanh toán.",
                    "Thanh toán sớm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirm != DialogResult.Yes) return;
            }

            PayRequested?.Invoke(order.Id);
        };

        var btnCancel = new Button
        {
            Text = "Hủy đơn",
            Location = new Point(pad + btnWidth + 8, y),
            Size = new Size(btnWidth, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = AppColors.Error,
            Font = AppFonts.LabelMd,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = AppColors.OutlineVariant;
        btnCancel.Click += async (s, e) => await CancelOrderAsync(order, code);

        card.Controls.Add(btnPay);
        card.Controls.Add(btnCancel);

        card.Height = y + 34 + 12;
        return card;
    }

    private async Task CancelOrderAsync(TakeawayOrderSummaryDto order, string code)
    {
        if (MessageBox.Show($"Hủy đơn mang về {code}?", "Hủy đơn",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            await _api.CancelOrderAsync(order.Id, _userId, "Hủy đơn mang về");
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không hủy được đơn: {ex.Message}");
        }
    }
}