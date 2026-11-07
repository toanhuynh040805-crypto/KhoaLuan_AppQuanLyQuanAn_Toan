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
        var hasConditions = c.Conditions.Count > 0;
        var h = Dp(hasConditions ? 128 : 104);
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

        Label? lblCond = null;
        if (hasConditions)
        {
            var condText = "🎯  Điều kiện: " + string.Join("  ·  ", c.Conditions.Select(x => x.Description));
            lblCond = MakeLabel(condText, FSmall, AppColors.Secondary);
            new ToolTip().SetToolTip(lblCond, condText);
            row.Controls.Add(lblCond);
        }

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
            lblCond?.SetBounds(colX, lblNote.Bottom + Dp(2), colW, FSmall.Height + Dp(2));
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
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        using var dialog = new Form
        {
            Text = isEdit ? "Sửa chiến dịch khuyến mãi" : "Thêm chiến dịch khuyến mãi",
            ClientSize = new Size(500, 640),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        // ---------- Thông tin chung ----------
        var lblName = new Label { Text = "Tên chiến dịch *", Font = AppFonts.BodyMd, Location = new Point(20, 14), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 34), Width = 460, Font = AppFonts.BodyMd, Text = campaign?.CampaignName ?? "" };

        var lblStart = new Label { Text = "Bắt đầu *", Font = AppFonts.BodyMd, Location = new Point(20, 72), AutoSize = true };
        var dtpStart = new DateTimePicker { Location = new Point(20, 92), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.StartDate ?? DateTime.Today };

        var lblEnd = new Label { Text = "Kết thúc *", Font = AppFonts.BodyMd, Location = new Point(190, 72), AutoSize = true };
        var dtpEnd = new DateTimePicker { Location = new Point(190, 92), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.EndDate ?? DateTime.Today.AddDays(7) };

        var lblPercent = new Label { Text = "Giảm (%) *", Font = AppFonts.BodyMd, Location = new Point(360, 72), AutoSize = true };
        var numPercent = new NumericUpDown { Location = new Point(360, 92), Width = 120, Minimum = 0, Maximum = 100, DecimalPlaces = 2, Value = campaign?.DiscountPercent ?? 10 };

        var lblNote = new Label { Text = "Ghi chú", Font = AppFonts.BodyMd, Location = new Point(20, 128), AutoSize = true };
        var txtNote = new TextBox { Location = new Point(20, 148), Width = 460, Height = 50, Font = AppFonts.BodyMd, Multiline = true, Text = campaign?.Note ?? "" };

        // ---------- Điều kiện áp dụng ----------
        var lblCondTitle = new Label { Text = "Điều kiện áp dụng", Font = new Font(AppFonts.BodyMd, FontStyle.Bold), Location = new Point(20, 212), AutoSize = true };
        var lblCondHint = new Label
        {
            Text = "Hóa đơn phải thỏa TẤT CẢ điều kiện. Không thêm điều kiện nào = áp dụng cho mọi hóa đơn.",
            Font = FSmall,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(20, 234),
            Size = new Size(460, 32),
            AutoSize = false,
        };

        var cbType = new ComboBox { Location = new Point(20, 272), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Font = AppFonts.BodyMd };
        var pnlInput = new Panel { Location = new Point(20, 304), Size = new Size(460, 70) };
        var btnAddCond = new Button { Text = "+ Thêm điều kiện", Location = new Point(20, 380), Size = new Size(160, 30), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White };
        var lstConds = new ListBox { Location = new Point(20, 418), Size = new Size(460, 110), Font = AppFonts.BodyMd };
        var btnRemoveCond = new Button { Text = "Xóa điều kiện đã chọn", Location = new Point(20, 534), Size = new Size(180, 28), FlatStyle = FlatStyle.Flat };

        var btnSave = new Button { Text = isEdit ? "Lưu" : "Tạo chiến dịch", Location = new Point(175, 590), Size = new Size(150, 36), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };

        // ---------- Trạng thái ----------
        var conditions = campaign?.Conditions
            .Select(c => new CampaignConditionDto
            {
                ConditionType = c.ConditionType,
                Value1 = c.Value1,
                Value2 = c.Value2,
                Description = c.Description,
            })
            .ToList() ?? new List<CampaignConditionDto>();

        var available = new List<(string Type, string Label)>();
        Func<(string V1, string? V2)?>? readInput = null;

        static (string V1, string? V2)? Result(string v1, string? v2 = null) => (v1, v2);

        static Label PLabel(string text, int x, int y) => new()
        {
            Text = text,
            Font = FSmall,
            ForeColor = AppColors.OnSurfaceVariant,
            AutoSize = true,
            Location = new Point(x, y),
        };

        // Vẽ ô nhập tương ứng với loại điều kiện đang chọn.
        void BuildInput(string type)
        {
            pnlInput.Controls.Clear();
            readInput = null;

            switch (type)
            {
                case CampaignConditionTypes.VisitCount:
                    {
                        var numVisits = new NumericUpDown { Location = new Point(0, 22), Width = 80, Minimum = 1, Maximum = 1000, Value = 3, Font = AppFonts.BodyMd };
                        var numDays = new NumericUpDown { Location = new Point(160, 22), Width = 80, Minimum = 1, Maximum = 3650, Value = 30, Font = AppFonts.BodyMd };
                        pnlInput.Controls.Add(PLabel("Số lần ghé tối thiểu", 0, 2));
                        pnlInput.Controls.Add(numVisits);
                        pnlInput.Controls.Add(PLabel("Trong (ngày gần nhất)", 160, 2));
                        pnlInput.Controls.Add(numDays);
                        readInput = () => Result(numVisits.Value.ToString("0", inv), numDays.Value.ToString("0", inv));
                        break;
                    }

                case CampaignConditionTypes.MemberLevel:
                    {
                        var levels = new (string Key, string Label)[]
                        {
                        ("ThanhVien", "Thành viên"), ("Bac", "Bạc"), ("Vang", "Vàng"), ("KimCuong", "Kim cương"),
                        };
                        var cbLevel = new ComboBox { Location = new Point(0, 22), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Font = AppFonts.BodyMd };
                        foreach (var lv in levels) cbLevel.Items.Add(lv.Label);
                        cbLevel.SelectedIndex = 1;
                        pnlInput.Controls.Add(PLabel("Hạng tối thiểu", 0, 2));
                        pnlInput.Controls.Add(cbLevel);
                        readInput = () => Result(levels[cbLevel.SelectedIndex].Key);
                        break;
                    }

                case CampaignConditionTypes.Birthday:
                    {
                        var modes = new (string Key, string Label)[]
                        {
                        ("Day", "Đúng ngày sinh nhật của khách"), ("Month", "Trong tháng sinh nhật của khách"),
                        };
                        var cbMode = new ComboBox { Location = new Point(0, 22), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Font = AppFonts.BodyMd };
                        foreach (var m in modes) cbMode.Items.Add(m.Label);
                        cbMode.SelectedIndex = 0;
                        pnlInput.Controls.Add(PLabel("Áp dụng khi", 0, 2));
                        pnlInput.Controls.Add(cbMode);
                        readInput = () => Result(modes[cbMode.SelectedIndex].Key);
                        break;
                    }

                case CampaignConditionTypes.MinBill:
                    {
                        var numAmount = new NumericUpDown
                        {
                            Location = new Point(0, 22),
                            Width = 180,
                            Minimum = 1000,
                            Maximum = 1000000000,
                            Increment = 10000,
                            ThousandsSeparator = true,
                            Value = 200000,
                            Font = AppFonts.BodyMd,
                        };
                        pnlInput.Controls.Add(PLabel("Tạm tính tối thiểu (VNĐ)", 0, 2));
                        pnlInput.Controls.Add(numAmount);
                        readInput = () => Result(numAmount.Value.ToString("0", inv));
                        break;
                    }

                case CampaignConditionTypes.TimeRange:
                    {
                        var dtpFrom = new DateTimePicker { Location = new Point(0, 22), Width = 90, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = DateTime.Today.AddHours(14) };
                        var dtpTo = new DateTimePicker { Location = new Point(130, 22), Width = 90, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = DateTime.Today.AddHours(17) };
                        pnlInput.Controls.Add(PLabel("Từ giờ", 0, 2));
                        pnlInput.Controls.Add(dtpFrom);
                        pnlInput.Controls.Add(PLabel("Đến giờ", 130, 2));
                        pnlInput.Controls.Add(dtpTo);
                        readInput = () =>
                        {
                            var from = dtpFrom.Value.ToString("HH:mm", inv);
                            var to = dtpTo.Value.ToString("HH:mm", inv);
                            if (from == to)
                            {
                                MessageBox.Show("Giờ bắt đầu và giờ kết thúc không được trùng nhau.");
                                return null;
                            }
                            return Result(from, to);
                        };
                        break;
                    }

                case CampaignConditionTypes.DayOfWeek:
                    {
                        var dayDefs = new (int Value, string Label)[]
                        {
                        (1, "T2"), (2, "T3"), (3, "T4"), (4, "T5"), (5, "T6"), (6, "T7"), (0, "CN"),
                        };
                        var checks = new List<CheckBox>();
                        var posX = 0;
                        pnlInput.Controls.Add(PLabel("Áp dụng vào các ngày", 0, 2));
                        foreach (var def in dayDefs)
                        {
                            var chk = new CheckBox { Text = def.Label, Tag = def.Value, AutoSize = true, Location = new Point(posX, 24), Font = AppFonts.BodyMd };
                            checks.Add(chk);
                            pnlInput.Controls.Add(chk);
                            posX += 62;
                        }
                        readInput = () =>
                        {
                            var picked = checks.Where(k => k.Checked).Select(k => (int)k.Tag!).OrderBy(v => v).ToList();
                            if (picked.Count == 0)
                            {
                                MessageBox.Show("Vui lòng chọn ít nhất 1 ngày.");
                                return null;
                            }
                            return Result(string.Join(",", picked));
                        };
                        break;
                    }
            }
        }

        // Chỉ cho chọn các loại điều kiện CHƯA được thêm (mỗi loại 1 lần / chiến dịch).
        void RefillTypes()
        {
            available = CampaignConditionTypes.All
                .Where(t => conditions.All(c => c.ConditionType != t.Type))
                .ToList();

            cbType.Items.Clear();
            foreach (var t in available) cbType.Items.Add(t.Label);

            var any = available.Count > 0;
            cbType.Enabled = any;
            btnAddCond.Enabled = any;

            if (any)
            {
                cbType.SelectedIndex = 0;
            }
            else
            {
                pnlInput.Controls.Clear();
                readInput = null;
            }
        }

        void RefreshConditionList()
        {
            lstConds.Items.Clear();
            foreach (var c in conditions) lstConds.Items.Add(c.Description);
        }

        cbType.SelectedIndexChanged += (s, e) =>
        {
            if (cbType.SelectedIndex >= 0 && cbType.SelectedIndex < available.Count)
                BuildInput(available[cbType.SelectedIndex].Type);
        };

        btnAddCond.Click += (s, e) =>
        {
            if (cbType.SelectedIndex < 0 || cbType.SelectedIndex >= available.Count || readInput is null) return;

            var input = readInput();
            if (input is null) return;

            var type = available[cbType.SelectedIndex].Type;
            var (v1, v2) = input.Value;
            conditions.Add(new CampaignConditionDto
            {
                ConditionType = type,
                Value1 = v1,
                Value2 = v2,
                Description = DescribeCondition(type, v1, v2),
            });

            RefreshConditionList();
            RefillTypes();
        };

        btnRemoveCond.Click += (s, e) =>
        {
            var idx = lstConds.SelectedIndex;
            if (idx < 0 || idx >= conditions.Count)
            {
                MessageBox.Show("Vui lòng chọn điều kiện cần xóa trong danh sách.");
                return;
            }

            conditions.RemoveAt(idx);
            RefreshConditionList();
            RefillTypes();
        };

        dialog.Controls.AddRange(new Control[]
        {
            lblName, txtName, lblStart, dtpStart, lblEnd, dtpEnd, lblPercent, numPercent,
            lblNote, txtNote,
            lblCondTitle, lblCondHint, cbType, pnlInput, btnAddCond, lstConds, btnRemoveCond,
            btnSave,
        });
        dialog.AcceptButton = btnSave;

        RefreshConditionList();
        RefillTypes();

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
            _ = UpdateCampaignAsync(campaign!.Id, name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note, conditions);
        else
            _ = CreateCampaignAsync(name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note, conditions);
    }

    // Mô tả điều kiện vừa thêm trong dialog (điều kiện đã lưu thì Backend trả sẵn Description).
    private static string DescribeCondition(string type, string v1, string? v2)
    {
        switch (type)
        {
            case CampaignConditionTypes.VisitCount:
                return $"Khách ghé từ {v1} lần trong {v2} ngày gần nhất";

            case CampaignConditionTypes.MemberLevel:
                return $"Hạng thành viên từ {LevelLabel(v1)}";

            case CampaignConditionTypes.Birthday:
                return v1 == "Day" ? "Đúng ngày sinh nhật của khách" : "Trong tháng sinh nhật của khách";

            case CampaignConditionTypes.MinBill:
                {
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    return decimal.TryParse(v1, System.Globalization.NumberStyles.Number, inv, out var amount)
                        ? $"Hóa đơn từ {amount.ToString("#,##0", inv).Replace(',', '.')}đ"
                        : "Hóa đơn tối thiểu";
                }

            case CampaignConditionTypes.TimeRange:
                return $"Khung giờ {v1} – {v2}";

            case CampaignConditionTypes.DayOfWeek:
                {
                    var names = new[] { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };
                    var days = v1.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(p => int.TryParse(p, out var d) ? d : -1)
                        .Where(d => d is >= 0 and <= 6)
                        .OrderBy(d => (d + 6) % 7)
                        .Select(d => names[d]);
                    return "Các ngày: " + string.Join(", ", days);
                }

            default:
                return CampaignConditionTypes.LabelOf(type);
        }
    }

    private static string LevelLabel(string key) => key switch
    {
        "ThanhVien" => "Thành viên",
        "Bac" => "Bạc",
        "Vang" => "Vàng",
        "KimCuong" => "Kim cương",
        _ => key,
    };

    private async Task CreateCampaignAsync(string name, DateTime start, DateTime end, decimal percent, string? note, List<CampaignConditionDto> conditions)
    {
        try
        {
            await _api.CreateCampaignAsync(name, start, end, percent, note, _userId, conditions);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tạo được chiến dịch: {ex.Message}");
        }
    }

    private async Task UpdateCampaignAsync(int id, string name, DateTime start, DateTime end, decimal percent, string? note, List<CampaignConditionDto> conditions)
    {
        try
        {
            await _api.UpdateCampaignAsync(id, name, start, end, percent, note, conditions);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được: {ex.Message}");
        }
    }
}