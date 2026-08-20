using QuanAnWinForm.UserControls;

namespace QuanAnWinForm.Forms;

// Shell chinh sau dang nhap: Sidebar + Header + vung noi dung tu doi UserControl.
public partial class MainForm : Form
{
    private readonly int _userId;
    private readonly string _roleName;

    public MainForm(int userId, string userFullName, string roleName)
    {
        _userId = userId;
        _roleName = roleName;

        InitializeComponent();

        sidebar.SetRole(roleName);
        headerBar.SetUser(userFullName, DisplayRoleName(roleName));
        sidebar.NavItemSelected += OnNavItemSelected;

        OnNavItemSelected(DefaultTabFor(roleName));
    }

    private string DefaultTabFor(string role) => role switch
    {
        "QuanLy" => "dashboard",
        "ThuNgan" => "sales",
        "Bep" => "kitchen",
        "QuayNuoc" => "bar",
        _ => "dashboard",
    };

    private string DisplayRoleName(string role) => role switch
    {
        "QuanLy" => "Quản lý",
        "ThuNgan" => "Thu ngân",
        "Bep" => "Bếp",
        "QuayNuoc" => "Quầy nước",
        _ => role,
    };

    private void OnNavItemSelected(string key)
    {
        panelContent.Controls.Clear();

        UserControl content = key switch
        {
            "dashboard" => new DashboardControl(),
            "sales" => new SalesControl(_userId),
            "tables" => new TableManagementControl(),
            "menu" => new MenuManagementControl(),
            "staff" => new StaffControl(),
            "kitchen" => new KitchenBoardControl("Bep", "Màn hình Bếp"),
            "bar" => new KitchenBoardControl("QuayNuoc", "Màn hình Quầy nước"),
            "revenue" => new RevenueControl(),
            "settings" => new SettingsControl(),
            _ => new DashboardControl(),
        };

        content.Dock = DockStyle.Fill;
        panelContent.Controls.Add(content);
    }
}
