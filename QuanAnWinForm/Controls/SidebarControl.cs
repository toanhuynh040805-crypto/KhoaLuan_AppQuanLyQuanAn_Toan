using QuanAnWinForm.Models;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Sidebar ben trai dung chung cho toan he thong. Nhan Role cua nguoi dang
// dang nhap, tu dong an/hien muc menu tuong ung (Huong 1: 1 app duy nhat,
// khong tach rieng app Quan ly/Thu ngan - chi khac nhau menu hien ra).
public partial class SidebarControl : UserControl
{
    public event Action<string>? NavItemSelected;

    private readonly List<NavItem> _allItems = new()
    {
        new NavItem { Key = "dashboard", Label = "Tổng quan",  AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "sales",     Label = "Bán hàng",   AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "tables",    Label = "Sơ đồ bàn",  AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "menu",      Label = "Thực đơn",   AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "staff",     Label = "Nhân viên",  AllowedRoles = new[] { "QuanLy" } },
        new NavItem { Key = "kitchen",   Label = "Bếp",        AllowedRoles = new[] { "QuanLy", "Bep" } },
        new NavItem { Key = "bar",       Label = "Quầy nước",  AllowedRoles = new[] { "QuanLy", "QuayNuoc" } },
        new NavItem { Key = "revenue",   Label = "Doanh thu",  AllowedRoles = new[] { "QuanLy", "ThuNgan" } },
        new NavItem { Key = "settings",  Label = "Cài đặt",    AllowedRoles = new[] { "QuanLy" } },
    };

    private readonly List<Button> _navButtons = new();
    private string _role = "QuanLy";

    public SidebarControl()
    {
        InitializeComponent();
        WireUpEvents();
        BuildNavButtons();
    }

    // Xem giai thich chi tiet trong DashboardControl.cs: lambda khong duoc dat trong .Designer.cs.
    private void WireUpEvents()
    {
        panelLogo.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold);
            e.Graphics.DrawString("G", font, brush, 10, 6);
        };
    }

    // Goi lai tu MainForm sau khi biet Role thuc su cua nguoi dang dang nhap
    public void SetRole(string role)
    {
        _role = role;
        flowNavItems.Controls.Clear();
        _navButtons.Clear();
        BuildNavButtons();
    }

    private void BuildNavButtons()
    {
        var visibleItems = _allItems.Where(i => i.IsVisibleFor(_role)).ToList();

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
