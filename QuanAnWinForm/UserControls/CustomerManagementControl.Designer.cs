using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Khach hang & Khach hang than thiet - danh cho Role QuanLy,
// ThuNgan (xem SidebarControl). Gom: danh sach/tim kiem, Them/Sua/Xoa, va
// dialog chi tiet xem lich su tich/doi diem + don hang gan day + cong/tru diem
// thu cong (vd tang diem sinh nhat).
public partial class CustomerManagementControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => LoadCustomersAsync();


    private readonly ApiService _api = new();

    private List<CustomerDto> _customers = new();
    private CustomerDto? _selected;
    private static readonly string[] MemberLevelLabels = { "Thành viên", "Bạc", "Vàng", "Kim cương" };
    private static readonly string[] LoyaltyTypeLabels = { "Tích điểm", "Đổi điểm" };
    private static readonly string[] OrderStatusLabels = { "Đang mở", "Đã thanh toán", "Đã hủy" };

    public CustomerManagementControl()
    {
        InitializeComponent();
        StyleGrid();
        btnToggleWeb.Click += async (s, e) => await ToggleWebAccountAsync();
        Load += async (s, e) => await LoadCustomersAsync();
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        btnAddNew.Click += (s, e) => OpenAddOrEditDialog(null);

        gridCustomers.SelectionChanged += (s, e) => SyncSelectionFromGrid();
        gridCustomers.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0 && _selected is not null) await OpenDetailDialogAsync(_selected.Id); };

        btnViewDetail.Click += async (s, e) => { if (_selected is not null) await OpenDetailDialogAsync(_selected.Id); else MessageBox.Show("Vui lòng chọn 1 khách hàng."); };
        btnEdit.Click += (s, e) => { if (_selected is not null) OpenAddOrEditDialog(_selected); else MessageBox.Show("Vui lòng chọn 1 khách hàng."); };
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
    }

    // =====================================================================
    // GIAO DIỆN BẢNG (vẽ tay phần "huy hiệu" cho Hạng & Tài khoản web)
    // =====================================================================

    private void StyleGrid()
    {
        // Chống nhấp nháy khi cuộn / đổi dòng chọn
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(gridCustomers, true);

        // Tiêu đề cột số canh phải cho thẳng hàng với dữ liệu
        foreach (var col in new[] { colPoint, colOrders, colSpent })
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        colName.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        // Căn ô nhập giữa theo chiều dọc để thẳng hàng với biểu tượng kính lúp
        void CenterSearchBox() => txtSearch.Top = Math.Max(0, (panelSearch.Height - txtSearch.Height) / 2);
        CenterSearchBox();
        panelSearch.Resize += (s, e) => CenterSearchBox();

        // Ô tìm kiếm liền mạch: kính lúp vẽ vector đơn sắc bên trong khung, viền đổi màu khi đang gõ
        panelSearch.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var borderColor = txtSearch.Focused ? AppColors.Primary : Color.FromArgb(208, 208, 208);
            using (var border = new Pen(borderColor))
                g.DrawRectangle(border, 0, 0, panelSearch.Width - 1, panelSearch.Height - 1);

            var iconColor = txtSearch.Focused ? AppColors.Primary : Color.FromArgb(143, 112, 104);
            using var pen = new Pen(iconColor, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            int cy = panelSearch.Height / 2;
            g.DrawEllipse(pen, 14, cy - 8, 12, 12);   // thấu kính
            g.DrawLine(pen, 24, cy + 3, 29, cy + 8);  // cán
        };
        txtSearch.Enter += (s, e) => panelSearch.Invalidate();
        txtSearch.Leave += (s, e) => panelSearch.Invalidate();
        panelSearch.Click += (s, e) => txtSearch.Focus();
        panelSearch.Cursor = Cursors.IBeam;

        // Tắt hoàn toàn hệ thống viền có sẵn của DataGridView (nó xử lý riêng dòng đầu / cột đầu
        // nên viền không đồng nhất). Thay vào đó TỰ kẻ lưới cho mọi ô trong GridCustomers_CellPainting.
        gridCustomers.CellBorderStyle = DataGridViewCellBorderStyle.None;
        gridCustomers.CellPainting += GridCustomers_CellPainting;
        gridCustomers.Paint += GridCustomers_PaintGridLines;
    }

    private static (Color Fg, Color Bg) BadgeColors(string text) => text switch
    {
        "Thành viên" => (Color.FromArgb(84, 96, 103), Color.FromArgb(238, 238, 238)),
        "Bạc" => (Color.FromArgb(69, 90, 100), Color.FromArgb(222, 230, 235)),
        "Vàng" => (Color.FromArgb(166, 106, 0), Color.FromArgb(255, 243, 205)),
        "Kim cương" => (Color.FromArgb(0, 96, 150), Color.FromArgb(225, 240, 252)),
        "Đang hoạt động" => (Color.FromArgb(0, 104, 92), Color.FromArgb(224, 242, 241)),
        "Đã khóa" => (AppColors.Error, AppColors.ErrorContainer),
        _ => (Color.FromArgb(140, 140, 140), Color.FromArgb(242, 242, 242)), // "Chưa có"
    };

    // Kẻ lưới 1 lần duy nhất cho CẢ bảng (đè lên trên cùng, nét đặc, không khử răng cưa) nên
    // mọi dòng - kể cả dòng đầu - có đường kẻ giống hệt nhau, và che luôn mọi vạch phụ do
    // DataGridView tự vẽ ở mép trên/trái của ô.
    private void GridCustomers_PaintGridLines(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        using var pen = new Pen(gridCustomers.GridColor);

        var rows = new List<Rectangle>();
        for (int i = 0; i < gridCustomers.Rows.Count; i++)
        {
            var rr = gridCustomers.GetRowDisplayRectangle(i, false);
            if (rr.Height > 0) rows.Add(rr);
        }
        if (rows.Count == 0) return;

        var cols = new List<Rectangle>();
        foreach (DataGridViewColumn c in gridCustomers.Columns)
        {
            if (!c.Visible) continue;
            var cr = gridCustomers.GetColumnDisplayRectangle(c.Index, false);
            if (cr.Width > 0) cols.Add(cr);
        }
        if (cols.Count == 0) return;

        int left = cols[0].Left, right = cols[^1].Right - 1;
        int top = rows[0].Top, bottom = rows[^1].Bottom - 1;

        // Đường ngang: mép trên của từng dòng + mép dưới dòng cuối
        foreach (var r in rows) g.DrawLine(pen, left, r.Top, right, r.Top);
        g.DrawLine(pen, left, bottom, right, bottom);

        // Đường dọc: mép trái của từng cột (trừ cột đầu: đã có khung ngoài panelGridCard)
        for (int i = 1; i < cols.Count; i++) g.DrawLine(pen, cols[i].Left, top, cols[i].Left, bottom);
    }

    private void GridCustomers_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null) return;
        var col = gridCustomers.Columns[e.ColumnIndex];
        var isBadgeCol = col == colLevel || col == colWebAccount;
        var text = e.Value?.ToString();

        // Ô thường: vẽ nền + nội dung, KHÔNG vẽ viền (lưới do GridCustomers_PaintGridLines lo).
        if (!isBadgeCol || string.IsNullOrEmpty(text))
        {
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Border);
            e.Handled = true;
            return;
        }

        e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);

        var (fg, bg) = BadgeColors(text);
        using var font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        var textSize = TextRenderer.MeasureText(e.Graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);

        int h = 26;
        int w = Math.Min(textSize.Width + 22, e.CellBounds.Width - 16);
        var rect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + (e.CellBounds.Height - h) / 2, w, h);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            int d = h; // bo tròn hoàn toàn 2 đầu
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();
            using var brush = new SolidBrush(bg);
            e.Graphics.FillPath(brush, path);
        }
        TextRenderer.DrawText(e.Graphics, text, font, rect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None; // trả lại chế độ mặc định cho ô kế tiếp
        e.Handled = true;
    }

    // =====================================================================
    // TẢI DỮ LIỆU & DANH SÁCH
    // =====================================================================

    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _api.GetCustomersAsync();

            // Người dùng có thể đã chuyển màn hình trong lúc request đang chạy.
            // Không chạm vào DataGridView sau khi control đã bị Dispose.
            if (IsDisposed || Disposing)
                return;

            _customers = customers;
            ApplyFilter();
            RenderStatCards();
        }
        catch (OperationCanceledException)
        {
            // Hủy request khi rời màn hình là bình thường, không hiện popup.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing && Visible)
                MessageBox.Show($"Không tải được danh sách khách hàng: {ex.Message}");
        }
    }

    private void RenderStatCards()
    {
        var total = _customers.Count;
        var vip = _customers.Count(c => c.MemberLevel > 0);
        var today = DateTime.Today;
        var newThisMonth = _customers.Count(c => c.CreatedAt.Year == today.Year && c.CreatedAt.Month == today.Month);
        var withWeb = _customers.Count(c => c.HasWebAccount);

        cardTotalCustomers.Configure("👥", AppColors.Primary, AppColors.ErrorContainer, "TỔNG KHÁCH HÀNG", total.ToString("N0"));
        cardVipCustomers.Configure("⭐", AppColors.Warning, Color.FromArgb(255, 243, 224), "KHÁCH THÂN THIẾT", vip.ToString("N0"));
        cardNewCustomers.Configure("🆕", AppColors.Tertiary, Color.FromArgb(224, 242, 241), "KHÁCH MỚI THÁNG NÀY", newThisMonth.ToString("N0"));
        cardWebAccounts.Configure("🌐", AppColors.Secondary, AppColors.SecondaryContainer, "CÓ TÀI KHOẢN WEB", withWeb.ToString("N0"));
    }

    // Lọc phía client theo tên/SĐT - danh sách khách của 1 quán vừa/nhỏ không
    // nhiều nên không cần gọi lại API mỗi lần gõ phím.
    private void ApplyFilter()
    {
        var keyword = txtSearch.Text.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _customers
            : _customers.Where(c =>
                c.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.Phone.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        RenderGrid(filtered);
    }

    private void RenderGrid(List<CustomerDto> list)
    {
        gridCustomers.Rows.Clear();
        foreach (var c in list)
        {
            var levelIndex = Math.Clamp(c.MemberLevel, 0, MemberLevelLabels.Length - 1);
            var rowIndex = gridCustomers.Rows.Add(
                c.Id,
                c.FullName,
                c.Phone,
                c.Email ?? "",
                c.DateOfBirth?.ToString("dd/MM/yyyy") ?? "",
                c.LoyaltyPoint.ToString("N0"),
                MemberLevelLabels[levelIndex],
                c.OrderCount.ToString(),
                $"{c.TotalSpent:N0}đ",
                WebAccountText(c)
            );
            gridCustomers.Rows[rowIndex].Tag = c;
        }

        gridCustomers.ClearSelection();
        _selected = null;
        UpdateWebButton();
    }

    private void SyncSelectionFromGrid()
    {
        _selected = gridCustomers.SelectedRows.Count > 0 && gridCustomers.SelectedRows[0].Tag is CustomerDto c
            ? c
            : null;
        UpdateWebButton();
    }

    // =====================================================================
    // THÊM / SỬA
    // =====================================================================

    private void OpenAddOrEditDialog(CustomerDto? existing)
    {
        var isEdit = existing is not null;
        using var dialog = new Form
        {
            Text = isEdit ? "Sửa thông tin khách hàng" : "Thêm khách hàng mới",
            Width = 380,
            Height = 400,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var txtFullName = MakeLabeledInput(dialog, "Họ tên *", 20);
        var txtPhone = MakeLabeledInput(dialog, "Số điện thoại *", 80);
        var txtEmail = MakeLabeledInput(dialog, "Email", 140);

        var lblDobCap = new Label { Text = "Ngày sinh", Font = AppFonts.BodyMd, Location = new Point(20, 200), AutoSize = true };
        var chkHasDob = new CheckBox { Text = "Có", Location = new Point(240, 198), AutoSize = true };
        var dtpDob = new DateTimePicker { Location = new Point(20, 220), Width = 200, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddYears(-20), Enabled = false };
        chkHasDob.CheckedChanged += (s, e) => dtpDob.Enabled = chkHasDob.Checked;
        dialog.Controls.Add(lblDobCap);
        dialog.Controls.Add(chkHasDob);
        dialog.Controls.Add(dtpDob);

        if (isEdit)
        {
            txtFullName.Text = existing!.FullName;
            txtPhone.Text = existing.Phone;
            txtEmail.Text = existing.Email ?? "";
            if (existing.DateOfBirth.HasValue)
            {
                chkHasDob.Checked = true;
                dtpDob.Value = existing.DateOfBirth.Value;
            }
        }

        var btnSave = new Button { Text = "Lưu", Location = new Point(140, 280), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(txtFullName.Text) || string.IsNullOrWhiteSpace(txtPhone.Text))
        {
            MessageBox.Show("Vui lòng nhập đủ các trường có dấu *");
            return;
        }

        DateTime? dob = chkHasDob.Checked ? dtpDob.Value.Date : null;

        if (isEdit)
            _ = SaveEditAsync(existing!.Id, txtFullName.Text.Trim(), txtPhone.Text.Trim(), txtEmail.Text.Trim(), dob);
        else
            _ = SaveNewAsync(txtFullName.Text.Trim(), txtPhone.Text.Trim(), txtEmail.Text.Trim(), dob);
    }

    private async Task SaveNewAsync(string fullName, string phone, string email, DateTime? dob)
    {
        try
        {
            await _api.CreateCustomerAsync(fullName, phone, string.IsNullOrEmpty(email) ? null : email, dob);
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thêm được khách hàng: {ex.Message}");
        }
    }

    private async Task SaveEditAsync(int id, string fullName, string phone, string email, DateTime? dob)
    {
        try
        {
            await _api.UpdateCustomerAsync(id, fullName, phone, string.IsNullOrEmpty(email) ? null : email, dob);
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được thay đổi: {ex.Message}");
        }
    }

    private TextBox MakeLabeledInput(Form dialog, string label, int y)
    {
        var lbl = new Label { Text = label, Font = AppFonts.BodyMd, Location = new Point(20, y), AutoSize = true };
        var txt = new TextBox { Location = new Point(20, y + 20), Width = 320, Font = AppFonts.BodyMd };
        dialog.Controls.Add(lbl);
        dialog.Controls.Add(txt);
        return txt;
    }

    // =====================================================================
    // XÓA
    // =====================================================================

    private async Task DeleteSelectedAsync()
    {
        if (_selected is null)
        {
            MessageBox.Show("Vui lòng chọn 1 khách hàng.");
            return;
        }

        if (MessageBox.Show($"Xóa khách hàng \"{_selected.FullName}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        try
        {
            await _api.DeleteCustomerAsync(_selected.Id);
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }
    private static string WebAccountText(CustomerDto c) =>
    !c.HasWebAccount ? "Chưa có" : c.WebAccountActive ? "Đang hoạt động" : "Đã khóa";

    private void UpdateWebButton()
    {
        if (_selected is null || !_selected.HasWebAccount)
        {
            btnToggleWeb.Enabled = false;
            btnToggleWeb.Text = "Khóa/Mở TK web";
            btnToggleWeb.BackColor = Color.FromArgb(238, 238, 238);
            return;
        }
        btnToggleWeb.Enabled = true;
        btnToggleWeb.Text = _selected.WebAccountActive ? "Khóa TK web" : "Mở khóa TK web";
        btnToggleWeb.BackColor = _selected.WebAccountActive ? AppColors.Error : AppColors.Success;
    }

    private async Task ToggleWebAccountAsync()
    {
        if (_selected is null || !_selected.HasWebAccount) return;

        var newActive = !_selected.WebAccountActive;
        var action = newActive ? "mở khóa" : "khóa";
        var extra = newActive ? "" : "\nKhách đang đăng nhập trên web sẽ bị đăng xuất.";
        if (MessageBox.Show($"Bạn muốn {action} tài khoản web của \"{_selected.FullName}\"?{extra}",
                "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

        btnToggleWeb.Enabled = false;
        try
        {
            await _api.SetCustomerWebAccountActiveAsync(_selected.Id, newActive);
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không đổi được trạng thái tài khoản web: {ex.Message}");
            btnToggleWeb.Enabled = true;
        }
    }
    // =====================================================================
    // CHI TIẾT: LỊCH SỬ ĐIỂM + ĐƠN HÀNG GẦN ĐÂY + CỘNG/TRỪ ĐIỂM THỦ CÔNG
    // =====================================================================

    private async Task OpenDetailDialogAsync(int customerId)
    {
        CustomerDetailDto? detail;
        try
        {
            detail = await _api.GetCustomerAsync(customerId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được chi tiết khách hàng: {ex.Message}");
            return;
        }
        if (detail is null)
        {
            MessageBox.Show("Không tìm thấy khách hàng này.");
            return;
        }

        const int W = 560;
        using var dialog = new Form
        {
            Text = $"Chi tiết khách hàng - {detail.FullName}",
            AutoScaleDimensions = new SizeF(96F, 96F),
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(W, 700),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = AppColors.Background,
            Font = new Font("Segoe UI", 9F),
        };

        var levelIndex = Math.Clamp(detail.MemberLevel, 0, MemberLevelLabels.Length - 1);
        var levelText = MemberLevelLabels[levelIndex];
        var fullName = detail.FullName.Trim();
        var initial = fullName.Length > 0 ? fullName[0].ToString().ToUpper() : "?";

        // ================= 1. THÔNG TIN KHÁCH + ĐIỂM =================
        var panelProfile = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.White };
        panelProfile.Paint += (s, e) => DrawBottomLine(e.Graphics, panelProfile);

        var avatar = new Panel { Location = new Point(20, 24), Size = new Size(64, 64), BackColor = Color.White };
        avatar.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(AppColors.ErrorContainer);
            e.Graphics.FillEllipse(b, 0, 0, avatar.Width - 1, avatar.Height - 1);
            using var initialFont = new Font("Segoe UI", 20F, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, initial, initialFont, new Rectangle(0, 0, avatar.Width, avatar.Height),
                AppColors.Primary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };

        var lblName = new Label { Text = fullName, Font = AppFonts.HeadlineMd, Location = new Point(100, 14), Size = new Size(280, 32), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        var lblPhone = new Label { Text = detail.Phone, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurface, Location = new Point(100, 50), Size = new Size(280, 22), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        var lblEmail = new Label
        {
            Text = string.IsNullOrWhiteSpace(detail.Email) ? "Chưa có email" : detail.Email,
            Font = AppFonts.BodyMd,
            ForeColor = RowMuted,
            Location = new Point(100, 72),
            Size = new Size(280, 22),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var lblDob = new Label
        {
            Text = detail.DateOfBirth.HasValue ? $"Ngày sinh: {detail.DateOfBirth.Value:dd/MM/yyyy}" : "Ngày sinh: chưa có",
            Font = AppFonts.BodyMd,
            ForeColor = RowMuted,
            Location = new Point(100, 94),
            Size = new Size(280, 22),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var lblPoints = new Label { Text = detail.LoyaltyPoint.ToString("N0"), Font = new Font("Segoe UI", 26F, FontStyle.Bold), ForeColor = AppColors.Primary, Location = new Point(W - 20 - 160, 10), Size = new Size(160, 50), TextAlign = ContentAlignment.MiddleRight };
        var lblPointsCap = new Label { Text = "điểm hiện có", Font = AppFonts.BodyMd, ForeColor = RowMuted, Location = new Point(W - 20 - 160, 60), Size = new Size(160, 22), TextAlign = ContentAlignment.MiddleRight };
        var (lvFg, lvBg) = BadgeColors(levelText);
        var pillLevel = new PillLabel(levelText, lvFg, lvBg);
        pillLevel.Location = new Point(W - 20 - pillLevel.Width, 88);

        panelProfile.Controls.AddRange(new Control[] { avatar, lblName, lblPhone, lblEmail, lblDob, lblPoints, lblPointsCap, pillLevel });

        // ================= 2. CỘNG / TRỪ ĐIỂM THỦ CÔNG =================
        var panelAdjust = new Panel { Dock = DockStyle.Top, Height = 108, BackColor = AppColors.Background };
        panelAdjust.Paint += (s, e) => DrawBottomLine(e.Graphics, panelAdjust);

        var lblAdjustTitle = new Label { Text = "Cộng / trừ điểm thủ công", Font = AppFonts.LabelLg, Location = new Point(20, 12), AutoSize = true };
        var lblAdjustHint = new Label { Text = "Dùng để tặng điểm (vd: sinh nhật) hoặc điều chỉnh khi có sai sót.", Font = AppFonts.BodyMd, ForeColor = RowMuted, Location = new Point(20, 34), AutoSize = true };
        var numAdjust = new NumericUpDown { Location = new Point(20, 62), Width = 120, Minimum = 1, Maximum = 100000, Value = 10, Font = new Font("Segoe UI", 11F), TextAlign = HorizontalAlignment.Right, ThousandsSeparator = true };
        var btnAddPoint = MakeDetailButton("+  Cộng", AppColors.Success, Color.White, new Point(156, 60), new Size(110, 36));
        var btnSubPoint = MakeDetailButton("−  Trừ", AppColors.Error, Color.White, new Point(274, 60), new Size(110, 36));
        numAdjust.Top = btnAddPoint.Top + (btnAddPoint.Height - numAdjust.Height) / 2;

        async void DoAdjust(int sign)
        {
            try
            {
                await _api.AdjustCustomerPointsAsync(customerId, sign * (int)numAdjust.Value);
                dialog.DialogResult = DialogResult.Retry; // báo cho vòng lặp bên ngoài biết cần mở lại/refresh
                dialog.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không điều chỉnh được điểm: {ex.Message}");
            }
        }
        btnAddPoint.Click += (s, e) => DoAdjust(+1);
        btnSubPoint.Click += (s, e) => DoAdjust(-1);

        panelAdjust.Controls.AddRange(new Control[] { lblAdjustTitle, lblAdjustHint, numAdjust, btnAddPoint, btnSubPoint });

        // ================= 3. TAB: LỊCH SỬ ĐIỂM | ĐƠN HÀNG GẦN ĐÂY =================
        var panelTabs = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };
        var tabHistory = new Label { Text = $"Lịch sử điểm ({detail.LoyaltyHistory.Count})", Font = AppFonts.LabelLg, Location = new Point(12, 0), Size = new Size(170, 46), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
        var tabOrders = new Label { Text = $"Đơn hàng gần đây ({detail.RecentOrders.Count})", Font = AppFonts.LabelLg, Location = new Point(188, 0), Size = new Size(200, 46), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
        panelTabs.Controls.Add(tabHistory);
        panelTabs.Controls.Add(tabOrders);

        var showHistory = true;
        panelTabs.Paint += (s, e) =>
        {
            DrawBottomLine(e.Graphics, panelTabs);
            var tab = showHistory ? tabHistory : tabOrders;
            using var b = new SolidBrush(AppColors.Primary);
            e.Graphics.FillRectangle(b, tab.Left + 14, panelTabs.Height - 3, tab.Width - 28, 3);
        };

        // ================= 4. DANH SÁCH =================
        var listHistory = BuildDetailList(detail.LoyaltyHistory, 68, DrawHistoryRow, "Chưa có lịch sử tích/đổi điểm.");
        var listOrders = BuildDetailList(detail.RecentOrders, 68, DrawOrderRow, "Chưa có đơn hàng nào.");
        listOrders.Visible = false;
        var panelList = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        panelList.Controls.Add(listHistory);
        panelList.Controls.Add(listOrders);

        void SwitchTab(bool history)
        {
            showHistory = history;
            listHistory.Visible = history;
            listOrders.Visible = !history;
            tabHistory.ForeColor = history ? AppColors.Primary : RowMuted;
            tabOrders.ForeColor = history ? RowMuted : AppColors.Primary;
            panelTabs.Invalidate();
        }
        tabHistory.Click += (s, e) => SwitchTab(true);
        tabOrders.Click += (s, e) => SwitchTab(false);
        SwitchTab(true);

        // ================= 5. THANH DƯỚI: NÚT ĐÓNG =================
        var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = AppColors.Background };
        panelBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(pen, 0, 0, panelBottom.Width, 0);
        };
        var btnClose = MakeDetailButton("Đóng", AppColors.Secondary, Color.White, new Point(W - 20 - 120, 15), new Size(120, 38));
        btnClose.DialogResult = DialogResult.Cancel;
        panelBottom.Controls.Add(btnClose);
        dialog.CancelButton = btnClose;

        // Thứ tự Add: control thêm SAU sẽ được xếp lên TRÊN -> thêm từ dưới lên trên
        dialog.Controls.Add(panelList);
        dialog.Controls.Add(panelBottom);
        dialog.Controls.Add(panelTabs);
        dialog.Controls.Add(panelAdjust);
        dialog.Controls.Add(panelProfile);

        var result = dialog.ShowDialog();

        // Nếu vừa cộng/trừ điểm thành công (DialogResult.Retry) -> làm mới danh
        // sách ngoài lưới + mở lại dialog chi tiết để thấy số dư điểm mới ngay.
        if (result == DialogResult.Retry)
        {
            await LoadCustomersAsync();
            await OpenDetailDialogAsync(customerId);
        }
    }

    // =====================================================================
    // THÀNH PHẦN DÙNG CHUNG CỦA DIALOG CHI TIẾT
    // =====================================================================

    private static readonly Font RowTitleFont = new("Segoe UI", 10F, FontStyle.Bold);
    private static readonly Font RowSubFont = new("Segoe UI", 9F);
    private static readonly Font RowBigFont = new("Segoe UI", 14F, FontStyle.Bold);
    private static readonly Font RowAmountFont = new("Segoe UI", 11F, FontStyle.Bold);
    private static readonly Font RowIconFont = new("Segoe UI", 15F, FontStyle.Bold);
    private static readonly Font RowEmojiFont = new("Segoe UI Emoji", 12F);
    private static readonly Color RowMuted = Color.FromArgb(96, 104, 110);

    private const TextFormatFlags TfLine = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    private const TextFormatFlags TfCenter = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

    private static void DrawBottomLine(Graphics g, Control c)
    {
        using var pen = new Pen(AppColors.OutlineVariant);
        g.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
    }

    private static Button MakeDetailButton(string text, Color back, Color fore, Point location, Size size)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = location,
            Size = size,
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    // Danh sách vẽ tay: không bị chồng chữ, không có thanh cuộn ngang, cuộn mượt khi nhiều dòng.
    private static Control BuildDetailList<T>(IReadOnlyList<T> items, int rowHeight, Action<Graphics, Rectangle, T> draw, string emptyText) where T : class
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        if (items.Count == 0)
        {
            host.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = emptyText,
                Font = AppFonts.BodyMd,
                ForeColor = RowMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
            });
            return host;
        }

        var list = new DetailListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = rowHeight,
            IntegralHeight = false,
            SelectionMode = SelectionMode.None,
            HorizontalScrollbar = false,
            BackColor = Color.White,
        };
        foreach (var it in items) list.Items.Add(it);
        list.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Color.White)) e.Graphics.FillRectangle(bg, e.Bounds);
            draw(e.Graphics, e.Bounds, (T)list.Items[e.Index]);
        };
        host.Controls.Add(list);
        return host;
    }

    private static void DrawRowSeparator(Graphics g, Rectangle r, int left)
    {
        using var sep = new Pen(AppColors.OutlineVariant);
        g.DrawLine(sep, r.X + left, r.Bottom - 1, r.Right, r.Bottom - 1);
    }

    private static void DrawHistoryRow(Graphics g, Rectangle r, LoyaltyHistoryDto h)
    {
        bool earn = h.Points >= 0;
        var fg = earn ? Color.FromArgb(0, 104, 92) : AppColors.Error;
        var tint = earn ? Color.FromArgb(224, 242, 241) : AppColors.ErrorContainer;

        var circle = new Rectangle(r.X + 20, r.Y + (r.Height - 40) / 2, 40, 40);
        using (var b = new SolidBrush(tint)) g.FillEllipse(b, circle);
        TextRenderer.DrawText(g, earn ? "+" : "−", RowIconFont, circle, fg, TfCenter);

        const int left = 76;
        int right = r.Right - 20;
        string pts = (earn ? "+" : "") + h.Points.ToString("N0");
        int ptsW = TextRenderer.MeasureText(g, pts, RowBigFont, Size.Empty, TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, pts, RowBigFont, new Rectangle(right - ptsW, r.Y, ptsW, r.Height), fg, TfLine);

        int textW = right - ptsW - 16 - (r.X + left);
        int typeIndex = Math.Clamp(h.Type, 0, LoyaltyTypeLabels.Length - 1);
        TextRenderer.DrawText(g, LoyaltyTypeLabels[typeIndex], RowTitleFont, new Rectangle(r.X + left, r.Y + 12, textW, 22), AppColors.OnSurface, TfLine);

        var sub = h.CreatedAt.ToString("HH:mm dd/MM/yyyy") + (h.RelatedInvoiceId.HasValue ? $"   ·   HĐ #{h.RelatedInvoiceId}" : "");
        TextRenderer.DrawText(g, sub, RowSubFont, new Rectangle(r.X + left, r.Y + 35, textW, 20), RowMuted, TfLine);

        DrawRowSeparator(g, r, left);
    }

    private static void DrawOrderRow(Graphics g, Rectangle r, CustomerOrderSummaryDto o)
    {
        var circle = new Rectangle(r.X + 20, r.Y + (r.Height - 40) / 2, 40, 40);
        using (var b = new SolidBrush(AppColors.SecondaryContainer)) g.FillEllipse(b, circle);
        TextRenderer.DrawText(g, "🧾", RowEmojiFont, circle, AppColors.Secondary, TfCenter);

        const int left = 76;
        int right = r.Right - 20;
        string amount = $"{o.TotalAmount:N0}đ";
        int amountW = TextRenderer.MeasureText(g, amount, RowAmountFont, Size.Empty, TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, amount, RowAmountFont, new Rectangle(right - amountW, r.Y, amountW, r.Height), AppColors.Primary, TfLine);

        int textW = right - amountW - 16 - (r.X + left);
        TextRenderer.DrawText(g, $"Đơn #{o.Id}  ·  {o.TableName}", RowTitleFont, new Rectangle(r.X + left, r.Y + 12, textW, 22), AppColors.OnSurface, TfLine);

        // dòng phụ: thời gian + chấm màu trạng thái
        var date = o.OrderTime.ToString("HH:mm dd/MM/yyyy");
        int dateW = TextRenderer.MeasureText(g, date, RowSubFont, Size.Empty, TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, date, RowSubFont, new Rectangle(r.X + left, r.Y + 35, dateW + 4, 20), RowMuted, TfLine);

        int statusIndex = Math.Clamp(o.Status, 0, OrderStatusLabels.Length - 1);
        var statusColor = statusIndex switch
        {
            0 => Color.FromArgb(214, 138, 0),
            1 => Color.FromArgb(0, 104, 92),
            _ => AppColors.Error,
        };
        int dotX = r.X + left + dateW + 16;
        using (var b = new SolidBrush(statusColor)) g.FillEllipse(b, dotX, r.Y + 41, 8, 8);
        TextRenderer.DrawText(g, OrderStatusLabels[statusIndex], RowSubFont,
            new Rectangle(dotX + 14, r.Y + 35, Math.Max(40, right - amountW - 16 - (dotX + 14)), 20), statusColor, TfLine);

        DrawRowSeparator(g, r, left);
    }

    private sealed class DetailListBox : ListBox
    {
        public DetailListBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }
    }

    // Huy hiệu bo tròn (dùng cho hạng thành viên trong dialog chi tiết)
    private sealed class PillLabel : Control
    {
        private readonly Color _fg;
        private readonly Color _bg;
        private readonly Font _font = new("Segoe UI", 8.5F, FontStyle.Bold);

        public PillLabel(string text, Color fg, Color bg)
        {
            _fg = fg;
            _bg = bg;
            Text = text;
            DoubleBuffered = true;
            var size = TextRenderer.MeasureText(text, _font, Size.Empty, TextFormatFlags.NoPadding);
            Size = new Size(size.Width + 26, 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.White);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int h = Height - 1;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, h, h, 90, 180);
            path.AddArc(Width - 1 - h, 0, h, h, 270, 180);
            path.CloseFigure();
            using (var brush = new SolidBrush(_bg)) g.FillPath(brush, path);

            TextRenderer.DrawText(g, Text, _font, new Rectangle(0, 0, Width, Height), _fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _font.Dispose();
            base.Dispose(disposing);
        }
    }
}