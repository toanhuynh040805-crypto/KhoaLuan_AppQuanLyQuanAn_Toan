using System.Drawing.Drawing2D;
using QuanAnWinForm.Controls;
using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh "Khuyen mai" - danh cho Quan ly tao/sua/tat cac chien dich giam gia
// theo % ap dung chung cho moi khach (xem ghi chu pham vi o DiscountCampaign.cs
// ben Backend: CHUA ho tro dieu kien "khach quen" vi chua co module Khach hang).
// Chien dich dang hieu luc se xuat hien trong ComboBox chon khuyen mai luc thanh
// toan o SalesControl (GET /api/discountcampaigns/active).
//
// Giao dien: the thong ke (tong / dang dien ra / sap dien ra / da ket thuc) + chip loc
// nhanh + moi chien dich la 1 the (badge %, ten, thoi gian con lai, thanh tien do).
public partial class PromotionsControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => LoadCampaignsAsync();


    private enum StatusKind { Off, Upcoming, Running, Expired }
    private enum CampaignFilter { All, Running, Upcoming, Ended }

    private readonly ApiService _api = new();
    private readonly int _userId;
    private List<DiscountCampaignDto> _campaigns = new();
    private CampaignFilter _filter = CampaignFilter.All;

    private static readonly Font FBody = new("Segoe UI", 10F);
    private static readonly Font FName = new("Segoe UI", 11.5F, FontStyle.Bold);
    private static readonly Font FSmall = new("Segoe UI", 9F);
    private static readonly Font FSmallBold = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FPercent = new("Segoe UI", 18F, FontStyle.Bold);

    public PromotionsControl() : this(0) { }

    public PromotionsControl(int userId)
    {
        _userId = userId;
        InitializeComponent();

        DisableHorizontalScroll(flowCampaigns);
        flowCampaigns.ClientSizeChanged += (s, e) => StretchRows();

        Load += async (s, e) => await LoadCampaignsAsync();
        btnAddCampaign.Click += (s, e) => OpenAddOrEditCampaignDialog(null);

        btnFilterAll.Click += (s, e) => SetFilter(CampaignFilter.All);
        btnFilterRunning.Click += (s, e) => SetFilter(CampaignFilter.Running);
        btnFilterUpcoming.Click += (s, e) => SetFilter(CampaignFilter.Upcoming);
        btnFilterEnded.Click += (s, e) => SetFilter(CampaignFilter.Ended);
        UpdateFilterButtons();
    }

    // ------------------------------------------------------------------
    // HAM DUNG CHUNG (giao dien khong phu thuoc DPI)
    // ------------------------------------------------------------------

    private int Dp(int px) => (int)Math.Round(px * DeviceDpi / 96.0);

    private static int TextW(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 2;

    private static Label MakeLabel(string text, Font font, Color color, bool ellipsis = true) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
        AutoSize = !ellipsis,
        AutoEllipsis = ellipsis,
    };

    private static GraphicsPath RoundedPath(int w, int h, int r)
    {
        var d = Math.Max(1, r * 2);
        var p = new GraphicsPath();
        p.AddArc(0, 0, d, d, 180, 90);
        p.AddArc(w - d, 0, d, d, 270, 90);
        p.AddArc(w - d, h - d, d, d, 0, 90);
        p.AddArc(0, h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private Label MakePill(string text, Color fg, Color bg)
    {
        var h = FSmallBold.Height + Dp(6);
        var w = TextW(text, FSmallBold) + Dp(18);
        var pill = new Label
        {
            Text = text,
            Font = FSmallBold,
            ForeColor = fg,
            BackColor = bg,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(w, h),
        };
        using var path = RoundedPath(w, h, h / 2);
        pill.Region = new Region(path);
        return pill;
    }

    private Button MakeOutlineButton(string text, Color fg, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Font = FSmallBold,
            ForeColor = fg,
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Size = new Size(width, height),
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderColor = AppColors.OutlineVariant;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(253, 245, 242);
        return btn;
    }

    private static void DisableHorizontalScroll(FlowLayoutPanel flow)
    {
        flow.AutoScroll = false;
        flow.HorizontalScroll.Enabled = false;
        flow.HorizontalScroll.Visible = false;
        flow.HorizontalScroll.Maximum = 0;
        flow.AutoScroll = true;
    }

    private int RowWidth(Padding margin) => Math.Max(400, flowCampaigns.ClientSize.Width - margin.Horizontal);

    // Moi the gian het chieu rong khung khi cua so doi kich thuoc.
    private void StretchRows()
    {
        var avail = flowCampaigns.ClientSize.Width;
        flowCampaigns.SuspendLayout();
        foreach (Control c in flowCampaigns.Controls)
        {
            if (c is not CardPanel) continue;
            var w = avail - c.Margin.Horizontal;
            if (w > 0 && c.Width != w) c.Width = w;
        }
        flowCampaigns.ResumeLayout();
    }

    // ------------------------------------------------------------------
    // TAI DU LIEU + HIEN THI
    // ------------------------------------------------------------------

    private async Task LoadCampaignsAsync()
    {
        try
        {
            _campaigns = await _api.GetCampaignsAsync();
            RenderCampaignList();
        }
        catch (Exception ex)
        {
            flowCampaigns.Controls.Clear();
            flowCampaigns.Controls.Add(new Label { Text = $"Không tải được danh sách khuyến mãi: {ex.Message}", Font = FBody, ForeColor = AppColors.Error, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
        }
    }

    // Trang thai hien thi: Sap dien ra (chua toi StartDate) / Dang dien ra (trong
    // khoang ngay VA IsActive) / Da het han (qua EndDate) / Da tat (IsActive=false).
    private static StatusKind KindOf(DiscountCampaignDto c)
    {
        if (!c.IsActive) return StatusKind.Off;
        var now = DateTime.Now;
        if (now < c.StartDate) return StatusKind.Upcoming;
        if (now > c.EndDate) return StatusKind.Expired;
        return StatusKind.Running;
    }

    private static (string Label, Color Fg, Color Bg) StatusInfo(StatusKind kind) => kind switch
    {
        StatusKind.Running => ("Đang diễn ra", AppColors.Success, Color.FromArgb(224, 242, 239)),
        StatusKind.Upcoming => ("Sắp diễn ra", Color.FromArgb(180, 110, 0), Color.FromArgb(255, 243, 224)),
        StatusKind.Expired => ("Đã hết hạn", AppColors.OnSurfaceVariant, AppColors.SurfaceContainer),
        _ => ("Đã tắt", AppColors.OnSurfaceVariant, AppColors.SurfaceContainer),
    };

    private bool Matches(DiscountCampaignDto c) => _filter switch
    {
        CampaignFilter.Running => KindOf(c) == StatusKind.Running,
        CampaignFilter.Upcoming => KindOf(c) == StatusKind.Upcoming,
        CampaignFilter.Ended => KindOf(c) is StatusKind.Expired or StatusKind.Off,
        _ => true,
    };

    private void SetFilter(CampaignFilter filter)
    {
        _filter = filter;
        RenderCampaignList();
    }

    private void UpdateFilterButtons()
    {
        var map = new[]
        {
            (btnFilterAll, CampaignFilter.All),
            (btnFilterRunning, CampaignFilter.Running),
            (btnFilterUpcoming, CampaignFilter.Upcoming),
            (btnFilterEnded, CampaignFilter.Ended),
        };
        foreach (var (btn, f) in map)
        {
            var selected = f == _filter;
            btn.BackColor = selected ? AppColors.Primary : Color.White;
            btn.ForeColor = selected ? Color.White : AppColors.OnSurfaceVariant;
            btn.FlatAppearance.BorderColor = selected ? AppColors.Primary : AppColors.OutlineVariant;
        }
    }

    private void UpdateStats()
    {
        var total = _campaigns.Count;
        var running = _campaigns.Count(c => KindOf(c) == StatusKind.Running);
        var upcoming = _campaigns.Count(c => KindOf(c) == StatusKind.Upcoming);
        var ended = total - running - upcoming;

        cardTotal.Configure("🎟️", AppColors.Primary, AppColors.ErrorContainer, "TỔNG CHIẾN DỊCH", total.ToString());
        cardRunning.Configure("🔥", AppColors.Success, Color.FromArgb(224, 242, 239), "ĐANG DIỄN RA", running.ToString());
        cardUpcoming.Configure("⏳", AppColors.Warning, Color.FromArgb(255, 243, 224), "SẮP DIỄN RA", upcoming.ToString());
        cardEnded.Configure("📁", AppColors.Outline, AppColors.SurfaceContainer, "ĐÃ KẾT THÚC / TẮT", ended.ToString());

        btnFilterAll.Text = $"Tất cả ({total})";
        btnFilterRunning.Text = $"Đang diễn ra ({running})";
        btnFilterUpcoming.Text = $"Sắp diễn ra ({upcoming})";
        btnFilterEnded.Text = $"Đã kết thúc ({ended})";
    }

    private void RenderCampaignList()
    {
        UpdateStats();
        UpdateFilterButtons();
        flowCampaigns.Controls.Clear();

        if (_campaigns.Count == 0)
        {
            flowCampaigns.Controls.Add(new Label { Text = "Chưa có chiến dịch khuyến mãi nào. Bấm \"+ Thêm chiến dịch\" để tạo mới.", Font = FBody, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        var items = _campaigns.Where(Matches).OrderByDescending(c => c.StartDate).ToList();
        if (items.Count == 0)
        {
            flowCampaigns.Controls.Add(new Label { Text = "Không có chiến dịch nào trong mục này.", Font = FBody, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        foreach (var c in items)
            flowCampaigns.Controls.Add(BuildCampaignRow(c));
    }

    // Dong chu "con N ngay" / "bat dau sau N ngay" ... hien canh khoang ngay.
    private static string TimeHint(DiscountCampaignDto c, StatusKind kind)
    {
        var now = DateTime.Now;
        switch (kind)
        {
            case StatusKind.Running:
                var left = (int)Math.Ceiling((c.EndDate - now).TotalDays);
                return left <= 1 ? "Kết thúc trong hôm nay" : $"Còn {left} ngày";
            case StatusKind.Upcoming:
                var wait = (int)Math.Ceiling((c.StartDate - now).TotalDays);
                return $"Bắt đầu sau {wait} ngày";
            case StatusKind.Expired:
                return "Đã kết thúc";
            default:
                return "Đang tạm tắt";
        }
    }

    private CardPanel BuildCampaignRow(DiscountCampaignDto c)
    {
        var margin = new Padding(0, 0, 0, Dp(12));
        var pad = Dp(20);
        var h = Dp(104);
        var kind = KindOf(c);
        var (statusLabel, statusFg, statusBg) = StatusInfo(kind);
        var live = kind is StatusKind.Running or StatusKind.Upcoming;

        var row = new CardPanel
        {
            Margin = margin,
            Size = new Size(RowWidth(margin), h),
            StripeColor = kind == StatusKind.Running ? AppColors.Success : (kind == StatusKind.Upcoming ? AppColors.Warning : AppColors.Outline),
            StripeWidth = Dp(4),
        };

        // --- Badge % giam ---
        var pctText = $"-{c.DiscountPercent:0.#}%";
        var pctW = Math.Max(Dp(96), TextW(pctText, FPercent) + Dp(28));
        var pctH = Dp(64);
        var lblPercent = new Label
        {
            Text = pctText,
            Font = FPercent,
            ForeColor = live ? AppColors.Primary : AppColors.OnSurfaceVariant,
            BackColor = live ? AppColors.ErrorContainer : AppColors.SurfaceContainer,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Size = new Size(pctW, pctH),
        };
        using (var path = RoundedPath(pctW, pctH, Dp(10)))
            lblPercent.Region = new Region(path);
        row.Controls.Add(lblPercent);

        // --- Ten / thoi gian / ghi chu ---
        var lblName = MakeLabel(c.CampaignName, FName, (kind == StatusKind.Off || kind == StatusKind.Expired) ? AppColors.OnSurfaceVariant : AppColors.OnSurface);
        var lblDate = MakeLabel($"📅  {c.StartDate:dd/MM/yyyy} → {c.EndDate:dd/MM/yyyy}   ·   {TimeHint(c, kind)}", FSmall, AppColors.OnSurfaceVariant);
        var hasNote = !string.IsNullOrWhiteSpace(c.Note);
        var lblNote = MakeLabel(hasNote ? c.Note! : "", FSmall, AppColors.Outline);
        row.Controls.Add(lblName);
        row.Controls.Add(lblDate);
        row.Controls.Add(lblNote);

        // --- Thanh tien do (chi voi chien dich dang dien ra) ---
        Panel? track = null, fill = null;
        double progress = 0;
        if (kind == StatusKind.Running)
        {
            var total = (c.EndDate - c.StartDate).TotalSeconds;
            progress = total <= 0 ? 1 : Math.Clamp((DateTime.Now - c.StartDate).TotalSeconds / total, 0, 1);
            track = new Panel { BackColor = AppColors.SurfaceContainer, Height = Dp(4) };
            fill = new Panel { BackColor = AppColors.Success, Height = Dp(4) };
            row.Controls.Add(track);
            row.Controls.Add(fill);
            fill.BringToFront();
        }

        // --- Trang thai + the "khach quen" ---
        var statusPill = MakePill(statusLabel, statusFg, statusBg);
        row.Controls.Add(statusPill);
        Label? memberPill = null;
        if (c.RequireMembership)
        {
            memberPill = MakePill("Khách quen", AppColors.Secondary, AppColors.SecondaryContainer);
            row.Controls.Add(memberPill);
        }

        // --- Nut thao tac ---
        var btnH = Dp(34);
        var btnEdit = MakeOutlineButton("Sửa", AppColors.Secondary, Dp(72), btnH);
        btnEdit.Click += (s, e) => OpenAddOrEditCampaignDialog(c);

        var toggleText = c.IsActive ? "Tắt" : "Bật lại";
        var btnToggle = MakeOutlineButton(toggleText, c.IsActive ? AppColors.Error : AppColors.Success, Dp(80), btnH);
        btnToggle.Click += async (s, e) =>
        {
            btnToggle.Enabled = false;
            try
            {
                await _api.SetCampaignActiveAsync(c.Id, !c.IsActive);
                await LoadCampaignsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không cập nhật được: {ex.Message}");
                btnToggle.Enabled = true;
            }
        };

        var btnDelete = MakeOutlineButton("Xóa", AppColors.OnSurfaceVariant, Dp(72), btnH);
        btnDelete.Click += async (s, e) =>
        {
            if (MessageBox.Show($"Xóa chiến dịch \"{c.CampaignName}\"? Các hóa đơn đã áp dụng chiến dịch này trước đây vẫn giữ nguyên số tiền đã giảm.", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                await _api.DeleteCampaignAsync(c.Id);
                await LoadCampaignsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không xóa được: {ex.Message}");
            }
        };

        row.Controls.Add(btnEdit);
        row.Controls.Add(btnToggle);
        row.Controls.Add(btnDelete);

        void LayoutRow()
        {
            var w = row.Width;
            var gap = Dp(8);

            // Phai -> trai: Xoa | Tat | Sua | nhan trang thai | nhan khach quen
            var by = (h - btnH) / 2;
            btnDelete.Location = new Point(w - pad - btnDelete.Width, by);
            btnToggle.Location = new Point(btnDelete.Left - gap - btnToggle.Width, by);
            btnEdit.Location = new Point(btnToggle.Left - gap - btnEdit.Width, by);

            statusPill.Location = new Point(btnEdit.Left - Dp(24) - statusPill.Width, (h - statusPill.Height) / 2);
            var leftMost = statusPill.Left;
            if (memberPill is not null)
            {
                memberPill.Location = new Point(statusPill.Left - Dp(8) - memberPill.Width, statusPill.Top);
                leftMost = memberPill.Left;
            }

            // Trai: badge % + khoi chu
            lblPercent.Location = new Point(pad, (h - pctH) / 2);
            var colX = pad + pctW + Dp(18);
            var colW = Math.Max(Dp(120), leftMost - Dp(20) - colX);

            lblName.SetBounds(colX, Dp(14), colW, FName.Height + Dp(2));
            lblDate.SetBounds(colX, lblName.Bottom + Dp(2), colW, FSmall.Height + Dp(2));
            lblNote.SetBounds(colX, lblDate.Bottom + Dp(2), colW, FSmall.Height + Dp(2));

            if (track is not null && fill is not null)
            {
                track.SetBounds(colX, h - Dp(16), colW, Dp(4));
                fill.SetBounds(colX, h - Dp(16), (int)(colW * progress), Dp(4));
            }
        }
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();
        return row;
    }

    // ------------------------------------------------------------------
    // DIALOG THEM / SUA
    // ------------------------------------------------------------------

    // Dung 1 dialog cho ca Them moi (campaign == null) va Sua (campaign != null),
    // giong pattern da dung o TableManagementControl/StaffControl.
    private void OpenAddOrEditCampaignDialog(DiscountCampaignDto? campaign)
    {
        var isEdit = campaign is not null;
        using var dialog = new Form
        {
            Text = isEdit ? "Sửa chiến dịch khuyến mãi" : "Thêm chiến dịch khuyến mãi",
            Width = 380,
            Height = 420,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblName = new Label { Text = "Tên chiến dịch *", Font = AppFonts.BodyMd, Location = new Point(20, 16), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 36), Width = 320, Font = AppFonts.BodyMd, Text = campaign?.CampaignName ?? "" };

        var lblStart = new Label { Text = "Bắt đầu *", Font = AppFonts.BodyMd, Location = new Point(20, 76), AutoSize = true };
        var dtpStart = new DateTimePicker { Location = new Point(20, 96), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.StartDate ?? DateTime.Today };

        var lblEnd = new Label { Text = "Kết thúc *", Font = AppFonts.BodyMd, Location = new Point(190, 76), AutoSize = true };
        var dtpEnd = new DateTimePicker { Location = new Point(190, 96), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.EndDate ?? DateTime.Today.AddDays(7) };

        var lblPercent = new Label { Text = "Phần trăm giảm (%) *", Font = AppFonts.BodyMd, Location = new Point(20, 136), AutoSize = true };
        var numPercent = new NumericUpDown { Location = new Point(20, 156), Width = 150, Minimum = 0, Maximum = 100, DecimalPlaces = 2, Value = campaign?.DiscountPercent ?? 10 };

        var lblNote = new Label { Text = "Ghi chú", Font = AppFonts.BodyMd, Location = new Point(20, 196), AutoSize = true };
        var txtNote = new TextBox { Location = new Point(20, 216), Width = 320, Height = 60, Font = AppFonts.BodyMd, Multiline = true, Text = campaign?.Note ?? "" };

        if (campaign is { RequireMembership: true })
        {
            var lblWarn = new Label
            {
                Text = "⚠ Chiến dịch này yêu cầu điều kiện khách quen - hệ thống hiện chưa hỗ trợ sửa loại này qua giao diện.",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Error,
                Location = new Point(20, 284),
                Size = new Size(320, 40),
            };
            dialog.Controls.Add(lblWarn);
        }

        dialog.Controls.Add(lblName);
        dialog.Controls.Add(txtName);
        dialog.Controls.Add(lblStart);
        dialog.Controls.Add(dtpStart);
        dialog.Controls.Add(lblEnd);
        dialog.Controls.Add(dtpEnd);
        dialog.Controls.Add(lblPercent);
        dialog.Controls.Add(numPercent);
        dialog.Controls.Add(lblNote);
        dialog.Controls.Add(txtNote);

        var btnSave = new Button { Text = isEdit ? "Lưu" : "Tạo chiến dịch", Location = new Point(110, 330), Size = new Size(150, 34), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        var name = txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập tên chiến dịch.");
            return;
        }
        if (dtpEnd.Value.Date <= dtpStart.Value.Date)
        {
            MessageBox.Show("Ngày kết thúc phải sau ngày bắt đầu.");
            return;
        }

        var note = string.IsNullOrWhiteSpace(txtNote.Text) ? null : txtNote.Text.Trim();

        if (isEdit)
            _ = UpdateCampaignAsync(campaign!.Id, name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note);
        else
            _ = CreateCampaignAsync(name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note);
    }

    private async Task CreateCampaignAsync(string name, DateTime start, DateTime end, decimal percent, string? note)
    {
        try
        {
            await _api.CreateCampaignAsync(name, start, end, percent, note, _userId);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tạo được chiến dịch: {ex.Message}");
        }
    }

    private async Task UpdateCampaignAsync(int id, string name, DateTime start, DateTime end, decimal percent, string? note)
    {
        try
        {
            await _api.UpdateCampaignAsync(id, name, start, end, percent, note);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được: {ex.Message}");
        }
    }
}