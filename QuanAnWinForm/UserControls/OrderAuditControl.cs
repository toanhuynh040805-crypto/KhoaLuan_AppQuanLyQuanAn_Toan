using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

public sealed class OrderAuditControl : UserControl, IRefreshableContent
{
    private readonly ApiService _api = new();

    private readonly ComboBox _cbDateMode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 120
    };
    private readonly DateTimePicker _dtpDate = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy",
        Width = 120
    };
    private readonly ComboBox _cbTable = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 210
    };
    private readonly ComboBox _cbRequester = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 230
    };
    private readonly Button _btnRefresh = new()
    {
        Text = "Làm mới",
        Width = 92,
        Height = 34
    };
    private readonly Label _lblSummary = new()
    {
        AutoSize = true,
        ForeColor = AppColors.OnSurfaceVariant
    };
    private readonly FlowLayoutPanel _flowRecords = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(16, 8, 16, 16)
    };

    private bool _suppress;
    private int _loadVersion;
    private readonly System.Windows.Forms.Timer _realtimeTimer =
        new() { Interval = 500 };

    public OrderAuditControl()
    {
        BuildUi();
        WireEvents();

        _realtimeTimer.Tick += async (_, _) =>
        {
            _realtimeTimer.Stop();
            await LoadDataAsync(silent: true);
        };

        _cbDateMode.Items.AddRange(new object[]
        {
            "Hôm nay",
            "Hôm qua",
            "Chọn ngày"
        });
        _cbDateMode.SelectedIndex = 0;
        _dtpDate.Value = DateTime.Today;
        _dtpDate.Enabled = false;

        Load += async (_, _) =>
        {
            SubscribeRealtime();
            await LoadDataAsync();
        };
        Disposed += (_, _) =>
        {
            UnsubscribeRealtime();
            _realtimeTimer.Stop();
            _realtimeTimer.Dispose();
        };
        _flowRecords.Resize += (_, _) => ResizeCards();
    }

    public Task RefreshDataAsync() => LoadDataAsync(silent: true);

    private void SubscribeRealtime()
    {
        UnsubscribeRealtime();
        NotificationHubClient.OrderUpdated += OnOrderUpdated;
        NotificationHubClient.TableStatusUpdated += OnTableStatusUpdated;
        NotificationHubClient.PaymentRequested += OnPaymentRequested;
        NotificationHubClient.PaymentCompleted += OnPaymentCompleted;
        NotificationHubClient.ItemStatusUpdated += OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled += OnOrderItemCancelled;
        NotificationHubClient.ConnectionRestored += OnConnectionRestored;
    }

    private void UnsubscribeRealtime()
    {
        NotificationHubClient.OrderUpdated -= OnOrderUpdated;
        NotificationHubClient.TableStatusUpdated -= OnTableStatusUpdated;
        NotificationHubClient.PaymentRequested -= OnPaymentRequested;
        NotificationHubClient.PaymentCompleted -= OnPaymentCompleted;
        NotificationHubClient.ItemStatusUpdated -= OnItemStatusUpdated;
        NotificationHubClient.OrderItemCancelled -= OnOrderItemCancelled;
        NotificationHubClient.ConnectionRestored -= OnConnectionRestored;
    }

    private void OnOrderUpdated(OrderUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnTableStatusUpdated(TableStatusUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnPaymentRequested(PaymentRequestedPayload _) => QueueRealtimeRefresh();
    private void OnPaymentCompleted(PaymentCompletedPayload _) => QueueRealtimeRefresh();
    private void OnItemStatusUpdated(ItemStatusUpdatedPayload _) => QueueRealtimeRefresh();
    private void OnOrderItemCancelled(OrderItemCancelledPayload _) => QueueRealtimeRefresh();
    private void OnConnectionRestored() => QueueRealtimeRefresh();

    private void QueueRealtimeRefresh()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                _realtimeTimer.Stop();
                _realtimeTimer.Start();
            });
        }
        catch (InvalidOperationException) { }
    }

    private void BuildUi()
    {
        BackColor = AppColors.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Nhật ký Order",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = AppColors.OnSurface,
            AutoSize = true,
            Location = new Point(0, 4)
        };
        var subtitle = new Label
        {
            Text = "Theo dõi hóa đơn, đợt gọi món, món đã xóa và hành trình chuyển/gộp bàn.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            AutoSize = true,
            Location = new Point(2, 40)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 6)
        };

        filters.Controls.Add(Field("Ngày", _cbDateMode));
        filters.Controls.Add(Field("Chọn ngày", _dtpDate));
        filters.Controls.Add(Field("Bàn", _cbTable));
        filters.Controls.Add(Field("Nhân viên gọi thanh toán", _cbRequester));

        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.BackColor = AppColors.Primary;
        _btnRefresh.ForeColor = Color.White;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Margin = new Padding(12, 22, 0, 0);
        filters.Controls.Add(_btnRefresh);

        _lblSummary.Margin = new Padding(14, 29, 0, 0);
        filters.Controls.Add(_lblSummary);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(filters, 0, 1);
        root.Controls.Add(_flowRecords, 0, 2);

        Controls.Add(root);
    }

    private static Control Field(string title, Control control)
    {
        var panel = new Panel
        {
            Width = control.Width,
            Height = 58,
            Margin = new Padding(0, 0, 12, 0)
        };
        var label = new Label
        {
            Text = title,
            Font = AppFonts.LabelMd,
            ForeColor = AppColors.OnSurfaceVariant,
            AutoSize = true,
            Location = new Point(0, 0)
        };
        control.Location = new Point(0, 22);
        control.Height = 32;
        panel.Controls.Add(label);
        panel.Controls.Add(control);
        return panel;
    }

    private void WireEvents()
    {
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();

        _cbDateMode.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppress) return;

            switch (_cbDateMode.SelectedIndex)
            {
                case 0:
                    _dtpDate.Enabled = false;
                    SetDateSilently(DateTime.Today);
                    await LoadDataAsync();
                    break;
                case 1:
                    _dtpDate.Enabled = false;
                    SetDateSilently(DateTime.Today.AddDays(-1));
                    await LoadDataAsync();
                    break;
                default:
                    _dtpDate.Enabled = true;
                    break;
            }
        };

        _dtpDate.ValueChanged += async (_, _) =>
        {
            if (_suppress || _cbDateMode.SelectedIndex != 2) return;
            await LoadDataAsync();
        };

        _cbTable.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppress) return;
            await LoadDataAsync();
        };

        _cbRequester.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppress) return;
            await LoadDataAsync();
        };
    }

    private void SetDateSilently(DateTime value)
    {
        _suppress = true;
        try
        {
            _dtpDate.Value = value.Date;
        }
        finally
        {
            _suppress = false;
        }
    }

    private async Task LoadDataAsync(bool silent = false)
    {
        var version = ++_loadVersion;
        _btnRefresh.Enabled = false;

        try
        {
            var selectedTableId =
                (_cbTable.SelectedItem as FilterItem)?.Id ?? 0;
            var selectedRequesterId =
                (_cbRequester.SelectedItem as FilterItem)?.Id ?? 0;

            var data = await _api.GetOrderAuditAsync(
                _dtpDate.Value.Date,
                selectedTableId > 0 ? selectedTableId : null,
                selectedRequesterId > 0 ? selectedRequesterId : null);

            if (version != _loadVersion || IsDisposed) return;

            PopulateFilters(
                data,
                selectedTableId,
                selectedRequesterId);

            RenderRecords(data.Records);
            _lblSummary.Text =
                $"{data.Records.Count:N0} order/hóa đơn";
        }
        catch (Exception ex)
        {
            if (version != _loadVersion || IsDisposed) return;
            RenderError(ex.Message);
            if (!silent)
            {
                MessageBox.Show(
                    $"Không tải được Nhật ký Order: {ex.Message}",
                    "Nhật ký Order",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            if (version == _loadVersion && !IsDisposed)
                _btnRefresh.Enabled = true;
        }
    }

    private void PopulateFilters(
        OrderAuditResponseDto data,
        int selectedTableId,
        int selectedRequesterId)
    {
        _suppress = true;
        try
        {
            _cbTable.Items.Clear();
            _cbTable.Items.Add(new FilterItem(0, "Tất cả bàn"));
            foreach (var table in data.Tables)
            {
                _cbTable.Items.Add(
                    new FilterItem(table.Id, table.ToString()));
            }
            SelectFilter(_cbTable, selectedTableId);

            _cbRequester.Items.Clear();
            _cbRequester.Items.Add(
                new FilterItem(0, "Tất cả nhân viên"));
            foreach (var requester in data.PaymentRequesters)
            {
                _cbRequester.Items.Add(
                    new FilterItem(
                        requester.UserId,
                        requester.FullName));
            }
            SelectFilter(_cbRequester, selectedRequesterId);
        }
        finally
        {
            _suppress = false;
        }
    }

    private static void SelectFilter(ComboBox combo, int id)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is FilterItem item &&
                item.Id == id)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void RenderRecords(List<OrderAuditRecordDto> records)
    {
        _flowRecords.SuspendLayout();
        try
        {
            foreach (var old in
                     _flowRecords.Controls.Cast<Control>().ToList())
            {
                _flowRecords.Controls.Remove(old);
                old.Dispose();
            }
            _flowRecords.Controls.Clear();

            if (records.Count == 0)
            {
                _flowRecords.Controls.Add(new Label
                {
                    Text = "Không có order/hóa đơn theo bộ lọc đã chọn.",
                    Font = AppFonts.BodyMd,
                    ForeColor = AppColors.OnSurfaceVariant,
                    AutoSize = true,
                    Margin = new Padding(6, 16, 0, 0)
                });
                return;
            }

            foreach (var record in records)
                _flowRecords.Controls.Add(BuildRecordCard(record));

            ResizeCards();
        }
        finally
        {
            _flowRecords.ResumeLayout(true);
        }
    }

    private void RenderError(string message)
    {
        _flowRecords.Controls.Clear();
        _flowRecords.Controls.Add(new Label
        {
            Text = message,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.Error,
            AutoSize = true,
            Margin = new Padding(6, 16, 0, 0)
        });
    }

    private Control BuildRecordCard(OrderAuditRecordDto record)
    {
        var card = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.White,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 14),
            Tag = "audit-card"
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(
                pen,
                0,
                0,
                card.Width - 1,
                card.Height - 1);
        };

        var invoiceLabel = record.InvoiceId.HasValue
            ? $"Hóa đơn #{record.InvoiceId} · Order #{record.OrderId}"
            : $"Order #{record.OrderId} · Chưa có hóa đơn";

        card.Controls.Add(LabelLine(
            invoiceLabel,
            new Font("Segoe UI", 12F, FontStyle.Bold),
            AppColors.OnSurface));

        var when = record.InvoiceDate ?? record.OrderTime;
        card.Controls.Add(LabelLine(
            $"{string.Join(", ", record.TableNames)} · {when:HH:mm dd/MM/yyyy}",
            AppFonts.BodyMd,
            AppColors.OnSurfaceVariant));

        card.Controls.Add(LabelLine(
            $"Phục vụ: {record.WaiterName}  |  Gọi thanh toán: " +
            $"{(string.IsNullOrWhiteSpace(record.PaymentRequestedByName) ? "Chưa gọi" : record.PaymentRequestedByName)}",
            AppFonts.BodyMd,
            AppColors.OnSurface));

        if (record.PaymentRequestedAt.HasValue)
        {
            card.Controls.Add(LabelLine(
                $"Thời điểm gọi thanh toán: {record.PaymentRequestedAt:HH:mm:ss}",
                AppFonts.BodyMd,
                AppColors.OnSurfaceVariant));
        }

        card.Controls.Add(LabelLine(
            record.DepositAmount > 0
                ? $"Tổng còn thanh toán: {record.TotalAmount:N0}đ · Cọc: {record.DepositAmount:N0}đ"
                : $"Tổng tiền: {record.TotalAmount:N0}đ",
            AppFonts.LabelLg,
            AppColors.Primary));

        if (record.TableJourney.Count > 0)
        {
            card.Controls.Add(SectionTitle("Hành trình bàn"));
            foreach (var operation in record.TableJourney)
            {
                card.Controls.Add(LabelLine(
                    $"{operation.CreatedAt:HH:mm} · " +
                    $"{operation.Details ?? operation.OperationLabel} · " +
                    $"{operation.ActorName ?? "Hệ thống"}",
                    AppFonts.BodyMd,
                    AppColors.OnSurfaceVariant));
            }
        }

        card.Controls.Add(SectionTitle("Các đợt gọi món"));

        if (record.Batches.Count == 0)
        {
            card.Controls.Add(LabelLine(
                "Order chưa có món.",
                AppFonts.BodyMd,
                AppColors.OnSurfaceVariant));
        }
        else
        {
            foreach (var batch in record.Batches)
                card.Controls.Add(BuildBatch(batch));
        }

        return card;
    }

    private Control BuildBatch(OrderAuditBatchDto batch)
    {
        var box = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = AppColors.SurfaceLow,
            Padding = new Padding(10),
            Margin = new Padding(0, 4, 0, 7),
            Tag = "audit-batch"
        };

        box.Controls.Add(LabelLine(
            $"Đợt {batch.Batch} · {batch.CalledAt:HH:mm:ss}",
            AppFonts.LabelLg,
            AppColors.OnSurface));

        foreach (var item in batch.Items)
        {
            var name = $"{item.Name} ×{item.Quantity} · " +
                       $"{item.UnitPrice * item.Quantity:N0}đ · " +
                       item.StatusLabel;

            var font = item.IsDeleted
                ? new Font(
                    "Segoe UI",
                    9.5F,
                    FontStyle.Strikeout | FontStyle.Bold)
                : AppFonts.BodyMd;

            box.Controls.Add(LabelLine(
                name,
                font,
                item.IsDeleted
                    ? AppColors.Error
                    : AppColors.OnSurface));

            if (item.IsDeleted &&
                !string.IsNullOrWhiteSpace(item.CancelReason))
            {
                box.Controls.Add(LabelLine(
                    $"   Lý do xóa: {item.CancelReason}" +
                    (string.IsNullOrWhiteSpace(item.CancelledBy)
                        ? ""
                        : $" · {item.CancelledBy}"),
                    AppFonts.BodyMd,
                    AppColors.Error));
            }
        }

        return box;
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = AppFonts.LabelLg,
        ForeColor = AppColors.OnSurface,
        Margin = new Padding(0, 12, 0, 4)
    };

    private static Label LabelLine(
        string text,
        Font font,
        Color color) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(1100, 0),
        Font = font,
        ForeColor = color,
        Margin = new Padding(0, 2, 0, 2)
    };

    private void ResizeCards()
    {
        var width = Math.Max(
            420,
            _flowRecords.ClientSize.Width -
            _flowRecords.Padding.Horizontal -
            SystemInformation.VerticalScrollBarWidth -
            8);

        foreach (Control control in _flowRecords.Controls)
        {
            if (control.Tag as string == "audit-card")
            {
                control.Width = width;
                foreach (Control child in control.Controls)
                {
                    if (child.Tag as string == "audit-batch")
                        child.Width = Math.Max(380, width - 30);
                }
            }
        }
    }

    private sealed record FilterItem(int Id, string Label)
    {
        public override string ToString() => Label;
    }
}
