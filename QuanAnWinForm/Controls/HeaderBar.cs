using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Thanh header tren cung: o tim kiem, chuong thong bao, avatar + ten nguoi dung.
public partial class HeaderBar : UserControl
{
    public event Action? NotificationClicked;
    public event Action<string>? SearchTextChanged;
    public event Action? LogoutRequested;

    public event Action? ChangePasswordRequested;

    // Menu bung ra khi bam vao avatar/ten nguoi dung: dong thong tin tai khoan + "Dang xuat".
    private readonly ContextMenuStrip _userMenu = new() { ShowImageMargin = false };
    private readonly ToolStripMenuItem _miUserInfo = new() { Enabled = false };

    public HeaderBar()
    {
        InitializeComponent();
        btnNotify.Click += (s, e) => NotificationClicked?.Invoke();
        txtSearch.TextChanged += (s, e) => SearchTextChanged?.Invoke(txtSearch.Text);

        var miChangePw = new ToolStripMenuItem { Text = "Đổi mật khẩu", Font = AppFonts.LabelLg };
        miChangePw.Click += (s, e) => ChangePasswordRequested?.Invoke();

        var miLogout = new ToolStripMenuItem { Text = "Đăng xuất", ForeColor = AppColors.Error, Font = AppFonts.LabelLg };
        miLogout.Click += (s, e) => LogoutRequested?.Invoke();
        _miUserInfo.Font = AppFonts.BodyMd;
        _userMenu.Items.Add(_miUserInfo);
        _userMenu.Items.Add(new ToolStripSeparator());
        _userMenu.Items.Add(miChangePw);
        _userMenu.Items.Add(miLogout);

        foreach (Control c in new Control[] { panelUserInfo, panelAvatar, lblAvatarInitial, lblUserName, lblUserRole })
        {
            c.Cursor = Cursors.Hand;
            c.Click += (s, e) => _userMenu.Show(panelUserInfo, new Point(0, panelUserInfo.Height + 4));
        }
        Disposed += (s, e) => _userMenu.Dispose();
    }

    // Goi lai tu MainForm sau khi biet thong tin nguoi dang dang nhap
    public void SetUser(string fullName, string roleDisplayName)
    {
        lblUserName.Text = fullName;
        lblUserRole.Text = roleDisplayName;
        lblAvatarInitial.Text = fullName.Length > 0 ? fullName[0].ToString().ToUpper() : "?";
        _miUserInfo.Text = $"{fullName} · {roleDisplayName}";
        LayoutUserInfo();
    }

    // Tính lại bề rộng khối tên + avatar theo độ dài tên thật -> tên không bao giờ đè lên avatar.
    private void LayoutUserInfo()
    {
        const int avatarSize = 36, gap = 12;
        int nameW = TextRenderer.MeasureText(lblUserName.Text, lblUserName.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int roleW = TextRenderer.MeasureText(lblUserRole.Text, lblUserRole.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int textW = Math.Max(nameW, roleW) + 8;

        lblUserName.Location = new Point(0, 0);
        lblUserRole.Location = new Point(0, 21);
        panelAvatar.Location = new Point(textW + gap, 3);
        panelUserInfo.Width = textW + gap + avatarSize;
    }

    // Cham do o goc chuong 🔔 - hien so luong thong bao CHUA DOC. count<=0 -> an di.
    public void SetUnreadBadge(int count)
    {
        if (count <= 0)
        {
            lblNotifyBadge.Visible = false;
            return;
        }
        lblNotifyBadge.Text = count > 9 ? "9+" : count.ToString();
        // "9+" rộng hơn "9" -> nới badge và canh sát mép phải nút chuông để không bị cắt chữ.
        lblNotifyBadge.Width = lblNotifyBadge.Text.Length > 1 ? 22 : 16;
        lblNotifyBadge.Left = btnNotify.Width - lblNotifyBadge.Width;
        lblNotifyBadge.Visible = true;
        lblNotifyBadge.BringToFront();
    }

    // Toa do man hinh cua goc duoi-trai nut chuong - de MainForm tinh vi tri mo
    // popup NotificationPopupForm ngay ben duoi.
    public Point GetNotifyButtonScreenLocation() => btnNotify.PointToScreen(new Point(0, btnNotify.Height));

    // Toa do man hinh cua goc duoi-trai o tim kiem - de MainForm tinh vi tri mo
    // popup SearchPopupForm ngay ben duoi.
    public Point GetSearchBoxScreenLocation() => txtSearch.PointToScreen(new Point(0, txtSearch.Height));
}