using System.Drawing.Drawing2D;
using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QuanAnWinForm.Forms;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Nhan vien - chi danh cho Role QuanLy.
// Gom 4 tab: Nhan vien | Ca lam | Nhiem vu | Lich su ca lam.
//
// Ban cap nhat nay chinh lai giao dien theo gop y:
//  - Tab Nhan vien: the nhan vien thiet ke lai gon/ro rang hon (avatar tron +
//    mau theo vai tro thay vi khoi vuong xam + pill do giong het nhau).
//  - Tab Ca lam: gop lam 1 danh sach DUY NHAT gom TAT CA nhan vien cho ngay
//    dang chon (ai da co ca / chua co ca deu nam chung 1 cho, phan cong nhanh
//    ngay tren dong thay vi phai mo popup rieng). MOI nhan vien co the co
//    NHIEU ca trong cung 1 ngay, moi ca hien 1 dong rieng. Khi phan cong 1 ca,
//    Quan ly co the gan luon "Khu vuc ban phu trach" cho DUNG ca do - vi khu
//    vuc duoc luu chung 1 ban ghi ShiftAssignment voi ca lam (EmployeeId +
//    ShiftId + WorkDate), viec gan/doi khu vuc CHI THUC HIEN DUOC tren 1 ca
//    da duoc phan cong that su -> khong bao gio xay ra tinh huong gan nham
//    khu vuc cho 1 khung gio ma nhan vien khong lam (vd nhan vien chi co ca
//    sang hom do se khong the gan khu vuc cho "buoi chieu" vi khong co ban
//    ghi ca chieu nao de gan vao). Xem OpenAssignDialog/OpenChangeAreaDialog.
//  - Tab Nhiem vu: them lua chon "mau nhiem vu" + "khu vuc phu trach" khi giao
//    viec cho nhan vien Phuc vu, dap ung nghiep vu "phan nhan vien phuc vu
//    theo khu vuc ban". Khong doi schema Backend (Title/Description van la
//    text thuong), chi ho tro dien nhanh o phia WinForm.
//  - Tab Lich su ca lam (MOI): xem lai cac ca da/se lam theo khoang ngay (mac
//    dinh 7 ngay gan nhat, co nut nhanh 7/30 ngay), loc theo 1 nhan vien hoac
//    tat ca, trinh bay dang timeline nhom theo ngay (ngay gan nhat len dau)
//    kem khu vuc phu trach va trang thai - phong cach lich su giao dich/
//    newsfeed cua cac app hien dai thay vi 1 bang DataGridView day dac.
public partial class StaffControl : UserControl
{
    private readonly ApiService _api = new();

    private List<EmployeeDto> _employees = new();
    private List<RoleDto> _roles = new();

    // Lấy danh sách vai trò từ DB 1 lần rồi dùng lại (thay cho RoleId hardcode)
    private async Task<List<RoleDto>> EnsureRolesAsync()
    {
        if (_roles.Count == 0) _roles = await _api.GetRolesAsync();
        return _roles;
    }
    private List<ShiftDto> _shifts = new();
    private List<ShiftAssignmentDto> _assignments = new();
    private List<StaffTaskDto> _tasks = new();
    private List<AreaDto> _areas = new();
    private List<ShiftAssignmentDto> _history = new();

    // Tranh goi LoadHistoryAsync qua nhieu lan thua khi dang set gia tri mac dinh
    // ban dau cho ca 2 DateTimePicker + ComboBox cung luc (moi lan set deu kich
    // hoat event rieng cua no).
    private bool _suppressHistoryReload;

    public StaffControl()
    {
        InitializeComponent();

        // ----- Tab "Ca làm": bo Anchor cua Designer (neo theo 1200x760), tu can lai theo cua so that;
        // dong thoi tat han thanh cuon NGANG cua danh sach nhan vien theo ngay.
        foreach (Control c in new Control[] { flowAssignments, flowShiftDefs, dividerShifts, btnAddAssignment })
            c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        DisableHorizontalScroll(flowAssignments);
        DisableHorizontalScroll(flowShiftDefs);
        tabShifts.Resize += (s, e) => FitShiftsTab();
        tabsMain.SelectedIndexChanged += (s, e) => FitShiftsTab();
        Load += (s, e) => FitShiftsTab();

        Load += async (s, e) => await LoadEverythingAsync();
        btnAddNew.Click += (s, e) => OpenAddEmployeeDialog();

        btnAddShift.Click += (s, e) => OpenAddOrEditShiftDialog(null);
        dtpAssignDate.ValueChanged += async (s, e) => await LoadAssignmentsAsync();
        btnAddAssignment.Click += (s, e) => OpenAssignDialog();

        btnAddTask.Click += (s, e) => OpenAddOrEditTaskDialog(null);
        cbTaskFilter.SelectedIndexChanged += async (s, e) => await LoadTasksAsync();

        cbHistoryEmployee.SelectedIndexChanged += async (s, e) => await LoadHistoryAsync();
        dtpHistoryFrom.ValueChanged += async (s, e) => await LoadHistoryAsync();
        dtpHistoryTo.ValueChanged += async (s, e) => await LoadHistoryAsync();
        btnHistoryQuick7.Click += async (s, e) => await SetHistoryRangeAsync(6);
        btnHistoryQuick30.Click += async (s, e) => await SetHistoryRangeAsync(29);
    }

    // Tat thanh cuon ngang: noi dung luon duoc co gian vua chieu rong khung, chi con cuon doc.
    private static void DisableHorizontalScroll(FlowLayoutPanel flow)
    {
        flow.AutoScroll = false;
        flow.HorizontalScroll.Enabled = false;
        flow.HorizontalScroll.Visible = false;
        flow.HorizontalScroll.Maximum = 0;
        flow.AutoScroll = true;
    }

    // Can lai bo cuc tab Ca lam theo kich thuoc that cua tab.
    private void FitShiftsTab()
    {
        var cw = tabShifts.ClientSize.Width;
        var ch = tabShifts.ClientSize.Height;
        if (cw <= 0 || ch <= 0) return;

        const int left = 336;        // cot phai bat dau (cot trai = dinh nghia ca + duong ke)
        const int rightMargin = 16;
        const int top = 82;          // ngang hang voi danh sach ca ben trai
        const int bottomMargin = 16;
        var listHeight = Math.Max(100, ch - top - bottomMargin);

        // Hang "Ngay: [dd/MM/yyyy]   ..........   [+ Phan cong]" nam duoi tieu de, khong de len nhau.
        lblAssignDate.Location = new Point(left, 42);
        dtpAssignDate.Location = new Point(lblAssignDate.Right + 8, 38);
        btnAddAssignment.Location = new Point(cw - rightMargin - btnAddAssignment.Width, 36);

        flowAssignments.SetBounds(left, top, Math.Max(400, cw - left - rightMargin), listHeight);
        flowShiftDefs.Height = listHeight;
        dividerShifts.Height = ch;

        FitRosterRows();
    }

    // Moi dong nhan vien gian vua chieu rong khung (cac nut ben phai da neo Right nen tu dich theo).
    private void FitRosterRows()
    {
        var w = Math.Max(700, flowAssignments.ClientSize.Width - 4);
        foreach (var p in flowAssignments.Controls.OfType<Panel>())
            if (p.Width != w) p.Width = w;
    }

