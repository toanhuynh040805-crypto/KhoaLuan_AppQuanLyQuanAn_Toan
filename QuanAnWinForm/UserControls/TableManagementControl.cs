using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QuanAnWinForm.Utils;

namespace QuanAnWinForm.UserControls;

public partial class TableManagementControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => RefreshRealtimeAsync(reloadTables: true, reloadItems: true);


    private readonly ApiService _api = new();
    private readonly int _userId;
    private List<TableDto> _tables = new();
    private List<AreaDto> _areas = new();
    private TableDto? _selectedTable;
    private HashSet<int> _paymentRequestedTableIds = new();

    private bool CanCompletePayment
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

    // Gom nhiều sự kiện realtime dồn dập (vd mobile tick nhiều món liên tiếp) thành 1 lần tải lại
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 300 };
    private bool _pendingReloadTables;
    private bool _pendingReloadItems;
    private bool _refreshRunning;

    private Button? _btnGoToSales;
    private Button? _btnEditTable;
    private Button? _btnDeleteTable;
    private Button? _btnCheckIn;

    public event Action<int>? GoToSalesRequested;

    public TableManagementControl() : this(0) { }

    public TableManagementControl(int userId)
    {
        _userId = userId;
        InitializeComponent();

        Load += async (s, e) =>
        {
            SubscribeRealtime();
            await LoadAllAsync();
        };
        btnAddTable.Click += (s, e) => OpenAddOrEditTableDialog(null);
        btnManageAreas.Click += (s, e) => OpenManageAreasDialog();
        btnReservations.Click += (s, e) => OpenReservationsDialog();

        cbFilterStatus.Items.AddRange(new object[] { "Tất cả trạng thái", "Trống", "Có khách", "Đã đặt", "Đang dọn dẹp" });
        cbFilterStatus.SelectedIndex = 0;
        cbFilterStatus.SelectedIndexChanged += (s, e) => RenderTableGrid();
        txtSearchTable.TextChanged += (s, e) => RenderTableGrid();

        _refreshTimer.Tick += async (s, e) => await OnRefreshTimerTickAsync();

        // Sự kiện realtime là static -> bắt buộc hủy đăng ký khi control bị Dispose để tránh rò rỉ
        Disposed += (s, e) =>
        {
            UnsubscribeRealtime();
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        };
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _areas = await _api.GetAreasAsync();
            _tables = NormalizePhysicalTables(
                (await _api.GetTablesAsync())
                    .Where(t => !IsTakeawaySlot(t)));

            if (CanCompletePayment)
            {
                try
                {
                    _paymentRequestedTableIds =
                        await _api.GetPendingPaymentTableIdsAsync();
                }
                catch
                {
                    _paymentRequestedTableIds.Clear();
                }
            }
            else
            {
                _paymentRequestedTableIds.Clear();
            }

            RenderTableGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được sơ đồ bàn: {ex.Message}");
        }
    }

    // "Bàn mang về" (khu vực hệ thống "Mang về" do Mobile tạo) không phải bàn vật lý:
    // ẩn khỏi sơ đồ bàn, được quản lý ở tab "Mang về".
    private static bool IsTakeawaySlot(TableDto t)
        => string.Equals(t.AreaName?.Trim(), "Mang về", StringComparison.OrdinalIgnoreCase);

    // DB cũ có thể tồn tại nhiều DiningTable khác TableId nhưng trùng tên trong
    // cùng khu. Chỉ hiển thị một bàn đại diện, ưu tiên bàn đang có nghiệp vụ:
    // Có khách > Đã đặt > Đang dọn > Trống; nếu ngang nhau lấy TableId mới hơn.
    // Không xóa record cũ ở đây để tránh ảnh hưởng lịch sử đơn/đặt bàn.
    private static List<TableDto> NormalizePhysicalTables(
        IEnumerable<TableDto> source)
    {
        return source
            .GroupBy(table => new
            {
                table.AreaId,
                Name = NormalizeTableName(table.TableName)
            })
            .Select(group => group
                .OrderByDescending(table => TableDisplayPriority(table.Status))
                .ThenByDescending(table => table.Id)
                .First())
            .OrderBy(table => table.AreaName)
            .ThenBy(table => table.TableName)
            .ToList();
    }

    private static string NormalizeTableName(string? value)
        => (value ?? "")
            .Trim()
            .ToUpperInvariant();

    private static int TableDisplayPriority(int status) => status switch
    {
        1 => 400, // Có khách
        2 => 300, // Đã đặt
        3 => 200, // Đang dọn
        _ => 100  // Trống
    };

    private IEnumerable<TableDto> FilterTables()
    {
        var keyword = txtSearchTable.Text.Trim();
        var statusFilter = cbFilterStatus.SelectedIndex;

        return _tables.Where(t =>
        {
            if (statusFilter > 0 && t.Status != statusFilter - 1) return false;
            if (keyword.Length == 0) return true;
            return t.TableName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || t.AreaName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void RenderTableGrid()
    {
        flowTables.Controls.Clear();
        var filtered = FilterTables().ToList();

        if (filtered.Count == 0)
        {
            flowTables.Controls.Add(new Label { Text = "Không tìm thấy bàn nào phù hợp.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        foreach (var table in filtered)
        {
            flowTables.Controls.Add(BuildTableTile(table));
        }
    }

    private (Color border, Color bg, string label) StyleFor(int status) => status switch
    {
        1 => (AppColors.TableCoKhach, Color.White, "Có khách"),
        2 => (AppColors.Warning, Color.White, "Đã đặt"),
        3 => (AppColors.TableDonDep, Color.White, "Đang dọn dẹp"),
        _ => (AppColors.TableTrong, Color.FromArgb(235, 247, 243), "Trống"),
    };

    private Panel BuildTableTile(TableDto table)
    {
        var (border, bg, label) = StyleFor(table.Status);
        var waitingPayment =
            CanCompletePayment &&
            _paymentRequestedTableIds.Contains(table.Id);

        if (waitingPayment)
        {
            border = Color.FromArgb(220, 74, 58);
            bg = Color.FromArgb(255, 238, 233);
            label = "Chờ thanh toán";
        }

        var linked = table.IsLinked;
        if (linked && !waitingPayment)
            bg = Color.FromArgb(255, 244, 238); // nền kem nhạt = đang ghép

        var tile = new Panel
        {
            Size = new Size(140, 110),
            BackColor = bg,
            Margin = new Padding(8),
            Cursor = Cursors.Hand,
            Tag = table,
        };
        tile.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(border, 2), 1, 1, tile.Width - 3, tile.Height - 3);

        var lblName = new Label { Text = table.TableName, Font = AppFonts.HeadlineSm, Location = new Point(12, 10), AutoSize = true };
        var lblCapacity = new Label { Text = $"{table.Capacity} khách", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(12, 36), AutoSize = true };
        var lblStatus = new Label { Text = label, Font = AppFonts.LabelMd, ForeColor = border, Location = new Point(12, 78), AutoSize = true };

        tile.Controls.Add(lblName);
        tile.Controls.Add(lblCapacity);
        tile.Controls.Add(lblStatus);

        void onClick(object? s, EventArgs e) => ShowDetail(table);
        tile.Click += onClick;
        lblName.Click += onClick;
        lblCapacity.Click += onClick;
        lblStatus.Click += onClick;

        if (linked)
        {
            var lblLink = new Label
            {
                Text = "Ghép: " + table.LinkedWith,
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.OnSurfaceVariant,
                Location = new Point(12, 58),
                Size = new Size(120, 18),
                AutoEllipsis = true,
            };
            lblLink.Click += onClick;
            tile.Controls.Add(lblLink);
        }

        return tile;
    }

    private void ShowDetail(TableDto table)
    {
        _selectedTable = table;
        ApplyDetailHeader(table);
        panelDetail.Visible = true;

        EnsureDetailActionButtons();
        _ = LoadServingItemsAsync(table.Id);
    }

    private void ApplyDetailHeader(TableDto table)
    {
        lblDetailName.Text = table.TableName;
        lblDetailInfo.Text = $"{table.AreaName} · Sức chứa: {table.Capacity} khách";
        var (_, _, label) = StyleFor(table.Status);

        if (CanCompletePayment &&
            _paymentRequestedTableIds.Contains(table.Id))
        {
            label = "Chờ thanh toán";
        }

        lblDetailStatus.Text = table.IsLinked
            ? $"{label} · Ghép {table.LinkedWith}"
            : label;
    }

    private void EnsureDetailActionButtons()
    {
        if (_btnEditTable is null)
        {
            _btnEditTable = new Button
            {
                Text = "Sửa bàn",
                Location = new Point(16, 104),
                Size = new Size(126, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Secondary,
                ForeColor = Color.White,
            };
            _btnEditTable.FlatAppearance.BorderSize = 0;
            _btnEditTable.Click += (s, e) => { if (_selectedTable is not null) OpenAddOrEditTableDialog(_selectedTable); };
            panelDetail.Controls.Add(_btnEditTable);
        }

        if (_btnDeleteTable is null)
        {
            _btnDeleteTable = new Button
            {
                Text = "Xóa bàn",
                Location = new Point(158, 104),
                Size = new Size(126, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Error,
                ForeColor = Color.White,
            };
            _btnDeleteTable.FlatAppearance.BorderSize = 0;
            _btnDeleteTable.Click += async (s, e) => { if (_selectedTable is not null) await DeleteTableAsync(_selectedTable); };
            panelDetail.Controls.Add(_btnDeleteTable);
        }

        if (_btnCheckIn is null)
        {
            _btnCheckIn = new Button
            {
                Text = "Nhận bàn",
                Location = new Point(16, 144),
                Size = new Size(268, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Tertiary,
                ForeColor = Color.White,
            };
            _btnCheckIn.FlatAppearance.BorderSize = 0;
            _btnCheckIn.Click += async (s, e) => await CheckInSelectedTableAsync();
            panelDetail.Controls.Add(_btnCheckIn);
        }
        _btnCheckIn.Visible = _selectedTable!.Status == 0;

        if (_btnGoToSales is null)
        {
            _btnGoToSales = new Button
            {
                Text = "Sang bán hàng",
                Location = new Point(16, 192),
                Size = new Size(268, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Primary,
                ForeColor = Color.White,
            };
            _btnGoToSales.FlatAppearance.BorderSize = 0;
            panelDetail.Controls.Add(_btnGoToSales);
        }

        _btnGoToSales.Tag = _selectedTable!.Id;
        _btnGoToSales.Click -= BtnGoToSales_Click;
        _btnGoToSales.Click += BtnGoToSales_Click;
    }

    private async Task CheckInSelectedTableAsync()
    {
        if (_selectedTable is null) return;
        if (_btnCheckIn is not null) _btnCheckIn.Enabled = false;
        try
        {
            await _api.CheckInTableAsync(_selectedTable.Id, _userId);
            await LoadAllAsync();
            panelDetail.Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không nhận bàn được: {ex.Message}");
        }
        finally
        {
            if (_btnCheckIn is not null) _btnCheckIn.Enabled = true;
        }
    }

    private void BtnGoToSales_Click(object? sender, EventArgs e)
    {
        if (sender is Button { Tag: int tableId })
        {
            GoToSalesRequested?.Invoke(tableId);
        }
    }

    private static readonly string[] KitchenStatusLabels = { "Mới gọi", "Đang chế biến", "Đã chế biến", "Đã phục vụ", "Đã hủy" };
    private static Color KitchenStatusColor(int status) => status switch
    {
        0 => AppColors.Outline,
        1 => AppColors.Warning,
        2 => AppColors.Tertiary,
        3 => AppColors.Success,
        _ => AppColors.Error,
    };

    private async Task LoadServingItemsAsync(int tableId)
    {
        try
        {
            var items = await _api.GetOrderItemsByTableAsync(tableId);
            // Người dùng có thể đã chọn bàn khác trong lúc chờ API -> bỏ kết quả cũ
            if (_selectedTable?.Id != tableId) return;
            RenderServingItems(items);
        }
        catch (Exception ex)
        {
            if (_selectedTable?.Id != tableId) return;
            flowServingItems.Controls.Clear();
            flowServingItems.Controls.Add(new Label { Text = $"Không tải được: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true });
        }
    }

    private void RenderServingItems(List<OrderItemStatusDto> items)
    {
        flowServingItems.Controls.Clear();
        if (items.Count == 0)
        {
            flowServingItems.Controls.Add(new Label { Text = "Bàn chưa có món nào.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            return;
        }
        foreach (var item in items)
            flowServingItems.Controls.Add(BuildServingItemRow(item));
    }

    private Panel BuildServingItemRow(OrderItemStatusDto item)
    {
        var statusIndex = Math.Clamp(item.KitchenStatus, 0, KitchenStatusLabels.Length - 1);
        var row = new Panel { Size = new Size(268, 80), BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var lblName = new Label { Text = $"{item.Quantity}x {item.MenuItemName}", Font = AppFonts.LabelLg, Location = new Point(10, 6), Size = new Size(180, 20), AutoEllipsis = true };
        var lblTime = new Label
        {
            Text = item.CreatedAt > DateTime.MinValue
                ? TimeDisplay.FormatClock(item.CreatedAt)
                : "",
            Font = AppFonts.LabelMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(10, 28),
            Size = new Size(238, 18),
            AutoEllipsis = true
        };
        var lblStatus = new Label { Text = KitchenStatusLabels[statusIndex], Font = AppFonts.LabelMd, ForeColor = KitchenStatusColor(statusIndex), Location = new Point(10, 50), AutoSize = true };

        row.Controls.Add(lblName);
        row.Controls.Add(lblTime);
        row.Controls.Add(lblStatus);

        // Chỉ cho phép bấm "Đã phục vụ" khi món ở trạng thái Đã chế biến (DaCheBien = 2)
        if (statusIndex == 2)
        {
            var btnServe = new Button
            {
                Text = "Đã phục vụ",
                Location = new Point(150, 44),
                Size = new Size(108, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Tertiary,
                ForeColor = Color.White,
                Font = AppFonts.LabelMd,
            };
            btnServe.FlatAppearance.BorderSize = 0;
            btnServe.Click += async (s, e) =>
            {
                btnServe.Enabled = false;
                try
                {
                    await _api.UpdateOrderItemStatusAsync(item.Id, (int)KitchenStatus.DaPhucVu, _userId, null, item.RowVer);
                    if (_selectedTable is not null) await LoadServingItemsAsync(_selectedTable.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không cập nhật được: {ex.Message}", "Cảnh báo / Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnServe.Enabled = true;
                }
            };
            row.Controls.Add(btnServe);
        }

        return row;
    }

    // ================= REALTIME (SignalR) =================

    private void SubscribeRealtime()
    {
        // Gỡ trước rồi gắn lại để chắc chắn không bị đăng ký 2 lần
        UnsubscribeRealtime();
        NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
        NotificationHubClient.ItemStatusUpdated += OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled += OnOrderItemCancelled;
        NotificationHubClient.PaymentRequested += OnPaymentRequested;
        NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
        NotificationHubClient.OrderUpdated += OnOrderUpdated;
        NotificationHubClient.ConnectionRestored += OnConnectionRestored;
    }

    private void UnsubscribeRealtime()
    {
        NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
        NotificationHubClient.ItemStatusUpdated -= OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled -= OnOrderItemCancelled;
        NotificationHubClient.PaymentRequested -= OnPaymentRequested;
        NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
        NotificationHubClient.OrderUpdated -= OnOrderUpdated;
        NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
    }

    // Các handler này chạy trên luồng nền của SignalR -> phải đẩy về luồng giao diện
    private void OnTableStatusUpdated(TableStatusUpdatedPayload p) =>
        QueueRefresh(tables: true, items: false);

    private void OnItemStatusUpdated(ItemStatusUpdatedPayload p) =>
        QueueRefresh(tables: true, items: true);

    private void OnOrderItemCancelled(OrderItemCancelledPayload p) =>
        QueueRefresh(tables: true, items: true);

    private void OnPaymentRequested(PaymentRequestedPayload p) =>
        QueueRefresh(tables: true, items: false);

    private void OnPaymentCompleted(PaymentCompletedPayload p) =>
        QueueRefresh(tables: true, items: true);

    private void OnOrderUpdated(OrderUpdatedPayload p) =>
        QueueRefresh(tables: true, items: true);

    private void OnConnectionRestored() =>
        QueueRefresh(tables: true, items: true);

    private void QueueRefresh(bool tables, bool items)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                _pendingReloadTables |= tables;
                _pendingReloadItems |= items;
                _refreshTimer.Stop();
                _refreshTimer.Start(); // đặt lại bộ đếm 300ms
            });
        }
        catch (InvalidOperationException) { /* control đang bị hủy */ }
    }

    private async Task OnRefreshTimerTickAsync()
    {
        _refreshTimer.Stop();
        if (_refreshRunning)
        {
            _refreshTimer.Start(); // đang tải dở -> thử lại sau
            return;
        }

        var reloadTables = _pendingReloadTables;
        var reloadItems = _pendingReloadItems;
        _pendingReloadTables = false;
        _pendingReloadItems = false;

        _refreshRunning = true;
        try
        {
            await RefreshRealtimeAsync(reloadTables, reloadItems);
        }
        finally
        {
            _refreshRunning = false;
        }
    }

    private async Task RefreshRealtimeAsync(bool reloadTables, bool reloadItems)
    {
        try
        {
            if (reloadTables)
            {
                var selectedId = _selectedTable?.Id;
                // Giữ vị trí cuộn của lưới bàn (Clear() sẽ làm nhảy về đầu)
                var scroll = new Point(-flowTables.AutoScrollPosition.X, -flowTables.AutoScrollPosition.Y);

                _tables = (await _api.GetTablesAsync()).Where(t => !IsTakeawaySlot(t)).ToList();

                if (CanCompletePayment)
                {
                    try
                    {
                        _paymentRequestedTableIds =
                            await _api.GetPendingPaymentTableIdsAsync();
                    }
                    catch
                    {
                        // Giữ trạng thái pending hiện tại nếu endpoint tạm mất kết nối.
                    }
                }
                else
                {
                    _paymentRequestedTableIds.Clear();
                }

                RenderTableGrid();
                flowTables.AutoScrollPosition = scroll;

                if (selectedId.HasValue)
                {
                    var fresh = _tables.FirstOrDefault(t => t.Id == selectedId.Value);
                    if (fresh is null)
                    {
                        _selectedTable = null;
                        panelDetail.Visible = false;
                        return;
                    }

                    // _selectedTable đang trỏ tới object cũ -> gán lại bản mới,
                    // cập nhật tiêu đề, nút "Nhận bàn" và Tag của nút "Sang bán hàng"
                    _selectedTable = fresh;
                    ApplyDetailHeader(fresh);
                    EnsureDetailActionButtons();
                }
            }

            if ((reloadItems || reloadTables) && _selectedTable is not null && panelDetail.Visible)
                await LoadServingItemsAsync(_selectedTable.Id);
        }
        catch
        {
            // Mất mạng thoáng qua: bỏ qua, lần sự kiện sau sẽ tải lại
        }
    }

    private void OpenAddOrEditTableDialog(TableDto? existing)
    {
        if (_areas.Count == 0)
        {
            MessageBox.Show("Chưa có khu vực nào, hãy tạo khu vực trước khi thêm bàn.");
            return;
        }

        using var dialog = new Form
        {
            Text = existing is null ? "Thêm bàn" : "Sửa bàn",
            Width = 340,
            Height = 300,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblName = new Label { Text = "Tên bàn *", Font = AppFonts.BodyMd, Location = new Point(20, 20), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 40), Width = 280, Font = AppFonts.BodyMd, Text = existing?.TableName ?? "" };

        var lblCapacity = new Label { Text = "Sức chứa (số khách) *", Font = AppFonts.BodyMd, Location = new Point(20, 80), AutoSize = true };
        var numCapacity = new NumericUpDown { Location = new Point(20, 100), Width = 120, Minimum = 1, Maximum = 50, Value = existing?.Capacity ?? 4 };

        var lblArea = new Label { Text = "Khu vực *", Font = AppFonts.BodyMd, Location = new Point(20, 140), AutoSize = true };
        var cbArea = new ComboBox { Location = new Point(20, 160), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbArea.Items.AddRange(_areas.Cast<object>().ToArray());
        var preselect = existing is null ? 0 : _areas.FindIndex(a => a.Id == existing.AreaId);
        cbArea.SelectedIndex = preselect >= 0 ? preselect : 0;

        dialog.Controls.Add(lblName);
        dialog.Controls.Add(txtName);
        dialog.Controls.Add(lblCapacity);
        dialog.Controls.Add(numCapacity);
        dialog.Controls.Add(lblArea);
        dialog.Controls.Add(cbArea);

        var btnSave = new Button { Text = "Lưu", Location = new Point(110, 210), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(txtName.Text) || cbArea.SelectedItem is not AreaDto area)
        {
            MessageBox.Show("Vui lòng nhập tên bàn và chọn khu vực");
            return;
        }

        _ = SaveTableAsync(existing?.Id, txtName.Text.Trim(), (int)numCapacity.Value, area.Id);
    }

    private async Task SaveTableAsync(int? id, string tableName, int capacity, int areaId)
    {
        try
        {
            if (id.HasValue) await _api.UpdateTableAsync(id.Value, tableName, capacity, areaId);
            else await _api.CreateTableAsync(tableName, capacity, areaId);
            await LoadAllAsync();
            panelDetail.Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được bàn: {ex.Message}");
        }
    }

    private async Task DeleteTableAsync(TableDto table)
    {
        if (MessageBox.Show($"Xóa bàn \"{table.TableName}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            await _api.DeleteTableAsync(table.Id);
            panelDetail.Visible = false;
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }

    private void OpenManageAreasDialog()
    {
        using var dialog = new Form
        {
            Text = "Quản lý khu vực",
            Width = 420,
            Height = 480,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var flowAreas = new FlowLayoutPanel { Location = new Point(16, 16), Size = new Size(370, 340), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        dialog.Controls.Add(flowAreas);

        var txtNewArea = new TextBox { Location = new Point(16, 370), Width = 250, Font = AppFonts.BodyMd, PlaceholderText = "Tên khu vực mới" };
        var btnAddArea = new Button { Text = "+  Thêm", Location = new Point(276, 368), Size = new Size(110, 30), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        btnAddArea.FlatAppearance.BorderSize = 0;
        dialog.Controls.Add(txtNewArea);
        dialog.Controls.Add(btnAddArea);

        var btnClose = new Button { Text = "Đóng", Location = new Point(150, 410), Size = new Size(120, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White, DialogResult = DialogResult.OK };
        dialog.Controls.Add(btnClose);
        dialog.AcceptButton = btnClose;

        async Task RefreshAreaListAsync()
        {
            _areas = await _api.GetAreasAsync();
            flowAreas.Controls.Clear();
            foreach (var area in _areas)
                flowAreas.Controls.Add(BuildAreaRow(area, RefreshAreaListAsync));
        }

        btnAddArea.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtNewArea.Text))
            {
                MessageBox.Show("Vui lòng nhập tên khu vực");
                return;
            }
            try
            {
                await _api.CreateAreaAsync(txtNewArea.Text.Trim(), null);
                txtNewArea.Clear();
                await RefreshAreaListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thêm được khu vực: {ex.Message}");
            }
        };

        _ = RefreshAreaListAsync();
        dialog.ShowDialog();
        _ = LoadAllAsync();
    }

    private Panel BuildAreaRow(AreaDto area, Func<Task> onChanged)
    {
        var row = new Panel { Size = new Size(360, 44), BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var lblName = new Label { Text = area.Name, Font = AppFonts.LabelLg, Location = new Point(10, 12), AutoSize = true };
        var lblEdit = new Label { Text = "Sửa", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(240, 14), AutoSize = true, Cursor = Cursors.Hand };
        var lblDelete = new Label { Text = "Xóa", Font = AppFonts.LabelMd, ForeColor = AppColors.Error, Location = new Point(290, 14), AutoSize = true, Cursor = Cursors.Hand };

        lblEdit.Click += async (s, e) =>
        {
            var newName = PromptForText("Sửa tên khu vực", area.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == area.Name) return;
            try
            {
                await _api.UpdateAreaAsync(area.Id, newName.Trim(), area.Description);
                await onChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không sửa được: {ex.Message}");
            }
        };

        lblDelete.Click += async (s, e) =>
        {
            if (MessageBox.Show($"Xóa khu vực \"{area.Name}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                await _api.DeleteAreaAsync(area.Id);
                await onChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không xóa được: {ex.Message}");
            }
        };

        row.Controls.Add(lblName);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);
        return row;
    }

    private string? PromptForText(string title, string initialValue)
    {
        using var dialog = new Form { Text = title, Width = 320, Height = 170, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var txt = new TextBox { Location = new Point(20, 20), Width = 260, Font = AppFonts.BodyMd, Text = initialValue };
        var btnOk = new Button { Text = "Lưu", Location = new Point(90, 70), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(txt);
        dialog.Controls.Add(btnOk);
        dialog.AcceptButton = btnOk;
        return dialog.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }

    private void OpenReservationsDialog()
    {
        using var dialog = new Form
        {
            Text = "Đặt bàn trước",
            Width = 620,
            Height = 610,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblFilter = new Label { Text = "Trạng thái:", Font = AppFonts.BodyMd, Location = new Point(16, 18), AutoSize = true };
        var cbStatusFilter = new ComboBox { Location = new Point(120, 14), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        cbStatusFilter.Items.AddRange(new object[] { "Đang chờ (Đã xác nhận)", "Tất cả" });
        cbStatusFilter.SelectedIndex = 0;

        var btnNew = new Button { Text = "+ Đặt bàn mới", Location = new Point(430, 12), Size = new Size(150, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        btnNew.FlatAppearance.BorderSize = 0;

        // BO SUNG: che do "Lich su" - truoc day chi xem duoc dat ban HOM NAY tro
        // di (mac dinh khong loc theo ngay), khong the xem lai cac ngay QUA KHU
        // (da huy/hoan tat) cung ai la nguoi huy. Khi tick, chuyen sang chon 1
        // ngay bat ky + day du 6 trang thai (thay vi chi 2 muc Dang cho/Tat ca).
        var chkHistory = new CheckBox { Text = "Xem lịch sử theo ngày", Font = AppFonts.BodyMd, Location = new Point(16, 50), AutoSize = true };
        var dtpHistoryDate = new DateTimePicker { Location = new Point(260, 46), Width = 140, Format = DateTimePickerFormat.Short, Value = DateTime.Today, Visible = false };

        var flowReservations = new FlowLayoutPanel { Location = new Point(16, 88), Size = new Size(568, 424), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var btnClose = new Button { Text = "Đóng", Location = new Point(250, 522), Size = new Size(120, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White, DialogResult = DialogResult.OK };

        dialog.Controls.Add(lblFilter);
        dialog.Controls.Add(cbStatusFilter);
        dialog.Controls.Add(btnNew);
        dialog.Controls.Add(chkHistory);
        dialog.Controls.Add(dtpHistoryDate);
        dialog.Controls.Add(flowReservations);
        dialog.Controls.Add(btnClose);
        dialog.AcceptButton = btnClose;

        var upcomingStatusItems = new object[] { "Đang chờ (Đã xác nhận)", "Tất cả" };
        var historyStatusItems = new object[] { "Tất cả", "Đã xác nhận", "Đã nhận bàn", "Đã hủy", "Không đến", "Hoàn tất" };

        void ApplyModeToStatusCombo()
        {
            cbStatusFilter.Items.Clear();
            cbStatusFilter.Items.AddRange(chkHistory.Checked ? historyStatusItems : upcomingStatusItems);
            cbStatusFilter.SelectedIndex = 0;
            dtpHistoryDate.Visible = chkHistory.Checked;
        }

        async Task RefreshAsync()
        {
            flowReservations.Controls.Clear();
            flowReservations.Controls.Add(new Label { Text = "Đang tải...", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true });
            try
            {
                DateTime? dateFilter;
                int? statusFilter;
                if (chkHistory.Checked)
                {
                    // Lich su: luon loc theo 1 ngay cu the (co the la qua khu),
                    // muc 0 = "Tat ca" (khong loc trang thai), tu 1 tro di khop
                    // thang voi ReservationStatusEnum (index-1).
                    dateFilter = dtpHistoryDate.Value;
                    statusFilter = cbStatusFilter.SelectedIndex == 0 ? null : cbStatusFilter.SelectedIndex - 1;
                }
                else
                {
                    // Sap toi (mac dinh): khong loc ngay (Backend tu tra tu HOM
                    // NAY tro di), muc 0 = "Dang cho" (DaXacNhan = 0).
                    dateFilter = null;
                    statusFilter = cbStatusFilter.SelectedIndex == 0 ? 0 : null;
                }

                var reservations = await _api.GetReservationsAsync(date: dateFilter, status: statusFilter);

                flowReservations.Controls.Clear();
                if (reservations.Count == 0)
                {
                    flowReservations.Controls.Add(new Label { Text = "Không có đặt bàn nào.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 8, 0, 0) });
                    return;
                }
                foreach (var r in reservations)
                    flowReservations.Controls.Add(BuildReservationRow(r, RefreshAsync));
            }
            catch (Exception ex)
            {
                flowReservations.Controls.Clear();
                flowReservations.Controls.Add(new Label { Text = $"Không tải được: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true });
            }
        }

        cbStatusFilter.SelectedIndexChanged += async (s, e) => await RefreshAsync();
        chkHistory.CheckedChanged += (s, e) => ApplyModeToStatusCombo(); // set SelectedIndex=0 -> tu kich hoat SelectedIndexChanged ben duoi, khong goi RefreshAsync 2 lan
        dtpHistoryDate.ValueChanged += async (s, e) => await RefreshAsync();
        btnNew.Click += (s, e) => OpenAddReservationDialog(RefreshAsync);

        // Realtime: khi Mobile nhận bàn / hủy / đặt bàn, backend phát TableStatusUpdated
        // -> tải lại danh sách ngay trong hộp thoại (đang mở dạng modal nên phải tự đăng ký).
        void RefreshReservationDialog()
        {
            try
            {
                if (dialog.IsDisposed || !dialog.IsHandleCreated) return;
                dialog.BeginInvoke(new Action(async () =>
                {
                    if (dialog.IsDisposed) return;
                    await RefreshAsync();
                }));
            }
            catch (InvalidOperationException) { /* dialog đang đóng */ }
        }

        void OnReservationRealtime(TableStatusUpdatedPayload p) =>
            RefreshReservationDialog();

        void OnReservationUpdated(ReservationUpdatedPayload p) =>
            RefreshReservationDialog();

        NotificationHubClient.TableStatusUpdated += OnReservationRealtime;
        NotificationHubClient.ReservationUpdated += OnReservationUpdated;
        try
        {
            _ = RefreshAsync();
            dialog.ShowDialog();
        }
        finally
        {
            NotificationHubClient.TableStatusUpdated -= OnReservationRealtime;
            NotificationHubClient.ReservationUpdated -= OnReservationUpdated;
        }
        _ = LoadAllAsync();
    }

    private static readonly string[] ReservationStatusLabels = { "Đã xác nhận", "Đã nhận bàn", "Đã hủy", "Không đến", "Hoàn tất", "Chờ xác nhận" };
    private static Color ReservationStatusColor(int status) => status switch
    {
        1 => AppColors.Success,
        2 => AppColors.Error,
        3 => AppColors.Outline,
        4 => AppColors.Tertiary, // Hoan tat - khach da an xong & thanh toan, khong con giu cho nua
        _ => AppColors.Warning,
    };

    private Panel BuildReservationRow(ReservationDto r, Func<Task> onChanged)
    {
        var statusIndex = Math.Clamp(r.Status, 0, ReservationStatusLabels.Length - 1);

        // BO SUNG: hien "Huy boi: <nhan vien> luc <gio>" cho cac luot Da huy/Khong
        // den de xem lai lich su - chi co du lieu neu huy qua nut Huy (backend ghi
        // nhan tu JWT cua nguoi goi API, xem ReservationsController.UpdateStatus).
        var hasCancelInfo = (statusIndex == 2 || statusIndex == 3) && !string.IsNullOrWhiteSpace(r.CancelledByUserName);
        var rowHeight = hasCancelInfo ? 96 : 76;

        var row = new Panel { Size = new Size(548, rowHeight), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        // Hien ca gio den VA gio tra ban du kien (truoc day chi co gio den, khong
        // biet giu ban toi may gio) tren 1 dong rieng de khong dam vao cot trang thai.
        var holdUntil = r.HoldUntil == default ? r.ReservationEndTime : r.HoldUntil;
        var holdExpired = r.IsHoldExpired || DateTime.Now >= holdUntil;
        var lblTime = new Label
        {
            Text = $"{r.ReservationTime:HH:mm dd/MM} · Giữ đến {holdUntil:HH:mm}",
            Font = AppFonts.LabelLg,
            Location = new Point(10, 6),
            AutoSize = true
        };
        var lblTable = new Label { Text = $"{r.TableName} · {r.AreaName} · {r.NumberOfGuests} khách", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(10, 30), AutoSize = true };
        var customerText = string.IsNullOrWhiteSpace(r.CustomerName) && string.IsNullOrWhiteSpace(r.CustomerPhone)
            ? "(Chưa có thông tin khách)"
            : $"{r.CustomerName}  {r.CustomerPhone}".Trim();
        var lblCustomer = new Label { Text = customerText, Font = AppFonts.BodyMd, Location = new Point(10, 52), AutoSize = true };

        var lblStatus = new Label { Text = ReservationStatusLabels[statusIndex], Font = AppFonts.LabelMd, ForeColor = ReservationStatusColor(statusIndex), Location = new Point(300, 8), AutoSize = true };

        row.Controls.Add(lblTime);
        row.Controls.Add(lblTable);
        row.Controls.Add(lblCustomer);
        row.Controls.Add(lblStatus);
        // Nhãn nguồn đặt bàn: chỉ hiện "Web" cho lượt khách tự đặt trên website
        if (r.IsFromWeb)
        {
            var lblSource = new Label
            {
                Text = "🌐 Web",
                Font = AppFonts.LabelMd,
                ForeColor = Color.White,
                BackColor = AppColors.Secondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(60, 20),
                Location = new Point(468, 8)
            };
            row.Controls.Add(lblSource);
        }
        if (hasCancelInfo)
        {
            var lblCancelInfo = new Label
            {
                Text = $"Huỷ bởi: {r.CancelledByUserName} lúc {r.CancelledAt:HH:mm dd/MM/yyyy}",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurfaceVariant,
                Location = new Point(10, 74),
                AutoSize = true
            };
            row.Controls.Add(lblCancelInfo);
        }

        if (statusIndex == 0 || statusIndex == 5)
        {
            var btnCheckIn = new Button { Text = "Nhận bàn", Location = new Point(320, 32), Size = new Size(100, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Tertiary, ForeColor = Color.White, Font = AppFonts.LabelMd };
            btnCheckIn.FlatAppearance.BorderSize = 0;
            btnCheckIn.Click += async (s, e) =>
            {
                btnCheckIn.Enabled = false;
                try
                {
                    await _api.CheckInTableAsync(r.TableId, _userId, r.Id);
                    await onChanged();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không nhận bàn được: {ex.Message}");
                    btnCheckIn.Enabled = true;
                }
            };
            row.Controls.Add(btnCheckIn);

            var btnCancel = new Button
            {
                Text = holdExpired ? "Không đến" : "Hủy",
                Location = new Point(430, 32),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Error,
                ForeColor = Color.White,
                Font = AppFonts.LabelMd
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += async (s, e) =>
            {
                var confirmText = holdExpired
                    ? $"Đã quá thời gian giữ đến {holdUntil:HH:mm}. Đánh dấu \"{r.CustomerName}\" không đến và nhả bàn?"
                    : $"Hủy đặt bàn của \"{r.CustomerName}\"?";

                if (MessageBox.Show(confirmText, "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                btnCancel.Enabled = false;
                try
                {
                    if (holdExpired)
                        await _api.ReleaseOverdueReservationAsync(r.Id);
                    else
                        await _api.UpdateReservationStatusAsync(r.Id, 2);
                    await onChanged();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không hủy được: {ex.Message}");
                    btnCancel.Enabled = true;
                }
            };
            row.Controls.Add(btnCancel);
        }

        return row;
    }

    // Rang buoc "khong dat qua lau" (yeu cau moi): thoi luong giu ban toi thieu/toi
    // da, dung chung voi Backend (QuanAnBackend/Models/Reservation.cs) de UI va
    // API luon khop nhau. Buoc NumericUpDown (Increment=15) nen luon la boi so cua 15.
    // Phai khop tay voi Reservation.MinDurationMinutes/MaxDurationMinutes/DefaultDurationMinutes
    // ben Backend (QuanAnBackend/Models/Reservation.cs) - WinForm khong tham
    // chieu truc tiep duoc sang project Backend nen can sua CA 2 cho khop.
    private const int ReservationMinDurationMinutes = 15;
    private const int ReservationMaxDurationMinutes = 45;
    private const int ReservationDefaultDurationMinutes = 30;

    private static readonly string[] MemberLevelLabels = { "Thành viên", "Bạc", "Vàng", "Kim cương" };

    // CheckedListBox KHÔNG phát sự kiện DrawItem -> phải kế thừa và override OnDrawItem
    // thì mới tự vẽ được từng dòng (dùng để vẽ ký hiệu ✕ cho bàn đã được đặt).
    private sealed class TableCheckedListBox : CheckedListBox
    {
        public Action<DrawItemEventArgs>? CustomDraw { get; set; }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (CustomDraw is null) base.OnDrawItem(e);
            else CustomDraw(e);
        }
    }

    // Một dòng trong danh sách chọn bàn (CheckedListBox hiển thị theo ToString)
    private sealed class TableChoice
    {
        public TableAvailabilityDto Table { get; }
        public TableChoice(TableAvailabilityDto table) => Table = table;
        public bool IsAvailable => Table.IsAvailable;

        // Phần tên bàn: "T1-A1 · 4 chỗ · Tầng 1 - Khu A"
        public string BaseText => $"{Table.TableName}   ·   {Table.Capacity} chỗ   ·   {Table.AreaName}";

        // Phần lý do (chỉ có khi bàn không chọn được), vd "Đã đặt 11:57 → 12:12"
        public string? ReasonText
        {
            get
            {
                if (Table.IsAvailable) return null;
                var reason = Table.Reason ?? "Không khả dụng";
                var m = System.Text.RegularExpressions.Regex.Match(reason, @"(\d{2}:\d{2})\s+\d{2}/\d{2}\s*-\s*(\d{2}:\d{2})");
                return m.Success ? $"Đã đặt {m.Groups[1].Value} → {m.Groups[2].Value}" : reason;
            }
        }

        public override string ToString() => ReasonText is null ? BaseText : $"{BaseText}   —   {ReasonText}";
    }

    // Chia đều số khách cho các bàn: mỗi bàn >= 1 khách, không vượt sức chứa từng bàn.
    private static int[] DistributeGuests(int totalGuests, IReadOnlyList<int> capacities)
    {
        var result = new int[capacities.Count];
        var left = totalGuests;
        for (var i = 0; i < result.Length && left > 0; i++) { result[i] = 1; left--; }

        while (left > 0)
        {
            var progressed = false;
            for (var i = 0; i < result.Length && left > 0; i++)
            {
                if (result[i] >= capacities[i]) continue;
                result[i]++;
                left--;
                progressed = true;
            }
            if (!progressed) break;
        }
        return result;
    }

    // Gợi ý bàn cho "Tự chọn bàn":
    // 1) Ưu tiên bàn tiêu chuẩn (sức chứa phổ biến nhất, vd 4 chỗ) để không chiếm phòng VIP.
    // 2) 1 bàn đủ chỗ -> chọn bàn nhỏ nhất vừa đủ; không đủ -> các bàn LIỀN NHAU cùng khu vực, ít bàn nhất.
    // 3) Không được thì mới xét cả phòng lớn, cuối cùng là chọn bàn lớn nhất cho tới khi đủ.
    private static List<TableAvailabilityDto> SuggestTables(List<TableAvailabilityDto> available, int guests)
    {
        if (available.Count == 0) return new List<TableAvailabilityDto>();

        static List<TableAvailabilityDto>? Pick(List<TableAvailabilityDto> pool, int need)
        {
            var single = pool.Where(x => x.Capacity >= need).OrderBy(x => x.Capacity).FirstOrDefault();
            if (single is not null) return new List<TableAvailabilityDto> { single };

            List<TableAvailabilityDto>? best = null;
            var bestCap = 0;
            foreach (var area in pool.GroupBy(x => x.AreaId))
            {
                var list = area.ToList();
                for (var i = 0; i < list.Count; i++)
                {
                    var sum = 0;
                    for (var j = i; j < list.Count; j++)
                    {
                        sum += list[j].Capacity;
                        if (sum < need) continue;
                        var count = j - i + 1;
                        if (best is null || count < best.Count || (count == best.Count && sum < bestCap))
                        {
                            best = list.GetRange(i, count);
                            bestCap = sum;
                        }
                        break;
                    }
                }
            }
            return best;
        }

        var standardCap = available.GroupBy(x => x.Capacity).OrderByDescending(g => g.Count()).First().Key;
        var regular = available.Where(x => x.Capacity <= standardCap).ToList();

        var picked = Pick(regular, guests) ?? Pick(available, guests);
        if (picked is not null) return picked;

        var fallback = new List<TableAvailabilityDto>();
        var total = 0;
        foreach (var x in available.OrderByDescending(y => y.Capacity))
        {
            fallback.Add(x);
            total += x.Capacity;
            if (total >= guests) break;
        }
        return fallback;
    }

    private void OpenAddReservationDialog(Func<Task> onCreated)
    {
        const int dialogWidth = 640;
        const int padX = 20;
        const int fullWidth = dialogWidth - padX * 2; // 600
        const int colGap = 12;
        const int colWidth = (fullWidth - colGap) / 2; // 234

        using var dialog = new Form
        {
            Text = "Đặt bàn mới",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
        };

        // Lưu ý: KHÔNG để Width = 0 - Windows hiểu là "rộng tối đa 0" và ép cửa sổ co lại chỉ còn ~150px.
        var workArea = Screen.FromControl(this).WorkingArea;
        dialog.MaximumSize = new Size(workArea.Width, Math.Max(600, workArea.Height - 20));

        // ---------- Helper dựng giao diện: nhãn + ô nhập xếp dọc, 2 cột, tự co giãn ----------
        FlowLayoutPanel Field(string text, Control input, int width, int rightGap = 0)
        {
            var p = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, rightGap, 12),
            };
            p.Controls.Add(new Label { Text = text, Font = AppFonts.BodyMd, AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
            input.Width = width;
            input.Margin = Padding.Empty;
            p.Controls.Add(input);
            return p;
        }

        TableLayoutPanel Row(Control left, Control right)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = fullWidth,
                Margin = Padding.Empty,
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, colWidth + colGap));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, colWidth));
            t.Controls.Add(left, 0, 0);
            t.Controls.Add(right, 1, 0);
            return t;
        }

        // ---------- Ô nhập ----------
        var txtName = new TextBox { Font = AppFonts.BodyMd };
        var txtPhone = new TextBox { Font = AppFonts.BodyMd };

        var dtpTime = new DateTimePicker { Font = AppFonts.BodyMd, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm dd/MM/yyyy", Value = DateTime.Now.AddHours(1) };
        var numDuration = new NumericUpDown
        {
            Font = AppFonts.BodyMd,
            Minimum = ReservationMinDurationMinutes,
            Maximum = ReservationMaxDurationMinutes,
            Increment = 15,
            Value = ReservationDefaultDurationMinutes
        };
        var lblEndTime = new Label { Text = "Hết hạn giữ chỗ lúc: --:--", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };

        var numGuests = new NumericUpDown { Font = AppFonts.BodyMd, Minimum = 1, Maximum = 100, Value = 4 };
        var numDeposit = new NumericUpDown { Font = AppFonts.BodyMd, Minimum = 0, Maximum = 50_000_000, Increment = 50_000, ThousandsSeparator = true };

        // ---------- Gợi ý khách cũ ----------
        var lblHint = new Label { Font = AppFonts.BodyMd, ForeColor = AppColors.Tertiary, AutoSize = true, MaximumSize = new Size(fullWidth - 130, 0), Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = Padding.Empty };
        var btnUseHint = new Button { Text = "Điền lại", Size = new Size(96, 30), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Tertiary, ForeColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(8, 0, 0, 0) };
        btnUseHint.FlatAppearance.BorderSize = 0;
        var pnlHint = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = fullWidth,
            BackColor = AppColors.SurfaceLow,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 12),
            Visible = false,
        };
        pnlHint.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlHint.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlHint.Controls.Add(lblHint, 0, 0);
        pnlHint.Controls.Add(btnUseHint, 1, 0);

        // ---------- Đăng ký thành viên ----------
        var chkMember = new CheckBox { Text = "Đăng ký thành viên cho khách này", Font = AppFonts.BodyMd, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        var txtEmail = new TextBox { Font = AppFonts.BodyMd };
        var btnRegister = new Button { Text = "Đăng ký thành viên mới", Size = new Size(210, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Tertiary, ForeColor = Color.White, Margin = new Padding(0, 0, 12, 0) };
        btnRegister.FlatAppearance.BorderSize = 0;
        var lblMemberStatus = new Label { Font = AppFonts.BodyMd, AutoSize = true, Margin = new Padding(0, 7, 0, 0) };
        var rowRegister = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 12) };
        rowRegister.Controls.Add(btnRegister);
        rowRegister.Controls.Add(lblMemberStatus);
        var pnlMember = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, Visible = false };
        pnlMember.Controls.Add(Field("Email khách hàng *", txtEmail, fullWidth));
        pnlMember.Controls.Add(rowRegister);

        // ---------- Chọn bàn (nhiều bàn) ----------
        var lblTables = new Label { Text = "Chọn bàn * (tick nhiều bàn nếu nhóm đông)", Font = AppFonts.BodyMd, AutoSize = true, Anchor = AnchorStyles.Left };
        var btnAuto = new Button { Text = "Tự chọn bàn", Size = new Size(120, 30), FlatStyle = FlatStyle.Flat, BackColor = AppColors.SecondaryContainer, ForeColor = AppColors.OnSurface, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        btnAuto.FlatAppearance.BorderSize = 0;
        var pnlTablesHeader = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = fullWidth, Margin = new Padding(0, 0, 0, 4) };
        pnlTablesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlTablesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        pnlTablesHeader.Controls.Add(lblTables, 0, 0);
        pnlTablesHeader.Controls.Add(btnAuto, 1, 0);

        var clbTables = new TableCheckedListBox { Width = fullWidth, Height = 150, Font = AppFonts.BodyMd, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false, Margin = new Padding(0, 0, 0, 8) };
        var lblStatus = new Label { Font = AppFonts.BodyMd, AutoSize = true, MaximumSize = new Size(fullWidth, 0), Margin = new Padding(0, 0, 0, 10), Visible = false };

        var btnSave = new Button { Text = "Đặt bàn", Location = new Point((fullWidth - 200) / 2, 4), Size = new Size(200, 44), Font = AppFonts.LabelLg, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        var pnlSave = new Panel { Width = fullWidth, Height = 54, Margin = Padding.Empty };
        pnlSave.Controls.Add(btnSave);

        // ---------- Ghép giao diện ----------
        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = Point.Empty,
            Padding = new Padding(padX, 16, padX, 8),
        };
        root.Controls.Add(Row(Field("Tên khách", txtName, colWidth, colGap), Field("Số điện thoại", txtPhone, colWidth)));
        root.Controls.Add(pnlHint);
        root.Controls.Add(chkMember);
        root.Controls.Add(pnlMember);
        root.Controls.Add(Row(Field("Giờ đến *", dtpTime, colWidth, colGap), Field($"Giữ chỗ tối đa (phút) [{ReservationMinDurationMinutes}-{ReservationMaxDurationMinutes}]", numDuration, colWidth)));
        root.Controls.Add(lblEndTime);
        root.Controls.Add(Row(Field("Số khách (cả nhóm) *", numGuests, colWidth, colGap), Field("Tiền cọc (đ)", numDeposit, colWidth)));
        root.Controls.Add(pnlTablesHeader);
        root.Controls.Add(clbTables);
        root.Controls.Add(lblStatus);
        root.Controls.Add(pnlSave);

        dialog.Controls.Add(root);
        dialog.AcceptButton = btnSave;

        // Chiều rộng cố định; chiều cao theo nội dung. Màn hình thấp thì tự hiện thanh cuộn.
        var fitting = false;
        void FitDialog()
        {
            if (fitting || dialog.IsDisposed) return;
            fitting = true;
            try
            {
                var wa = Screen.FromControl(this).WorkingArea;
                var maxClientHeight = wa.Height - 90; // chừa thanh tiêu đề + taskbar
                var needed = root.GetPreferredSize(new Size(dialogWidth, 0)).Height;
                var scroll = needed > maxClientHeight;

                dialog.AutoScroll = scroll;
                dialog.ClientSize = new Size(
                    scroll ? dialogWidth + SystemInformation.VerticalScrollBarWidth : dialogWidth,
                    Math.Min(needed, maxClientHeight));

                if (dialog.Visible && dialog.Bottom > wa.Bottom)
                    dialog.Top = Math.Max(wa.Top, wa.Bottom - dialog.Height);
            }
            finally { fitting = false; }
        }
        root.SizeChanged += (s, e) => FitDialog(); // gợi ý khách cũ / ô email hiện ra thì tự giãn

        void ApplyLayout() => pnlMember.Visible = chkMember.Visible && chkMember.Checked;
        chkMember.CheckedChanged += (s, e) => ApplyLayout();

        // ---------- Gợi ý khách cũ theo SĐT ----------
        ReservationHintDto? hint = null;
        var registered = false;

        void ShowHint(ReservationHintDto h)
        {
            var head = new List<string>();
            if (h.IsMember)
            {
                var lv = MemberLevelLabels[Math.Clamp(h.MemberLevel ?? 0, 0, MemberLevelLabels.Length - 1)];
                head.Add($"★ {lv} · {h.LoyaltyPoint ?? 0:N0} điểm");
            }
            else head.Add("Chưa là thành viên");
            head.Add($"Đã đặt {h.ReservationCount} lần");

            var lines = new List<string> { string.Join(" · ", head) };
            if (h.LastReservationTime.HasValue)
                lines.Add($"Gần nhất: {h.LastReservationTime:HH:mm dd/MM/yyyy} · {h.LastTableName} · {h.LastNumberOfGuests} khách");
            if (!string.IsNullOrWhiteSpace(h.LastNote))
                lines.Add($"Ghi chú: {h.LastNote}");

            lblHint.Text = string.Join("\n", lines);
            pnlHint.Visible = true;
        }

        async Task LookupHintAsync()
        {
            var phone = txtPhone.Text.Trim();
            hint = null;
            pnlHint.Visible = false;
            if (!registered) chkMember.Visible = true;

            try
            {
                if (phone.Length >= 8)
                {
                    var h = await _api.GetReservationHintByPhoneAsync(phone);
                    if (dialog.IsDisposed || txtPhone.Text.Trim() != phone) return; // SĐT đã đổi trong lúc chờ

                    if (h is { Found: true })
                    {
                        hint = h;
                        ShowHint(h);
                        if (h.IsMember && !registered)
                        {
                            chkMember.Checked = false;
                            chkMember.Visible = false;
                        }
                    }
                }
            }
            catch
            {
                // Gợi ý chỉ là phụ - lỗi mạng không được chặn việc đặt bàn
            }
            finally
            {
                if (!dialog.IsDisposed) ApplyLayout();
            }
        }

        var lookupTimer = new System.Windows.Forms.Timer { Interval = 500 };
        lookupTimer.Tick += async (s, e) => { lookupTimer.Stop(); await LookupHintAsync(); };
        dialog.Disposed += (s, e) => lookupTimer.Dispose();
        txtPhone.TextChanged += (s, e) => { lookupTimer.Stop(); lookupTimer.Start(); };
        txtPhone.Leave += async (s, e) => { lookupTimer.Stop(); await LookupHintAsync(); };

        btnUseHint.Click += (s, e) =>
        {
            if (hint is null) return;
            if (!string.IsNullOrWhiteSpace(hint.CustomerName)) txtName.Text = hint.CustomerName;
            if (hint.LastNumberOfGuests.HasValue)
                numGuests.Value = Math.Clamp(hint.LastNumberOfGuests.Value, (int)numGuests.Minimum, (int)numGuests.Maximum);
        };

        btnRegister.Click += async (s, e) =>
        {
            var name = txtName.Text.Trim();
            var phone = txtPhone.Text.Trim();
            var email = txtEmail.Text.Trim();

            if (name.Length == 0 || phone.Length == 0)
            {
                MessageBox.Show("Vui lòng nhập Tên khách và Số điện thoại trước khi đăng ký thành viên.");
                return;
            }
            if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
            {
                MessageBox.Show("Email không hợp lệ.");
                return;
            }

            btnRegister.Enabled = false;
            try
            {
                await _api.CreateCustomerAsync(name, phone, email, null);
                registered = true;
                lblMemberStatus.Text = "✓ Đã đăng ký thành viên";
                lblMemberStatus.ForeColor = AppColors.Success;
                txtEmail.Enabled = false;
                chkMember.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không đăng ký được thành viên: {ex.Message}");
                btnRegister.Enabled = true;
            }
        };

        // ---------- Chọn nhiều bàn ----------
        var available = new List<TableAvailabilityDto>();
        var suppressCheck = false;
        var loadError = "";
        var loadSeq = 0; // mỗi lần tải tăng 1 -> bỏ qua kết quả của lần tải cũ về trễ

        List<TableChoice> GetChosen() => clbTables.CheckedItems.Cast<TableChoice>().ToList();

        void SetStatus(string text, bool isError)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = isError ? AppColors.Error : AppColors.OnSurfaceVariant;
            lblStatus.Visible = text.Length > 0;
        }

        void UpdateSummary()
        {
            if (dialog.IsDisposed) return;
            if (loadError.Length > 0) { SetStatus(loadError, true); return; }

            var chosen = GetChosen();
            var guests = (int)numGuests.Value;
            if (chosen.Count == 0)
            {
                SetStatus("Chưa chọn bàn. Bấm \"Tự chọn bàn\" để hệ thống gợi ý theo số khách.", false);
                return;
            }

            var capacity = chosen.Sum(c => c.Table.Capacity);
            var head = $"Đã chọn {chosen.Count} bàn · {capacity} chỗ · {guests} khách";
            if (capacity < guests)
                SetStatus($"{head} — thiếu {guests - capacity} chỗ, hãy chọn thêm bàn.", true);
            else if (guests < chosen.Count)
                SetStatus($"{head} — số khách ít hơn số bàn, hãy bỏ bớt bàn.", true);
            else if (chosen.Count > 1)
            {
                var split = DistributeGuests(guests, chosen.Select(c => c.Table.Capacity).ToList());
                var detail = string.Join(", ", chosen.Select((c, i) => $"{c.Table.TableName}: {split[i]}"));
                SetStatus($"{head}\nChia khách: {detail}", false);
            }
            else
                SetStatus(head, false);
        }

        // Vẽ tay từng dòng để bàn đã được đặt hiển thị màu xám + checkbox bị khóa
        clbTables.CustomDraw = e =>
        {
            if (e.Index < 0 || e.Index >= clbTables.Items.Count) return;
            var choice = (TableChoice)clbTables.Items[e.Index];
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            const int box = 16;
            var boxRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + (e.Bounds.Height - box) / 2, box, box);

            if (!choice.IsAvailable)
            {
                // Bàn đã được đặt: nền hồng nhạt, KHÔNG highlight chọn, ký hiệu ✕ đỏ thay cho ô tick
                using (var bg = new SolidBrush(Color.FromArgb(253, 238, 236)))
                    g.FillRectangle(bg, e.Bounds);

                using (var red = new SolidBrush(AppColors.Error))
                    g.FillEllipse(red, boxRect);
                using (var pen = new Pen(Color.White, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                {
                    g.DrawLine(pen, boxRect.X + 5, boxRect.Y + 5, boxRect.Right - 5, boxRect.Bottom - 5);
                    g.DrawLine(pen, boxRect.Right - 5, boxRect.Y + 5, boxRect.X + 5, boxRect.Bottom - 5);
                }

                const TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                var x = boxRect.Right + 8;
                var baseSize = TextRenderer.MeasureText(g, choice.BaseText, e.Font, new Size(int.MaxValue, e.Bounds.Height), fl);
                TextRenderer.DrawText(g, choice.BaseText, e.Font,
                    new Rectangle(x, e.Bounds.Y, e.Bounds.Width - x, e.Bounds.Height), SystemColors.GrayText, fl);

                using var bold = new Font(e.Font, FontStyle.Bold);
                var rx = x + baseSize.Width + 12;
                TextRenderer.DrawText(g, "— " + choice.ReasonText, bold,
                    new Rectangle(rx, e.Bounds.Y, Math.Max(0, e.Bounds.Right - rx), e.Bounds.Height), AppColors.Error,
                    fl | TextFormatFlags.EndEllipsis);
                return;
            }

            e.DrawBackground();

            var state = clbTables.GetItemChecked(e.Index)
                ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
            var glyph = CheckBoxRenderer.GetGlyphSize(g, state);
            CheckBoxRenderer.DrawCheckBox(g, new Point(boxRect.X, e.Bounds.Y + (e.Bounds.Height - glyph.Height) / 2), state);

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var tx = boxRect.Right + 8;
            TextRenderer.DrawText(g, choice.BaseText, e.Font,
                new Rectangle(tx, e.Bounds.Y, e.Bounds.Width - tx, e.Bounds.Height),
                selected ? SystemColors.HighlightText : e.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            e.DrawFocusRectangle();
        };

        clbTables.ItemCheck += (s, e) =>
        {
            // Bàn đã được đặt / không khả dụng: không cho tick
            if (e.Index >= 0 && e.Index < clbTables.Items.Count &&
                !((TableChoice)clbTables.Items[e.Index]).IsAvailable)
            {
                e.NewValue = e.CurrentValue;
                return;
            }

            if (suppressCheck) return;
            clbTables.BeginInvoke(new Action(UpdateSummary)); // chờ trạng thái tick cập nhật xong
        };
        numGuests.ValueChanged += (s, e) => UpdateSummary();

        btnAuto.Click += (s, e) =>
        {
            var picks = SuggestTables(available, (int)numGuests.Value);
            var pickIds = picks.Select(p => p.Id).ToHashSet();

            var firstIndex = -1;
            suppressCheck = true;
            for (var i = 0; i < clbTables.Items.Count; i++)
            {
                var isPicked = pickIds.Contains(((TableChoice)clbTables.Items[i]).Table.Id);
                clbTables.SetItemChecked(i, isPicked);
                if (isPicked && firstIndex < 0) firstIndex = i;
            }
            suppressCheck = false;

            if (firstIndex >= 0) clbTables.TopIndex = firstIndex;
            UpdateSummary();
        };

        void UpdateEndTimeLabel() => lblEndTime.Text = $"Hết hạn giữ chỗ lúc: {dtpTime.Value.AddMinutes((double)numDuration.Value):HH:mm dd/MM}";

        // Nạp lại danh sách bàn trống theo đúng khung giờ; giữ lại các bàn đang tick nếu còn trống.
        async Task ReloadAvailableTablesAsync()
        {
            UpdateEndTimeLabel();
            var seq = ++loadSeq;
            var reqStart = dtpTime.Value;
            var reqMinutes = (int)numDuration.Value;
            var keepIds = GetChosen().Select(c => c.Table.Id).ToHashSet();
            clbTables.Enabled = false;
            btnAuto.Enabled = false;
            btnSave.Enabled = false;
            loadError = "";
            try
            {
                var all = await _api.GetAvailableTablesAsync(reqStart, reqMinutes);

                // Đối chiếu thêm với danh sách đặt bàn thật (cùng dữ liệu màn "Đặt bàn trước")
                // để chắc chắn bàn đã có người đặt trùng khung giờ luôn bị khóa.
                List<ReservationDto> booked;
                try { booked = await _api.GetReservationsAsync(); }
                catch { booked = new List<ReservationDto>(); }

                if (dialog.IsDisposed || seq != loadSeq) return; // có lần tải mới hơn -> bỏ kết quả này

                var reqEnd = reqStart.AddMinutes(reqMinutes);
                foreach (var tbl in all)
                {
                    if (!tbl.IsAvailable) continue;
                    var hit = booked.FirstOrDefault(rv =>
                        rv.TableId == tbl.Id &&
                        (rv.Status == (int)ReservationStatusEnum.DaXacNhan || rv.Status == (int)ReservationStatusEnum.DaNhanBan || rv.Status == (int)ReservationStatusEnum.ChoXacNhan) &&
                        reqStart < rv.ReservationTime.AddMinutes(rv.DurationMinutes) &&
                        rv.ReservationTime < reqEnd);
                    if (hit is null) continue;

                    tbl.IsAvailable = false;
                    tbl.Reason = $"Đã có khách đặt {hit.ReservationTime:HH:mm dd/MM} - {hit.ReservationTime.AddMinutes(hit.DurationMinutes):HH:mm dd/MM}";
                }

                // Hiển thị TẤT CẢ bàn; bàn đã được đặt (trùng khung giờ) bị khóa, không tick được.
                var ordered = all.OrderBy(x => x.AreaId)
                                 .ThenBy(x => x.TableName, StringComparer.Ordinal)
                                 .ToList();
                available = ordered.Where(x => x.IsAvailable).ToList(); // dùng cho "Tự chọn bàn"

                suppressCheck = true;
                clbTables.Items.Clear();
                foreach (var x in ordered)
                    clbTables.Items.Add(new TableChoice(x), x.IsAvailable && keepIds.Contains(x.Id));
                suppressCheck = false;

                if (available.Count == 0)
                    loadError = "Không còn bàn trống ở khung giờ này, vui lòng đổi giờ đến/thời lượng.";
            }
            catch (Exception ex)
            {
                loadError = $"Không tải được danh sách bàn: {ex.Message}";
            }
            finally
            {
                if (!dialog.IsDisposed && seq == loadSeq)
                {
                    suppressCheck = false;
                    clbTables.Enabled = true;
                    btnAuto.Enabled = available.Count > 0;
                    btnSave.Enabled = available.Count > 0;
                    UpdateSummary();
                }
            }
        }

        dtpTime.ValueChanged += async (s, e) => await ReloadAvailableTablesAsync();
        numDuration.ValueChanged += async (s, e) => await ReloadAvailableTablesAsync();

        List<(int TableId, int NumberOfGuests)>? resultSlots = null;

        btnSave.Click += (s, e) =>
        {
            var chosen = GetChosen();
            var guests = (int)numGuests.Value;

            if (chosen.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 bàn trống.");
                return;
            }
            var capacity = chosen.Sum(c => c.Table.Capacity);
            if (capacity < guests)
            {
                MessageBox.Show($"Các bàn đã chọn chỉ chứa tối đa {capacity} khách, vui lòng chọn thêm bàn cho {guests} khách.");
                return;
            }
            if (guests < chosen.Count)
            {
                MessageBox.Show($"Số khách ({guests}) ít hơn số bàn đã chọn ({chosen.Count}), vui lòng bỏ bớt bàn.");
                return;
            }

            if (chkMember.Visible && chkMember.Checked && !registered)
            {
                var ask = MessageBox.Show(
                    "Bạn đã tick \"Đăng ký thành viên\" nhưng chưa bấm \"Đăng ký thành viên mới\".\nBỏ qua đăng ký và tiếp tục đặt bàn?",
                    "Chưa đăng ký thành viên", MessageBoxButtons.YesNo);
                if (ask != DialogResult.Yes) return;
            }

            var split = DistributeGuests(guests, chosen.Select(c => c.Table.Capacity).ToList());
            resultSlots = chosen.Select((c, i) => (c.Table.Id, split[i])).ToList();
            dialog.DialogResult = DialogResult.OK;
        };

        _ = ReloadAvailableTablesAsync();

        FitDialog();

        if (dialog.ShowDialog() != DialogResult.OK || resultSlots is null) return;

        _ = SaveReservationAsync(
            resultSlots,
            txtName.Text.Trim(),
            txtPhone.Text.Trim(),
            dtpTime.Value,
            (int)numDuration.Value,
            numDeposit.Value,
            onCreated);
    }

    private async Task SaveReservationAsync(List<(int TableId, int NumberOfGuests)> tables, string customerName, string customerPhone, DateTime reservationTime, int durationMinutes, decimal depositAmount, Func<Task> onCreated)
    {
        try
        {
            await _api.CreateReservationBatchAsync(tables, customerName, customerPhone, reservationTime, durationMinutes, depositAmount, null);
            await onCreated();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không đặt bàn được: {ex.Message}");
        }
    }
}