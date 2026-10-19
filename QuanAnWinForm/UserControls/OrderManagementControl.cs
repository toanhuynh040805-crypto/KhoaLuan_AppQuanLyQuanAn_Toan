using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

public partial class OrderManagementControl : UserControl
{
    private readonly ApiService _api = new();
    private readonly int _userId;
    private List<OrderSummaryDto> _orders = new();
    private int? _selectedOrderId;

    private static readonly string[] OrderStatusLabels = { "Đang mở", "Đã thanh toán", "Đã hủy" };
    private static readonly string[] KitchenStatusLabels = { "Mới gọi", "Đang chế biến", "Đã chế biến", "Đã phục vụ", "Đã hủy" };

    public OrderManagementControl() : this(0) { }

    public OrderManagementControl(int userId)
    {
        _userId = userId;
        InitializeComponent();

        cbFilterStatus.Items.Add("Tất cả trạng thái");
        cbFilterStatus.Items.AddRange(OrderStatusLabels.Cast<object>().ToArray());
        cbFilterStatus.SelectedIndex = 0;

        dtpFilterDate.Value = DateTime.Today;

        Load += async (s, e) => await LoadOrdersAsync();
        dtpFilterDate.ValueChanged += async (s, e) => await LoadOrdersAsync();
        cbFilterStatus.SelectedIndexChanged += async (s, e) => await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            int? status = cbFilterStatus.SelectedIndex > 0 ? cbFilterStatus.SelectedIndex - 1 : null;
            _orders = await _api.GetOrdersAsync(dtpFilterDate.Value.Date, status);
            RenderOrderList();
        }
        catch (Exception ex)
        {
            flowOrders.Controls.Clear();
            flowOrders.Controls.Add(new Label { Text = $"Không tải được danh sách đơn: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
        }
    }

    private void RenderOrderList()
    {
        flowOrders.Controls.Clear();

        if (_orders.Count == 0)
        {
            flowOrders.Controls.Add(new Label { Text = "Không có đơn hàng nào trong ngày này.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        foreach (var order in _orders)
            flowOrders.Controls.Add(BuildOrderRow(order));
    }

    private static Color OrderStatusColor(int status) => status switch
    {
        1 => AppColors.Success,
        2 => AppColors.Error,
        _ => AppColors.Tertiary,
    };

    private Panel BuildOrderRow(OrderSummaryDto order)
    {
        var isSelected = _selectedOrderId == order.Id;
        var row = new Panel { Size = new Size(720, 64), BackColor = isSelected ? AppColors.SurfaceLow : Color.White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand, Tag = order.Id };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(isSelected ? AppColors.Primary : AppColors.OutlineVariant, isSelected ? 2 : 1), 1, 1, row.Width - 3, row.Height - 3);

        var statusIndex = Math.Clamp(order.Status, 0, OrderStatusLabels.Length - 1);
        var stripe = new Panel { Size = new Size(4, 64), Location = new Point(0, 0), BackColor = OrderStatusColor(statusIndex) };

        var lblTable = new Label { Text = $"Đơn #{order.Id} · {order.TableName}", Font = AppFonts.LabelLg, Location = new Point(16, 8), AutoSize = true };
        var lblMeta = new Label { Text = $"{order.EmployeeName} · {order.OrderTime:HH:mm dd/MM} · {order.ItemCount} món", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(16, 32), AutoSize = true };
        var lblTotal = new Label { Text = $"{order.TotalAmount:N0}đ", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(500, 12), AutoSize = true };
        var lblStatus = new Label { Text = OrderStatusLabels[statusIndex], Font = AppFonts.LabelMd, ForeColor = OrderStatusColor(statusIndex), Location = new Point(500, 34), AutoSize = true };

        row.Controls.Add(stripe);
        row.Controls.Add(lblTable);
        row.Controls.Add(lblMeta);
        row.Controls.Add(lblTotal);
        row.Controls.Add(lblStatus);
        if (order.IsFromWeb)
        {
            row.Controls.Add(new Label
            {
                Text = "🌐 Web",
                Font = AppFonts.LabelMd,
                ForeColor = Color.White,
                BackColor = AppColors.Secondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(60, 20),
                Location = new Point(640, 8)
            });
        }
        async void OpenDetail(object? s, EventArgs e) => await ShowOrderDetailAsync(order.Id);
        row.Click += OpenDetail;
        lblTable.Click += OpenDetail;
        lblMeta.Click += OpenDetail;
        lblTotal.Click += OpenDetail;
        lblStatus.Click += OpenDetail;

        return row;
    }

    private async Task ShowOrderDetailAsync(int orderId)
    {
        _selectedOrderId = orderId;
        RenderOrderList();

        panelDetail.Controls.Clear();
        panelDetail.Visible = true;
        panelDetail.Controls.Add(new Label { Text = "Đang tải...", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true });

        OrderDto? order;
        try
        {
            order = await _api.GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            panelDetail.Controls.Clear();
            panelDetail.Controls.Add(new Label { Text = $"Không tải được chi tiết đơn: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true });
            return;
        }

        panelDetail.Controls.Clear();
        if (order is null)
        {
            panelDetail.Controls.Add(new Label { Text = "Không tìm thấy đơn hàng này.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true });
            return;
        }

        var lblTitle = new Label { Text = $"Đơn #{order.Id} · {order.Table?.TableName}", Font = AppFonts.HeadlineSm, Location = new Point(0, 0), AutoSize = true };
        var statusIndex = Math.Clamp(order.Status, 0, OrderStatusLabels.Length - 1);
        var lblStatus = new Label { Text = OrderStatusLabels[statusIndex], Font = AppFonts.LabelMd, ForeColor = OrderStatusColor(statusIndex), Location = new Point(0, 32), AutoSize = true };
        var sourceText = order.Source is null ? "" : $" · Nguồn: {DataSources.Label(order.Source)}";
        var lblTime = new Label { Text = order.CreatedAt.ToString("HH:mm dd/MM/yyyy") + sourceText, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(0, 56), AutoSize = true };
        panelDetail.Controls.Add(lblTitle);
        panelDetail.Controls.Add(lblStatus);
        panelDetail.Controls.Add(lblTime);

        var flowItems = new FlowLayoutPanel { Location = new Point(0, 92), Size = new Size(370, 440), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BorderStyle = BorderStyle.FixedSingle };
        if (order.OrderItems.Count == 0)
        {
            flowItems.Controls.Add(new Label { Text = "(Chưa có món nào)", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(6) });
        }
        else
        {
            foreach (var item in order.OrderItems)
                flowItems.Controls.Add(BuildOrderItemRow(item, order.Status));
        }
        panelDetail.Controls.Add(flowItems);

        var lblTotal = new Label { Text = $"Tổng cộng: {order.TotalAmount:N0}đ", Font = AppFonts.HeadlineSm, ForeColor = AppColors.Primary, Location = new Point(0, 544), AutoSize = true };
        panelDetail.Controls.Add(lblTotal);

        if (order.Status == 0)
        {
            var btnCancelOrder = new Button { Text = "Hủy toàn bộ đơn", Location = new Point(0, 584), Size = new Size(370, 40), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Error, ForeColor = Color.White };
            btnCancelOrder.FlatAppearance.BorderSize = 0;
            btnCancelOrder.Click += async (s, e) =>
            {
                string reason = PromptForCancelReason("Hủy toàn bộ đơn", "Nhập lý do hủy đơn hàng:");
                if (string.IsNullOrWhiteSpace(reason)) return;

                btnCancelOrder.Enabled = false;
                try
                {
                    await _api.CancelOrderAsync(order.Id, _userId, reason);
                    await LoadOrdersAsync();
                    await ShowOrderDetailAsync(order.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không hủy được đơn: {ex.Message}", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnCancelOrder.Enabled = true;
                }
            };
            panelDetail.Controls.Add(btnCancelOrder);
        }
    }

    private static Color KitchenStatusColor(int status) => status switch
    {
        2 => AppColors.Tertiary,
        3 => AppColors.Success,
        4 => AppColors.Outline,
        _ => AppColors.Warning,
    };

    private Panel BuildOrderItemRow(OrderItemDto item, int orderStatus)
    {
        var row = new Panel { Size = new Size(350, 60), Margin = new Padding(4) };

        var lblName = new Label { Text = $"{item.MenuItem?.Name} x{item.Quantity}", Font = AppFonts.LabelMd, Location = new Point(0, 2), Size = new Size(230, 20), AutoEllipsis = true };
        var statusIndex = Math.Clamp(item.KitchenStatus, 0, KitchenStatusLabels.Length - 1);
        var lblStatus = new Label { Text = KitchenStatusLabels[statusIndex], Font = AppFonts.BodyMd, ForeColor = KitchenStatusColor(statusIndex), Location = new Point(0, 22), AutoSize = true };
        var lblPrice = new Label
        {
            Text = item.MenuItem is null ? "" : $"{item.MenuItem.Price * item.Quantity:N0}đ",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(0, 40),
            AutoSize = true,
        };

        row.Controls.Add(lblName);
        row.Controls.Add(lblStatus);
        row.Controls.Add(lblPrice);

        // Đánh dấu món khách yêu cầu mang về
        if (item.IsTakeaway)
        {
            var lblTakeaway = new Label
            {
                Text = "· Mang về",
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.Primary,
                AutoSize = true
            };
            row.Controls.Add(lblTakeaway);
            lblTakeaway.Location = new Point(lblStatus.PreferredWidth + 6, 22);
        }

        // Chỉ cho phép bấm hủy món khi món đang ở trạng thái Mới gọi (MoiGoi = 0)
        if (orderStatus == 0 && statusIndex == 0)
        {
            var btnCancelItem = new Button { Text = "Hủy món", Location = new Point(250, 16), Size = new Size(90, 28), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Error, ForeColor = Color.White, Font = AppFonts.LabelMd };
            btnCancelItem.FlatAppearance.BorderSize = 0;
            btnCancelItem.Click += async (s, e) =>
            {
                string reason = PromptForCancelReason("Hủy món", $"Nhập lý do hủy món \"{item.MenuItem?.Name}\":");
                if (string.IsNullOrWhiteSpace(reason)) return;

                btnCancelItem.Enabled = false;
                try
                {
                    await _api.CancelOrderItemAsync(item.Id, _userId, reason, item.RowVer);
                    if (_selectedOrderId.HasValue) await ShowOrderDetailAsync(_selectedOrderId.Value);
                    await LoadOrdersAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không hủy được món: {ex.Message}", "Lỗi / Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnCancelItem.Enabled = true;
                }
            };
            row.Controls.Add(btnCancelItem);
        }

        return row;
    }

    private string PromptForCancelReason(string title, string prompt)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 360,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblPrompt = new Label { Text = prompt, Font = AppFonts.BodyMd, Location = new Point(16, 12), AutoSize = true };
        var txtReason = new TextBox { Location = new Point(16, 36), Width = 310, Font = AppFonts.BodyMd, Text = "Khách đổi ý" };
        var btnOk = new Button { Text = "Đồng ý", Location = new Point(110, 80), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };

        dialog.Controls.Add(lblPrompt);
        dialog.Controls.Add(txtReason);
        dialog.Controls.Add(btnOk);
        dialog.AcceptButton = btnOk;

        return dialog.ShowDialog() == DialogResult.OK ? txtReason.Text.Trim() : string.Empty;
    }
}