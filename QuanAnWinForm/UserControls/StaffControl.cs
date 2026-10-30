using System.Drawing.Drawing2D;
using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QuanAnWinForm.Controls;
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
    private List<StationDto> _stations = new();

    // Lấy danh sách vai trò từ DB 1 lần rồi dùng lại (thay cho RoleId hardcode)
    private async Task<List<RoleDto>> EnsureRolesAsync()
    {
        if (_roles.Count == 0) _roles = await _api.GetRolesAsync();
        return _roles;
    }

    // Bếp và Quầy nước là Station của cùng vai trò Bep, không phải 2 Role khác nhau.
    private async Task<List<StationDto>> EnsureStationsAsync()
    {
        if (_stations.Count == 0) _stations = await _api.GetStationsAsync();
        return _stations;
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

    // =====================================================================
    // HAM DUNG CHUNG - giao dien khong phu thuoc DPI
    // May dang de man hinh scale > 100%: chu to len nhung kich thuoc/toa do viet cung
    // bang pixel thi khong -> chu bi cat, de len nhau. Dp() quy doi pixel thiet ke
    // (o 96 DPI) sang pixel that; vi tri ben phai/giua deu tinh tu do rong that cua
    // dong va do rong chu do bang TextRenderer.
    // =====================================================================

    private int Dp(int px) => (int)Math.Round(px * DeviceDpi / 96.0);

    private static readonly Font FBody = new("Segoe UI", 10F);
    private static readonly Font FBodyBold = new("Segoe UI", 10F, FontStyle.Bold);
    private static readonly Font FName = new("Segoe UI", 11F, FontStyle.Bold);
    private static readonly Font FSmall = new("Segoe UI", 9F);
    private static readonly Font FSmallBold = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FLink = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FLinkHover = new("Segoe UI", 9F, FontStyle.Bold | FontStyle.Underline);
    private static readonly Font FEmpty = new("Segoe UI", 10F, FontStyle.Italic);
    private static readonly Font FAvatar = new("Segoe UI", 14F, FontStyle.Bold);
    private static readonly Font FPlus = new("Segoe UI", 26F);

    private static int TextW(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 2;

    // ellipsis = true -> do rong co dinh (tu dat Size), chu dai bi cat "..." thay vi de len nhau.
    private static Label MakeLabel(string text, Font font, Color color, bool ellipsis = false) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
        AutoSize = !ellipsis,
        AutoEllipsis = ellipsis,
    };

    private static Label MakeLink(string text, Color color)
    {
        var link = new Label
        {
            Text = text,
            Font = FLink,
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Cursor = Cursors.Hand,
        };
        link.MouseEnter += (s, e) => link.Font = FLinkHover;
        link.MouseLeave += (s, e) => link.Font = FLink;
        return link;
    }

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

    // Nhan bo tron (pill) mau nen nhat - dung cho vai tro / trang thai.
    private Label MakePill(string text, Color fg, Color bg, int maxWidth = int.MaxValue)
    {
        var font = FSmallBold;
        var h = font.Height + Dp(6);
        var w = Math.Min(TextW(text, font) + Dp(18), maxWidth);
        var pill = new Label
        {
            Text = text,
            Font = font,
            ForeColor = fg,
            BackColor = bg,
            AutoSize = false,
            AutoEllipsis = true,
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

    // Moi dong trong flow doc gian het chieu rong khung (tru le), khi khung doi kich thuoc.
    private static void StretchRows(FlowLayoutPanel flow)
    {
        var avail = flow.ClientSize.Width;
        flow.SuspendLayout();
        foreach (Control c in flow.Controls)
        {
            if (c is not CardPanel) continue;
            var w = avail - c.Margin.Horizontal;
            if (w > 0 && c.Width != w) c.Width = w;
        }
        flow.ResumeLayout();
    }

    private static int RowWidth(FlowLayoutPanel flow, Padding margin) =>
        Math.Max(300, flow.ClientSize.Width - margin.Horizontal);

    // Ve vien mong quanh thanh loc (FlowLayoutPanel khong tu ve vien).
    private static void StyleFilterBar(FlowLayoutPanel bar)
    {
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, bar.Width - 1, bar.Height - 1);
        };
        bar.Resize += (s, e) => bar.Invalidate();
    }

    public StaffControl()
    {
        InitializeComponent();

        // Cac danh sach cuon doc: tat thanh cuon ngang + dong tu gian theo chieu rong.
        foreach (var flow in new[] { flowShiftDefs, flowAssignments, flowTasks, flowHistory })
        {
            var f = flow;
            DisableHorizontalScroll(f);
            f.ClientSizeChanged += (s, e) => StretchRows(f);
        }
        StyleFilterBar(flowTaskFilter);
        StyleFilterBar(flowHistoryFilter);

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

        // Đồng bộ Mobile: chọn trực tiếp một ngày và xem ngay số ca trong ngày đó.
        var btnHistorySpecificDate = MakeOutlineButton(
            "Ngày cụ thể",
            AppColors.Primary,
            Dp(116),
            Dp(32));
        btnHistorySpecificDate.Margin = new Padding(Dp(6), 0, 0, 0);
        btnHistorySpecificDate.Click += async (s, e) => await PickHistorySpecificDateAsync();
        flowHistoryFilter.Controls.Add(btnHistorySpecificDate);
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

    private async Task LoadEverythingAsync()
    {
        // Nhân viên phải có trước vì các màn Ca làm/Nhiệm vụ dùng danh sách này.
        await LoadStaffAsync();
        if (IsDisposed || Disposing) return;

        RenderTaskFilterOptions();

        // Khu vực và định nghĩa ca độc lập nhau -> tải song song để giảm thời gian mở màn.
        await Task.WhenAll(
            LoadAreasAsync(),
            LoadShiftsAsync());

        if (IsDisposed || Disposing) return;

        // Sau khi đã có nhân viên + ca + khu vực, hai danh sách này cũng có thể tải song song.
        await Task.WhenAll(
            LoadAssignmentsAsync(),
            LoadTasksAsync());

        if (IsDisposed || Disposing) return;

        _suppressHistoryReload = true;
        RenderHistoryFilterOptions();
        _suppressHistoryReload = false;
        await SetHistoryRangeAsync(6); // mặc định xem 7 ngày gần nhất
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

    private static string DisplayStationName(string? station) => station switch
    {
        "Bep" => "Bếp",
        "QuayNuoc" => "Quầy nước",
        null or "" => "Chưa phân công",
        _ => station,
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

    // Avatar tron + chu cai dau.
    private static Label BuildAvatarLabel(string initials, Color bg, int size)
    {
        var lbl = new Label
        {
            Text = initials,
            Font = FAvatar,
            ForeColor = Color.White,
            BackColor = bg,
            Size = new Size(size, size),
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

    private void AddInfoRow(Panel card, string icon, string text, int y, int pad, int width)
    {
        var lblIcon = MakeLabel(icon, FBody, AppColors.Outline, ellipsis: true);
        lblIcon.SetBounds(pad, y, Dp(24), FBody.Height + Dp(2));
        var lblText = MakeLabel(text, FBody, AppColors.OnSurfaceVariant, ellipsis: true);
        lblText.SetBounds(pad + Dp(26), y, width - pad * 2 - Dp(26), FBody.Height + Dp(2));
        card.Controls.Add(lblIcon);
        card.Controls.Add(lblText);
    }

    private CardPanel BuildStaffCard(EmployeeDto emp)
    {
        var w = Dp(292);
        var h = Dp(272);
        var pad = Dp(16);
        var (badgeBg, badgeFg) = RoleBadgeColors(emp.RoleName);
        var isSelf = emp.UserId == SessionContext.UserId;
        var accent = emp.IsActive ? badgeFg : AppColors.Outline; // tai khoan khoa -> xam

        var card = new CardPanel
        {
            Size = new Size(w, h),
            Margin = new Padding(0, 0, Dp(16), Dp(16)),
            StripeColor = accent,
            StripeWidth = Dp(4),
            StripeOnTop = true,
        };

        // --- Dau the: avatar + ten + vai tro ---
        var avatarSize = Dp(54);
        var avatar = BuildAvatarLabel(InitialsOf(emp.FullName), accent, avatarSize);
        avatar.Location = new Point(pad, Dp(20));
        card.Controls.Add(avatar);

        var textX = pad + avatarSize + Dp(12);
        var lblName = MakeLabel(isSelf ? emp.FullName + " (Bạn)" : emp.FullName, FName,
            emp.IsActive ? AppColors.OnSurface : AppColors.OnSurfaceVariant, ellipsis: true);
        lblName.SetBounds(textX, Dp(20), w - textX - pad, FName.Height + Dp(2));
        card.Controls.Add(lblName);

        var roleText = emp.RoleName == "Bep"
            ? $"{DisplayRoleName(emp.RoleName)} · {DisplayStationName(emp.StationName)}"
            : DisplayRoleName(emp.RoleName);
        var rolePill = MakePill(roleText, emp.IsActive ? badgeFg : AppColors.OnSurfaceVariant,
            emp.IsActive ? badgeBg : AppColors.SurfaceContainer, w - textX - pad);
        rolePill.Location = new Point(textX, lblName.Bottom + Dp(6));
        card.Controls.Add(rolePill);

        card.Controls.Add(new Panel { BackColor = AppColors.SurfaceContainer, Location = new Point(pad, Dp(90)), Size = new Size(w - pad * 2, 1) });

        // --- Thong tin lien he ---
        AddInfoRow(card, "☎", string.IsNullOrEmpty(emp.Phone) ? "Chưa cập nhật" : emp.Phone, Dp(100), pad, w);
        AddInfoRow(card, "✉", string.IsNullOrEmpty(emp.Email) ? "Chưa cập nhật" : emp.Email, Dp(126), pad, w);
        AddInfoRow(card, "🪪", emp.EmployeeCode, Dp(152), pad, w);

        card.Controls.Add(new Panel { BackColor = AppColors.SurfaceContainer, Location = new Point(pad, Dp(184)), Size = new Size(w - pad * 2, 1) });

        // --- Trang thai + Khoa/Kich hoat ---
        var statusY = Dp(194);
        var statusPill = emp.IsActive
            ? MakePill("Đang làm việc", AppColors.Success, Color.FromArgb(232, 245, 233))
            : MakePill("Đã khóa", AppColors.OnSurfaceVariant, AppColors.SurfaceContainer);
        statusPill.Location = new Point(pad, statusY);
        card.Controls.Add(statusPill);

        // Khong cho tu khoa chinh minh (Backend cung chan)
        if (!isSelf)
        {
            var toggleText = emp.IsActive ? "Tạm khóa" : "Kích hoạt";
            var lblToggle = MakeLink(toggleText, emp.IsActive ? AppColors.Error : AppColors.Success);
            lblToggle.Location = new Point(w - pad - TextW(toggleText, FLink), statusY + (statusPill.Height - FLink.Height) / 2);
            lblToggle.Click += async (sender, e) => await ToggleActiveAsync(emp);
            card.Controls.Add(lblToggle);
        }

        // --- Nut thao tac ---
        var btnH = Dp(32);
        var gap = Dp(8);
        var btnW = (w - pad * 2 - gap) / 2;
        var btnY = h - pad - btnH;

        var btnEdit = MakeOutlineButton("Sửa", AppColors.Primary, btnW, btnH);
        btnEdit.Location = new Point(pad, btnY);
        btnEdit.Click += async (sender, e) => await OpenEditEmployeeDialogAsync(emp);

        var btnReset = MakeOutlineButton("Đặt lại mật khẩu", AppColors.Primary, btnW, btnH);
        btnReset.Location = new Point(pad + btnW + gap, btnY);
        btnReset.Click += (sender, e) => OpenResetPasswordDialog(emp);

        card.Controls.Add(btnEdit);
        card.Controls.Add(btnReset);
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

    private CardPanel BuildAddNewTile()
    {
        var w = Dp(292);
        var h = Dp(272);
        var tile = new CardPanel
        {
            Size = new Size(w, h),
            Margin = new Padding(0, 0, Dp(16), Dp(16)),
            BackColor = Color.FromArgb(253, 245, 242),
            BorderColor = AppColors.Primary,
            Dashed = true,
            Cursor = Cursors.Hand,
        };

        const string caption = "Thêm nhân sự mới";
        var plus = MakeLabel("+", FPlus, AppColors.Primary);
        var text = MakeLabel(caption, FBodyBold, AppColors.Primary);
        plus.Cursor = text.Cursor = Cursors.Hand;

        var blockH = FPlus.Height + Dp(4) + FBodyBold.Height;
        var y = (h - blockH) / 2;
        plus.Location = new Point((w - TextW("+", FPlus)) / 2, y);
        text.Location = new Point((w - TextW(caption, FBodyBold)) / 2, y + FPlus.Height + Dp(4));

        tile.Controls.Add(plus);
        tile.Controls.Add(text);
        tile.Click += (s, e) => OpenAddEmployeeDialog();
        plus.Click += (s, e) => OpenAddEmployeeDialog();
        text.Click += (s, e) => OpenAddEmployeeDialog();
        return tile;
    }

    // ---------- Thêm nhân viên (danh sách vai trò lấy từ DB) ----------
    private void OpenAddEmployeeDialog() => _ = OpenAddEmployeeDialogAsync();

    private async Task OpenAddEmployeeDialogAsync()
    {
        List<RoleDto> roles;
        List<StationDto> stations;
        try
        {
            roles = await EnsureRolesAsync();
            stations = await EnsureStationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được vai trò/bộ phận chế biến: {ex.Message}");
            return;
        }
        if (roles.Count == 0)
        {
            MessageBox.Show("Hệ thống chưa có vai trò nào trong bảng Roles.");
            return;
        }

        using var dialog = new Form { Text = "Thêm nhân viên mới", Width = 380, Height = 620, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

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

        var lblStation = new Label
        {
            Text = "Bộ phận chế biến *",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 440),
            AutoSize = true,
            Visible = false,
        };
        var cbStation = new ComboBox
        {
            Location = new Point(20, 460),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false,
        };
        cbStation.Items.AddRange(stations.Cast<object>().ToArray());
        if (stations.Count > 0) cbStation.SelectedIndex = 0;
        dialog.Controls.Add(lblStation);
        dialog.Controls.Add(cbStation);

        void SyncStationVisibility()
        {
            var isKitchen = cbRole.SelectedItem is RoleDto selectedRole &&
                            selectedRole.RoleName == "Bep";
            lblStation.Visible = isKitchen;
            cbStation.Visible = isKitchen;
        }

        cbRole.SelectedIndexChanged += (s, e) => SyncStationVisibility();
        SyncStationVisibility();

        var btnSave = new Button { Text = "Lưu", Location = new Point(140, 525), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
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

        var selectedRole = (RoleDto)cbRole.SelectedItem!;
        int? stationId = null;
        if (selectedRole.RoleName == "Bep")
        {
            if (cbStation.SelectedItem is not StationDto selectedStation)
            {
                MessageBox.Show("Tài khoản chế biến phải chọn Bếp hoặc Quầy nước.");
                return;
            }
            stationId = selectedStation.Id;
        }

        await SaveNewEmployeeAsync(txtFullName.Text.Trim(), txtUsername.Text.Trim(), txtPassword.Text,
            txtPhone.Text.Trim(), txtEmail.Text.Trim(), txtEmployeeCode.Text.Trim(),
            selectedRole.RoleId, stationId);
    }

    private async Task SaveNewEmployeeAsync(string fullName, string username, string password, string phone, string email, string employeeCode, int roleId, int? stationId)
    {
        try
        {
            await _api.CreateEmployeeAsync(fullName, username, password,
                string.IsNullOrEmpty(phone) ? null : phone,
                string.IsNullOrEmpty(email) ? null : email,
                employeeCode, roleId, stationId);
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
        List<StationDto> stations;
        try
        {
            roles = await EnsureRolesAsync();
            stations = await EnsureStationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được vai trò/bộ phận chế biến: {ex.Message}");
            return;
        }

        var isSelf = staff.UserId == SessionContext.UserId;

        using var dialog = new Form { Text = $"Sửa nhân viên - {staff.EmployeeCode}", Width = 380, Height = 500, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

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

        var lblStation = new Label
        {
            Text = "Bộ phận chế biến *",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 288),
            AutoSize = true,
            Visible = false,
        };
        var cbStation = new ComboBox
        {
            Location = new Point(20, 308),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = !isSelf,
            Visible = false,
        };
        cbStation.Items.AddRange(stations.Cast<object>().ToArray());
        var stationIdx = stations.FindIndex(s => s.Id == staff.StationId);
        if (stations.Count > 0)
            cbStation.SelectedIndex = stationIdx >= 0 ? stationIdx : 0;
        dialog.Controls.Add(lblStation);
        dialog.Controls.Add(cbStation);

        void SyncStationVisibility()
        {
            var isKitchen = cbRole.SelectedItem is RoleDto selectedRole &&
                            selectedRole.RoleName == "Bep";
            lblStation.Visible = isKitchen;
            cbStation.Visible = isKitchen;
        }

        cbRole.SelectedIndexChanged += (s, e) => SyncStationVisibility();
        SyncStationVisibility();

        var btnSave = new Button { Text = "Lưu", Location = new Point(140, 375), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
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
            var selectedRole = (RoleDto)cbRole.SelectedItem!;
            int? stationId = null;
            if (selectedRole.RoleName == "Bep")
            {
                if (cbStation.SelectedItem is not StationDto selectedStation)
                {
                    MessageBox.Show("Tài khoản chế biến phải chọn Bếp hoặc Quầy nước.");
                    return;
                }
                stationId = selectedStation.Id;
            }

            await _api.UpdateEmployeeAsync(staff.Id, txtFullName.Text.Trim(),
                string.IsNullOrEmpty(phone) ? null : phone,
                string.IsNullOrEmpty(email) ? null : email,
                selectedRole.RoleId,
                stationId);
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

    private CardPanel BuildShiftDefRow(ShiftDto shift)
    {
        var margin = new Padding(0, 0, 0, Dp(10));
        var pad = Dp(16);
        var row = new CardPanel
        {
            Margin = margin,
            Size = new Size(RowWidth(flowShiftDefs, margin), Dp(88)),
            StripeColor = AppColors.Primary,
            StripeWidth = Dp(4),
        };

        var lblName = MakeLabel(shift.Name, FBodyBold, AppColors.OnSurface, ellipsis: true);
        var lblTime = MakeLabel($"🕒  {shift.StartTime:hh\\:mm} – {shift.EndTime:hh\\:mm}", FBody, AppColors.OnSurfaceVariant, ellipsis: true);
        var lblEdit = MakeLink("Sửa", AppColors.Primary);
        var lblDelete = MakeLink("Xóa", AppColors.Error);
        lblEdit.Click += (s, e) => OpenAddOrEditShiftDialog(shift);
        lblDelete.Click += async (s, e) => await DeleteShiftAsync(shift);

        row.Controls.Add(lblName);
        row.Controls.Add(lblTime);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);

        void LayoutRow()
        {
            var w = row.Width;
            lblName.SetBounds(pad, Dp(12), w - pad * 2, FBodyBold.Height + Dp(2));
            lblTime.SetBounds(pad, lblName.Bottom + Dp(2), w - pad * 2, FBody.Height + Dp(2));
            var y = row.Height - FLink.Height - Dp(10);
            lblDelete.Location = new Point(w - pad - TextW("Xóa", FLink), y);
            lblEdit.Location = new Point(lblDelete.Left - Dp(16) - TextW("Sửa", FLink), y);
        }
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();
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
        StretchRows(flowAssignments);
    }

    // Chuoi hien thi gio vao/ra thuc te cua 1 ca (khop voi man hinh Diem danh tren mobile).
    private static string FormatActualTimes(ShiftAssignmentDto a)
    {
        if (a.Status == 2 || a.AttendanceStatus == "VangMat")
            return "Vắng mặt";

        if (a.CheckInTime is null)
            return "Chưa điểm danh";

        var parts = new List<string>
        {
            $"Vào {a.CheckInTime:HH:mm}"
        };

        if (a.LateMinutes > 0)
            parts.Add($"Đi trễ {a.LateMinutes} phút");
        else
            parts.Add("Đúng giờ");

        if (a.CheckOutTime is null)
        {
            parts.Add("Đang làm");
        }
        else
        {
            parts.Add($"Ra {a.CheckOutTime:HH:mm}");
            parts.Add(
                a.EarlyLeaveMinutes > 0
                    ? $"Về sớm {a.EarlyLeaveMinutes} phút"
                    : "Đủ giờ");
        }

        return string.Join(" · ", parts);
    }

    private CardPanel BuildRosterRow(EmployeeDto emp, List<ShiftAssignmentDto> empAssignments)
    {
        var margin = new Padding(0, 0, 0, Dp(10));
        var pad = Dp(18);
        var headH = Dp(46);
        var subH = Dp(60);
        var rowHeight = empAssignments.Count == 0 ? headH + Dp(40) : headH + empAssignments.Count * subH + Dp(6);
        var (badgeBg, badgeFg) = RoleBadgeColors(emp.RoleName);

        var row = new CardPanel
        {
            Margin = margin,
            Size = new Size(RowWidth(flowAssignments, margin), rowHeight),
            StripeColor = badgeFg,
            StripeWidth = Dp(4),
        };

        // Dong dau: ten + vai tro
        var lblName = MakeLabel(emp.FullName, FBodyBold, AppColors.OnSurface);
        lblName.Location = new Point(pad, Dp(12));
        var rolePill = MakePill(DisplayRoleName(emp.RoleName), badgeFg, badgeBg);
        rolePill.Location = new Point(pad + TextW(emp.FullName, FBodyBold) + Dp(10), Dp(12) + (FBodyBold.Height - rolePill.Height) / 2);
        row.Controls.Add(lblName);
        row.Controls.Add(rolePill);

        // ----- Chua co ca nao -----
        if (empAssignments.Count == 0)
        {
            var lblEmpty = MakeLabel("Chưa xếp ca", FEmpty, AppColors.Outline);
            lblEmpty.Location = new Point(pad, headH + Dp(4));
            var lblAssignNow = MakeLink("+ Phân công ngay", AppColors.Primary);
            lblAssignNow.Click += (s, e) => OpenAssignDialog(emp);
            row.Controls.Add(lblEmpty);
            row.Controls.Add(lblAssignNow);

            void LayoutEmpty() =>
                lblAssignNow.Location = new Point(row.Width - pad - TextW(lblAssignNow.Text, FLink), headH + Dp(6));
            row.Resize += (s, e) => LayoutEmpty();
            LayoutEmpty();
            return row;
        }

        // ----- Co (nhieu) ca -----
        var lblAddMore = MakeLink("+ Thêm ca khác", AppColors.Primary);
        lblAddMore.Click += (s, e) => OpenAssignDialog(emp);
        row.Controls.Add(lblAddMore);

        var subs = new List<(Panel sep, Label shift, Label actual, Label area, ComboBox status, Label del)>();

        foreach (var assignment in empAssignments)
        {
            var sep = new Panel { BackColor = AppColors.SurfaceContainer, Height = 1 };

            var lblShift = MakeLabel(
                $"🕒 {assignment.ShiftName} ({assignment.ShiftStart:hh\\:mm}-{assignment.ShiftEnd:hh\\:mm})",
                FSmallBold, AppColors.Primary, ellipsis: true);

            // Gio vao/ra THUC TE (nhan vien diem danh tren mobile, hoac backend tu dong bo
            // khi quan ly doi trang thai) - de WinForm va mobile nhin cung 1 du lieu.
            var lblActual = MakeLabel(FormatActualTimes(assignment), FSmall,
                assignment.Status == 2 ? AppColors.Error : AppColors.OnSurfaceVariant, ellipsis: true);

            // Khu vuc phu trach CUA DUNG ca nay (neu co) - bam vao de doi/gan khu vuc khac.
            var hasArea = !string.IsNullOrWhiteSpace(assignment.AreaLabel);
            var lblArea = MakeLabel(
                hasArea ? $"📍 {assignment.AreaLabel}" : "📍 Chưa gán khu vực",
                FBody,
                hasArea ? AppColors.Tertiary : AppColors.Outline,
                ellipsis: true);
            lblArea.Cursor = Cursors.Hand;
            lblArea.Click += (s, e) => OpenChangeAreaDialog(assignment);

            var cbStatus = new ComboBox { Width = Dp(150), DropDownStyle = ComboBoxStyle.DropDownList, Font = FBody };
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

            var lblDelete = MakeLink("Xóa", AppColors.Error);
            lblDelete.Click += async (s, e) => await DeleteAssignmentAsync(assignment);

            row.Controls.Add(sep);
            row.Controls.Add(lblShift);
            row.Controls.Add(lblActual);
            row.Controls.Add(lblArea);
            row.Controls.Add(cbStatus);
            row.Controls.Add(lblDelete);
            subs.Add((sep, lblShift, lblActual, lblArea, cbStatus, lblDelete));
        }

        void LayoutRow()
        {
            var w = row.Width;
            lblAddMore.Location = new Point(w - pad - TextW(lblAddMore.Text, FLink), Dp(14));

            var y = headH;
            foreach (var (sep, shift, actual, area, status, del) in subs)
            {
                sep.SetBounds(pad, y, w - pad * 2, 1);

                del.Location = new Point(w - pad - TextW("Xóa", FLink), y + (subH - FLink.Height) / 2);
                status.Location = new Point(del.Left - Dp(16) - status.Width, y + (subH - status.Height) / 2);

                var areaLeft = Math.Max(Dp(320), (int)(w * 0.40));
                area.SetBounds(areaLeft, y + (subH - (FBody.Height + Dp(2))) / 2,
                    Math.Max(Dp(60), status.Left - Dp(16) - areaLeft), FBody.Height + Dp(2));

                var textW = Math.Max(Dp(60), areaLeft - pad - Dp(12));
                shift.SetBounds(pad, y + Dp(10), textW, FSmallBold.Height + Dp(2));
                actual.SetBounds(pad + Dp(18), shift.Bottom + Dp(2), textW - Dp(18), FSmall.Height + Dp(2));

                y += subH;
            }
        }
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();
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
            Width = 430,
            Height = 430,
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
            Size = new Size(370, 40),
        };

        var lblArea = new Label
        {
            Text = "Khu vực phụ trách (có thể chọn nhiều)",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 62),
            AutoSize = true,
        };

        var checkedAreas = new CheckedListBox
        {
            Location = new Point(20, 88),
            Size = new Size(370, 230),
            CheckOnClick = true,
            Font = AppFonts.BodyMd,
            IntegralHeight = false,
        };

        var selectedIds = assignment.AreaIds.Count > 0
            ? assignment.AreaIds.ToHashSet()
            : assignment.AreaId.HasValue
                ? new HashSet<int> { assignment.AreaId.Value }
                : new HashSet<int>();

        foreach (var area in _areas)
        {
            var index = checkedAreas.Items.Add(area);
            if (selectedIds.Contains(area.Id))
                checkedAreas.SetItemChecked(index, true);
        }

        var lblHint = new Label
        {
            Text = "Bỏ chọn tất cả nếu ca này chưa cần phân khu vực.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(20, 324),
            AutoSize = true,
        };

        var btnSave = new Button
        {
            Text = "Lưu khu vực",
            Location = new Point(135, 350),
            Size = new Size(150, 34),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };

        dialog.Controls.AddRange(new Control[]
        {
            lblInfo,
            lblArea,
            checkedAreas,
            lblHint,
            btnSave,
        });
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        var areaIds = checkedAreas.CheckedItems
            .Cast<AreaDto>()
            .Select(a => a.Id)
            .ToList();

        _ = SaveAssignmentAreasAsync(
            assignment,
            areaIds);
    }

    private async Task SaveAssignmentAreasAsync(
        ShiftAssignmentDto assignment,
        IReadOnlyCollection<int> areaIds)
    {
        try
        {
            await _api.UpdateAssignmentAreasAsync(
                assignment.Id,
                areaIds);

            await LoadAssignmentsAsync();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không cập nhật được khu vực: {ex.Message}");
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
            Width = 430,
            Height = 510,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblEmp = new Label
        {
            Text = "Nhân viên *",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 16),
            AutoSize = true,
        };

        var cbEmp = new ComboBox
        {
            Location = new Point(20, 38),
            Width = 370,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "FullName",
        };
        cbEmp.Items.AddRange(
            _employees
                .Where(e => e.IsActive)
                .Cast<object>()
                .ToArray());

        var selIdx = 0;
        if (preselect is not null)
        {
            for (var i = 0; i < cbEmp.Items.Count; i++)
            {
                if (cbEmp.Items[i] is EmployeeDto e &&
                    e.UserId == preselect.UserId)
                {
                    selIdx = i;
                    break;
                }
            }
        }

        if (cbEmp.Items.Count > 0)
            cbEmp.SelectedIndex = selIdx;

        var lblShift = new Label
        {
            Text = "Ca làm *",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 78),
            AutoSize = true,
        };

        var cbShift = new ComboBox
        {
            Location = new Point(20, 100),
            Width = 370,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "Name",
        };
        cbShift.Items.AddRange(
            _shifts.Cast<object>().ToArray());
        cbShift.SelectedIndex = 0;

        var lblArea = new Label
        {
            Text = "Khu vực phụ trách (có thể chọn nhiều)",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 142),
            AutoSize = true,
        };

        var checkedAreas = new CheckedListBox
        {
            Location = new Point(20, 168),
            Size = new Size(370, 210),
            CheckOnClick = true,
            Font = AppFonts.BodyMd,
            IntegralHeight = false,
        };

        foreach (var area in _areas)
            checkedAreas.Items.Add(area);

        var lblAreaHint = new Label
        {
            Text = "Các khu đã chọn chỉ áp dụng cho đúng ca làm này. Có thể để trống.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(20, 384),
            Size = new Size(370, 36),
        };

        var btnSave = new Button
        {
            Text = "Phân công",
            Location = new Point(140, 425),
            Size = new Size(140, 34),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };

        dialog.Controls.AddRange(new Control[]
        {
            lblEmp,
            cbEmp,
            lblShift,
            cbShift,
            lblArea,
            checkedAreas,
            lblAreaHint,
            btnSave,
        });
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        if (cbEmp.SelectedItem is not EmployeeDto emp ||
            cbShift.SelectedItem is not ShiftDto shift)
            return;

        var areaIds = checkedAreas.CheckedItems
            .Cast<AreaDto>()
            .Select(a => a.Id)
            .ToList();

        _ = SaveAssignmentAsync(
            emp.UserId,
            shift.Id,
            dtpAssignDate.Value.Date,
            areaIds);
    }

    private async Task SaveAssignmentAsync(
        int userId,
        int shiftId,
        DateTime workDate,
        IReadOnlyCollection<int> areaIds)
    {
        try
        {
            await _api.AssignShiftAsync(
                userId,
                shiftId,
                workDate,
                areaIds);

            await LoadAssignmentsAsync();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không phân công được: {ex.Message}");
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

    private CardPanel BuildTaskRow(StaffTaskDto t)
    {
        var margin = new Padding(0, 0, 0, Dp(10));
        var pad = Dp(18);
        var statusIndex = Math.Clamp(t.Status, 0, TaskStatusLabels.Length - 1);
        var h = Dp(90);

        var row = new CardPanel
        {
            Margin = margin,
            Size = new Size(RowWidth(flowTasks, margin), h),
            StripeColor = TaskStatusColors[statusIndex],
            StripeWidth = Dp(4),
        };

        var lblTitle = MakeLabel(t.Title, FBodyBold, AppColors.OnSurface, ellipsis: true);
        var lblDesc = MakeLabel(string.IsNullOrWhiteSpace(t.Description) ? "" : t.Description, FSmall, AppColors.OnSurfaceVariant, ellipsis: true);
        var lblAssignee = MakeLabel("👤 " + t.AssignedToName, FBody, AppColors.OnSurface, ellipsis: true);

        // Qua han (chua hoan thanh) -> to do de quan ly de nhin thay.
        var overdue = t.DueAt.HasValue && t.DueAt.Value.Date < DateTime.Today && t.Status != 2;
        var dueText = t.DueAt.HasValue
            ? (overdue ? "⏰ Quá hạn: " : "⏰ Hạn: ") + t.DueAt.Value.ToString("dd/MM/yyyy")
            : "Không có hạn";
        var lblDue = MakeLabel(dueText, FSmall, overdue ? AppColors.Error : AppColors.OnSurfaceVariant, ellipsis: true);

        var cbStatus = new ComboBox { Width = Dp(150), DropDownStyle = ComboBoxStyle.DropDownList, Font = FBody };
        cbStatus.Items.AddRange(TaskStatusLabels);
        cbStatus.SelectedIndex = statusIndex;
        cbStatus.SelectedIndexChanged += async (s, e) =>
        {
            if (cbStatus.SelectedIndex == t.Status) return;
            try
            {
                await _api.UpdateTaskStatusAsync(t.Id, cbStatus.SelectedIndex);
                t.Status = cbStatus.SelectedIndex;
                row.StripeColor = TaskStatusColors[Math.Clamp(t.Status, 0, TaskStatusColors.Length - 1)];
                row.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
                cbStatus.SelectedIndex = t.Status;
            }
        };

        var lblEdit = MakeLink("Sửa", AppColors.Primary);
        var lblDelete = MakeLink("Xóa", AppColors.Error);
        lblEdit.Click += (s, e) => OpenAddOrEditTaskDialog(t);
        lblDelete.Click += async (s, e) => await DeleteTaskAsync(t);

        row.Controls.Add(lblTitle);
        row.Controls.Add(lblDesc);
        row.Controls.Add(lblAssignee);
        row.Controls.Add(lblDue);
        row.Controls.Add(cbStatus);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);

        void LayoutRow()
        {
            var w = row.Width;

            lblDelete.Location = new Point(w - pad - TextW("Xóa", FLink), (h - FLink.Height) / 2);
            lblEdit.Location = new Point(lblDelete.Left - Dp(16) - TextW("Sửa", FLink), lblDelete.Top);
            cbStatus.Location = new Point(lblEdit.Left - Dp(24) - cbStatus.Width, (h - cbStatus.Height) / 2);

            var leftW = (int)(w * 0.40);
            lblTitle.SetBounds(pad, Dp(12), leftW - pad, FBodyBold.Height + Dp(2));
            lblDesc.SetBounds(pad, lblTitle.Bottom + Dp(4), leftW - pad, FSmall.Height * 2 + Dp(2));

            var midX = leftW + Dp(16);
            var midW = Math.Max(Dp(80), cbStatus.Left - Dp(16) - midX);
            lblAssignee.SetBounds(midX, Dp(14), midW, FBody.Height + Dp(2));
            lblDue.SetBounds(midX, lblAssignee.Bottom + Dp(6), midW, FSmall.Height + Dp(2));
        }
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();
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

    private async Task PickHistorySpecificDateAsync()
    {
        using var dialog = new Form
        {
            Text = "Chọn ngày lịch sử",
            Width = 330,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lbl = new Label
        {
            Text = "Ngày cần xem",
            Font = AppFonts.BodyMd,
            Location = new Point(20, 18),
            AutoSize = true,
        };

        var picker = new DateTimePicker
        {
            Location = new Point(20, 44),
            Width = 270,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy",
            Value = dtpHistoryTo.Value.Date,
        };

        var btn = new Button
        {
            Text = "Xem ngày này",
            Location = new Point(95, 88),
            Size = new Size(130, 34),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };

        dialog.Controls.AddRange(new Control[]
        {
            lbl,
            picker,
            btn,
        });
        dialog.AcceptButton = btn;

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        _suppressHistoryReload = true;
        dtpHistoryFrom.Value = picker.Value.Date;
        dtpHistoryTo.Value = picker.Value.Date;
        _suppressHistoryReload = false;

        await LoadHistoryAsync();
    }

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
            flowHistory.Controls.Add(BuildHistoryDateHeader(date, today, dayItems.Count));
            foreach (var item in dayItems)
                flowHistory.Controls.Add(BuildHistoryRow(item));
        }
    }

    private static readonly string[] VietnameseWeekdays =
        { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

    private Label BuildHistoryDateHeader(DateTime date, DateTime today, int count)
    {
        string dateLabel;
        if (date == today) dateLabel = $"Hôm nay · {date:dd/MM/yyyy}";
        else if (date == today.AddDays(-1)) dateLabel = $"Hôm qua · {date:dd/MM/yyyy}";
        else if (date == today.AddDays(1)) dateLabel = $"Ngày mai · {date:dd/MM/yyyy}";
        else dateLabel = $"{VietnameseWeekdays[(int)date.DayOfWeek]} · {date:dd/MM/yyyy}";

        return new Label
        {
            Text = $"{dateLabel}   ({count} ca)",
            Font = FBodyBold,
            ForeColor = date == today ? AppColors.Primary : AppColors.OnSurface,
            AutoSize = true,
            Margin = new Padding(Dp(2), Dp(14), 0, Dp(8)),
        };
    }

    private static Color HistoryStatusColor(ShiftAssignmentDto item)
    {
        if (item.Status == 2 ||
            item.AttendanceStatus == "VangMat")
            return AppColors.Error;

        if (item.LateMinutes > 0 ||
            item.EarlyLeaveMinutes > 0)
            return AppColors.Warning;

        return item.Status switch
        {
            1 => AppColors.Tertiary,
            3 => AppColors.Success,
            _ => AppColors.Outline,
        };
    }

    // 1 dong = 1 ca lam cu the cua 1 nhan vien trong ngay: gio ca, ten NV (an khi
    // dang loc theo dung 1 nhan vien do), khu vuc phu trach, gio vao/ra va trang thai.
    private CardPanel BuildHistoryRow(ShiftAssignmentDto item)
    {
        var margin = new Padding(0, 0, 0, Dp(8));
        var pad = Dp(18);
        var h = Dp(64);
        var statusColor = HistoryStatusColor(item);

        var row = new CardPanel
        {
            Margin = margin,
            Size = new Size(RowWidth(flowHistory, margin), h),
            StripeColor = statusColor,
            StripeWidth = Dp(4),
        };

        var timeText = $"{item.ShiftStart:hh\\:mm}-{item.ShiftEnd:hh\\:mm}";
        var lblTime = MakeLabel(timeText, FBodyBold, AppColors.OnSurface);
        var lblShiftName = MakeLabel(item.ShiftName, FSmall, AppColors.OnSurfaceVariant, ellipsis: true);

        var showEmployeeName = cbHistoryEmployee.SelectedIndex == 0; // dang xem "Tat ca nhan vien"
        var lblEmployee = MakeLabel("👤 " + item.UserName, FBodyBold, AppColors.OnSurface, ellipsis: true);
        lblEmployee.Visible = showEmployeeName;

        var lblArea = MakeLabel(
            string.IsNullOrWhiteSpace(item.AreaLabel)
                ? ""
                : $"📍 {item.AreaLabel}",
            FSmall,
            AppColors.Tertiary,
            ellipsis: true);

        var actualText = FormatActualTimes(item);
        var lblActual = MakeLabel(actualText, FSmall, item.Status == 2 ? AppColors.Error : AppColors.OnSurfaceVariant, ellipsis: true);

        var statusText =
            item.CheckInTime is not null ||
            item.Status == 2
                ? item.AttendanceStatusLabel
                : AssignmentStatusLabels[
                    Math.Clamp(
                        item.Status,
                        0,
                        AssignmentStatusLabels.Length - 1)];

        var statusPill = MakePill(
            statusText,
            Color.White,
            statusColor,
            Dp(190));

        row.Controls.Add(lblTime);
        row.Controls.Add(lblShiftName);
        row.Controls.Add(lblEmployee);
        row.Controls.Add(lblArea);
        row.Controls.Add(lblActual);
        row.Controls.Add(statusPill);

        var timeColW = Math.Max(TextW(timeText, FBodyBold), TextW(item.ShiftName, FSmall)) + Dp(8);
        var actualW = TextW(actualText, FSmall) + Dp(8);

        void LayoutRow()
        {
            var w = row.Width;

            statusPill.Location = new Point(w - pad - statusPill.Width, (h - statusPill.Height) / 2);
            lblActual.SetBounds(statusPill.Left - Dp(20) - actualW, (h - FSmall.Height) / 2, actualW, FSmall.Height + Dp(2));

            lblTime.Location = new Point(pad, Dp(10));
            lblShiftName.SetBounds(pad, lblTime.Bottom + Dp(2), timeColW, FSmall.Height + Dp(2));

            var colX = pad + timeColW + Dp(24);
            var colW = Math.Max(Dp(100), lblActual.Left - Dp(16) - colX);
            if (showEmployeeName)
            {
                lblEmployee.SetBounds(colX, Dp(10), colW, FBodyBold.Height + Dp(2));
                lblArea.SetBounds(colX, lblEmployee.Bottom + Dp(2), colW, FSmall.Height + Dp(2));
            }
            else
            {
                lblArea.SetBounds(colX, (h - FSmall.Height) / 2, colW, FSmall.Height + Dp(2));
            }
        }
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();
        return row;
    }
}