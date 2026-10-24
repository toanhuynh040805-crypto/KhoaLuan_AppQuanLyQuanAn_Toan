using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.UserControls;
using QuanAnWinForm.Forms;

namespace QuanAnWinForm.Forms;

public partial class MainForm : Form
{
    private readonly int _userId;
    private readonly string _roleName;
    private readonly ApiService _api = new();
    private readonly NotificationHubClient _notificationHub = new();
    private readonly NotificationPopupForm _notificationPopup = new();
    private readonly SearchPopupForm _searchPopup = new();
    private System.Windows.Forms.Timer? _searchDebounceTimer;

    private List<NotificationDto> _notifications = new();

    // true khi dong form do nguoi dung bam "Dang xuat" (LoginForm dua vao day de biet
    // nen hien lai man hinh dang nhap hay thoat han ung dung).
    public bool LoggedOut { get; private set; }

    public MainForm(int userId, string userFullName, string roleName)
    {
        _userId = userId;
        _roleName = roleName;

        InitializeComponent();

        sidebar.SetRole(roleName);
        headerBar.SetUser(userFullName, DisplayRoleName(roleName));
        sidebar.NavItemSelected += OnNavItemSelected;

        headerBar.NotificationClicked += ToggleNotificationPopup;
        _notificationPopup.NotificationItemClicked += OnNotificationItemClicked;
        _notificationPopup.MarkAllReadClicked += OnMarkAllReadClicked;

        headerBar.SearchTextChanged += OnSearchTextChanged;
        _searchPopup.ResultClicked += OnSearchResultClicked;
        headerBar.LogoutRequested += OnLogoutRequested;
        headerBar.ChangePasswordRequested += OnChangePasswordRequested;

        FormClosed += async (s, e) =>
        {
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();
            // 2 popup la Form rieng (khong phai con cua MainForm) nen khong tu dong dispose.
            _notificationPopup.Dispose();
            _searchPopup.Dispose();
            try { await _notificationHub.DisconnectAsync(); } catch { /* dang thoat, bo qua */ }
        };

        OnNavItemSelected(DefaultTabFor(roleName));

        Load += async (s, e) => await InitNotificationsAsync();
    }

