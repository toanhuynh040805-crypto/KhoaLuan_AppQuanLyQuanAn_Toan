using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QRCoder;

namespace QuanAnWinForm.UserControls;

// Man hinh Ban hang / Thanh toan (POS) - man hinh chinh cua Role Thu ngan.
// Ben trai: chon mon theo danh muc. Ben phai: gio hang cua ban dang chon + thanh toan.
//
// BỔ SUNG (khách hàng thân thiết): thêm ô tìm khách theo SĐT ngay trên gio
// hàng - nếu tìm/gắn được khách, khách sẽ được TỰ ĐỘNG tích điểm khi thanh
// toán thành công (xem PaymentsController.Create bên Backend). Đổi điểm lấy
// giảm giá NGAY TẠI POS chưa làm ở bản này (API Backend đã hỗ trợ sẵn qua
// tham số RedeemPoints - xem CreatePaymentAsync trong ApiService) - việc đổi
// điểm hiện thực hiện ở màn Khách hàng (CustomerManagementControl) bằng
// cộng/trừ điểm thủ công.
public partial class SalesControl : UserControl
{
    private readonly ApiService _api = new();
    private readonly int _userId;
    private readonly int? _preselectedTableId;

    private List<MenuItemDto> _allMenuItems = new();
    private int? _selectedCategoryId; // null = "Tất cả"
    private readonly ToolTip _tip = new();
    private readonly Dictionary<int, (MenuItemDto Item, int Qty)> _cart = new();
    private int? _selectedTableId;

    // true = dang ban MANG VE (khong gan ban); false = ban tai ban nhu cu.
    private bool _isTakeaway;
    private readonly Button btnDineIn = new() { Text = "Tại bàn" };
    private readonly Button btnTakeaway = new() { Text = "Mang về" };
    private List<ActiveCampaignDto> _activeCampaigns = new();
    private CustomerSearchResultDto? _selectedCustomer;

    // Đơn ĐANG MỞ của bàn đang chọn (các món khách đã gọi trước đó, kể cả từ Mobile).
    // Hiển thị chỉ-đọc ở giỏ hàng; _cart chỉ chứa các món THÊM MỚI tại quầy.
    private OpenTableOrderDto? _existingOrder;
    private readonly System.Windows.Forms.Timer _realtimeRefreshTimer =
        new() { Interval = 300 };
    private bool _realtimeRefreshRunning;
    private bool _realtimeDirty;

    // Món THÊM MỚI mà khách ăn tại chỗ yêu cầu MANG VỀ (cùng 1 đơn / 1 hóa đơn với đơn tại bàn).
    private readonly Dictionary<int, (MenuItemDto Item, int Qty)> _cartTake = new();
    private readonly int? _preselectedTakeawayOrderId;
    private readonly Button btnSendKitchen = new() { Text = "Gửi bếp - trả sau" };
    private readonly Button btnReceiptPreview = new() { Text = "🖨 Xem bill" };
    private readonly Button btnConfirmOrder = new() { Text = "Xác nhận" };

