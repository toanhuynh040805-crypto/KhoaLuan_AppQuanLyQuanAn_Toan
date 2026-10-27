using System.Drawing.Drawing2D;
using System.Media;
using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

public partial class KitchenBoardControl : UserControl
{
    private readonly int _chefUserId;
    private readonly string _departmentName;
    private int? _stationId;
    private readonly SignalRService _signalR = new();
    private readonly ApiService _api = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 15_000 };

    private readonly Dictionary<int, TicketCard> _cardsByOrderId = new();

    public KitchenBoardControl() : this(0, "Bep", "Màn hình Bếp", null) { }

    public KitchenBoardControl(
        int chefUserId,
        string departmentName,
        string title,
        int? stationId = null)
    {
        _chefUserId = chefUserId;
        _departmentName = departmentName;
        _stationId = stationId;
        InitializeComponent();
        lblTitle.Text = title;

        Load += async (s, e) =>
        {
            await LoadPendingTicketsAsync();
            await ConnectSignalRAsync();
            _refreshTimer.Tick += (s2, e2) => RefreshAllUrgency();
            _refreshTimer.Start();
        };
        Disposed += async (s, e) =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            await _signalR.DisconnectAsync();
        };
    }

    private async Task<int> ResolveStationIdAsync()
    {
        if (_stationId.HasValue) return _stationId.Value;

        var stations = await _api.GetStationsAsync();
        var station = stations.FirstOrDefault(s =>
            string.Equals(s.Name, _departmentName, StringComparison.OrdinalIgnoreCase));

        if (station is null)
            throw new InvalidOperationException(
                $"Không tìm thấy bộ phận chế biến '{_departmentName}' trong Backend.");

        _stationId = station.Id;
        return station.Id;
    }

    private async Task LoadPendingTicketsAsync()
    {
        try
        {
            var stationId = await ResolveStationIdAsync();
            var items = await _api.GetOrderItemsAsync(stationId);
            var groups = items
                .GroupBy(i => new { i.OrderId, i.TableName })
                .OrderBy(g => g.Min(i => i.CreatedAt));

            foreach (var g in groups)
            {
                var payload = new NewTicketPayload
                {
                    OrderId = g.Key.OrderId,
                    TableName = g.Key.TableName,
                    Department = _departmentName,
                    Items = g.Select(i => new TicketItem
                    {
                        Id = i.Id,
                        MenuItemName = i.MenuItemName,
                        Quantity = i.Quantity,
                        Note = i.Note,
                        IsTakeaway = i.IsTakeaway,
                        Status = i.KitchenStatus,
                        CreatedAt = i.CreatedAt,
                    }).ToList(),
                };
                AddTicketColumn(payload, playSound: false);
            }

            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được các phiếu đang chờ: {ex.Message}");
        }
    }

    private async Task ConnectSignalRAsync()
    {
        _signalR.OnNewTicket += payload =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) Invoke(() => AddTicketColumn(payload, playSound: true));
            else AddTicketColumn(payload, playSound: true);
        };
        _signalR.OnItemStatusUpdated += payload =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) Invoke(() => HandleItemStatusUpdated(payload));
            else HandleItemStatusUpdated(payload);
        };
        _signalR.OnOrderItemCancelled += payload =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) Invoke(() => HandleOrderItemCancelled(payload));
            else HandleOrderItemCancelled(payload);
        };

        try
        {
            await _signalR.ConnectAndJoinDepartmentAsync(_departmentName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không kết nối được realtime: {ex.Message}");
        }
    }

    private void AddTicketColumn(NewTicketPayload payload, bool playSound)
    {
        if (_cardsByOrderId.ContainsKey(payload.OrderId)) return;

        var card = BuildTicketCard(payload);
        _cardsByOrderId[payload.OrderId] = card;
        flowColumns.Controls.Add(card.Column);

        UpdateCardUrgency(card);
        UpdateEmptyState();

        if (playSound)
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    private void RemoveTicket(int orderId)
    {
        if (!_cardsByOrderId.TryGetValue(orderId, out var card)) return;
        flowColumns.Controls.Remove(card.Column);
        card.Column.Dispose();
        _cardsByOrderId.Remove(orderId);
        UpdateEmptyState();
    }

    private async Task RemoveTicketAfterDelayAsync(int orderId)
    {
        await Task.Delay(1200);
        if (IsDisposed) return;
        RemoveTicket(orderId);
    }

    private void UpdateEmptyState()
    {
        lblEmptyState.Visible = _cardsByOrderId.Count == 0;
    }

    private void HandleItemStatusUpdated(ItemStatusUpdatedPayload payload)
    {
        foreach (var card in _cardsByOrderId.Values)
        {
            var row = card.Rows.FirstOrDefault(r => r.ItemId == payload.Id);
            if (row is null) continue;

            row.CurrentStatus = payload.Status;

            if (payload.Quantity.HasValue &&
                payload.Quantity.Value > 0)
            {
                row.QuantityLabel.Text =
                    $"{payload.Quantity.Value}x";
            }

            ApplyItemStatusUI(row);
            ApplyBulkActionUI(card);

            if (card.Rows.All(r => r.CurrentStatus is (int)KitchenStatus.DaCheBien or (int)KitchenStatus.DaPhucVu or (int)KitchenStatus.DaHuy))
                _ = RemoveTicketAfterDelayAsync(card.OrderId);

            return;
        }
    }

    private void HandleOrderItemCancelled(OrderItemCancelledPayload payload)
    {
        foreach (var card in _cardsByOrderId.Values)
        {
            var row = card.Rows.FirstOrDefault(r => r.ItemId == payload.Id);
            if (row is null) continue;

            row.CurrentStatus = (int)KitchenStatus.DaHuy;
            row.CancelReason = payload.Reason;
            ApplyItemStatusUI(row);
            ApplyBulkActionUI(card);

            // Báo hủy bằng màu đỏ, tự động loại bỏ phiếu sau 3 giây
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                if (IsDisposed) return;
                Invoke(() =>
                {
                    if (card.Rows.All(r => r.CurrentStatus is (int)KitchenStatus.DaCheBien or (int)KitchenStatus.DaPhucVu or (int)KitchenStatus.DaHuy))
                        RemoveTicket(card.OrderId);
                });
            });

            return;
        }
    }

    private void RefreshAllUrgency()
    {
        foreach (var card in _cardsByOrderId.Values)
            UpdateCardUrgency(card);
    }

    private void UpdateCardUrgency(TicketCard card)
    {
        var earliestCreatedAt = card.Rows.Count > 0 ? card.Rows.Min(r => r.CreatedAt) : DateTime.Now;
        var minutes = Math.Max(0, (int)(DateTime.Now - earliestCreatedAt).TotalMinutes);

        var (bg, fg, pillFg) = minutes switch
        {
            < 5 => (AppColors.Success, Color.White, AppColors.Success),
            < 10 => (AppColors.Warning, AppColors.OnSurface, Color.FromArgb(146, 64, 14)),
            _ => (AppColors.Error, Color.White, AppColors.Error),
        };

        var elapsed = FormatElapsed(minutes);
        var badge = card.ElapsedBadge;
        badge.Text = minutes >= 10 ? $"{elapsed} · TRỄ" : elapsed;

        // Badge dạng viên thuốc: nền trắng, chữ theo màu mức độ khẩn.
        using (var f = AppFonts.LabelLg)
        {
            var w = TextRenderer.MeasureText(badge.Text, f).Width + 28;
            badge.Size = new Size(w, 28);
            badge.Location = new Point(CardWidth - w - 14, 12);
        }
        badge.BackColor = Color.White;
        badge.ForeColor = pillFg;
        RoundCorners(badge, 14);

        card.Header.BackColor = bg;
        foreach (var lbl in new[] { card.LblTable, card.LblOrder })
        {
            lbl.BackColor = bg;
            lbl.ForeColor = fg;
        }
    }

    private static string FormatElapsed(int minutes) => minutes switch
    {
        < 60 => $"{minutes} phút",
        < 24 * 60 => $"{minutes / 60} giờ {minutes % 60} phút",
        _ => $"{minutes / (24 * 60)} ngày {minutes % (24 * 60) / 60} giờ",
    };

    private const int CardWidth = 380;
    private const int HeaderHeight = 68;
    private const int MaxVisibleRows = 6;
    private const int ActionBtnWidth = 104;
    private const int ActionBtnHeight = 34;
    private const int NameX = 60;
    private const int RowPadX = 14;

    private static int NameWidth(int rowWidth) => rowWidth - NameX - ActionBtnWidth - RowPadX - 8;

    // Tên món dài được xuống dòng (tối đa 2 dòng) thay vì bị cắt "...".
    private static int NameLines(TicketItem item, int rowWidth)
    {
        using var font = AppFonts.LabelLg;
        var size = TextRenderer.MeasureText(
            item.MenuItemName ?? string.Empty, font,
            new Size(NameWidth(rowWidth), 0), TextFormatFlags.WordBreak);
        return Math.Clamp((int)Math.Round(size.Height / (double)font.Height), 1, 2);
    }

    private static int RowHeightFor(TicketItem item, int rowWidth)
    {
        var h = 10 + NameLines(item, rowWidth) * 20 + 10;
        if (item.IsTakeaway) h += 22;
        if (!string.IsNullOrEmpty(item.Note)) h += 20;
        return Math.Max(56, h);
    }

    private static void RoundCorners(Control c, int radius)
    {
        if (c.Width <= 0 || c.Height <= 0) return;
        var d = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(c.Width - d, 0, d, d, 270, 90);
        path.AddArc(c.Width - d, c.Height - d, d, d, 0, 90);
        path.AddArc(0, c.Height - d, d, d, 90, 90);
        path.CloseFigure();
        c.Region = new Region(path);
    }

    private TicketCard BuildTicketCard(NewTicketPayload payload)
    {
        var card = new TicketCard { OrderId = payload.OrderId, TableName = payload.TableName };

        var column = new Panel { Width = CardWidth, Margin = new Padding(10), BackColor = Color.White, Tag = payload.OrderId };
        column.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, column.Width - 1, column.Height - 1);
        };

        var header = new Panel { Size = new Size(CardWidth, HeaderHeight), Location = new Point(0, 0), BackColor = AppColors.Success };
        var lblTable = new Label { Text = payload.TableName, Font = AppFonts.HeadlineSm, Location = new Point(14, 10), AutoSize = true };
        var lblOrderId = new Label { Text = $"Đơn #{payload.OrderId}  ·  {payload.Items.Count} món", Font = AppFonts.BodyMd, Location = new Point(14, 38), AutoSize = true };
        var elapsedBadge = new Label
        {
            Font = AppFonts.LabelLg,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Size = new Size(110, 28),
            Location = new Point(CardWidth - 110 - 14, 12),
        };
        header.Controls.Add(lblTable);
        header.Controls.Add(lblOrderId);
        header.Controls.Add(elapsedBadge);
        column.Controls.Add(header);
        card.Header = header;
        card.LblTable = lblTable;
        card.LblOrder = lblOrderId;
        card.ElapsedBadge = elapsedBadge;

        var scrolling = payload.Items.Count > MaxVisibleRows;
        // Chừa chỗ cho thanh cuộn dọc để không bị thanh cuộn ngang.
        var rowWidth = CardWidth - (scrolling ? SystemInformation.VerticalScrollBarWidth : 0);

        var itemsHeight = payload.Items.Take(MaxVisibleRows).Sum(i => RowHeightFor(i, rowWidth));
        var itemsPanel = new FlowLayoutPanel
        {
            Location = new Point(0, HeaderHeight),
            Size = new Size(CardWidth, Math.Max(56, itemsHeight)),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = scrolling,
            BackColor = Color.White,
            Padding = new Padding(0),
        };
        foreach (var item in payload.Items)
            itemsPanel.Controls.Add(BuildItemRow(item, card, rowWidth));
        column.Controls.Add(itemsPanel);

        var footerY = HeaderHeight + itemsPanel.Height;

        if (payload.Items.Count > 1)
        {
            var btnDoneAll = new Button
            {
                Location = new Point(14, footerY + 10),
                Size = new Size(CardWidth - 28, 42),
                FlatStyle = FlatStyle.Flat,
                Font = AppFonts.LabelLg,
                Cursor = Cursors.Hand,
            };
            btnDoneAll.FlatAppearance.BorderSize = 0;
            RoundCorners(btnDoneAll, 8);
            card.BulkActionButton = btnDoneAll;
            ApplyBulkActionUI(card);

            btnDoneAll.Click += async (s, e) =>
            {
                btnDoneAll.Enabled = false;
                try
                {
                    var hasNewItems = card.Rows.Any(
                        r => r.CurrentStatus == (int)KitchenStatus.MoiGoi);

                    if (hasNewItems)
                    {
                        // Bước 1: chỉ bắt đầu các món đang Mới gọi.
                        foreach (var row in card.Rows.Where(
                                     r => r.CurrentStatus == (int)KitchenStatus.MoiGoi))
                        {
                            var result = await _api.UpdateOrderItemStatusAsync(
                                row.ItemId,
                                (int)KitchenStatus.DangCheBien,
                                _chefUserId);
                            row.CurrentStatus =
                                result?.KitchenStatus ?? (int)KitchenStatus.DangCheBien;
                            ApplyItemStatusUI(row);
                        }
                    }
                    else
                    {
                        // Bước 2: khi tất cả món đã bắt đầu mới cho xác nhận hoàn thành.
                        foreach (var row in card.Rows.Where(
                                     r => r.CurrentStatus == (int)KitchenStatus.DangCheBien))
                        {
                            var result = await _api.UpdateOrderItemStatusAsync(
                                row.ItemId,
                                (int)KitchenStatus.DaCheBien,
                                _chefUserId);
                            row.CurrentStatus =
                                result?.KitchenStatus ?? (int)KitchenStatus.DaCheBien;
                            ApplyItemStatusUI(row);
                        }
                    }

                    ApplyBulkActionUI(card);

                    if (card.Rows.All(r =>
                            r.CurrentStatus is (int)KitchenStatus.DaCheBien
                                or (int)KitchenStatus.DaPhucVu
                                or (int)KitchenStatus.DaHuy))
                    {
                        await RemoveTicketAfterDelayAsync(card.OrderId);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
                }
                finally
                {
                    if (!btnDoneAll.IsDisposed)
                    {
                        btnDoneAll.Enabled = true;
                        ApplyBulkActionUI(card);
                    }
                }
            };
            column.Controls.Add(btnDoneAll);
            column.Height = footerY + 10 + 42 + 14;
        }
        else
        {
            column.Height = footerY + 2;
        }

        card.Column = column;
        UpdateCardUrgency(card);
        return card;
    }

    private Panel BuildItemRow(TicketItem item, TicketCard card, int rowWidth)
    {
        var hasNote = !string.IsNullOrEmpty(item.Note);
        var rowHeight = RowHeightFor(item, rowWidth);
        var nameLines = NameLines(item, rowWidth);
        var nameWidth = NameWidth(rowWidth);
        var row = new Panel { Size = new Size(rowWidth, rowHeight), Margin = new Padding(0), BackColor = Color.White };

        row.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(pen, RowPadX, row.Height - 1, row.Width - RowPadX, row.Height - 1);
        };

        var lblQty = new Label { Text = $"{item.Quantity}x", Font = AppFonts.HeadlineSm, ForeColor = AppColors.Primary, Location = new Point(RowPadX, 8), AutoSize = true };
        var lblName = new Label
        {
            Text = item.MenuItemName,
            Font = AppFonts.LabelLg,
            Location = new Point(NameX, 10),
            Size = new Size(nameWidth, nameLines * 20),
            AutoEllipsis = true,
        };
        var btnAction = new Button
        {
            Text = "Bắt đầu",
            Size = new Size(ActionBtnWidth, ActionBtnHeight),
            Location = new Point(rowWidth - ActionBtnWidth - RowPadX, (rowHeight - ActionBtnHeight) / 2),
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.LabelMd,
            Cursor = Cursors.Hand,
        };
        btnAction.FlatAppearance.BorderSize = 0;
        RoundCorners(btnAction, 8);

        row.Controls.Add(lblQty);
        row.Controls.Add(lblName);
        row.Controls.Add(btnAction);

        var y = 10 + nameLines * 20 + 2;

        // Món khách yêu cầu mang về: hiện nhãn để bếp/quầy nước đóng hộp.
        if (item.IsTakeaway)
        {
            var lblTake = new Label
            {
                Text = "MANG VỀ",
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.Primary,
                BackColor = Color.FromArgb(255, 234, 226),
                Location = new Point(NameX, y),
                AutoSize = true,
                Padding = new Padding(6, 1, 6, 1),
            };
            row.Controls.Add(lblTake);
            y += 22;
        }

        if (hasNote)
        {
            var lblNote = new Label { Text = "Ghi chú: " + item.Note, Font = AppFonts.BodyMd, ForeColor = AppColors.Error, Location = new Point(NameX, y), Size = new Size(nameWidth, 20), AutoEllipsis = true };
            row.Controls.Add(lblNote);
        }

        var rowState = new TicketRow
        {
            ItemId = item.Id,
            CreatedAt = item.CreatedAt,
            CurrentStatus = item.Status,
            QuantityLabel = lblQty,
            NameLabel = lblName,
            ActionButton = btnAction,
            RowPanel = row
        };

        ApplyItemStatusUI(rowState);
        card.Rows.Add(rowState);

        btnAction.Click += async (s, e) =>
        {
            btnAction.Enabled = false;
            try
            {
                if (rowState.CurrentStatus == (int)KitchenStatus.MoiGoi)
                {
                    // Từ Mới gọi -> Bấm Bắt đầu -> Đang chế biến
                    var result = await _api.UpdateOrderItemStatusAsync(rowState.ItemId, (int)KitchenStatus.DangCheBien, _chefUserId);
                    rowState.CurrentStatus = result?.KitchenStatus ?? (int)KitchenStatus.DangCheBien;
                }
                else if (rowState.CurrentStatus == (int)KitchenStatus.DangCheBien)
                {
                    // Từ Đang chế biến -> Bấm Xong -> Đã chế biến
                    var result = await _api.UpdateOrderItemStatusAsync(rowState.ItemId, (int)KitchenStatus.DaCheBien, _chefUserId);
                    rowState.CurrentStatus = result?.KitchenStatus ?? (int)KitchenStatus.DaCheBien;
                }

                ApplyItemStatusUI(rowState);
                ApplyBulkActionUI(card);

                if (card.Rows.All(r => r.CurrentStatus is (int)KitchenStatus.DaCheBien or (int)KitchenStatus.DaPhucVu or (int)KitchenStatus.DaHuy))
                    await RemoveTicketAfterDelayAsync(card.OrderId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không cập nhật được trạng thái món: {ex.Message}");
            }
            finally
            {
                if (!btnAction.IsDisposed) btnAction.Enabled = true;
            }
        };

        return row;
    }

    private static void ApplyBulkActionUI(TicketCard card)
    {
        var button = card.BulkActionButton;
        if (button is null || button.IsDisposed) return;

        var hasNew = card.Rows.Any(r => r.CurrentStatus == (int)KitchenStatus.MoiGoi);
        var hasCooking = card.Rows.Any(r => r.CurrentStatus == (int)KitchenStatus.DangCheBien);

        if (hasNew)
        {
            button.Text = "Bắt đầu tất cả";
            button.BackColor = AppColors.Warning;
            button.ForeColor = AppColors.OnSurface;
            button.Visible = true;
            return;
        }

        if (hasCooking)
        {
            button.Text = "Xác nhận hoàn thành tất cả";
            button.BackColor = AppColors.Primary;
            button.ForeColor = Color.White;
            button.Visible = true;
            return;
        }

        button.Visible = false;
    }

    private static void ApplyItemStatusUI(TicketRow row)
    {
        switch (row.CurrentStatus)
        {
            case (int)KitchenStatus.MoiGoi:
                row.NameLabel.ForeColor = AppColors.OnSurface;
                row.NameLabel.Font = AppFonts.LabelLg;
                row.ActionButton.Text = "Bắt đầu";
                row.ActionButton.BackColor = AppColors.Warning;
                row.ActionButton.ForeColor = AppColors.OnSurface;
                row.ActionButton.Cursor = Cursors.Hand;
                row.ActionButton.Visible = true;
                break;

            case (int)KitchenStatus.DangCheBien:
                row.NameLabel.ForeColor = AppColors.OnSurface;
                row.NameLabel.Font = AppFonts.LabelLg;
                row.ActionButton.Text = "Xác nhận";
                row.ActionButton.BackColor = AppColors.Primary;
                row.ActionButton.ForeColor = Color.White;
                row.ActionButton.Cursor = Cursors.Hand;
                row.ActionButton.Visible = true;
                break;

            case (int)KitchenStatus.DaCheBien:
            case (int)KitchenStatus.DaPhucVu:
                row.NameLabel.ForeColor = AppColors.OnSurfaceVariant;
                row.NameLabel.Font = new Font(AppFonts.LabelLg, FontStyle.Strikeout);
                row.ActionButton.Text = "✓ Xong";
                row.ActionButton.BackColor = AppColors.Success;
                row.ActionButton.ForeColor = Color.White;
                row.ActionButton.Cursor = Cursors.Default;
                break;

            case (int)KitchenStatus.DaHuy:
                row.RowPanel.BackColor = Color.MistyRose;
                row.NameLabel.BackColor = Color.MistyRose;
                row.NameLabel.Text = $"{row.NameLabel.Text} (ĐÃ HỦY)";
                row.NameLabel.ForeColor = Color.Red;
                row.NameLabel.Font = new Font(AppFonts.LabelLg, FontStyle.Strikeout | FontStyle.Bold);
                row.ActionButton.Visible = false;
                break;
        }
    }

    private class TicketCard
    {
        public required int OrderId;
        public required string TableName;
        public Panel Column = null!;
        public Panel Header = null!;
        public Label LblTable = null!;
        public Label LblOrder = null!;
        public Label ElapsedBadge = null!;
        public Button? BulkActionButton;
        public List<TicketRow> Rows { get; } = new();
    }

    private class TicketRow
    {
        public required int ItemId;
        public required DateTime CreatedAt;
        public int CurrentStatus;
        public string? CancelReason;
        public required Label QuantityLabel;
        public required Label NameLabel;
        public required Button ActionButton;
        public required Panel RowPanel;
    }
}