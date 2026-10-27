using System.Globalization;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Cai dat he thong - chi danh cho Role QuanLy.
// TODO: hien chua luu duoc gi ca (chi la UI). Can thiet ke bang cau hinh
// rieng (VD: Settings key-value) ben Backend de luu that.
public partial class SettingsControl : UserControl
{
    private readonly ApiService _api = new();
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
        AddTabButton("attendance", "Vị trí điểm danh");
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
            var btnSave = new Button { Text = "Lưu thay đổi", Location = new Point(600, 16), Size = new Size(140, 36), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => MessageBox.Show("Thông tin nhà hàng hiện chưa có API lưu riêng.");

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
        else if (key == "attendance")
        {
            BuildAttendanceSettingsContent(panel);
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

    private void BuildAttendanceSettingsContent(Panel panel)
    {
        var lblHeader = new Label
        {
            Text = "Vị trí điểm danh",
            Font = AppFonts.HeadlineSm,
            Location = new Point(24, 20),
            AutoSize = true,
        };

        var lblSub = new Label
        {
            Text = "Dùng chung với Mobile. Nhân viên chỉ được điểm danh trong bán kính cấu hình quanh tọa độ này.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(24, 52),
            Size = new Size(690, 42),
        };

        var lblState = new Label
        {
            Text = "Đang tải cấu hình...",
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(24, 94),
            AutoSize = true,
        };

        var lblLat = MakeLabel("Vĩ độ (Latitude) *", 24, 132);
        var txtLat = MakeInput("", 24, 154, 250);

        var lblLng = MakeLabel("Kinh độ (Longitude) *", 300, 132);
        var txtLng = MakeInput("", 300, 154, 250);

        var lblRadius = MakeLabel("Bán kính điểm danh (m)", 24, 202);
        var txtRadius = MakeInput("100", 24, 224, 250);

        var lblAccuracy = MakeLabel("Sai số GPS tối đa (m)", 300, 202);
        var txtAccuracy = MakeInput("50", 300, 224, 250);

        var lblHint = new Label
        {
            Text = "Khuyến nghị: bán kính 50-100 m, sai số GPS 30-50 m. " +
                   "Mobile Admin có thể lấy GPS hiện tại và lưu vào cùng cấu hình này.",
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(24, 274),
            Size = new Size(690, 48),
        };

        var btnReload = new Button
        {
            Text = "Tải lại",
            Location = new Point(430, 330),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = AppColors.Primary,
        };
        btnReload.FlatAppearance.BorderColor = AppColors.OutlineVariant;

        var btnSave = new Button
        {
            Text = "Lưu vị trí",
            Location = new Point(570, 330),
            Size = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppColors.Primary,
            ForeColor = Color.White,
        };
        btnSave.FlatAppearance.BorderSize = 0;

        panel.Controls.AddRange(new Control[]
        {
            lblHeader,
            lblSub,
            lblState,
            lblLat,
            txtLat,
            lblLng,
            txtLng,
            lblRadius,
            txtRadius,
            lblAccuracy,
            txtAccuracy,
            lblHint,
            btnReload,
            btnSave,
        });

        async Task LoadAsync()
        {
            try
            {
                btnReload.Enabled = false;
                btnSave.Enabled = false;
                lblState.Text = "Đang tải cấu hình...";
                lblState.ForeColor = AppColors.OnSurfaceVariant;

                var settings = await _api.GetAttendanceLocationSettingsAsync();

                if (settings is null)
                    throw new InvalidOperationException("Backend không trả về cấu hình vị trí.");

                txtLat.Text = settings.Latitude?.ToString(
                    "0.000000",
                    CultureInfo.InvariantCulture) ?? "";

                txtLng.Text = settings.Longitude?.ToString(
                    "0.000000",
                    CultureInfo.InvariantCulture) ?? "";

                txtRadius.Text = settings.RadiusMeters
                    .ToString("0.##", CultureInfo.InvariantCulture);

                txtAccuracy.Text = settings.MaxAccuracyMeters
                    .ToString("0.##", CultureInfo.InvariantCulture);

                lblState.Text = settings.IsConfigured
                    ? $"Đang áp dụng · cập nhật {FormatUpdatedAt(settings.UpdatedAt)}"
                    : "Chưa thiết lập vị trí quán";

                lblState.ForeColor = settings.IsConfigured
                    ? AppColors.Success
                    : AppColors.Warning;
            }
            catch (Exception ex)
            {
                lblState.Text = $"Không tải được cấu hình: {ex.Message}";
                lblState.ForeColor = AppColors.Error;
            }
            finally
            {
                btnReload.Enabled = true;
                btnSave.Enabled = true;
            }
        }

        btnReload.Click += async (s, e) => await LoadAsync();

        btnSave.Click += async (s, e) =>
        {
            if (!TryParseDouble(txtLat.Text, out var latitude) ||
                latitude < -90 ||
                latitude > 90)
            {
                MessageBox.Show("Vĩ độ không hợp lệ (-90 đến 90).");
                txtLat.Focus();
                return;
            }

            if (!TryParseDouble(txtLng.Text, out var longitude) ||
                longitude < -180 ||
                longitude > 180)
            {
                MessageBox.Show("Kinh độ không hợp lệ (-180 đến 180).");
                txtLng.Focus();
                return;
            }

            if (!TryParseDouble(txtRadius.Text, out var radius) ||
                radius < 10 ||
                radius > 1000)
            {
                MessageBox.Show("Bán kính điểm danh phải từ 10 đến 1000 mét.");
                txtRadius.Focus();
                return;
            }

            if (!TryParseDouble(txtAccuracy.Text, out var accuracy) ||
                accuracy < 5 ||
                accuracy > 200)
            {
                MessageBox.Show("Sai số GPS tối đa phải từ 5 đến 200 mét.");
                txtAccuracy.Focus();
                return;
            }

            try
            {
                btnSave.Enabled = false;

                var saved = await _api.UpdateAttendanceLocationSettingsAsync(
                    latitude,
                    longitude,
                    radius,
                    accuracy);

                if (saved is not null)
                {
                    lblState.Text =
                        $"Đang áp dụng · cập nhật {FormatUpdatedAt(saved.UpdatedAt)}";
                    lblState.ForeColor = AppColors.Success;
                }

                MessageBox.Show(
                    "Đã lưu vị trí điểm danh. Mobile và WinForms đang dùng chung cấu hình này.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không lưu được vị trí điểm danh: {ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        };

        _ = LoadAsync();
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(
                   text.Trim(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value)
               || double.TryParse(
                   text.Trim(),
                   NumberStyles.Float,
                   CultureInfo.CurrentCulture,
                   out value);
    }

    private static string FormatUpdatedAt(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd/MM/yyyy HH:mm")
            : "chưa rõ thời gian";
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