    private bool CanViewPaymentReceiptQr
    {
        get
        {
            var role = (SessionContext.RoleName ?? "")
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "")
                .Replace("_", "");

            return role is "quanly"
                or "quảnlý"
                or "admin"
                or "thungan"
                or "thungân";
        }
    }

    private bool CartIsEmpty => _cart.Count == 0 && _cartTake.Count == 0;

    public SalesControl() : this(0, null) { }

    public SalesControl(int userId) : this(userId, null) { }

    // preselectedTableId: dung khi dieu huong tu So do ban (TableManagementControl)
    // bam "Sang bán hàng" tren 1 ban dang chon san.
    // preselectedTakeawayOrderId: dung khi mo 1 don MANG VE dang mo tu tab "Mang ve"
    // (TakeawayOrdersControl) de thanh toan.
    public SalesControl(int userId, int? preselectedTableId, int? preselectedTakeawayOrderId = null)
    {
        _userId = userId;
        _preselectedTableId = preselectedTableId;
        _preselectedTakeawayOrderId = preselectedTakeawayOrderId;
        InitializeComponent();
        BuildModeBar();
        BuildReceiptPreviewButton();

        btnAllCategory.Click += (s, e) => FilterCategory(null);

        // Khung thuc don/gio hang trong Designer co kich thuoc CO DINH (700x700) nen khong gian ra
        // theo cua so -> tu can lai moi khi cua so doi kich thuoc.
        panelLeft.Resize += (s, e) => FitLayout();
        panelRight.Resize += (s, e) => FitLayout();
        Load += (s, e) => FitLayout();

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
                await RefreshCurrentOrderRealtimeAsync();
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
            await LoadDataAsync();
        };

        Disposed += (s, e) =>
        {
            UnsubscribeRealtime();
            _realtimeRefreshTimer.Stop();
            _realtimeRefreshTimer.Dispose();
            _tip.Dispose();
        };
        btnVietQr.Click += async (s, e) => await ProcessPaymentAsync(method: 1);
        btnThanhToan.Click += async (s, e) => await ProcessPaymentAsync(method: 0);
        btnConfirmOrder.Click += async (s, e) => await ConfirmOrderAsync();
        lblInvoiceHistory.Click += async (s, e) => await OpenInvoiceHistoryDialogAsync();
        cbCampaign.SelectedIndexChanged += (s, e) => RenderCart();

        btnFindCustomer.Click += async (s, e) => await FindCustomerAsync();
        txtCustomerPhone.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await FindCustomerAsync();
            }
        };
        btnClearCustomer.Click += (s, e) =>
        {
            _selectedCustomer = null;
            txtCustomerPhone.Clear();
            UpdateCustomerInfoLabel();
        };

        UpdateCustomerInfoLabel();
    }

    private void SubscribeRealtime()
    {
        UnsubscribeRealtime();
        NotificationHubClient.OrderUpdated += OnOrderUpdated;
        NotificationHubClient.ItemStatusUpdated += OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled += OnOrderItemCancelled;
        NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
        NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
        NotificationHubClient.ConnectionRestored += OnConnectionRestored;
    }

    private void UnsubscribeRealtime()
    {
        NotificationHubClient.OrderUpdated -= OnOrderUpdated;
        NotificationHubClient.ItemStatusUpdated -= OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled -= OnOrderItemCancelled;
        NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
        NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
        NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
    }

    private bool IsCurrentOrder(int orderId) =>
        orderId > 0 && _existingOrder?.OrderId == orderId;

    private bool IsCurrentTable(int? tableId) =>
        !_isTakeaway &&
        tableId.HasValue &&
        tableId.Value > 0 &&
        _selectedTableId == tableId.Value;

    private void OnOrderUpdated(OrderUpdatedPayload payload)
    {
        if (IsCurrentOrder(payload.OrderId))
            QueueRealtimeRefresh();
    }

    private void OnItemStatusUpdated(ItemStatusUpdatedPayload payload)
    {
        if (IsCurrentOrder(payload.OrderId) || IsCurrentTable(payload.TableId))
            QueueRealtimeRefresh();
    }

    private void OnOrderItemCancelled(OrderItemCancelledPayload payload)
    {
        if (IsCurrentOrder(payload.OrderId) || IsCurrentTable(payload.TableId))
            QueueRealtimeRefresh();
    }

    private void OnTableStatusUpdated(TableStatusUpdatedPayload payload)
    {
        if (IsCurrentTable(payload.TableId))
            QueueRealtimeRefresh();
    }

    private void OnPaymentCompleted(PaymentCompletedPayload payload)
    {
        if (!IsCurrentOrder(payload.OrderId) && !IsCurrentTable(payload.TableId))
            return;

        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(async () =>
            {
                if (IsDisposed) return;

                // Đơn đã được thiết bị khác thanh toán: không giữ món/giỏ cục bộ
                // của lượt cũ để tránh thao tác tiếp trên hóa đơn đã đóng.
                ClearCart();
                _existingOrder = null;
                _selectedCustomer = null;
                txtCustomerPhone.Clear();
                UpdateCustomerInfoLabel();
                RenderCart();

                try { await RefreshTablesAsync(); } catch { }
            });
        }
        catch (InvalidOperationException) { }
    }

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
        catch (InvalidOperationException) { }
    }

    private async Task RefreshCurrentOrderRealtimeAsync()
    {
        try
        {
            if (_isTakeaway)
            {
                if (_existingOrder?.OrderId is int orderId && orderId > 0)
                    await LoadTakeawayOrderAsync(orderId, silent: true);
                return;
            }

            if (_selectedTableId is int tableId && tableId > 0)
                await LoadOpenOrderAsync(tableId, silent: true);
        }
        catch
        {
            // Realtime refresh không được làm gián đoạn thao tác bán hàng.
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Ba nguồn dữ liệu độc lập: tải song song để màn POS mở nhanh hơn.
            var menuTask = _api.GetMenuAsync();
            var tablesTask = RefreshTablesAsync();
            var campaignsTask = RefreshCampaignsAsync();

            await Task.WhenAll(menuTask, tablesTask, campaignsTask);

            if (IsDisposed || Disposing)
                return;

            _allMenuItems = (await menuTask)
                .OrderBy(m => m.CategoryId)
                .ThenBy(m => m.Id)
                .ToList();

            RenderCategoryTabs();
            FilterCategory(_selectedCategoryId);

            if (_preselectedTakeawayOrderId is > 0)
                await LoadTakeawayOrderAsync(_preselectedTakeawayOrderId.Value);
            else if (_preselectedTakeawayOrderId == 0)
                SetMode(takeaway: true); // 0 = mở sẵn chế độ Mang về để tạo đơn mới
        }
        catch (OperationCanceledException)
        {
            // Chuyển màn hình trong lúc đang tải: không hiện popup lỗi.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing && Visible)
                MessageBox.Show($"Không tải được dữ liệu: {ex.Message}");
        }
    }

    // Nap danh sach khuyen mai DANG hieu luc vao ComboBox (goi lai sau moi lan
    // thanh toan xong de bat kip truong hop 1 chien dich vua het han/bi tat giua
    // luc dang ban hang). Luon co san muc "(Không áp dụng)" o dau danh sach.
    private async Task RefreshCampaignsAsync()
    {
        try
        {
            _activeCampaigns = await _api.GetActiveCampaignsAsync();
        }
        catch
        {
            _activeCampaigns = new List<ActiveCampaignDto>(); // khong chan luong ban hang chi vi loi tai khuyen mai
        }

        cbCampaign.Items.Clear();
        cbCampaign.Items.Add("(Không áp dụng)");
        cbCampaign.Items.AddRange(_activeCampaigns.Cast<object>().ToArray());
        cbCampaign.SelectedIndex = 0;
    }

    // Tai lai danh sach ban (dung sau khi thanh toan xong de cap nhat trang thai
    // Trong/Đang phục vụ moi nhat), co gang giu nguyen ban dang chon neu con ton tai.
    private async Task RefreshTablesAsync()
    {
        // "Bàn mang về" (khu vực hệ thống "Mang về" do Mobile tạo) không phải bàn ăn tại chỗ:
        // ẩn khỏi ô chọn bàn, xử lý ở tab "Mang về".
        var tables = (await _api.GetTablesAsync())
            .Where(t => !string.Equals(t.AreaName?.Trim(), "Mang về", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var keepTableId = _selectedTableId ?? _preselectedTableId;

        cbTable.Items.Clear();
        foreach (var t in tables) cbTable.Items.Add(t);
        cbTable.DisplayMember = "TableName";

        if (tables.Count == 0) return;

        var indexToSelect = keepTableId is null ? 0 : tables.FindIndex(t => t.Id == keepTableId.Value);
        cbTable.SelectedIndex = indexToSelect >= 0 ? indexToSelect : 0;
    }

    private void CbTable_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isTakeaway) return; // dang ban mang ve: bo qua thay doi combobox ban (vd sau khi nap lai danh sach ban)

        if (cbTable.SelectedItem is TableDto t)
        {
            _selectedTableId = t.Id;
            lblTableName.Text = t.TableName;
            ClearCart();
            _existingOrder = null;
            if (cbCampaign.Items.Count > 0) cbCampaign.SelectedIndex = 0;

            // Đổi bàn = bắt đầu lượt bán khác -> bỏ khách đang gắn của lượt trước.
            _selectedCustomer = null;
            txtCustomerPhone.Clear();
            UpdateCustomerInfoLabel();

            RenderCart();

            // Lấy đơn đang mở của bàn (các món đã gọi) rồi vẽ lại giỏ hàng.
            _ = LoadOpenOrderAsync(t.Id);
        }
    }

    private async Task LoadOpenOrderAsync(int tableId, bool silent = false)
    {
        try
        {
            var order = await _api.GetOpenOrderByTableAsync(tableId);

            // Người dùng đã chọn bàn khác trong lúc chờ -> bỏ kết quả này.
            if (_selectedTableId != tableId) return;

            _existingOrder = order;

            // Khách thành viên đã gắn với đơn/lượt đặt bàn -> tự điền SĐT, tên, điểm, email.
            ApplyOrderCustomer(order?.Customer);

            RenderCart();
        }
        catch (Exception ex)
        {
            if (!silent && !IsDisposed && Visible)
                MessageBox.Show($"Không tải được các món của bàn: {ex.Message}");
        }
    }

    // Thanh chon hinh thuc ban "Tai ban | Mang ve" o dau khung gio hang. Doi toan bo control co san
    // trong panelRight xuong 40px de nhuong cho thanh nay (khong phai sua Designer).
    private const int ModeBarHeight = 40;

    private void BuildModeBar()
    {
        foreach (Control c in panelRight.Controls) c.Top += ModeBarHeight;

        btnDineIn.SetBounds(16, 8, 183, 32);
        btnTakeaway.SetBounds(203, 8, 183, 32);
        foreach (var b in new[] { btnDineIn, btnTakeaway })
        {
            b.Font = btnAllCategory.Font;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            panelRight.Controls.Add(b);
        }
        StyleCategoryButton(btnDineIn, true);
        StyleCategoryButton(btnTakeaway, false);
        btnDineIn.Click += (s, e) => SetMode(takeaway: false);
        btnTakeaway.Click += (s, e) => SetMode(takeaway: true);

        // Nút "Gửi bếp - trả sau": chỉ hiện khi bán MANG VỀ (đơn mới). Tạo đơn + gửi bếp,
        // chưa thanh toán; đơn nằm ở tab "Mang về" chờ khách lấy đồ rồi thanh toán.
        btnSendKitchen.SetBounds(0, 302, 370, 42);
        btnSendKitchen.Font = btnVietQr.Font;
        btnSendKitchen.FlatStyle = FlatStyle.Flat;
        btnSendKitchen.Cursor = Cursors.Hand;
        StyleCategoryButton(btnSendKitchen, false);
        btnSendKitchen.Visible = false;
        btnSendKitchen.Click += async (s, e) => await SendTakeawayToKitchenAsync();
        panelTotals.Controls.Add(btnSendKitchen);
    }

    private void BuildReceiptPreviewButton()
    {
        // Chèn nút xem bill mà không sửa Designer thủ công.
        btnReceiptPreview.SetBounds(0, 154, 370, 34);
        btnReceiptPreview.Font = btnVietQr.Font;
        btnReceiptPreview.FlatStyle = FlatStyle.Flat;
        btnReceiptPreview.ForeColor = AppColors.OnSurfaceVariant;
        btnReceiptPreview.Cursor = Cursors.Hand;
        btnReceiptPreview.FlatAppearance.BorderColor = AppColors.OutlineVariant;

        // VietQR nằm một hàng riêng.
        btnVietQr.SetBounds(0, 196, 370, 42);

        // Hàng thao tác cuối:
        // - Xác nhận gọi món: nhỏ hơn, màu xanh, chỉ gửi món xuống đơn/bếp.
        // - Thanh toán: lớn hơn, chỉ hoàn tất sau bước xem bill + xác nhận.
        // Hai nút cuối cân bằng nhau: 181px + khoảng cách 8px + 181px.
        btnConfirmOrder.SetBounds(0, 246, 181, 46);
        btnConfirmOrder.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnConfirmOrder.FlatStyle = FlatStyle.Flat;
        btnConfirmOrder.FlatAppearance.BorderSize = 0;
        btnConfirmOrder.BackColor = Color.FromArgb(46, 125, 50);
        btnConfirmOrder.ForeColor = Color.White;
        btnConfirmOrder.Cursor = Cursors.Hand;
        btnConfirmOrder.UseVisualStyleBackColor = false;

        btnThanhToan.SetBounds(189, 246, 181, 46);

        panelTotals.Controls.Add(btnReceiptPreview);
        panelTotals.Controls.Add(btnConfirmOrder);
        panelTotals.Height = 300;

        // Bill có QR thanh toán chỉ dành cho Quản lý / Thu ngân.
        btnReceiptPreview.Visible = CanViewPaymentReceiptQr;
        btnVietQr.Visible = CanViewPaymentReceiptQr;

        btnReceiptPreview.Click += async (s, e) =>
            await ShowTemporaryReceiptAsync();
    }

    // Lưu các món đang chọn vào Order và gửi xuống bếp/quầy nhưng KHÔNG thanh toán.
    // Sau khi thành công, tải lại đơn từ Backend để phần "Món đã gọi" là nguồn sự thật.
    private async Task ConfirmOrderAsync()
    {
        if (!_isTakeaway && _selectedTableId is null)
        {
            MessageBox.Show("Vui lòng chọn bàn trước.");
            return;
        }

        if (CartIsEmpty)
        {
            MessageBox.Show(
                "Chưa có món mới để xác nhận.",
                "Xác nhận gọi món",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_isTakeaway && _existingOrder is not null)
        {
            MessageBox.Show(
                "Đơn mang về này đã được gửi bếp. Vui lòng tạo đơn mang về mới nếu cần gọi thêm.",
                "Xác nhận gọi món",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        btnConfirmOrder.Enabled = false;
        btnThanhToan.Enabled = false;
        btnVietQr.Enabled = false;

        try
        {
            var items = BuildOrderItems();
            if (items.Count == 0) return;

            if (_isTakeaway)
            {
                var orderId = await _api.AddTakeawayOrderItemsAsync(_userId, items);
                if (orderId is null)
                {
                    MessageBox.Show("Không tạo được đơn mang về.");
                    return;
                }

                ClearCart();
                await LoadTakeawayOrderAsync(orderId.Value);
            }
            else
            {
                var orderId = await _api.AddOrderItemsAsync(
                    _selectedTableId!.Value,
                    _userId,
                    items);

                if (orderId is null)
                {
                    MessageBox.Show("Không ghi nhận được món vừa gọi.");
                    return;
                }

                ClearCart();
                await LoadOpenOrderAsync(_selectedTableId.Value);
            }

            RenderCart();
            UpdateSendKitchenButton();

            MessageBox.Show(
                "Đã xác nhận gọi món và gửi xuống bộ phận chế biến.",
                "Gọi món thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không xác nhận được món: {ex.Message}",
                "Lỗi gọi món",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!btnConfirmOrder.IsDisposed)
                btnConfirmOrder.Enabled = true;
            if (!btnThanhToan.IsDisposed)
                btnThanhToan.Enabled = true;
            if (!btnVietQr.IsDisposed)
                btnVietQr.Enabled = true;
        }
    }

    private void SetMode(bool takeaway)
    {
        if (_isTakeaway == takeaway) return;
        if (!CartIsEmpty &&
            MessageBox.Show("Giỏ hàng hiện tại sẽ bị xóa khi đổi hình thức. Tiếp tục?", "Đổi hình thức",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _isTakeaway = takeaway;
        StyleCategoryButton(btnDineIn, !takeaway);
        StyleCategoryButton(btnTakeaway, takeaway);

        // Mang ve: khong can chon ban -> an o chon ban.
        lblTableLabel.Visible = !takeaway;
        cbTable.Visible = !takeaway;

        ClearCart();
        _existingOrder = null;
        if (cbCampaign.Items.Count > 0) cbCampaign.SelectedIndex = 0;
        _selectedCustomer = null;
        txtCustomerPhone.Clear();
        UpdateCustomerInfoLabel();

        if (takeaway)
        {
            lblTableName.Text = "Mang về";
            RenderCart();
        }
        else
        {
            // Quay lai tai ban: nap lai ban dang chon trong combobox (+ cac mon da goi cua ban do).
            lblTableName.Text = cbTable.SelectedItem is TableDto t ? t.TableName : "Chưa chọn bàn";
            CbTable_SelectedIndexChanged(null, EventArgs.Empty);
        }

        UpdateSendKitchenButton();
    }

    private void ClearCart()
    {
        _cart.Clear();
        _cartTake.Clear();
    }

    // Hien nut "Gui bep - tra sau" (va mo rong khung tong tien) chi khi dang ban MANG VE don moi.
    private void UpdateSendKitchenButton()
    {
        var show = false;
        btnSendKitchen.Visible = show;
        panelTotals.Height = show ? 350 : 300;
        FitLayout();
    }

    // Mo 1 don MANG VE dang mo (da gui bep truoc do) de thanh toan.
    private async Task LoadTakeawayOrderAsync(int orderId, bool silent = false)
    {
        SetMode(takeaway: true);

        var order = await _api.GetOrderByIdAsync(orderId);
        if (order is null)
        {
            _existingOrder = null;
            RenderCart();
            if (!silent)
                MessageBox.Show("Không tìm thấy đơn mang về.");
            return;
        }

        _existingOrder = new OpenTableOrderDto
        {
            OrderId = order.Id,
            TableId = 0,
            TableName = "Mang về",
            TotalAmount = order.TotalAmount,
            Items = order.OrderItems
                .Where(i => i.KitchenStatus != (int)KitchenStatus.DaHuy)
                .Select(i => new OpenTableOrderItemDto
                {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    Name = i.MenuItem?.Name ?? "Món",
                    Quantity = i.Quantity,
                    UnitPrice = i.MenuItem?.Price ?? 0,
                    Status = i.KitchenStatus,
                    Note = i.Note,
                    IsTakeaway = true
                })
                .ToList()
        };

        lblTableName.Text = $"Mang về (đơn #{order.Id})";
        RenderCart();
        UpdateSendKitchenButton();
    }

    // Chuyen 1 phan cua mon giua dong "an tai cho" va dong "mang ve".
    private void MoveOneUnit(MenuItemDto item, bool fromTakeaway)
    {
        var from = fromTakeaway ? _cartTake : _cart;
        var to = fromTakeaway ? _cart : _cartTake;
        if (!from.TryGetValue(item.Id, out var cur)) return;

        if (cur.Qty <= 1) from.Remove(item.Id); else from[item.Id] = (item, cur.Qty - 1);
        to[item.Id] = to.TryGetValue(item.Id, out var t) ? (item, t.Qty + 1) : (item, 1);
        RenderCart();
    }

    private List<OrderItemRequest> BuildOrderItems()
    {
        var dineOrAll = _cart.Values.Select(v => new OrderItemRequest
        {
            MenuItemId = v.Item.Id,
            Quantity = v.Qty,
            IsTakeaway = _isTakeaway
        });
        var takeLines = _cartTake.Values.Select(v => new OrderItemRequest
        {
            MenuItemId = v.Item.Id,
            Quantity = v.Qty,
            IsTakeaway = true
        });
        return dineOrAll.Concat(takeLines).ToList();
    }

    private async Task SendTakeawayToKitchenAsync()
    {
        if (!_isTakeaway || _existingOrder is not null) return;
        if (CartIsEmpty)
        {
            MessageBox.Show("Giỏ hàng đang trống");
            return;
        }

        btnSendKitchen.Enabled = false;
        try
        {
            var orderId = await _api.AddTakeawayOrderItemsAsync(_userId, BuildOrderItems());
            if (orderId is null)
            {
                MessageBox.Show("Không tạo được đơn hàng (Backend không trả về mã đơn).");
                return;
            }

            MessageBox.Show("Đã gửi bếp. Đơn mang về đang chờ thanh toán ở tab \"Mang về\".");
            ClearCart();
            if (cbCampaign.Items.Count > 0) cbCampaign.SelectedIndex = 0;
            _selectedCustomer = null;
            txtCustomerPhone.Clear();
            UpdateCustomerInfoLabel();
            RenderCart();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}");
        }
        finally
        {
            btnSendKitchen.Enabled = true;
        }
    }

    // Can lai bo cuc theo kich thuoc that cua khung (Designer dat kich thuoc co dinh).
    private void FitLayout()
    {
        // Ben trai: thanh danh muc tren cung, luoi mon chiem phan con lai.
        var leftW = panelLeft.ClientSize.Width;
        flowCategoryTabs.SetBounds(0, 0, leftW, flowCategoryTabs.Height);
        var gridTop = flowCategoryTabs.Bottom + 8;
        flowMenuTiles.SetBounds(0, gridTop, leftW, Math.Max(100, panelLeft.ClientSize.Height - gridTop));

        // Ben phai: khung tong tien ep sat day, danh sach mon trong gio gian phan giua.
        panelTotals.Top = Math.Max(flowCartItems.Top + 120, panelRight.ClientSize.Height - panelTotals.Height - 16);
        flowCartItems.Height = Math.Max(100, panelTotals.Top - flowCartItems.Top - 8);
    }

    // Tao cac nut loc theo LOAI MON tu chinh danh sach mon (khong can goi them API).
    private void RenderCategoryTabs()
    {
        for (int i = flowCategoryTabs.Controls.Count - 1; i >= 0; i--)
        {
            var c = flowCategoryTabs.Controls[i];
            if (c != btnAllCategory) { flowCategoryTabs.Controls.RemoveAt(i); c.Dispose(); }
        }

        StyleCategoryButton(btnAllCategory, _selectedCategoryId is null);

        var categories = _allMenuItems
            .GroupBy(m => new { m.CategoryId, m.CategoryName })
            .OrderBy(g => g.Key.CategoryId);

        foreach (var g in categories)
        {
            var id = g.Key.CategoryId;
            var btn = new Button
            {
                Text = string.IsNullOrWhiteSpace(g.Key.CategoryName) ? $"Loại {id}" : g.Key.CategoryName,
                Font = btnAllCategory.Font,
                Height = btnAllCategory.Height,
                Margin = btnAllCategory.Margin,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
            };
            btn.Width = Math.Max(90, TextRenderer.MeasureText(btn.Text, btn.Font).Width + 28);
            btn.Tag = id;
            StyleCategoryButton(btn, _selectedCategoryId == id);
            btn.Click += (s, e) => FilterCategory(id);
            flowCategoryTabs.Controls.Add(btn);
        }
    }

    // Chi doi mau nut dang chon (KHONG dung lai nut, vi dang nam trong su kien Click cua chinh no).
    private void UpdateCategoryTabStyles()
    {
        foreach (var b in flowCategoryTabs.Controls.OfType<Button>())
            StyleCategoryButton(b, (b.Tag as int?) == _selectedCategoryId);
    }

    private static void StyleCategoryButton(Button btn, bool selected)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.UseVisualStyleBackColor = false;
        btn.Cursor = Cursors.Hand;
        if (selected)
        {
            btn.BackColor = AppColors.Primary;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
        }
        else
        {
            btn.BackColor = Color.White;
            btn.ForeColor = AppColors.OnSurface;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = AppColors.OutlineVariant;
        }
    }

    // (Truoc day loc nham theo DepartmentId - gio loc dung theo CategoryId.)
    private void FilterCategory(int? categoryId)
    {
        _selectedCategoryId = categoryId;
        UpdateCategoryTabStyles();
        var filtered = categoryId is null ? _allMenuItems : _allMenuItems.Where(m => m.CategoryId == categoryId).ToList();
        RenderMenuTiles(filtered);
    }

    private void RenderMenuTiles(List<MenuItemDto> items)
    {
        flowMenuTiles.SuspendLayout();
        // Dispose the cu de giai phong anh nen (BackgroundImage) cua tung the.
        foreach (Control old in flowMenuTiles.Controls.Cast<Control>().ToList())
        {
            flowMenuTiles.Controls.Remove(old);
            old.Dispose();
        }
        foreach (var item in items)
            flowMenuTiles.Controls.Add(BuildMenuTile(item));
        flowMenuTiles.ResumeLayout();
    }

    private const int TileWidth = 176;
    private const int TileThumbHeight = 100;
    private const int TilePad = 8;

    private Panel BuildMenuTile(MenuItemDto item)
    {
        // Chieu cao cac dong TINH THEO FONT thuc te (khong hardcode pixel) de khong bi cat chu
        // khi may dung man hinh phong to 125%/150%.
        int nameLineH = TextRenderer.MeasureText("Ag", AppFonts.LabelLg).Height;
        int priceLineH = TextRenderer.MeasureText("Ag", AppFonts.LabelLg).Height;

        int nameY = TileThumbHeight + 6;
        int nameH = nameLineH * 2 + 2;                  // toi da 2 dong, du thi "..."
        int priceY = nameY + nameH + 2;
        int btnY = priceY + priceLineH + 6;
        int btnH = 34;
        int tileH = btnY + btnH + TilePad;

        var tile = new Panel { Size = new Size(TileWidth, tileH), BackColor = Color.White, Margin = new Padding(8) };
        var thumb = new Panel { Size = new Size(TileWidth, TileThumbHeight), Location = new Point(0, 0), BackColor = AppColors.SurfaceContainer };
        thumb.Disposed += (s, e) => thumb.BackgroundImage?.Dispose();
        if (!string.IsNullOrWhiteSpace(item.ImageUrl))
            _ = LoadTileImageAsync(thumb, item.ImageUrl);

        var lblName = new Label
        {
            Text = item.Name,
            Font = AppFonts.LabelLg,
            Location = new Point(TilePad, nameY),
            Size = new Size(TileWidth - TilePad * 2, nameH),
            AutoEllipsis = true,
        };
        _tip.SetToolTip(lblName, item.Name); // re chuot vao xem ten day du

        var lblPrice = new Label { Text = $"{item.Price:N0}đ", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(TilePad, priceY), AutoSize = true };

        var btnOrder = new Button
        {
            Text = item.IsAvailable ? "+  Gọi món" : "Hết hàng",
            Location = new Point(TilePad, btnY),
            Size = new Size(TileWidth - TilePad * 2, btnH),
            FlatStyle = FlatStyle.Flat,
            BackColor = item.IsAvailable ? AppColors.Primary : AppColors.SurfaceContainer,
            ForeColor = item.IsAvailable ? Color.White : AppColors.OnSurfaceVariant,
            Enabled = item.IsAvailable,
            Cursor = item.IsAvailable ? Cursors.Hand : Cursors.Default,
        };
        btnOrder.FlatAppearance.BorderSize = 0;
        btnOrder.Click += (s, e) => AddToCart(item);

        tile.Controls.Add(thumb);
        tile.Controls.Add(lblName);
        tile.Controls.Add(lblPrice);
        tile.Controls.Add(btnOrder);
        tile.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, tile.Width - 1, tile.Height - 1);
        return tile;
    }

    private async Task LoadTileImageAsync(Panel thumb, string? imageUrl)
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
        }
    }

    // Cat anh cho day vua khung (kieu "cover"), giu ty le, khong bi meo.
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

    private void AddToCart(MenuItemDto item)
    {
        if (!_isTakeaway && _selectedTableId is null)
        {
            MessageBox.Show("Vui lòng chọn bàn trước");
            return;
        }

        if (_isTakeaway && _existingOrder is not null)
        {
            MessageBox.Show("Đơn mang về này đã gửi bếp, không thể gọi thêm món. Vui lòng tạo đơn mang về mới.");
            return;
        }

        if (_cart.ContainsKey(item.Id))
        {
            var (i, qty) = _cart[item.Id];
            _cart[item.Id] = (i, qty + 1);
        }
        else
        {
            _cart[item.Id] = (item, 1);
        }
        RenderCart();
    }

    private void RenderCart()
    {
        flowCartItems.Controls.Clear();
        decimal subtotal = 0;

        // 1) Các món ĐÃ GỌI của bàn (từ đơn đang mở) - chỉ đọc
        var existingItems = _existingOrder?.Items ?? new List<OpenTableOrderItemDto>();
        if (existingItems.Count > 0)
        {
            flowCartItems.Controls.Add(BuildSectionHeader("Món đã gọi"));
            foreach (var old in existingItems)
            {
                subtotal += old.UnitPrice * old.Quantity;
                flowCartItems.Controls.Add(BuildExistingRow(old));
            }
        }

        // 2) Các món THÊM MỚI tại quầy (sẽ được gửi lên khi thanh toán)
        if (_cart.Count > 0)
        {
            if (existingItems.Count > 0 || _cartTake.Count > 0)
                flowCartItems.Controls.Add(BuildSectionHeader(existingItems.Count > 0 ? "Gọi thêm" : "Ăn tại chỗ"));

            foreach (var (item, qty) in _cart.Values)
            {
                subtotal += item.Price * qty;
                flowCartItems.Controls.Add(BuildCartRow(item, qty));
            }
        }

        // 3) Các món khách yêu cầu MANG VỀ (cùng đơn, cùng hóa đơn)
        if (_cartTake.Count > 0)
        {
            flowCartItems.Controls.Add(BuildSectionHeader("Món mang về"));

            foreach (var (item, qty) in _cartTake.Values)
            {
                subtotal += item.Price * qty;
                flowCartItems.Controls.Add(BuildCartRow(item, qty, takeawayLine: true));
            }
        }

        // Backend (PaymentsController) hien khong tinh VAT (Invoice.TaxAmount luon = 0)
        // nen o day khong duoc tu bia them 8% VAT nhu ban cu - se lam so tien hien thi
        // tren giao dien khac voi so tien Backend thuc su ghi nhan.
        lblSubtotalValue.Text = $"{subtotal:N0}đ";
        lblVatValue.Text = "0đ";

        var discount = GetEstimatedDiscount(subtotal);
        var hasDiscount = discount > 0;
        lblDiscountCaption.Visible = hasDiscount;
        lblDiscountValue.Visible = hasDiscount;
        lblDiscountValue.Text = $"-{discount:N0}đ";

        lblTotalValue.Text = $"{subtotal - discount:N0}đ";
    }

    // Lay % giam cua khuyen mai dang duoc chon trong cbCampaign (index 0 = "(Không
    // áp dụng)"), tinh so tien giam uoc tinh tren tam tinh hien tai.
    private decimal GetEstimatedDiscount(decimal subtotal)
    {
        if (cbCampaign.SelectedIndex <= 0 || cbCampaign.SelectedItem is not ActiveCampaignDto campaign)
            return 0;

        return Math.Round(subtotal * campaign.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
    }

    // Ma chien dich khuyen mai dang duoc chon (null = khong ap dung khuyen mai nao).
    private int? GetSelectedCampaignId()
        => cbCampaign.SelectedIndex > 0 && cbCampaign.SelectedItem is ActiveCampaignDto campaign ? campaign.Id : null;

    private static string KitchenStatusText(int status) => status switch
    {
        0 => "Chờ chế biến",
        1 => "Đang chế biến",
        2 => "Đã xong",
        3 => "Đã phục vụ",
        _ => ""
    };

    private Label BuildSectionHeader(string text)
        => new Label
        {
            Text = text,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.OnSurfaceVariant,
            Size = new Size(360, 24),
            Margin = new Padding(0, 4, 0, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };

    // Dòng món ĐÃ GỌI (chỉ đọc, không có nút +/-)
    private Panel BuildExistingRow(OpenTableOrderItemDto item)
    {
        var row = new Panel { Size = new Size(360, 56), Margin = new Padding(0, 0, 0, 6) };

        var lblName = new Label { Text = $"{item.Quantity}x {item.Name}", Font = AppFonts.LabelLg, Location = new Point(0, 4), Size = new Size(270, 20), AutoEllipsis = true };
        var lblStatus = new Label { Text = KitchenStatusText(item.Status) + (item.IsTakeaway && !_isTakeaway ? " · Mang về" : ""), Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(0, 26), AutoSize = true };
        var lblSum = new Label { Text = $"{item.UnitPrice * item.Quantity:N0}đ", Font = AppFonts.LabelLg, Location = new Point(280, 16), Size = new Size(80, 20), TextAlign = ContentAlignment.MiddleRight };

        row.Controls.Add(lblName);
        row.Controls.Add(lblStatus);
        row.Controls.Add(lblSum);
        return row;
    }

    private Panel BuildCartRow(MenuItemDto item, int qty, bool takeawayLine = false)
    {
        var dict = takeawayLine ? _cartTake : _cart;

        // Dành hẳn một dòng cho nút chuyển mang về/tại chỗ.
        // Chiều cao cũ 72px dễ bị cắt ở Windows scale 125%/150%.
        var rowHeight = _isTakeaway ? 62 : 92;
        var row = new Panel
        {
            Size = new Size(360, rowHeight),
            Margin = new Padding(0, 0, 0, 8)
        };

        var lblName = new Label
        {
            Text = item.Name,
            Font = AppFonts.LabelLg,
            Location = new Point(0, 4),
            Size = new Size(170, 22),
            AutoEllipsis = true
        };

        var lblUnitPrice = new Label
        {
            Text = $"{item.Price:N0}đ / phần",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(0, 28),
            Size = new Size(170, 24),
            AutoEllipsis = true
        };

        var lblSum = new Label
        {
            Text = $"{item.Price * qty:N0}đ",
            Font = AppFonts.LabelLg,
            Location = new Point(280, 16),
            Size = new Size(80, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        var btnMinus = new Button
        {
            Text = "-",
            Size = new Size(26, 26),
            Location = new Point(178, 12),
            FlatStyle = FlatStyle.Flat
        };
        var lblQty = new Label
        {
            Text = qty.ToString(),
            Font = AppFonts.LabelLg,
            Location = new Point(207, 13),
            Size = new Size(30, 26),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var btnPlus = new Button
        {
            Text = "+",
            Size = new Size(26, 26),
            Location = new Point(240, 12),
            FlatStyle = FlatStyle.Flat
        };

        btnMinus.Click += (s, e) =>
        {
            var (i, q) = dict[item.Id];
            if (q <= 1) dict.Remove(item.Id);
            else dict[item.Id] = (i, q - 1);
            RenderCart();
        };

        btnPlus.Click += (s, e) =>
        {
            var (i, q) = dict[item.Id];
            dict[item.Id] = (i, q + 1);
            RenderCart();
        };

        // Bán tại chỗ: cho chuyển từng phần của món sang "mang về" và ngược lại.
        if (!_isTakeaway)
        {
            var btnMove = new Button
            {
                Text = takeawayLine ? "← Chuyển tại chỗ" : "Chuyển mang về →",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Size = new Size(166, 30),
                Location = new Point(0, 56),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };

            if (takeawayLine)
            {
                btnMove.BackColor = AppColors.Primary;
                btnMove.ForeColor = Color.White;
                btnMove.FlatAppearance.BorderSize = 0;
            }
            else
            {
                btnMove.BackColor = Color.White;
                btnMove.ForeColor = AppColors.OnSurfaceVariant;
                btnMove.FlatAppearance.BorderColor = AppColors.OutlineVariant;
                btnMove.FlatAppearance.BorderSize = 1;
            }

            btnMove.Click += (s, e) => MoveOneUnit(item, takeawayLine);
            row.Controls.Add(btnMove);
        }

        row.Controls.Add(lblName);
        row.Controls.Add(lblUnitPrice);
        row.Controls.Add(lblSum);
        row.Controls.Add(btnMinus);
        row.Controls.Add(lblQty);
        row.Controls.Add(btnPlus);
        return row;
    }

    // =====================================================================
    // KHÁCH HÀNG THÂN THIẾT (tìm theo SĐT để tự động tích điểm khi thanh toán)
    // =====================================================================

    private async Task FindCustomerAsync()
    {
        var phone = txtCustomerPhone.Text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            MessageBox.Show("Nhập số điện thoại khách hàng để tìm.");
            return;
        }

        try
        {
            var results = await _api.SearchCustomersByPhoneAsync(phone);
            if (results.Count == 0)
            {
                var create = MessageBox.Show("Không tìm thấy khách hàng với SĐT này. Thêm khách hàng mới?", "Không tìm thấy", MessageBoxButtons.YesNo);
                if (create == DialogResult.Yes) await CreateQuickCustomerAsync(phone);
                return;
            }

            // Ưu tiên kết quả khớp CHÍNH XÁC SĐT vừa gõ, nếu không có thì lấy kết
            // quả gần đúng đầu tiên (vd khách chỉ gõ vài số cuối).
            _selectedCustomer = results.FirstOrDefault(c => c.Phone == phone) ?? results[0];
            UpdateCustomerInfoLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tìm được khách hàng: {ex.Message}");
        }
    }

    private async Task CreateQuickCustomerAsync(string phone)
    {
        using var dialog = new Form { Text = "Thêm khách hàng mới", Width = 340, Height = 290, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var lblPhone = new Label { Text = $"SĐT: {phone}", Font = AppFonts.BodyMd, Location = new Point(20, 20), AutoSize = true };
        var lblNameCap = new Label { Text = "Họ tên *", Font = AppFonts.BodyMd, Location = new Point(20, 50), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 70), Width = 280, Font = AppFonts.BodyMd };
        var lblEmailCap = new Label { Text = "Email (để gửi hóa đơn)", Font = AppFonts.BodyMd, Location = new Point(20, 105), AutoSize = true };
        var txtEmail = new TextBox { Location = new Point(20, 125), Width = 280, Font = AppFonts.BodyMd };
        var btnSave = new Button { Text = "Lưu", Location = new Point(100, 175), Size = new Size(120, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(lblPhone);
        dialog.Controls.Add(lblNameCap);
        dialog.Controls.Add(txtName);
        dialog.Controls.Add(lblEmailCap);
        dialog.Controls.Add(txtEmail);
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Vui lòng nhập họ tên.");
            return;
        }

        var email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
        if (email is not null && !email.Contains('@'))
        {
            MessageBox.Show("Email không hợp lệ.");
            return;
        }

        try
        {
            var id = await _api.CreateCustomerAsync(txtName.Text.Trim(), phone, email, null);
            _selectedCustomer = new CustomerSearchResultDto { Id = id, FullName = txtName.Text.Trim(), Phone = phone, Email = email, LoyaltyPoint = 0, MemberLevel = 0 };
            UpdateCustomerInfoLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thêm được khách hàng: {ex.Message}");
        }
    }

    // Tự điền khách thành viên đã gắn với đơn/lượt đặt bàn (trả về từ GET /api/orders/table/{id}).
    // Không ghi đè nếu thu ngân đã chọn khách khác.
    private void ApplyOrderCustomer(CustomerSearchResultDto? customer)
    {
        if (customer is null || _selectedCustomer is not null) return;

        _selectedCustomer = customer;
        txtCustomerPhone.Text = customer.Phone;
        UpdateCustomerInfoLabel();
    }

    private void UpdateCustomerInfoLabel()
    {
        if (_selectedCustomer is null)
        {
            lblCustomerInfo.Text = "Khách vãng lai (không tích điểm)";
            lblCustomerInfo.ForeColor = AppColors.OnSurfaceVariant;
            _tip.SetToolTip(lblCustomerInfo, "");
        }
        else
        {
            var email = string.IsNullOrWhiteSpace(_selectedCustomer.Email)
                ? "chưa có email"
                : _selectedCustomer.Email;
            lblCustomerInfo.Text = $"✓ {_selectedCustomer.FullName} · {_selectedCustomer.LoyaltyPoint:N0} điểm · {email}";
            lblCustomerInfo.ForeColor = AppColors.Success;
            _tip.SetToolTip(lblCustomerInfo, lblCustomerInfo.Text);
        }
    }

    // Luong that, khop dung Backend:
    // 1. POST /api/orders/add-items -> tao/lay Order dang mo cua ban + luu OrderItems tu _cart,
    //    Backend tu kich hoat OrderRoutingService (phan luong Bep/Quầy nước) ngay sau do.
    // 2. POST /api/payments voi OrderId vua nhan (+ CustomerId neu co gan khach) -> Backend tu
    //    tinh TotalAmount that, tich diem cho khach (neu co) va tra ve QRCodeContent that neu la VietQR.
    private async Task ShowTemporaryReceiptAsync()
    {
        if (CartIsEmpty && _existingOrder is null)
        {
            MessageBox.Show("Chưa có món để xem bill tạm tính.");
            return;
        }

        btnReceiptPreview.Enabled = false;
        try
        {
            var lines = new List<InvoiceLineDto>();

            if (_existingOrder is not null)
            {
                lines.AddRange(
                    _existingOrder.Items
                        .Where(i => i.Status != (int)KitchenStatus.DaHuy)
                        .Select(i => new InvoiceLineDto
                        {
                            Name = i.Name,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            LineTotal = i.UnitPrice * i.Quantity,
                            IsTakeaway = i.IsTakeaway
                        }));
            }

            lines.AddRange(
                _cart.Values.Select(v => new InvoiceLineDto
                {
                    Name = v.Item.Name,
                    Quantity = v.Qty,
                    UnitPrice = v.Item.Price,
                    LineTotal = v.Item.Price * v.Qty,
                    IsTakeaway = _isTakeaway
                }));

            lines.AddRange(
                _cartTake.Values.Select(v => new InvoiceLineDto
                {
                    Name = v.Item.Name,
                    Quantity = v.Qty,
                    UnitPrice = v.Item.Price,
                    LineTotal = v.Item.Price * v.Qty,
                    IsTakeaway = true
                }));

            var subtotal = lines.Sum(x => x.LineTotal);
            var discount = GetEstimatedDiscount(subtotal);
            var total = Math.Max(0m, subtotal - discount);
            var orderId = _existingOrder?.OrderId ?? 0;

            // Profile/QR lấy từ Backend dùng chung, không hard-code lại tài khoản.
            var profile = await _api.GetReceiptPreviewAsync(total, orderId);
            if (profile is null)
            {
                MessageBox.Show("Không tải được thông tin bill/QR.");
                return;
            }

            // Nếu đã có order thì lấy đúng người phục vụ và giờ vào từ Backend.
            PaymentPreviewOrderDto? orderMeta = null;
            if (orderId > 0)
            {
                try
                {
                    orderMeta = await _api.GetPaymentPreviewAsync(orderId);
                }
                catch
                {
                    // Bill vẫn xem được; chỉ thiếu metadata phục vụ/giờ vào.
                }
            }

            var tempInvoice = new InvoiceDetailDto
            {
                Id = orderId,
                OrderId = orderId,
                InvoiceDate = DateTime.Now,
                OrderTime = orderMeta?.OrderTime,
                TableName = _isTakeaway
                    ? (_existingOrder?.TableName ?? "Mang về")
                    : (cbTable.SelectedItem is TableDto t ? t.TableName : lblTableName.Text),
                OrderType = _isTakeaway ? "MangVe" : "TaiBan",
                WaiterName = !string.IsNullOrWhiteSpace(orderMeta?.WaiterName)
                    ? orderMeta!.WaiterName
                    : SessionContext.FullName,
                CashierName = "",
                Items = lines,
                SubTotal = subtotal,
                DiscountAmount = discount,
                TaxAmount = 0,
                TotalAmount = total,
                Status = 0,
                ShopName = profile.ShopName,
                ShopAddress = profile.ShopAddress,
                BankName = profile.BankName,
                BankAccountNo = profile.BankAccountNo,
                BankAccountName = profile.BankAccountName,
                BankBranch = profile.BankBranch,
                TransferContent = profile.TransferContent,
                QrImageUrl = profile.QrImageUrl,
                QrPayload = profile.QrPayload,
            };

            await ShowInvoiceReceiptDialogAsync(
                tempInvoice,
                "PHIẾU TẠM TÍNH",
                showPaymentQr: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không xem được bill tạm tính: {ex.Message}",
                "Xem bill",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!btnReceiptPreview.IsDisposed)
                btnReceiptPreview.Enabled = true;
        }
    }

    private async Task<InvoiceDetailDto> BuildPaymentConfirmationInvoiceAsync(
        int orderId,
        PaymentPreviewOrderDto preview)
    {
        // Sau khi các món mới đã được lưu vào Order, đồng bộ lại đơn từ Backend
        // để bill xác nhận phản ánh đúng dữ liệu thực tế và tránh gửi trùng món nếu
        // người dùng đóng/hủy bước thanh toán rồi thử lại.
        if (_isTakeaway)
        {
            await LoadTakeawayOrderAsync(orderId);
        }
        else if (_selectedTableId.HasValue)
        {
            await LoadOpenOrderAsync(_selectedTableId.Value);
        }

        var lines = (_existingOrder?.Items ?? new List<OpenTableOrderItemDto>())
            .Where(i => i.Status != (int)KitchenStatus.DaHuy)
            .Select(i => new InvoiceLineDto
            {
                Name = i.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.UnitPrice * i.Quantity,
                IsTakeaway = i.IsTakeaway
            })
            .ToList();

        return new InvoiceDetailDto
        {
            Id = orderId,
            OrderId = orderId,
            InvoiceDate = DateTime.Now,
            OrderTime = preview.OrderTime,
            TableName = _isTakeaway
                ? (_existingOrder?.TableName ?? "Mang về")
                : (cbTable.SelectedItem is TableDto t ? t.TableName : lblTableName.Text),
            OrderType = _isTakeaway ? "MangVe" : "TaiBan",
            WaiterName = !string.IsNullOrWhiteSpace(preview.WaiterName)
                ? preview.WaiterName
                : SessionContext.FullName,
            // Đây là người đang chuẩn bị xác nhận. Backend vẫn là nguồn cuối cùng
            // ghi ConfirmedByUserId khi CreatePaymentAsync chạy thành công.
            CashierName = SessionContext.FullName,
            Items = lines,
            SubTotal = preview.SubTotal,
            DiscountAmount = preview.DiscountAmount,
            TaxAmount = 0,
            TotalAmount = preview.TotalAmount,
            Status = 0,
            ShopName = preview.ShopName,
            ShopAddress = preview.ShopAddress,
            BankName = preview.BankName,
            BankAccountNo = preview.BankAccountNo,
            BankAccountName = preview.BankAccountName,
            BankBranch = preview.BankBranch,
            TransferContent = preview.TransferContent,
            QrImageUrl = preview.QrImageUrl,
            QrPayload = preview.QrPayload,
        };
    }

    private async Task ProcessPaymentAsync(int method)
    {
        if (!_isTakeaway && _selectedTableId is null)
        {
            MessageBox.Show("Vui lòng chọn bàn trước");
            return;
        }

        if (CartIsEmpty && _existingOrder is null)
        {
            MessageBox.Show("Giỏ hàng đang trống");
            return;
        }

        btnThanhToan.Enabled = false;
        btnVietQr.Enabled = false;

        try
        {
            var items = BuildOrderItems();
            var hadNewItems = items.Count > 0;

            // Bước 1: bảo đảm các món mới đã nằm trong Order, nhưng CHƯA tạo Payment.
            int? orderId = _isTakeaway
                ? (_existingOrder is not null && !hadNewItems
                    ? _existingOrder.OrderId
                    : await _api.AddTakeawayOrderItemsAsync(_userId, items))
                : hadNewItems
                    ? await _api.AddOrderItemsAsync(_selectedTableId!.Value, _userId, items)
                    : _existingOrder?.OrderId;

            if (orderId is null)
            {
                MessageBox.Show("Không tạo được đơn hàng (Backend không trả về mã đơn).");
                return;
            }

            // Các món vừa gửi đã thuộc Order. Xóa giỏ local trước khi đồng bộ lại để
            // nếu người dùng hủy bước xác nhận, lần bấm sau không gửi trùng món.
            if (hadNewItems)
                ClearCart();

            var campaignId = GetSelectedCampaignId();

            // Bước 2: chỉ PREVIEW số tiền/QR. Endpoint này không tạo Payment/Invoice.
            var preview = await _api.GetPaymentPreviewAsync(
                orderId.Value,
                campaignId);

            if (preview is null)
            {
                MessageBox.Show("Không tải được thông tin xác nhận thanh toán.");
                return;
            }

            var confirmationInvoice = await BuildPaymentConfirmationInvoiceAsync(
                orderId.Value,
                preview);

            var paymentLabel = method == 1 ? "VIETQR / CHUYỂN KHOẢN" : "TIỀN MẶT";

            // Bước 3: hiện BILL trước. Chỉ nút xác nhận trên bill mới cho phép
            // đi tiếp sang CreatePaymentAsync.
            var confirmed = await ShowInvoiceReceiptDialogAsync(
                confirmationInvoice,
                $"XÁC NHẬN THANH TOÁN - {paymentLabel}",
                showPaymentQr: method == 1,
                requirePaymentConfirmation: true);

            if (!confirmed)
            {
                RenderCart();
                UpdateSendKitchenButton();
                return;
            }

            // Bước 4: lúc này mới thật sự hoàn tất thanh toán.
            var payment = await _api.CreatePaymentAsync(
                orderId.Value,
                method,
                campaignId,
                _selectedCustomer?.Id,
                transactionRef: method == 1 ? preview.TransferContent : null);

            if (payment is null)
            {
                MessageBox.Show(
                    "Thanh toán chưa được ghi nhận. Đơn vẫn ở trạng thái chưa thanh toán.",
                    "Thanh toán thất bại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                $"Thanh toán thành công.\nHóa đơn #{payment.InvoiceId}\nTổng tiền: {payment.TotalAmount:N0}đ",
                "Hoàn tất thanh toán",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Báo riêng phần điểm tích lũy (nếu có gắn khách).
            if (payment.CustomerId.HasValue && payment.PointsEarned > 0)
            {
                var emailNote = payment.EmailSent
                    ? "\nĐã gửi hóa đơn qua email cho khách."
                    : "";

                MessageBox.Show(
                    $"Đã tích {payment.PointsEarned:N0} điểm cho khách hàng.\n" +
                    $"Số điểm hiện có: {payment.LoyaltyPointBalance:N0} điểm.{emailNote}",
                    "Tích điểm thành công");
            }
            else if (payment.EmailSent)
            {
                MessageBox.Show(
                    "Đã gửi hóa đơn qua email cho khách hàng.",
                    "Gửi email thành công");
            }

            ClearCart();
            _existingOrder = null;

            if (_isTakeaway)
                lblTableName.Text = "Mang về";

            RenderCart();
            UpdateSendKitchenButton();

            if (cbCampaign.Items.Count > 0)
                cbCampaign.SelectedIndex = 0;

            _selectedCustomer = null;
            txtCustomerPhone.Clear();
            UpdateCustomerInfoLabel();

            await RefreshTablesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}");
        }
        finally
        {
            btnThanhToan.Enabled = true;
            btnVietQr.Enabled = true;
        }
    }

    // Hiển thị QR ngân hàng thật theo số tiền Backend vừa preview.
    // Chỉ khi Thu ngân xác nhận đã nhận chuyển khoản thì mới tạo Payment/Invoice.
    private async Task<bool> ShowVietQrDialogAsync(
        PaymentPreviewOrderDto preview)
    {
        using var dialog = new Form
        {
            Text = "Thanh toán QR / Chuyển khoản",
            Width = 420,
            Height = 610,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
        };

        var lblTitle = new Label
        {
            Text = "QUÉT QR ĐỂ THANH TOÁN",
            Font = AppFonts.HeadlineSm,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 18),
            Size = new Size(360, 30),
        };

        var pic = new PictureBox
        {
            Location = new Point(75, 58),
            Size = new Size(260, 260),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var lblAmount = new Label
        {
            Text = $"{preview.TotalAmount:N0}đ",
            Font = AppFonts.HeadlineSm,
            ForeColor = AppColors.Primary,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 326),
            Size = new Size(360, 34),
        };

        var lblBank = new Label
        {
            Text =
                $"{preview.BankAccountName}\n" +
                $"{preview.BankName} · {preview.BankAccountNo}\n" +
                $"{preview.BankBranch}\n" +
                $"Nội dung: {preview.TransferContent}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(30, 368),
            Size = new Size(340, 90),
        };

        var lblHint = new Label
        {
            Text = "Chỉ bấm xác nhận sau khi đã kiểm tra tiền vào tài khoản.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.Error,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(30, 462),
            Size = new Size(340, 42),
        };

        var btnCancel = new Button
        {
            Text = "Hủy",
            DialogResult = DialogResult.Cancel,
            Location = new Point(44, 515),
            Size = new Size(130, 38),
            FlatStyle = FlatStyle.Flat,
        };

        var btnConfirm = new Button
        {
            Text = "Đã nhận tiền",
            DialogResult = DialogResult.OK,
            Location = new Point(210, 515),
            Size = new Size(150, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };
        btnConfirm.FlatAppearance.BorderSize = 0;

        dialog.Controls.AddRange(new Control[]
        {
            lblTitle,
            pic,
            lblAmount,
            lblBank,
            lblHint,
            btnCancel,
            btnConfirm,
        });

        dialog.AcceptButton = btnConfirm;
        dialog.CancelButton = btnCancel;

        try
        {
            if (string.IsNullOrWhiteSpace(preview.QrPayload))
                throw new InvalidOperationException("Backend chưa trả payload VietQR.");

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(
                preview.QrPayload,
                QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);

            pic.Image?.Dispose();
            pic.Image = qrCode.GetGraphic(
                10,
                Color.Black,
                Color.White,
                true);
        }
        catch (Exception ex)
        {
            pic.Controls.Add(new Label
            {
                Text =
                    "Không tạo được QR.\n" +
                    ex.Message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AppColors.Error,
            });
            btnConfirm.Enabled = false;
        }

        return dialog.ShowDialog(FindForm()) == DialogResult.OK;
    }

    // =====================================================================
    // HÓA ĐƠN (Invoice) - xem chi tiết bill vừa thanh toán + tra cứu lịch sử
    // =====================================================================

    private static readonly string[] PaymentMethodLabels = { "Tiền mặt", "Thẻ ngân hàng", "Ví điện tử", "Chuyển khoản" };
    private static readonly string[] InvoiceStatusLabels = { "Chưa thanh toán", "Đã thanh toán", "Đã hủy" };

    // Hoa don day du kieu bien lai: liet ke tung mon (da loai bo mon bi huy o
    // Backend), tam tinh/giam gia/thue/tong cong, phuong thuc + thoi diem thanh
    // toan. Dung chung cho ca 2 luong: vua thanh toan xong VA tra cuu lai tu lich su.
    private async Task<bool> ShowInvoiceReceiptDialogAsync(
        InvoiceDetailDto invoice,
        string receiptTitle = "HÓA ĐƠN THANH TOÁN",
        bool showPaymentQr = false,
        bool requirePaymentConfirmation = false)
    {
        using var dialog = new Form
        {
            Text = $"Bill #{invoice.Id}",
            ClientSize = new Size(460, 790),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(238, 238, 238),
        };

        var paper = new Panel
        {
            Location = new Point(18, 14),
            Size = new Size(424, 720),
            BackColor = Color.White,
            AutoScroll = true,
        };
        dialog.Controls.Add(paper);

        var shopName = new Label
        {
            Text = string.IsNullOrWhiteSpace(invoice.ShopName)
                ? "Lẩu Bò Nhà Gỗ Sài Gòn"
                : invoice.ShopName,
            Font = AppFonts.HeadlineSm,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(12, 14),
            Size = new Size(400, 30),
        };

        var shopAddress = new Label
        {
            Text = invoice.ShopAddress,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 44),
            Size = new Size(384, 34),
        };

        var lblTitle = new Label
        {
            Text = receiptTitle,
            Font = AppFonts.HeadlineSm,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(12, 82),
            Size = new Size(400, 30),
        };

        var paymentTime = invoice.Payments
            .OrderBy(p => p.PaymentTime)
            .LastOrDefault()?.PaymentTime
            ?? invoice.InvoiceDate;

        var referenceText = invoice.Id > 0
            ? $"Số HĐ/Order: #{invoice.Id}"
            : "Phiếu tạm tính chưa tạo hóa đơn";

        var cashierLine = string.IsNullOrWhiteSpace(invoice.CashierName)
            ? ""
            : $"\nThu ngân: {invoice.CashierName}";

        var meta = new Label
        {
            Text =
                $"{referenceText}\n" +
                $"Bàn: {invoice.TableName}    Ngày: {paymentTime:dd/MM/yyyy}\n" +
                $"Giờ vào: {(invoice.OrderTime.HasValue ? invoice.OrderTime.Value.ToString("HH:mm") : "-")}    Giờ ra: {(receiptTitle == "PHIẾU TẠM TÍNH" ? "-" : paymentTime.ToString("HH:mm"))}\n" +
                $"Phục vụ: {(string.IsNullOrWhiteSpace(invoice.WaiterName) ? "-" : invoice.WaiterName)}" +
                cashierLine,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurface,
            Location = new Point(20, 118),
            Size = new Size(384, 88),
        };

        var flowItems = new FlowLayoutPanel
        {
            Location = new Point(20, 214),
            Size = new Size(384, 230),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };

        if (invoice.Items.Count == 0)
        {
            flowItems.Controls.Add(new Label
            {
                Text = "(Không có món nào)",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurfaceVariant,
                AutoSize = true,
                Margin = new Padding(6),
            });
        }
        else
        {
            foreach (var line in invoice.Items)
                flowItems.Controls.Add(
                    BuildInvoiceLineRow(
                        line,
                        !string.Equals(
                            invoice.OrderType,
                            "MangVe",
                            StringComparison.OrdinalIgnoreCase)));
        }

        var totalQty = invoice.Items.Sum(x => x.Quantity);
        var lblTotals = new Label
        {
            Text =
                $"Tổng số món: {totalQty:N0}\n" +
                $"Tạm tính: {invoice.SubTotal:N0}đ\n" +
                (invoice.DiscountAmount > 0
                    ? $"Giảm giá: -{invoice.DiscountAmount:N0}đ\n"
                    : "") +
                $"TỔNG TIỀN: {invoice.TotalAmount:N0}đ",
            Font = AppFonts.LabelLg,
            Location = new Point(20, 454),
            Size = new Size(384, invoice.DiscountAmount > 0 ? 82 : 64),
        };

        var qrTop = invoice.DiscountAmount > 0 ? 542 : 524;
        var picQr = new PictureBox
        {
            Location = new Point(122, qrTop),
            Size = new Size(180, 180),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
        };

        var lblBank = new Label
        {
            Text =
                $"{invoice.BankAccountName}\n" +
                $"{invoice.BankName} · {invoice.BankAccountNo}\n" +
                $"{invoice.BankBranch}\n" +
                $"Nội dung: {invoice.TransferContent}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, qrTop + 184),
            Size = new Size(384, 72),
        };

        paper.Controls.Add(shopName);
        paper.Controls.Add(shopAddress);
        paper.Controls.Add(lblTitle);
        paper.Controls.Add(meta);
        paper.Controls.Add(flowItems);
        paper.Controls.Add(lblTotals);

        if (showPaymentQr && CanViewPaymentReceiptQr)
        {
            paper.Controls.Add(picQr);
            paper.Controls.Add(lblBank);
        }

        if (showPaymentQr && CanViewPaymentReceiptQr && invoice.TotalAmount > 0)
        {
            try
            {
                var qrPayload = invoice.QrPayload;

                // Bill tạm tính đã nhận payload cùng lúc với tổng tiền.
                // Chỉ fallback gọi preview order cho dữ liệu cũ chưa có QrPayload.
                if (string.IsNullOrWhiteSpace(qrPayload) && invoice.OrderId > 0)
                {
                    var preview = await _api.GetPaymentPreviewAsync(invoice.OrderId);
                    qrPayload = preview?.QrPayload ?? "";
                }

                if (string.IsNullOrWhiteSpace(qrPayload))
                    throw new InvalidOperationException("Không có payload VietQR cho bill này.");

                using var qrGenerator = new QRCodeGenerator();
                using var qrData = qrGenerator.CreateQrCode(
                    qrPayload,
                    QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new QRCode(qrData);

                picQr.Image?.Dispose();
                picQr.Image = qrCode.GetGraphic(
                    8,
                    Color.Black,
                    Color.White,
                    true);
            }
            catch (Exception ex)
            {
                picQr.Controls.Add(new Label
                {
                    Text =
                        "Không tạo được QR.\n" +
                        ex.Message,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = AppColors.Error,
                });
            }
        }

        if (requirePaymentConfirmation)
        {
            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(54, 744),
                Size = new Size(150, 36),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = AppColors.OnSurface,
            };
            btnCancel.FlatAppearance.BorderColor = AppColors.OutlineVariant;

            var btnConfirm = new Button
            {
                Text = "Xác nhận thanh toán",
                Location = new Point(218, 744),
                Size = new Size(190, 36),
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Primary,
                ForeColor = Color.White,
            };
            btnConfirm.FlatAppearance.BorderSize = 0;

            dialog.Controls.Add(btnCancel);
            dialog.Controls.Add(btnConfirm);
            dialog.AcceptButton = btnConfirm;
            dialog.CancelButton = btnCancel;

            return dialog.ShowDialog(FindForm()) == DialogResult.OK;
        }

        var btnClose = new Button
        {
            Text = "Đóng",
            Location = new Point(150, 744),
            Size = new Size(160, 36),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };
        btnClose.FlatAppearance.BorderSize = 0;

        dialog.Controls.Add(btnClose);
        dialog.AcceptButton = btnClose;
        dialog.ShowDialog(FindForm());
        return false;
    }

    private Panel BuildInvoiceLineRow(InvoiceLineDto line, bool showTakeawayTag = false)
    {
        var row = new Panel { Size = new Size(405, 46), Margin = new Padding(6, 4, 6, 4) };
        var lblName = new Label { Text = $"{line.Name} x{line.Quantity}", Font = AppFonts.BodyMd, Location = new Point(0, 0), Size = new Size(290, 22), AutoEllipsis = true };
        var lblUnit = new Label { Text = $"{line.UnitPrice:N0}đ/món", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(0, 24), AutoSize = true };
        var lblLineTotal = new Label { Text = $"{line.LineTotal:N0}đ", Font = AppFonts.LabelMd, Location = new Point(295, 10), Size = new Size(105, 22), TextAlign = ContentAlignment.MiddleRight };
        row.Controls.Add(lblName);
        row.Controls.Add(lblUnit);
        row.Controls.Add(lblLineTotal);

        if (showTakeawayTag && line.IsTakeaway)
        {
            var lblTag = new Label
            {
                Text = "· Mang về",
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.Primary,
                AutoSize = true
            };
            row.Controls.Add(lblTag);
            lblTag.Location = new Point(lblUnit.PreferredWidth + 4, 24);
        }

        return row;
    }

    // Danh sach hoa don trong ngay hom nay - bam vao 1 dong de xem lai chi tiet
    // (dung lai ShowInvoiceReceiptDialog o tren), phuc vu tinh huong khach can
    // xem/kiem tra lai hoa don cu, hoac Thu ngan doi ca can ra soat.
    private async Task OpenInvoiceHistoryDialogAsync()
    {
        using var dialog = new Form
        {
            Text = "Lịch sử hóa đơn hôm nay",
            Width = 460,
            Height = 560,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var flowInvoices = new FlowLayoutPanel { Location = new Point(16, 16), Size = new Size(410, 460), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var btnClose = new Button { Text = "Đóng", Location = new Point(170, 486), Size = new Size(110, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White };

        dialog.Controls.Add(flowInvoices);
        dialog.Controls.Add(btnClose);
        dialog.AcceptButton = btnClose;

        flowInvoices.Controls.Add(new Label { Text = "Đang tải...", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true });

        try
        {
            var invoices = await _api.GetInvoicesAsync(DateTime.Today);
            flowInvoices.Controls.Clear();

            if (invoices.Count == 0)
            {
                flowInvoices.Controls.Add(new Label { Text = "Chưa có hóa đơn nào hôm nay.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true });
            }
            else
            {
                foreach (var inv in invoices.OrderByDescending(i => i.InvoiceDate))
                    flowInvoices.Controls.Add(BuildInvoiceHistoryRow(inv));
            }
        }
        catch (Exception ex)
        {
            flowInvoices.Controls.Clear();
            flowInvoices.Controls.Add(new Label { Text = $"Không tải được: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true });
        }

        dialog.ShowDialog();
    }

    private Panel BuildInvoiceHistoryRow(InvoiceSummaryDto inv)
    {
        var row = new Panel { Size = new Size(390, 54), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var lblId = new Label { Text = $"Hóa đơn #{inv.Id}", Font = AppFonts.LabelLg, Location = new Point(12, 8), AutoSize = true, Cursor = Cursors.Hand };
        var lblMeta = new Label
        {
            Text = $"{inv.TableName} · {inv.InvoiceDate:HH:mm} · PV: {(string.IsNullOrWhiteSpace(inv.WaiterName) ? "-" : inv.WaiterName)} · TN: {(string.IsNullOrWhiteSpace(inv.CashierName) ? "-" : inv.CashierName)}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(12, 30),
            Size = new Size(260, 20),
            AutoEllipsis = true,
            Cursor = Cursors.Hand
        };
        var lblTotal = new Label { Text = $"{inv.TotalAmount:N0}đ", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(280, 16), AutoSize = true, Cursor = Cursors.Hand };

        row.Controls.Add(lblId);
        row.Controls.Add(lblMeta);
        row.Controls.Add(lblTotal);

        async void OpenDetail(object? s, EventArgs e)
        {
            try
            {
                var detail = await _api.GetInvoiceAsync(inv.Id);
                if (detail is not null) await ShowInvoiceReceiptDialogAsync(detail);
                else MessageBox.Show("Không tìm thấy hóa đơn này.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không tải được hóa đơn: {ex.Message}");
            }
        }

        row.Click += OpenDetail;
        lblId.Click += OpenDetail;
        lblMeta.Click += OpenDetail;
        lblTotal.Click += OpenDetail;

        return row;
    }
}