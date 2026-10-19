using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Khach hang & Khach hang than thiet - danh cho Role QuanLy,
// ThuNgan (xem SidebarControl). Gom: danh sach/tim kiem, Them/Sua/Xoa, va
// dialog chi tiet xem lich su tich/doi diem + don hang gan day + cong/tru diem
// thu cong (vd tang diem sinh nhat).
public partial class CustomerManagementControl : UserControl
{
    private readonly ApiService _api = new();

    private List<CustomerDto> _customers = new();
    private CustomerDto? _selected;
    private DataGridViewTextBoxColumn colWebAccount = null!;
    private Button btnToggleWeb = null!;
    private static readonly string[] MemberLevelLabels = { "Thành viên", "Bạc", "Vàng", "Kim cương" };
    private static readonly string[] LoyaltyTypeLabels = { "Tích điểm", "Đổi điểm" };
    private static readonly string[] OrderStatusLabels = { "Đang mở", "Đã thanh toán", "Đã hủy" };

    public CustomerManagementControl()
    {
        InitializeComponent();
        // Cột trạng thái tài khoản web (thêm bằng code, không đụng Designer)
        colWebAccount = new DataGridViewTextBoxColumn { Name = "colWebAccount", HeaderText = "Tài khoản web" };
        gridCustomers.Columns.Add(colWebAccount);

        // Nút khóa/mở tài khoản web, đặt cạnh nút Xóa
        btnToggleWeb = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = AppColors.Secondary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(459, 682),
            Size = new Size(190, 38),
            Text = "Khóa/Mở TK web",
            Enabled = false
        };
        btnToggleWeb.FlatAppearance.BorderSize = 0;
        Controls.Add(btnToggleWeb);
        btnToggleWeb.Click += async (s, e) => await ToggleWebAccountAsync();
        Load += async (s, e) => await LoadCustomersAsync();
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        btnAddNew.Click += (s, e) => OpenAddOrEditDialog(null);

        gridCustomers.SelectionChanged += (s, e) => SyncSelectionFromGrid();
        gridCustomers.CellDoubleClick += async (s, e) => { if (_selected is not null) await OpenDetailDialogAsync(_selected.Id); };