    // Nhân viên (mọi vai trò) tự đổi mật khẩu của mình
    private void OnChangePasswordRequested()
    {
        using var dlg = new ChangePasswordDialog(
            "Đổi mật khẩu",
            requireOldPassword: true,
            (oldPw, newPw) => _api.ChangeMyPasswordAsync(oldPw, newPw));

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            MessageBox.Show(this, "Đổi mật khẩu thành công.", "Đổi mật khẩu",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // Dang xuat: xac nhan -> xoa phien -> dong MainForm. LoginForm (dang an) se duoc hien lai.
    private void OnLogoutRequested()
    {
        var confirm = MessageBox.Show(this, "Bạn có chắc muốn đăng xuất?", "Đăng xuất",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _notificationPopup.Hide();
        _searchPopup.Hide();

        LoggedOut = true;
        SessionContext.Clear(); // Token/UserId/Role ve rong -> AuthHeaderHandler khong con gan token
        Close();                // FormClosed ben tren se ngat SignalR va giai phong popup
    }

    private async Task InitNotificationsAsync()
    {
        try
        {
            _notifications = await _api.GetNotificationsAsync(_userId, _roleName);
            UpdateBadge();

            _notificationHub.OnNewNotification += noti =>
            {
                // Callback SignalR chay tren thread nen - phai Invoke ve UI thread
                // truoc khi dong bo dong (cap nhat badge/list) hoac cham vao Control.
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _notifications.Insert(0, noti);
                        UpdateBadge();
                        if (_notificationPopup.Visible) _notificationPopup.Render(_notifications);
                    }));
                }
            };

            await _notificationHub.ConnectAsync(_userId, _roleName);
        }
        catch
        {
            // Khong chan luong dang nhap/su dung app chi vi loi tai thong bao
        }
    }

    private void UpdateBadge() => headerBar.SetUnreadBadge(_notifications.Count(n => !n.IsRead));

    private void ToggleNotificationPopup()
    {
        if (_notificationPopup.Visible)
        {
            _notificationPopup.Hide();
            return;
        }

        var anchor = headerBar.GetNotifyButtonScreenLocation();
        var location = new Point(anchor.X - 340, anchor.Y + 4);
        _notificationPopup.ShowAt(location, _notifications);
    }

    private async void OnNotificationItemClicked(NotificationDto noti)
    {
        if (noti.IsRead) return;
        try
        {
            await _api.MarkNotificationReadAsync(noti.Id);
            noti.IsRead = true;
            UpdateBadge();
            _notificationPopup.Render(_notifications);
        }
        catch
        {
            // Bo qua loi mark-read, khong lam gian doan trai nghiem
        }
    }

    private async void OnMarkAllReadClicked()
    {
        try
        {
            await _api.MarkAllNotificationsReadAsync(_userId, _roleName);
            foreach (var n in _notifications) n.IsRead = true;
            UpdateBadge();
            _notificationPopup.Render(_notifications);
        }
        catch
        {
            // Bo qua loi mark-all-read
        }
    }

    // Doi 350ms sau khi nguoi dung NGUNG go moi thuc su goi API - tranh goi lien
    // tuc theo tung phim go (vd go "Trà đá" se khong goi 6 lan cho 6 ky tu).
    private void OnSearchTextChanged(string keyword)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = null;

        var trimmed = keyword.Trim();
        if (trimmed.Length < 2)
        {
            _searchPopup.Hide();
            return;
        }

        _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 350 };
        _searchDebounceTimer.Tick += async (s, e) =>
        {
            _searchDebounceTimer?.Stop();
            await RunSearchAsync(trimmed);
        };
        _searchDebounceTimer.Start();
    }

    private async Task RunSearchAsync(string keyword)
    {
        if (!_searchPopup.Visible)
        {
            var anchor = headerBar.GetSearchBoxScreenLocation();
            _searchPopup.ShowAt(new Point(anchor.X, anchor.Y + 4));
        }
        _searchPopup.ShowLoading();

        try
        {
            var results = await _api.SearchAsync(keyword, _roleName);
            _searchPopup.Render(results);
        }
        catch (Exception ex)
        {
            _searchPopup.Render(new List<SearchResultDto>());
            System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
        }
    }

    // Bam vao 1 ket qua -> dieu huong dung MAN HINH tuong ung. (Phien ban dau:
    // chi mo dung man, CHUA tu dong mo chi tiet/chon dung dong trong man do -
    // co the nang cap sau neu can trai nghiem sau hon.)
    private void OnSearchResultClicked(SearchResultDto item)
    {
        _searchPopup.Hide();

        switch (item.Type)
        {
            case "MenuItem":
                sidebar.SelectNavItem("menu");
                ShowContent("menu", null);
                break;
            case "Table":
            case "Reservation":
                sidebar.SelectNavItem("tables");
                ShowContent("tables", null);
                break;
            case "Customer":
                sidebar.SelectNavItem("customers");
                ShowContent("customers", null);
                break;
            case "Employee":
                sidebar.SelectNavItem("staff");
                ShowContent("staff", null);
                break;
            case "Order":
                sidebar.SelectNavItem("orders");
                ShowContent("orders", null);
                break;
        }
    }

    private string DefaultTabFor(string role) => role switch
    {
        "QuanLy" => "dashboard",
        "ThuNgan" => "sales",
        "PhucVu" => "sales",
        "Bep" => "kitchen",
        _ => "dashboard",
    };

    private string DisplayRoleName(string role) => role switch
    {
        "QuanLy" => "Quản lý",
        "ThuNgan" => "Thu ngân",
        "PhucVu" => "Phục vụ",
        "Bep" => "Bếp",
        _ => role,
    };

    private void OnNavItemSelected(string key) => ShowContent(key, preselectedTableId: null);

    // takeawayOrderId (chỉ dùng cho "sales"): 0 = mở chế độ Mang về để tạo đơn mới,
    // > 0 = mở đơn mang về đang mở đó để thanh toán.
    private void ShowContent(string key, int? preselectedTableId, int? takeawayOrderId = null)
    {
        // Popup là các Form nổi độc lập. Nếu giữ chúng mở trong lúc đổi màn hình,
        // người dùng có cảm giác cửa sổ/nội dung bị chồng lên nhau.
        _notificationPopup.Hide();
        _searchPopup.Hide();

        panelContent.SuspendLayout();
        try
        {
            foreach (var old in panelContent.Controls.Cast<Control>().ToList())
            {
                panelContent.Controls.Remove(old);
                if (IsHandleCreated) BeginInvoke(new Action(old.Dispose));
                else old.Dispose();
            }

            UserControl content = key switch
            {
                "dashboard" => new DashboardControl(),
                "sales" => new SalesControl(_userId, preselectedTableId, takeawayOrderId),
                "tables" => BuildDiningTabsControl(),
                "orders" => new OrderManagementControl(_userId),
                "menu" => new MenuManagementControl(),
                "customers" => new CustomerManagementControl(),
                "staff" => new StaffControl(),
                "kitchen" => new KitchenBoardControl(_userId, "Bep", "Màn hình Bếp"),
                "bar" => new KitchenBoardControl(_userId, "QuayNuoc", "Màn hình Quầy nước"),
                "revenue" => new RevenueControl(),
                "promotions" => new PromotionsControl(_userId),
                "settings" => new SettingsControl(),
                _ => new DashboardControl(),
            };

            content.Dock = DockStyle.Fill;
            panelContent.Controls.Add(content);
            content.BringToFront();
        }
        finally
        {
            panelContent.ResumeLayout(performLayout: true);
        }
    }

    // Màn "Sơ đồ bàn" gồm 2 tab: Ăn tại chỗ (sơ đồ bàn) và Mang về (danh sách đơn mang về).
    private DiningTabsControl BuildDiningTabsControl()
    {
        var ctrl = new DiningTabsControl(_userId);
        ctrl.GoToSalesRequested += tableId =>
        {
            sidebar.SelectNavItem("sales");
            ShowContent("sales", tableId);
        };
        ctrl.GoToTakeawaySalesRequested += orderId =>
        {
            sidebar.SelectNavItem("sales");
            ShowContent("sales", null, orderId);
        };
        return ctrl;
    }

}