    private async Task LoadEverythingAsync()
    {
        // Nhan vien can duoc load truoc vi Ca lam/Nhiem vu deu can danh sach nay
        // de lam ComboBox chon nguoi (dung Users.UserId, xem EmployeeDto.UserId).
        await LoadStaffAsync();
        RenderTaskFilterOptions();
        await LoadAreasAsync();
        await LoadShiftsAsync();
        await LoadAssignmentsAsync();
        await LoadTasksAsync();

        _suppressHistoryReload = true;
        RenderHistoryFilterOptions();
        _suppressHistoryReload = false;
        await SetHistoryRangeAsync(6); // mac dinh mo tab Lich su la xem 7 ngay gan nhat
    }

    // Chi 4 Role thuc su ton tai trong bang Roles: QuanLy, ThuNgan, PhucVu, Bep.
    private static string DisplayRoleName(string role) => role switch
    {
        "QuanLy" => "Quản lý",
        "ThuNgan" => "Thu ngân",
        "PhucVu" => "Phục vụ",
        "Bep" => "Bếp",
        _ => role,
    };

    // Mau rieng cho tung vai tro de cac the/nhan de phan biet nhanh bang mat,
    // thay vi dung chung 1 mau do cho tat ca (nguyen nhan chinh khien man hinh
    // Nhan vien truoc day nhin "roi").
    private static (Color Bg, Color Fg) RoleBadgeColors(string role) => role switch
    {
        "QuanLy" => (AppColors.ErrorContainer, AppColors.Primary),
        "ThuNgan" => (Color.FromArgb(230, 245, 240), AppColors.Tertiary),
        "PhucVu" => (AppColors.SecondaryContainer, AppColors.Secondary),
        "Bep" => (Color.FromArgb(255, 243, 224), AppColors.Warning),
        _ => (AppColors.SurfaceContainer, AppColors.OnSurfaceVariant),
    };

    // =====================================================================
    // TAB 1: NHÂN VIÊN
    // =====================================================================

    private async Task LoadStaffAsync()
    {
        try
        {
            _employees = await _api.GetEmployeesAsync();
            RenderStaffCards();
            RenderStatCards();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách nhân viên: {ex.Message}");
        }
    }

    private void RenderStatCards()
    {
        var total = _employees.Count;
        var working = _employees.Count(e => e.IsActive);
        cardTotalStaff.Configure("👥", AppColors.Primary, AppColors.ErrorContainer, "TỔNG NHÂN SỰ", total.ToString());
        cardWorking.Configure("💼", AppColors.Tertiary, Color.FromArgb(230, 245, 240), "ĐANG LÀM VIỆC", working.ToString());
    }

    private void RenderStaffCards()
    {
        flowStaffCards.Controls.Clear();
        foreach (var staff in _employees)
        {
            flowStaffCards.Controls.Add(BuildStaffCard(staff));
        }
        flowStaffCards.Controls.Add(BuildAddNewTile());
    }

    // Kich thuoc the nhan vien dung chung cho BuildStaffCard + BuildAddNewTile.
    private const int StaffCardWidth = 244;
    private const int StaffCardHeight = 228;
    private const int StaffCardPad = 14;

    // Avatar tron + chu cai dau, mau theo vai tro - thay cho khoi vuong xam
    // truoc day khong the hien duoc thong tin gi.
    private static Label BuildAvatarLabel(string initials, Color bg)
    {
        var lbl = new Label
        {
            Text = initials,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = bg,
            Size = new Size(52, 52),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, lbl.Width, lbl.Height);
        lbl.Region = new Region(path);
        return lbl;
    }

    private static string InitialsOf(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
    }

    private Panel BuildStaffCard(EmployeeDto s)
    {
        var card = new Panel { Size = new Size(StaffCardWidth, StaffCardHeight), BackColor = Color.White, Margin = new Padding(8) };
        var (badgeBg, badgeFg) = RoleBadgeColors(s.RoleName);
        var isSelf = s.UserId == SessionContext.UserId;

        var avatar = BuildAvatarLabel(InitialsOf(s.FullName), badgeFg);
        avatar.Location = new Point(StaffCardPad, StaffCardPad);
        card.Controls.Add(avatar);

        var lblName = new Label
        {
            Text = isSelf ? s.FullName + " (Bạn)" : s.FullName,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.OnSurface,
            Location = new Point(StaffCardPad + 62, StaffCardPad),
            Size = new Size(StaffCardWidth - StaffCardPad * 2 - 62, 20),
            AutoEllipsis = true,
        };
        card.Controls.Add(lblName);

        var badge = new Label
        {
            Text = DisplayRoleName(s.RoleName),
            Font = AppFonts.LabelMd,
            ForeColor = badgeFg,
            BackColor = badgeBg,
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3),
            Location = new Point(StaffCardPad + 62, StaffCardPad + 24),
        };
        card.Controls.Add(badge);

        var divider = new Panel { Size = new Size(StaffCardWidth - StaffCardPad * 2, 1), Location = new Point(StaffCardPad, 76), BackColor = AppColors.OutlineVariant };
        card.Controls.Add(divider);

        var lblPhone = new Label { Text = "☎  " + (string.IsNullOrEmpty(s.Phone) ? "Chưa cập nhật" : s.Phone), Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(StaffCardPad, 88), AutoSize = true };
        var lblEmail = new Label
        {
            Text = "✉  " + (string.IsNullOrEmpty(s.Email) ? "Chưa cập nhật" : s.Email),
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(StaffCardPad, 110),
            Size = new Size(StaffCardWidth - StaffCardPad * 2, 18),
            AutoEllipsis = true,
        };
        var lblCode = new Label { Text = "🪪  " + s.EmployeeCode, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(StaffCardPad, 132), AutoSize = true };
        card.Controls.Add(lblPhone);
        card.Controls.Add(lblEmail);
        card.Controls.Add(lblCode);

        // Hàng thao tác: Sửa | Đặt lại mật khẩu
        var lblEdit = new Label { Text = "Sửa", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, AutoSize = true, Cursor = Cursors.Hand, Location = new Point(StaffCardPad, 160) };
        lblEdit.Click += async (sender, e) => await OpenEditEmployeeDialogAsync(s);
        var editWidth = TextRenderer.MeasureText(lblEdit.Text, AppFonts.LabelMd).Width;
        var lblReset = new Label { Text = "Đặt lại mật khẩu", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, AutoSize = true, Cursor = Cursors.Hand, Location = new Point(StaffCardPad + editWidth + 18, 160) };
        lblReset.Click += (sender, e) => OpenResetPasswordDialog(s);
        card.Controls.Add(lblEdit);
        card.Controls.Add(lblReset);

        var dot = new Panel { Size = new Size(8, 8), Location = new Point(StaffCardPad, StaffCardHeight - 30), BackColor = s.IsActive ? AppColors.Success : AppColors.Outline };
        var lblStatus = new Label { Text = s.IsActive ? "Đang làm việc" : "Đã khóa", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(StaffCardPad + 14, StaffCardHeight - 34), AutoSize = true };
        card.Controls.Add(dot);
        card.Controls.Add(lblStatus);