        btnViewDetail.Click += async (s, e) => { if (_selected is not null) await OpenDetailDialogAsync(_selected.Id); else MessageBox.Show("Vui lòng chọn 1 khách hàng."); };
        btnEdit.Click += (s, e) => { if (_selected is not null) OpenAddOrEditDialog(_selected); else MessageBox.Show("Vui lòng chọn 1 khách hàng."); };
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
    }

    // =====================================================================
    // TẢI DỮ LIỆU & DANH SÁCH
    // =====================================================================

    private async Task LoadCustomersAsync()
    {
        try
        {
            _customers = await _api.GetCustomersAsync();
            ApplyFilter();
            RenderStatCards();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách khách hàng: {ex.Message}");
        }
    }

    private void RenderStatCards()
    {
        var total = _customers.Count;
        var vip = _customers.Count(c => c.MemberLevel > 0);
        cardTotalCustomers.Configure("👥", AppColors.Primary, AppColors.ErrorContainer, "TỔNG KHÁCH HÀNG", total.ToString());
        cardVipCustomers.Configure("⭐", AppColors.Warning, Color.FromArgb(255, 243, 224), "KHÁCH THÂN THIẾT", vip.ToString());
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

        using var dialog = new Form
        {
            Text = $"Chi tiết khách hàng - {detail.FullName}",
            Width = 520,
            Height = 620,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var levelIndex = Math.Clamp(detail.MemberLevel, 0, MemberLevelLabels.Length - 1);

        var lblInfo = new Label
        {
            Text = $"{detail.FullName}   ·   {detail.Phone}" + (string.IsNullOrEmpty(detail.Email) ? "" : $"\n{detail.Email}"),
            Font = AppFonts.HeadlineSm,
            Location = new Point(20, 16),
            AutoSize = true,
        };

        var lblPoint = new Label
        {
            Text = $"Điểm hiện có: {detail.LoyaltyPoint:N0}   ·   Hạng: {MemberLevelLabels[levelIndex]}",
            Font = AppFonts.BodyLg,
            ForeColor = AppColors.Primary,
            Location = new Point(20, 56),
            AutoSize = true,
        };

        // Cộng/trừ điểm thủ công
        var lblAdjustCap = new Label { Text = "Cộng/trừ điểm thủ công:", Font = AppFonts.BodyMd, Location = new Point(20, 90), AutoSize = true };
        var numAdjust = new NumericUpDown { Location = new Point(200, 86), Width = 90, Minimum = 1, Maximum = 100000, Value = 10 };
        var btnAddPoint = new Button { Text = "+ Cộng", Location = new Point(298, 84), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Success, ForeColor = Color.White };
        var btnSubPoint = new Button { Text = "- Trừ", Location = new Point(384, 84), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Error, ForeColor = Color.White };

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

        var tabs = new TabControl { Location = new Point(20, 128), Size = new Size(460, 400) };
        var tabHistory = new TabPage("Lịch sử điểm");
        var tabOrders = new TabPage("Đơn hàng gần đây");
        tabs.TabPages.Add(tabHistory);
        tabs.TabPages.Add(tabOrders);

        var flowHistory = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        if (detail.LoyaltyHistory.Count == 0)
        {
            flowHistory.Controls.Add(new Label { Text = "Chưa có lịch sử tích/đổi điểm.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(8) });
        }
        else
        {
            foreach (var h in detail.LoyaltyHistory) flowHistory.Controls.Add(BuildHistoryRow(h));
        }
        tabHistory.Controls.Add(flowHistory);

        var flowOrders = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        if (detail.RecentOrders.Count == 0)
        {
            flowOrders.Controls.Add(new Label { Text = "Chưa có đơn hàng nào.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(8) });
        }
        else
        {
            foreach (var o in detail.RecentOrders) flowOrders.Controls.Add(BuildOrderRow(o));
        }
        tabOrders.Controls.Add(flowOrders);

        var btnClose = new Button { Text = "Đóng", Location = new Point(200, 540), Size = new Size(120, 32), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White };

        dialog.Controls.Add(lblInfo);
        dialog.Controls.Add(lblPoint);
        dialog.Controls.Add(lblAdjustCap);
        dialog.Controls.Add(numAdjust);
        dialog.Controls.Add(btnAddPoint);
        dialog.Controls.Add(btnSubPoint);
        dialog.Controls.Add(tabs);
        dialog.Controls.Add(btnClose);

        var result = dialog.ShowDialog();

        // Nếu vừa cộng/trừ điểm thành công (DialogResult.Retry) -> làm mới danh
        // sách ngoài lưới + mở lại dialog chi tiết để thấy số dư điểm mới ngay.
        if (result == DialogResult.Retry)
        {
            await LoadCustomersAsync();
            await OpenDetailDialogAsync(customerId);
        }
    }

    private Panel BuildHistoryRow(LoyaltyHistoryDto h)
    {
        var isEarn = h.Points >= 0;
        var row = new Panel { Size = new Size(420, 48), Margin = new Padding(0, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var typeIndex = Math.Clamp(h.Type, 0, LoyaltyTypeLabels.Length - 1);
        var lblType = new Label { Text = LoyaltyTypeLabels[typeIndex], Font = AppFonts.LabelLg, Location = new Point(12, 6), AutoSize = true };
        var lblDate = new Label { Text = h.CreatedAt.ToString("HH:mm dd/MM/yyyy"), Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(12, 26), AutoSize = true };
        var lblInvoice = new Label
        {
            Text = h.RelatedInvoiceId.HasValue ? $"HĐ #{h.RelatedInvoiceId}" : "",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(180, 26),
            AutoSize = true,
        };
        var lblPoints = new Label
        {
            Text = (isEarn ? "+" : "") + h.Points.ToString("N0"),
            Font = AppFonts.HeadlineSm,
            ForeColor = isEarn ? AppColors.Success : AppColors.Error,
            Location = new Point(330, 12),
            Size = new Size(80, 24),
            TextAlign = ContentAlignment.MiddleRight,
        };

        row.Controls.Add(lblType);
        row.Controls.Add(lblDate);
        row.Controls.Add(lblInvoice);
        row.Controls.Add(lblPoints);
        return row;
    }

    private Panel BuildOrderRow(CustomerOrderSummaryDto o)
    {
        var row = new Panel { Size = new Size(420, 48), Margin = new Padding(0, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var statusIndex = Math.Clamp(o.Status, 0, OrderStatusLabels.Length - 1);
        var lblTable = new Label { Text = $"Đơn #{o.Id} · {o.TableName}", Font = AppFonts.LabelLg, Location = new Point(12, 6), AutoSize = true };
        var lblDate = new Label { Text = $"{o.OrderTime:HH:mm dd/MM/yyyy} · {OrderStatusLabels[statusIndex]}", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(12, 26), AutoSize = true };
        var lblAmount = new Label { Text = $"{o.TotalAmount:N0}đ", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(320, 12), Size = new Size(90, 24), TextAlign = ContentAlignment.MiddleRight };

        row.Controls.Add(lblTable);
        row.Controls.Add(lblDate);
        row.Controls.Add(lblAmount);
        return row;
    }
}