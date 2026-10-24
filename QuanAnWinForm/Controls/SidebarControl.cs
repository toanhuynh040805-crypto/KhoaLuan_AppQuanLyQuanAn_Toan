using QuanAnWinForm.Models;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Sidebar ben trai dung chung cho toan he thong. Nhan Role cua nguoi dang
// dang nhap, tu dong an/hien muc menu tuong ung (Huong 1: 1 app duy nhat,
// khong tach rieng app Quan ly/Thu ngan - chi khac nhau menu hien ra).
public partial class SidebarControl : UserControl
{
    public event Action<string>? NavItemSelected;

    // LƯU Ý: bảng Roles trong QuanAnManagementDB_Final.sql chỉ seed đúng 4 vai trò:
    // QuanLy, ThuNgan, PhucVu, Bep (KHÔNG có vai trò "QuayNuoc" - Quầy nước chỉ là
    // một Station/bộ phận chế biến, không phải Role đăng nhập). Vì vậy màn hình
    // "Quầy nước" vẫn dùng chung Role "Bep" (nhân viên bếp có thể trực cả 2 bộ phận),
    // và nhân viên Phục vụ (PhucVu) được thêm quyền vào "Bán hàng"/"Sơ đồ bàn" để
    // gọi món cho khách - đúng nghiệp vụ thực tế của vai trò này.
    private readonly List<NavItem> _allItems = new()
    {
        new NavItem { Key = "dashboard", Label = "Tổng quan",  AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "sales",     Label = "Thanh toán",   AllowedRoles = new[] { "QuanLy", "ThuNgan", "PhucVu" } },
        new NavItem { Key = "tables",    Label = "Sơ đồ bàn",  AllowedRoles = new[] { "QuanLy", "ThuNgan", "PhucVu" } },
        new NavItem { Key = "orders",    Label = "Đơn hàng",   AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "menu",      Label = "Thực đơn",   AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "customers", Label = "Khách hàng", AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "staff",     Label = "Nhân viên",  AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "kitchen",   Label = "Bếp",        AllowedRoles = new[] { "QuanLy", "Bep" } },
        new NavItem { Key = "bar",       Label = "Quầy nước",  AllowedRoles = new[] { "QuanLy", "Bep" } },
        new NavItem { Key = "revenue",   Label = "Doanh thu",  AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "promotions", Label = "Khuyến mãi", AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "settings",  Label = "Cài đặt",    AllowedRoles = new[] { "QuanLy" } },
    };

    private readonly List<Button> _navButtons = new();
    private string _role = "QuanLy";
    private string _stationName = "";

    public SidebarControl()
    {
        InitializeComponent();
        WireUpEvents();
        BuildNavButtons();
    }

    private void WireUpEvents()
    {
        panelLogo.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold);
            e.Graphics.DrawString("G", font, brush, 10, 6);
        };
    }

    public void SetRole(string role, string? stationName = null)
    {
        _role = role;
        _stationName = stationName?.Trim() ?? "";
        flowNavItems.Controls.Clear();
        _navButtons.Clear();
        BuildNavButtons();
    }

    private void BuildNavButtons()
    {
        var visibleItems = _allItems.Where(i => i.IsVisibleFor(_role)).ToList();

        // Bếp và Quầy nước là hai Station của cùng Role Bep.
        // Nhân viên chế biến chỉ thấy đúng màn Station đã được phân công;
        // Quản lý vẫn thấy cả hai để giám sát.
        if (string.Equals(_role, "Bep", StringComparison.OrdinalIgnoreCase))
        {
            var isBar = string.Equals(_stationName, "QuayNuoc", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(_stationName, "Quầy nước", StringComparison.OrdinalIgnoreCase);
            var isKitchen = string.Equals(_stationName, "Bep", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(_stationName, "Bếp", StringComparison.OrdinalIgnoreCase);

            visibleItems = visibleItems
                .Where(i =>
                    (i.Key == "bar" && isBar) ||
                    (i.Key == "kitchen" && isKitchen))
                .ToList();
        }

        foreach (var item in visibleItems)
        {
            var btn = new Button
            {
                Text = "   " + item.Label,
                Tag = item.Key,
                Width = 200,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Font = AppFonts.LabelLg,
                ForeColor = AppColors.SidebarText,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 4),
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AppColors.SurfaceLow;
            btn.Click += NavButton_Click;

            _navButtons.Add(btn);
            flowNavItems.Controls.Add(btn);
        }

        if (visibleItems.Count > 0) SelectItem(visibleItems[0].Key);
    }

    public void SelectNavItem(string key) => SelectItem(key);

    private void NavButton_Click(object? sender, EventArgs e)
    {
        var key = (string)((Button)sender!).Tag!;
        SelectItem(key);
        NavItemSelected?.Invoke(key);
    }

    private void SelectItem(string key)
    {
        foreach (var btn in _navButtons)
        {
            var isSelected = (string)btn.Tag! == key;
            btn.BackColor = isSelected ? AppColors.SidebarSelectedBg : Color.Transparent;
            btn.ForeColor = isSelected ? Color.White : AppColors.SidebarText;
        }
    }
}