        // Không cho tự khóa chính mình (Backend cũng chặn)
        if (!isSelf)
        {
            var toggleText = s.IsActive ? "Tạm khóa" : "Kích hoạt";
            var toggleWidth = TextRenderer.MeasureText(toggleText, AppFonts.LabelMd).Width;
            var lblToggle = new Label
            {
                Text = toggleText,
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.Primary,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Location = new Point(StaffCardWidth - StaffCardPad - toggleWidth, StaffCardHeight - 34),
            };
            lblToggle.Click += async (sender, e) => await ToggleActiveAsync(s);
            card.Controls.Add(lblToggle);
        }

        card.Paint += (sender, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, card.Width - 1, card.Height - 1);

        return card;
    }

    // Khoa/mo lai tai khoan: khop PUT /api/users/{id}/active ben Backend
    // (id o day la EmployeeId, dung nhu EmployeeDto.Id tra ve tu GET /api/users).
    private async Task ToggleActiveAsync(EmployeeDto staff)
    {
        try
        {
            await _api.SetEmployeeActiveAsync(staff.Id, !staff.IsActive);
            await LoadStaffAsync();
            RenderTaskFilterOptions();
            RenderAssignments(); // nhan vien co the vua bi khoa -> cap nhat lai danh sach Ca lam
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
        }
    }

    private Panel BuildAddNewTile()
    {
        var tile = new Panel { Size = new Size(StaffCardWidth, StaffCardHeight), BackColor = Color.FromArgb(253, 245, 242), Margin = new Padding(8), Cursor = Cursors.Hand };
        var lblPlus = new Label { Text = "+", Font = new Font("Segoe UI", 22), ForeColor = AppColors.Primary, AutoSize = true };
        var lblText = new Label { Text = "Thêm nhân sự mới", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, AutoSize = true };
        tile.Controls.Add(lblPlus);
        tile.Controls.Add(lblText);
        lblPlus.Location = new Point((StaffCardWidth - lblPlus.Width) / 2, 68);
        lblText.Location = new Point((StaffCardWidth - lblText.Width) / 2, 112);
        tile.Click += (s, e) => OpenAddEmployeeDialog();
        return tile;
    }

    // ---------- Thêm nhân viên (danh sách vai trò lấy từ DB) ----------
    private void OpenAddEmployeeDialog() => _ = OpenAddEmployeeDialogAsync();

    private async Task OpenAddEmployeeDialogAsync()
    {
        List<RoleDto> roles;
        try { roles = await EnsureRolesAsync(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách vai trò: {ex.Message}");
            return;
        }
        if (roles.Count == 0)
        {
            MessageBox.Show("Hệ thống chưa có vai trò nào trong bảng Roles.");
            return;
        }

        using var dialog = new Form { Text = "Thêm nhân viên mới", Width = 380, Height = 540, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

        var txtFullName = MakeLabeledInput(dialog, "Họ tên *", 20);
        var txtUsername = MakeLabeledInput(dialog, "Tên đăng nhập *", 80);
        var txtPassword = MakeLabeledInput(dialog, "Mật khẩu * (tối thiểu 6 ký tự)", 140);
        txtPassword.PasswordChar = '•';
        var txtPhone = MakeLabeledInput(dialog, "Số điện thoại", 200);
        var txtEmail = MakeLabeledInput(dialog, "Email", 260);
        var txtEmployeeCode = MakeLabeledInput(dialog, "Mã nhân viên *", 320);

        dialog.Controls.Add(new Label { Text = "Vai trò *", Font = AppFonts.BodyMd, Location = new Point(20, 380), AutoSize = true });
        var cbRole = new ComboBox { Location = new Point(20, 400), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        cbRole.Items.AddRange(roles.Cast<object>().ToArray());
        var defaultIdx = roles.FindIndex(r => r.RoleName == "PhucVu");
        cbRole.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
        dialog.Controls.Add(cbRole);

        var btnSave = new Button { Text = "Lưu", Location = new Point(140, 450), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(txtFullName.Text) || string.IsNullOrWhiteSpace(txtUsername.Text) ||
            string.IsNullOrWhiteSpace(txtPassword.Text) || string.IsNullOrWhiteSpace(txtEmployeeCode.Text))
        {
            MessageBox.Show("Vui lòng nhập đủ các trường có dấu *");
            return;
        }
        if (txtPassword.Text.Length < 6)
        {
            MessageBox.Show("Mật khẩu phải có ít nhất 6 ký tự");
            return;
        }

        var roleId = ((RoleDto)cbRole.SelectedItem!).RoleId;
        await SaveNewEmployeeAsync(txtFullName.Text.Trim(), txtUsername.Text.Trim(), txtPassword.Text,
            txtPhone.Text.Trim(), txtEmail.Text.Trim(), txtEmployeeCode.Text.Trim(), roleId);
    }

    private async Task SaveNewEmployeeAsync(string fullName, string username, string password, string phone, string email, string employeeCode, int roleId)
    {
        try
        {
            await _api.CreateEmployeeAsync(fullName, username, password,
                string.IsNullOrEmpty(phone) ? null : phone,
                string.IsNullOrEmpty(email) ? null : email,
                employeeCode, roleId);
            await LoadStaffAsync();
            RenderTaskFilterOptions();
            RenderAssignments();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thêm được nhân viên: {ex.Message}");
        }
    }

    // ---------- Sửa nhân viên (thông tin + đổi vai trò) ----------
    private async Task OpenEditEmployeeDialogAsync(EmployeeDto staff)
    {
        List<RoleDto> roles;
        try { roles = await EnsureRolesAsync(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách vai trò: {ex.Message}");
            return;
        }

        var isSelf = staff.UserId == SessionContext.UserId;

        using var dialog = new Form { Text = $"Sửa nhân viên - {staff.EmployeeCode}", Width = 380, Height = 420, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

        dialog.Controls.Add(new Label { Text = $"Tên đăng nhập: {staff.Username} (không thể đổi)", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(20, 16), AutoSize = true });

        var txtFullName = MakeLabeledInput(dialog, "Họ tên *", 48);
        txtFullName.Text = staff.FullName;
        var txtPhone = MakeLabeledInput(dialog, "Số điện thoại", 108);
        txtPhone.Text = staff.Phone ?? "";
        var txtEmail = MakeLabeledInput(dialog, "Email", 168);
        txtEmail.Text = staff.Email ?? "";

        dialog.Controls.Add(new Label { Text = isSelf ? "Vai trò (không thể tự đổi vai trò của mình)" : "Vai trò *", Font = AppFonts.BodyMd, Location = new Point(20, 228), AutoSize = true });
        var cbRole = new ComboBox { Location = new Point(20, 248), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = !isSelf };
        cbRole.Items.AddRange(roles.Cast<object>().ToArray());
        var curIdx = roles.FindIndex(r => r.RoleId == staff.RoleId);
        cbRole.SelectedIndex = curIdx >= 0 ? curIdx : 0;
        dialog.Controls.Add(cbRole);

        var btnSave = new Button { Text = "Lưu", Location = new Point(140, 310), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(txtFullName.Text))
        {
            MessageBox.Show("Họ tên không được để trống");
            return;
        }

        try
        {
            var phone = txtPhone.Text.Trim();
            var email = txtEmail.Text.Trim();
            await _api.UpdateEmployeeAsync(staff.Id, txtFullName.Text.Trim(),
                string.IsNullOrEmpty(phone) ? null : phone,
                string.IsNullOrEmpty(email) ? null : email,
                ((RoleDto)cbRole.SelectedItem!).RoleId);
            await LoadStaffAsync();
            RenderTaskFilterOptions();
            RenderAssignments();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được nhân viên: {ex.Message}");
        }
    }

    // ---------- Quản lý đặt lại mật khẩu cho nhân viên ----------
    private void OpenResetPasswordDialog(EmployeeDto staff)
    {
        using var dlg = new ChangePasswordDialog(
            $"Đặt lại mật khẩu - {staff.FullName}",
            requireOldPassword: false,
            (_, newPw) => _api.ResetEmployeePasswordAsync(staff.Id, newPw));

        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            MessageBox.Show($"Đã đặt lại mật khẩu cho {staff.FullName}.");
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
    // TAB 2: CA LÀM & PHÂN CÔNG
    // =====================================================================

    private async Task LoadShiftsAsync()
    {
        try
        {
            _shifts = await _api.GetShiftsAsync();
            RenderShiftDefs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách ca làm: {ex.Message}");
        }
    }

    private void RenderShiftDefs()
    {
        flowShiftDefs.Controls.Clear();
        foreach (var shift in _shifts)
            flowShiftDefs.Controls.Add(BuildShiftDefRow(shift));
    }

    private Panel BuildShiftDefRow(ShiftDto shift)
    {
        var row = new Panel { Size = new Size(280, 76), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var lblName = new Label { Text = shift.Name, Font = AppFonts.LabelLg, Location = new Point(12, 10), AutoSize = true };
        var lblTime = new Label
        {
            Text = $"{shift.StartTime:hh\\:mm} - {shift.EndTime:hh\\:mm}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(12, 32),
            AutoSize = true,
        };
        var lblEdit = new Label { Text = "Sửa", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(190, 50), AutoSize = true, Cursor = Cursors.Hand };
        var lblDelete = new Label { Text = "Xóa", Font = AppFonts.LabelMd, ForeColor = AppColors.Error, Location = new Point(240, 50), AutoSize = true, Cursor = Cursors.Hand };

        lblEdit.Click += (s, e) => OpenAddOrEditShiftDialog(shift);
        lblDelete.Click += async (s, e) => await DeleteShiftAsync(shift);

        row.Controls.Add(lblName);
        row.Controls.Add(lblTime);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);
        return row;
    }

    private void OpenAddOrEditShiftDialog(ShiftDto? existing)
    {
        using var dialog = new Form
        {
            Text = existing is null ? "Thêm ca làm" : "Sửa ca làm",
            Width = 340,
            Height = 260,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var txtName = MakeLabeledInput(dialog, "Tên ca *", 20);
        txtName.Text = existing?.Name ?? "";

        var lblStart = new Label { Text = "Giờ bắt đầu", Font = AppFonts.BodyMd, Location = new Point(20, 80), AutoSize = true };
        var dtpStart = new DateTimePicker { Location = new Point(20, 100), Width = 130, Format = DateTimePickerFormat.Time, ShowUpDown = true };
        var lblEnd = new Label { Text = "Giờ kết thúc", Font = AppFonts.BodyMd, Location = new Point(170, 80), AutoSize = true };
        var dtpEnd = new DateTimePicker { Location = new Point(170, 100), Width = 130, Format = DateTimePickerFormat.Time, ShowUpDown = true };

        var today = DateTime.Today;
        dtpStart.Value = existing is null ? today.AddHours(8) : today.Add(existing.StartTime);
        dtpEnd.Value = existing is null ? today.AddHours(16) : today.Add(existing.EndTime);

        dialog.Controls.Add(lblStart);
        dialog.Controls.Add(dtpStart);
        dialog.Controls.Add(lblEnd);
        dialog.Controls.Add(dtpEnd);

        var btnSave = new Button { Text = "Lưu", Location = new Point(110, 150), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Vui lòng nhập tên ca");
            return;
        }

        _ = SaveShiftAsync(existing?.Id, txtName.Text.Trim(), dtpStart.Value.TimeOfDay, dtpEnd.Value.TimeOfDay);
    }

    private async Task SaveShiftAsync(int? id, string name, TimeSpan start, TimeSpan end)
    {
        try
        {
            if (id.HasValue) await _api.UpdateShiftAsync(id.Value, name, start, end);
            else await _api.CreateShiftAsync(name, start, end);
            await LoadShiftsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được ca làm: {ex.Message}");
        }
    }

    private async Task DeleteShiftAsync(ShiftDto shift)
    {
        if (MessageBox.Show($"Xóa ca \"{shift.Name}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            await _api.DeleteShiftAsync(shift.Id);
            await LoadShiftsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }

    private async Task LoadAssignmentsAsync()
    {
        try
        {
            _assignments = await _api.GetAssignmentsAsync(dtpAssignDate.Value.Date);
            RenderAssignments();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được phân công: {ex.Message}");
        }
    }

    // Khop dung enum AssignmentStatus ben Backend: 0=DaXepLich,1=DaDiemDanh,2=VangMat,3=DaHoanThanh
    private static readonly string[] AssignmentStatusLabels = { "Đã xếp lịch", "Đã điểm danh", "Vắng mặt", "Đã hoàn thành" };

    // Danh sach hop nhat: liet ke TAT CA nhan vien dang lam viec cho ngay dang
    // chon, kem theo (TAT CA) ca lam neu da duoc phan cong - 1 nhan vien co the
    // co NHIEU ca trong cung 1 ngay (vd ca sang + ca chieu), moi ca hien 1 dong
    // rieng kem khu vuc phu trach cua DUNG ca do (neu co).
    private void RenderAssignments()
    {
        flowAssignments.Controls.Clear();
        var activeStaff = _employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToList();

        if (activeStaff.Count == 0)
        {
            flowAssignments.Controls.Add(new Label { Text = "Chưa có nhân viên nào đang làm việc.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        foreach (var emp in activeStaff)
        {
            var empAssignments = _assignments.Where(a => a.UserId == emp.UserId).OrderBy(a => a.ShiftStart).ToList();
            flowAssignments.Controls.Add(BuildRosterRow(emp, empAssignments));
        }
        FitRosterRows();
    }

    // Chuoi hien thi gio vao/ra thuc te cua 1 ca (khop voi man hinh Diem danh tren mobile).
    private static string FormatActualTimes(ShiftAssignmentDto a)
    {
        if (a.Status == 2) return "Vắng mặt";
        if (a.CheckInTime is null) return "Chưa điểm danh";

        var text = $"Vào {a.CheckInTime:HH:mm}";
        text += a.CheckOutTime is null ? " · Đang làm" : $" · Ra {a.CheckOutTime:HH:mm}";
        return text;
    }

    private Panel BuildRosterRow(EmployeeDto emp, List<ShiftAssignmentDto> empAssignments)
    {
        // Chieu cao tang dan theo so ca trong ngay cua nhan vien nay: phan dau (ten +
        // vai tro) + 52px cho MOI ca lam (1 dong thong tin ca + 1 dong gio vao/ra thuc
        // te lay tu mobile). Chua co ca nao -> 1 dong "Chua xep ca" (68px).
        var rowHeight = empAssignments.Count == 0 ? 68 : 34 + empAssignments.Count * 52;
        var row = new Panel { Size = new Size(840, rowHeight), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);
        row.Resize += (s, e) => row.Invalidate(); // ve lai vien khi dong gian rong ra

        var (badgeBg, badgeFg) = RoleBadgeColors(emp.RoleName);
        var dot = new Panel { Size = new Size(8, 8), Location = new Point(14, 15), BackColor = badgeFg };
        var lblName = new Label { Text = emp.FullName, Font = AppFonts.LabelLg, Location = new Point(30, 4), AutoSize = true };
        var lblRole = new Label { Text = DisplayRoleName(emp.RoleName), Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(150, 6), AutoSize = true };

        row.Controls.Add(dot);
        row.Controls.Add(lblName);
        row.Controls.Add(lblRole);

        if (empAssignments.Count == 0)
        {
            var lblEmpty = new Label { Text = "Chưa xếp ca", Font = AppFonts.BodyMd, ForeColor = AppColors.Outline, Location = new Point(30, 34), AutoSize = true };
            row.Controls.Add(lblEmpty);

            var lblAssignNow = new Label { Text = "+ Phân công ngay", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(700, 34), AutoSize = true, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            lblAssignNow.Click += (s, e) => OpenAssignDialog(emp);
            row.Controls.Add(lblAssignNow);
            return row;
        }

        var y = 32;
        foreach (var assignment in empAssignments)
        {
            var lblShift = new Label
            {
                Text = $"🕒 {assignment.ShiftName} ({assignment.ShiftStart:hh\\:mm}-{assignment.ShiftEnd:hh\\:mm})",
                Font = AppFonts.LabelMd,
                ForeColor = AppColors.Primary,
                Location = new Point(30, y + 4),
                AutoSize = true,
            };
            row.Controls.Add(lblShift);

            // Gio vao/ra THUC TE (nhan vien diem danh tren mobile, hoac backend tu dong bo
            // khi quan ly doi trang thai) - de WinForm va mobile nhin cung 1 du lieu.
            var lblActual = new Label
            {
                Text = FormatActualTimes(assignment),
                Font = AppFonts.BodyMd,
                ForeColor = assignment.Status == 2 ? AppColors.Error : AppColors.OnSurfaceVariant,
                Location = new Point(48, y + 26),
                AutoSize = true,
            };
            row.Controls.Add(lblActual);

            // Khu vuc phu trach CUA DUNG ca nay (neu co) - bam vao de doi/gan khu
            // vuc khac. Chi hien duoc o day vi assignment (ban ghi ca lam that su
            // cua nhan vien hom nay) da chac chan ton tai -> khong the bao gio gan
            // nham khu vuc cho 1 khung gio nhan vien khong lam.
            var lblArea = new Label
            {
                Text = string.IsNullOrEmpty(assignment.AreaName) ? "📍 Chưa gán khu vực" : $"📍 {assignment.AreaName}",
                Font = AppFonts.BodyMd,
                ForeColor = string.IsNullOrEmpty(assignment.AreaName) ? AppColors.Outline : AppColors.Tertiary,
                Location = new Point(280, y + 6),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            lblArea.Click += (s, e) => OpenChangeAreaDialog(assignment);
            row.Controls.Add(lblArea);

            var cbStatus = new ComboBox { Location = new Point(480, y), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            cbStatus.Items.AddRange(AssignmentStatusLabels);
            cbStatus.SelectedIndex = Math.Clamp(assignment.Status, 0, AssignmentStatusLabels.Length - 1);
            cbStatus.SelectedIndexChanged += async (s, e) =>
            {
                if (cbStatus.SelectedIndex == assignment.Status) return;
                try
                {
                    await _api.UpdateAssignmentStatusAsync(assignment.Id, cbStatus.SelectedIndex);
                    assignment.Status = cbStatus.SelectedIndex;
                    // Backend vua dong bo lai CheckInTime/CheckOutTime theo trang thai moi
                    // -> tai lai de hien dung gio vao/ra (ca o tab Lich su ca lam).
                    await LoadAssignmentsAsync();
                    await LoadHistoryAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
                    cbStatus.SelectedIndex = assignment.Status;
                }
            };
            row.Controls.Add(cbStatus);

            var lblDelete = new Label { Text = "Xóa", Font = AppFonts.LabelMd, ForeColor = AppColors.Error, Location = new Point(650, y + 6), AutoSize = true, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            lblDelete.Click += async (s, e) => await DeleteAssignmentAsync(assignment);
            row.Controls.Add(lblDelete);

            y += 52;
        }

        var lblAddMore = new Label { Text = "+ Thêm ca khác", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(700, 34), AutoSize = true, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lblAddMore.Click += (s, e) => OpenAssignDialog(emp);
        row.Controls.Add(lblAddMore);

        return row;
    }

    // Dialog nho: chon lai khu vuc phu trach cho 1 ban ghi ca lam DA TON TAI.
    // Danh sach khu vuc luon la _areas (toan bo khu vuc nha hang) vi 1 nhan vien
    // van co the doi sang phu trach khu vuc khac trong CUNG ca dang lam - dieu
    // duoc "khoa" o day la KHONG THE thao tac neu khong co assignment nay (tuc
    // khong co ca lam thuc su), khong phai gioi han danh sach khu vuc duoc chon.
    private void OpenChangeAreaDialog(ShiftAssignmentDto assignment)
    {
        using var dialog = new Form
        {
            Text = $"Khu vực phụ trách - {assignment.UserName} ({assignment.ShiftName})",
            Width = 360,
            Height = 200,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblInfo = new Label
        {
            Text = $"Ca {assignment.ShiftName} ({assignment.ShiftStart:hh\\:mm}-{assignment.ShiftEnd:hh\\:mm}) ngày {assignment.WorkDate:dd/MM/yyyy}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(20, 16),
            AutoSize = true,
        };

        var lblArea = new Label { Text = "Khu vực phụ trách", Font = AppFonts.BodyMd, Location = new Point(20, 50), AutoSize = true };
        var cbArea = new ComboBox { Location = new Point(20, 70), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbArea.Items.Add("(Không chọn)");
        cbArea.Items.AddRange(_areas.Cast<object>().ToArray());
        var preselect = assignment.AreaId is int areaId ? _areas.FindIndex(a => a.Id == areaId) : -1;
        cbArea.SelectedIndex = preselect >= 0 ? preselect + 1 : 0;

        dialog.Controls.Add(lblInfo);
        dialog.Controls.Add(lblArea);
        dialog.Controls.Add(cbArea);

        var btnSave = new Button { Text = "Lưu", Location = new Point(110, 110), Size = new Size(120, 34), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        int? newAreaId = cbArea.SelectedIndex > 0 && cbArea.SelectedItem is AreaDto area ? area.Id : null;
        _ = SaveAssignmentAreaAsync(assignment, newAreaId);
    }

    private async Task SaveAssignmentAreaAsync(ShiftAssignmentDto assignment, int? areaId)
    {
        try
        {
            await _api.UpdateAssignmentAreaAsync(assignment.Id, areaId);
            await LoadAssignmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được khu vực: {ex.Message}");
        }
    }

    private async Task DeleteAssignmentAsync(ShiftAssignmentDto a)
    {
        if (MessageBox.Show($"Xóa phân công của \"{a.UserName}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            await _api.DeleteAssignmentAsync(a.Id);
            await LoadAssignmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }

    // preselect: khi bam "+ Phan cong ngay" tren 1 dong nhan vien cu the trong
    // danh sach, dialog se tu dong chon san nhan vien do (van cho phep doi lai
    // neu can). Bam nut "+ Phan cong" tren dau trang thi preselect = null.
    private void OpenAssignDialog(EmployeeDto? preselect = null)
    {
        if (_employees.Count == 0)
        {
            MessageBox.Show("Chưa có nhân viên nào để phân công.");
            return;
        }
        if (_shifts.Count == 0)
        {
            MessageBox.Show("Chưa có ca làm nào, hãy thêm ca làm trước.");
            return;
        }

        using var dialog = new Form
        {
            Text = "Phân công ca làm",
            Width = 360,
            Height = 340,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblEmp = new Label { Text = "Nhân viên *", Font = AppFonts.BodyMd, Location = new Point(20, 16), AutoSize = true };
        var cbEmp = new ComboBox { Location = new Point(20, 36), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName" };
        cbEmp.Items.AddRange(_employees.Where(e => e.IsActive).Cast<object>().ToArray());

        var selIdx = 0;
        if (preselect is not null)
        {
            for (var i = 0; i < cbEmp.Items.Count; i++)
                if (cbEmp.Items[i] is EmployeeDto e && e.UserId == preselect.UserId) { selIdx = i; break; }
        }
        if (cbEmp.Items.Count > 0) cbEmp.SelectedIndex = selIdx;

        var lblShift = new Label { Text = "Ca làm *", Font = AppFonts.BodyMd, Location = new Point(20, 76), AutoSize = true };
        var cbShift = new ComboBox { Location = new Point(20, 96), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbShift.Items.AddRange(_shifts.Cast<object>().ToArray());
        cbShift.SelectedIndex = 0;

        // Khu vuc phu trach: KHONG bat buoc, va chi co y nghia cho DUNG ca vua chon
        // o tren (vi se duoc luu chung 1 ban ghi ShiftAssignment voi ca do) - day
        // chinh la co che dam bao khu vuc luon "khop" voi ca lam that su.
        var lblArea = new Label { Text = "Khu vực phụ trách (không bắt buộc)", Font = AppFonts.BodyMd, Location = new Point(20, 136), AutoSize = true };
        var cbArea = new ComboBox { Location = new Point(20, 156), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbArea.Items.Add("(Không chọn)");
        cbArea.Items.AddRange(_areas.Cast<object>().ToArray());
        cbArea.SelectedIndex = 0;
        var lblAreaHint = new Label
        {
            Text = "Khu vực chỉ áp dụng cho đúng ca làm chọn ở trên.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(20, 190),
            Size = new Size(300, 34),
        };

        dialog.Controls.Add(lblEmp);
        dialog.Controls.Add(cbEmp);
        dialog.Controls.Add(lblShift);
        dialog.Controls.Add(cbShift);
        dialog.Controls.Add(lblArea);
        dialog.Controls.Add(cbArea);
        dialog.Controls.Add(lblAreaHint);

        var btnSave = new Button { Text = "Phân công", Location = new Point(100, 232), Size = new Size(140, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (cbEmp.SelectedItem is not EmployeeDto emp || cbShift.SelectedItem is not ShiftDto shift) return;

        int? areaId = cbArea.SelectedIndex > 0 && cbArea.SelectedItem is AreaDto area ? area.Id : null;
        _ = SaveAssignmentAsync(emp.UserId, shift.Id, dtpAssignDate.Value.Date, areaId);
    }

    private async Task SaveAssignmentAsync(int userId, int shiftId, DateTime workDate, int? areaId)
    {
        try
        {
            await _api.AssignShiftAsync(userId, shiftId, workDate, areaId);
            await LoadAssignmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không phân công được: {ex.Message}");
        }
    }

    // =====================================================================
    // TAB 3: NHIỆM VỤ
    // =====================================================================

    private async Task LoadAreasAsync()
    {
        try
        {
            _areas = await _api.GetAreasAsync();
        }
        catch
        {
            // Khong chan luong chinh neu load that bai - chi anh huong phan goi y
            // "mau nhiem vu theo khu vuc" trong dialog Giao viec, khong phai chuc
            // nang bat buoc.
        }
    }

    // Cac mau nhiem vu pho bien danh cho nhan vien Phuc vu, gan voi 1 khu vuc
    // ban cu the - giup Quan ly giao viec nhanh, khong phai go tay tu dau.
    private static readonly string[] AreaTaskTemplates =
    {
        "Vệ sinh khu vực trước giờ mở cửa",
        "Đón khách & sắp xếp bàn khu vực",
        "Kiểm tra bàn trống & dọn bàn sau khi khách rời",
        "Bổ sung gia vị, khăn giấy cho khu vực",
        "Hỗ trợ order & phục vụ khu vực giờ cao điểm",
    };

    private void RenderTaskFilterOptions()
    {
        cbTaskFilter.Items.Clear();
        cbTaskFilter.Items.Add("Tất cả nhân viên");
        foreach (var e in _employees) cbTaskFilter.Items.Add(e);
        cbTaskFilter.SelectedIndex = 0;
    }

    private async Task LoadTasksAsync()
    {
        try
        {
            int? userId = cbTaskFilter.SelectedItem is EmployeeDto emp ? emp.UserId : null;
            _tasks = await _api.GetTasksAsync(userId);
            RenderTasks();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách nhiệm vụ: {ex.Message}");
        }
    }

    // Khop dung enum StaffTaskStatus ben Backend: 0=ChuaLam,1=DangLam,2=HoanThanh
    private static readonly string[] TaskStatusLabels = { "Chưa làm", "Đang làm", "Hoàn thành" };
    private static readonly Color[] TaskStatusColors = { AppColors.Outline, AppColors.Warning, AppColors.Success };

    private void RenderTasks()
    {
        flowTasks.Controls.Clear();
        if (_tasks.Count == 0)
        {
            flowTasks.Controls.Add(new Label { Text = "Chưa có nhiệm vụ nào.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }
        foreach (var t in _tasks)
            flowTasks.Controls.Add(BuildTaskRow(t));
    }

    private Panel BuildTaskRow(StaffTaskDto t)
    {
        var row = new Panel { Size = new Size(1140, 78), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
        var statusIndex = Math.Clamp(t.Status, 0, TaskStatusLabels.Length - 1);
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var dot = new Panel { Size = new Size(8, 8), Location = new Point(12, 14), BackColor = TaskStatusColors[statusIndex] };
        var lblTitle = new Label { Text = t.Title, Font = AppFonts.LabelLg, Location = new Point(26, 8), Size = new Size(370, 20), AutoEllipsis = true };
        var lblDesc = new Label
        {
            Text = string.IsNullOrWhiteSpace(t.Description) ? "" : t.Description,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(12, 30),
            Size = new Size(380, 40),
            AutoEllipsis = true,
        };
        var lblAssignee = new Label { Text = "👤 " + t.AssignedToName, Font = AppFonts.BodyMd, Location = new Point(420, 12), AutoSize = true };
        var lblDue = new Label
        {
            Text = t.DueAt.HasValue ? "⏰ Hạn: " + t.DueAt.Value.ToString("dd/MM/yyyy") : "Không có hạn",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(420, 32),
            AutoSize = true,
        };

        var cbStatus = new ComboBox { Location = new Point(650, 20), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        cbStatus.Items.AddRange(TaskStatusLabels);
        cbStatus.SelectedIndex = statusIndex;
        cbStatus.SelectedIndexChanged += async (s, e) =>
        {
            if (cbStatus.SelectedIndex == t.Status) return;
            try
            {
                await _api.UpdateTaskStatusAsync(t.Id, cbStatus.SelectedIndex);
                t.Status = cbStatus.SelectedIndex;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
                cbStatus.SelectedIndex = t.Status;
            }
        };

        var lblEdit = new Label { Text = "Sửa", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(920, 26), AutoSize = true, Cursor = Cursors.Hand };
        var lblDelete = new Label { Text = "Xóa", Font = AppFonts.LabelMd, ForeColor = AppColors.Error, Location = new Point(970, 26), AutoSize = true, Cursor = Cursors.Hand };
        lblEdit.Click += (s, e) => OpenAddOrEditTaskDialog(t);
        lblDelete.Click += async (s, e) => await DeleteTaskAsync(t);

        row.Controls.Add(dot);
        row.Controls.Add(lblTitle);
        row.Controls.Add(lblDesc);
        row.Controls.Add(lblAssignee);
        row.Controls.Add(lblDue);
        row.Controls.Add(cbStatus);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);
        return row;
    }

    private void OpenAddOrEditTaskDialog(StaffTaskDto? existing)
    {
        if (_employees.Count == 0)
        {
            MessageBox.Show("Chưa có nhân viên nào để giao việc.");
            return;
        }

        using var dialog = new Form
        {
            Text = existing is null ? "Giao việc mới" : "Sửa nhiệm vụ",
            Width = 380,
            Height = 400,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var txtTitle = MakeLabeledInput(dialog, "Tiêu đề *", 20);
        txtTitle.Text = existing?.Title ?? "";

        var lblDesc = new Label { Text = "Mô tả", Font = AppFonts.BodyMd, Location = new Point(20, 80), AutoSize = true };
        var txtDesc = new TextBox { Location = new Point(20, 100), Width = 320, Height = 60, Multiline = true, Font = AppFonts.BodyMd };
        txtDesc.Text = existing?.Description ?? "";

        var lblEmp = new Label { Text = "Giao cho *", Font = AppFonts.BodyMd, Location = new Point(20, 170), AutoSize = true };
        var cbEmp = new ComboBox { Location = new Point(20, 190), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName" };
        cbEmp.Items.AddRange(_employees.Where(e => e.IsActive).Cast<object>().ToArray());
        var preselect = existing is null ? -1 : _employees.FindIndex(e => e.UserId == existing.AssignedToUserId);
        cbEmp.SelectedIndex = preselect >= 0 ? preselect : (cbEmp.Items.Count > 0 ? 0 : -1);

        // --- Ho tro nhanh "phan nhan vien phuc vu theo khu vuc ban" ---
        // Chi hien khi nguoi duoc giao la nhan vien Phuc vu VA da co du lieu
        // Khu vuc (GET /api/areas). Chon 1 mau + 1 khu vuc se tu dong dien
        // Tieu de/Mo ta ben tren, khong bat buoc phai dung (van co the tu go).
        var lblTemplate = new Label { Text = "Nhiệm vụ mẫu", Font = AppFonts.BodyMd, AutoSize = true };
        var cbTemplate = new ComboBox { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        cbTemplate.Items.Add("— Tự nhập tiêu đề —");
        cbTemplate.Items.AddRange(AreaTaskTemplates);
        cbTemplate.SelectedIndex = 0;

        var lblArea = new Label { Text = "Khu vực phụ trách", Font = AppFonts.BodyMd, AutoSize = true };
        var cbArea = new ComboBox { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbArea.Items.AddRange(_areas.Cast<object>().ToArray());
        if (cbArea.Items.Count > 0) cbArea.SelectedIndex = 0;

        var chkDue = new CheckBox { Text = "Có hạn chót", AutoSize = true, Checked = existing?.DueAt != null };
        var dtpDue = new DateTimePicker { Width = 200, Format = DateTimePickerFormat.Short, Enabled = chkDue.Checked };
        if (existing?.DueAt != null) dtpDue.Value = existing.DueAt.Value;
        chkDue.CheckedChanged += (s, e) => dtpDue.Enabled = chkDue.Checked;

        var btnSave = new Button { Text = "Lưu", Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };

        dialog.Controls.Add(lblDesc);
        dialog.Controls.Add(txtDesc);
        dialog.Controls.Add(lblEmp);
        dialog.Controls.Add(cbEmp);
        dialog.Controls.Add(lblTemplate);
        dialog.Controls.Add(cbTemplate);
        dialog.Controls.Add(lblArea);
        dialog.Controls.Add(cbArea);
        dialog.Controls.Add(chkDue);
        dialog.Controls.Add(dtpDue);
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        // Chi hien 2 dong "mau nhiem vu" + "khu vuc phu trach" khi nguoi duoc
        // giao la Phuc vu; an di thi day cac control ben duoi len lai va thu
        // gon chieu cao dialog, tranh de lai khoang trong xau.
        void RelayoutByRole()
        {
            var isPhucVu = cbEmp.SelectedItem is EmployeeDto emp && emp.RoleName == "PhucVu" && cbArea.Items.Count > 0;
            lblTemplate.Visible = isPhucVu;
            cbTemplate.Visible = isPhucVu;
            lblArea.Visible = isPhucVu;
            cbArea.Visible = isPhucVu;

            var y = 224;
            if (isPhucVu)
            {
                lblTemplate.Location = new Point(20, y);
                cbTemplate.Location = new Point(20, y + 20);
                y += 56;
                lblArea.Location = new Point(20, y);
                cbArea.Location = new Point(20, y + 20);
                y += 56;
            }

            chkDue.Location = new Point(20, y);
            dtpDue.Location = new Point(20, y + 24);
            y += 66;
            btnSave.Location = new Point(130, y);
            dialog.Height = y + 96;
        }

        cbEmp.SelectedIndexChanged += (s, e) => RelayoutByRole();
        void ApplyTemplate()
        {
            if (cbTemplate.SelectedIndex <= 0) return; // "Tự nhập tiêu đề" - không tự điền
            var template = cbTemplate.SelectedItem!.ToString()!;
            var areaName = cbArea.SelectedItem is AreaDto a ? a.Name : null;
            txtTitle.Text = areaName is null ? template : $"{template} – Khu vực {areaName}";
            txtDesc.Text = areaName is null ? template : $"{template} tại khu vực {areaName}.";
        }
        cbTemplate.SelectedIndexChanged += (s, e) => ApplyTemplate();
        cbArea.SelectedIndexChanged += (s, e) => ApplyTemplate();

        RelayoutByRole();

        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(txtTitle.Text) || cbEmp.SelectedItem is not EmployeeDto empSelected)
        {
            MessageBox.Show("Vui lòng nhập tiêu đề và chọn người được giao");
            return;
        }

        var dueAt = chkDue.Checked ? dtpDue.Value.Date : (DateTime?)null;
        var desc = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim();

        _ = SaveTaskAsync(existing?.Id, txtTitle.Text.Trim(), desc, empSelected.UserId, dueAt);
    }

    private async Task SaveTaskAsync(int? id, string title, string? description, int assignedToUserId, DateTime? dueAt)
    {
        try
        {
            if (id.HasValue) await _api.UpdateTaskAsync(id.Value, title, description, assignedToUserId, dueAt);
            else await _api.CreateTaskAsync(title, description, assignedToUserId, dueAt);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được nhiệm vụ: {ex.Message}");
        }
    }

    private async Task DeleteTaskAsync(StaffTaskDto t)
    {
        if (MessageBox.Show($"Xóa nhiệm vụ \"{t.Title}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            await _api.DeleteTaskAsync(t.Id);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }

    // =====================================================================
    // TAB 4: LỊCH SỬ CA LÀM
    // =====================================================================
    // Xem lại các ca đã/sẽ làm theo khoảng ngày (mặc định 7 ngày gần nhất),
    // lọc theo 1 nhân viên cụ thể hoặc tất cả. Trình bày dạng "timeline" nhóm
    // theo từng ngày, ngày gần nhất lên đầu - giống newsfeed/lịch sử giao dịch
    // của các app hiện đại, thay vì 1 bảng DataGridView dày đặc khó quét mắt.

    private void RenderHistoryFilterOptions()
    {
        cbHistoryEmployee.Items.Clear();
        cbHistoryEmployee.Items.Add("Tất cả nhân viên");
        foreach (var e in _employees) cbHistoryEmployee.Items.Add(e);
        cbHistoryEmployee.SelectedIndex = 0;
    }

    // Dat nhanh khoang ngay "N ngay gan nhat" (tinh ca hom nay) cho 2 nut bam nhanh
    // 7 ngày qua / 30 ngày qua - danh cho thao tac "xem nhanh" pho bien nhat.
    private async Task SetHistoryRangeAsync(int daysBack)
    {
        _suppressHistoryReload = true;
        dtpHistoryTo.Value = DateTime.Today;
        dtpHistoryFrom.Value = DateTime.Today.AddDays(-daysBack);
        _suppressHistoryReload = false;
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        if (_suppressHistoryReload) return;
        if (dtpHistoryFrom.Value.Date > dtpHistoryTo.Value.Date) return; // dang go do, doi lan doi hop le

        try
        {
            int? userId = cbHistoryEmployee.SelectedItem is EmployeeDto emp ? emp.UserId : null;
            _history = await _api.GetAssignmentHistoryAsync(userId, dtpHistoryFrom.Value.Date, dtpHistoryTo.Value.Date);
            RenderHistory();
        }
        catch (Exception ex)
        {
            flowHistory.Controls.Clear();
            flowHistory.Controls.Add(new Label { Text = $"Không tải được lịch sử: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
        }
    }

    private void RenderHistory()
    {
        flowHistory.Controls.Clear();

        if (_history.Count == 0)
        {
            flowHistory.Controls.Add(new Label { Text = "Không có ca làm nào trong khoảng ngày này.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        // Backend da sap xep WorkDate giam dan roi, o day chi can gom nhom giu
        // nguyen thu tu do (khong dung GroupBy roi OrderBy lai de tranh xao thu tu).
        var today = DateTime.Today;
        var groups = _history
            .GroupBy(h => h.WorkDate.Date)
            .Select(g => g.ToList());

        foreach (var dayItems in groups)
        {
            var date = dayItems[0].WorkDate.Date;
            flowHistory.Controls.Add(BuildHistoryDateHeader(date, today));
            foreach (var item in dayItems)
                flowHistory.Controls.Add(BuildHistoryRow(item));
        }
    }

    private static readonly string[] VietnameseWeekdays =
        { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

    private Label BuildHistoryDateHeader(DateTime date, DateTime today)
    {
        string dateLabel;
        if (date == today) dateLabel = $"Hôm nay · {date:dd/MM/yyyy}";
        else if (date == today.AddDays(-1)) dateLabel = $"Hôm qua · {date:dd/MM/yyyy}";
        else if (date == today.AddDays(1)) dateLabel = $"Ngày mai · {date:dd/MM/yyyy}";
        else dateLabel = $"{VietnameseWeekdays[(int)date.DayOfWeek]} · {date:dd/MM/yyyy}";

        return new Label
        {
            Text = dateLabel,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.OnSurface,
            AutoSize = true,
            Margin = new Padding(4, 14, 0, 6),
        };
    }

    private static Color HistoryStatusColor(int status) => status switch
    {
        1 => AppColors.Tertiary,   // Da diem danh
        2 => AppColors.Error,      // Vang mat
        3 => AppColors.Success,    // Da hoan thanh
        _ => AppColors.Outline,    // Da xep lich
    };

    // 1 dong = 1 ca lam cu the cua 1 nhan vien trong ngay: gio ca, ten NV (an khi
    // dang loc theo dung 1 nhan vien do de do rip), khu vuc phu trach (neu co) va
    // trang thai (mau sac hoa giong the chip cua cac app hien dai).
    private Panel BuildHistoryRow(ShiftAssignmentDto item)
    {
        var row = new Panel { Size = new Size(820, 54), BackColor = Color.White, Margin = new Padding(16, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var statusColor = HistoryStatusColor(item.Status);
        var stripe = new Panel { Size = new Size(4, 54), Location = new Point(0, 0), BackColor = statusColor };

        var lblTime = new Label
        {
            Text = $"{item.ShiftStart:hh\\:mm}-{item.ShiftEnd:hh\\:mm}",
            Font = AppFonts.LabelLg,
            Location = new Point(16, 8),
            Size = new Size(90, 20),
        };
        var lblShiftName = new Label { Text = item.ShiftName, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(16, 30), AutoSize = true };

        bool showEmployeeName = cbHistoryEmployee.SelectedIndex == 0; // dang xem "Tat ca nhan vien"
        var lblEmployee = new Label
        {
            Text = showEmployeeName ? "👤 " + item.UserName : "",
            Font = AppFonts.LabelMd,
            Location = new Point(120, 8),
            Size = new Size(200, 20),
            AutoEllipsis = true,
        };

        var lblArea = new Label
        {
            Text = string.IsNullOrEmpty(item.AreaName) ? "" : $"📍 {item.AreaName}",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.Tertiary,
            Location = new Point(120, 30),
            AutoSize = true,
        };

        var lblActual = new Label
        {
            Text = FormatActualTimes(item),
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(340, 18),
            AutoSize = true,
        };

        var statusIndex = Math.Clamp(item.Status, 0, AssignmentStatusLabels.Length - 1);
        var lblStatus = new Label
        {
            Text = AssignmentStatusLabels[statusIndex],
            Font = AppFonts.LabelMd,
            ForeColor = Color.White,
            BackColor = statusColor,
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3),
            Location = new Point(660, 16),
        };

        row.Controls.Add(stripe);
        row.Controls.Add(lblTime);
        row.Controls.Add(lblShiftName);
        row.Controls.Add(lblEmployee);
        row.Controls.Add(lblArea);
        row.Controls.Add(lblActual);
        row.Controls.Add(lblStatus);
        return row;
    }
}