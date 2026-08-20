using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Cai dat he thong - chi danh cho Role QuanLy.
// TODO: hien chua luu duoc gi ca (chi la UI). Can thiet ke bang cau hinh
// rieng (VD: Settings key-value) ben Backend de luu that.
public partial class SettingsControl : UserControl
{
    private readonly List<(string Key, Panel Panel)> _tabPanels = new();

    public SettingsControl()
    {
        InitializeComponent();
        BuildTabButtons();
        ShowTab("restaurant");
    }

    private void BuildTabButtons()
    {
        AddTabButton("restaurant", "Thông tin nhà hàng");
        AddTabButton("printer", "Máy in hóa đơn");
        AddTabButton("vat", "Thiết lập VAT");
        AddTabButton("branch", "Quản lý chi nhánh");
        AddTabButton("admin", "Tài khoản admin");
    }

    private void AddTabButton(string key, string label)
    {
        var btn = new Button
        {
            Text = label,
            Width = 220,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.LabelLg,
            Tag = key,
            Margin = new Padding(0, 0, 0, 4),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => ShowTab(key);
        flowSettingsTabs.Controls.Add(btn);
    }

    private void ShowTab(string key)
    {
        foreach (Button btn in flowSettingsTabs.Controls)
        {
            var isSelected = (string)btn.Tag! == key;
            btn.BackColor = isSelected ? AppColors.ErrorContainer : Color.Transparent;
            btn.ForeColor = isSelected ? AppColors.Primary : AppColors.OnSurface;
        }

        panelSettingsContent.Controls.Clear();
        panelSettingsContent.Controls.Add(BuildContentFor(key));
    }

    private Panel BuildContentFor(string key)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24) };

        if (key == "restaurant")
        {
            var lblHeader = new Label { Text = "Thông tin nhà hàng", Font = AppFonts.HeadlineSm, Location = new Point(24, 20), AutoSize = true };
            var lblSub = new Label { Text = "Cập nhật thông tin cơ bản hiển thị trên hóa đơn và hệ thống.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(24, 48), AutoSize = true };
            var btnSave = new Button { Text = "Lưu thay đổi", Location = new Point(880, 16), Size = new Size(140, 36), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
            btnSave.FlatAppearance.BorderSize = 0;
            // TODO: goi API luu cau hinh khi Backend co endpoint
            btnSave.Click += (s, e) => MessageBox.Show("Đã lưu (demo - chưa nối API)");

            var lblName = MakeLabel("Tên nhà hàng *", 24, 90);
            var txtName = MakeInput("GustoPOS - Chi nhánh Trung tâm", 24, 112, 500);
            var lblPhone = MakeLabel("Số điện thoại liên hệ", 24, 156);
            var txtPhone = MakeInput("090 123 4567", 24, 178, 240);
            var lblEmail = MakeLabel("Email quản lý", 300, 156);
            var txtEmail = MakeInput("admin@gustopos.vn", 300, 178, 240);
            var lblAddress = MakeLabel("Địa chỉ chi tiết", 24, 222);
            var txtAddress = MakeInput("123 Đường Lê Lợi, Phường Bến Thành, Quận 1, TP.HCM", 24, 244, 500);

            panel.Controls.AddRange(new Control[] { lblHeader, lblSub, btnSave, lblName, txtName, lblPhone, txtPhone, lblEmail, txtEmail, lblAddress, txtAddress });
        }
        else
        {
            var lblPlaceholder = new Label
            {
                Text = "Mục cài đặt này chưa được triển khai chi tiết (demo).",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.OnSurfaceVariant,
                Location = new Point(24, 20),
                AutoSize = true,
            };
            panel.Controls.Add(lblPlaceholder);
        }

        return panel;
    }

    private Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text, Font = AppFonts.LabelLg, Location = new Point(x, y), AutoSize = true,
    };

    private TextBox MakeInput(string text, int x, int y, int width) => new()
    {
        Text = text, Location = new Point(x, y), Width = width, Height = 36, Font = AppFonts.BodyMd, BorderStyle = BorderStyle.FixedSingle,
    };
}
