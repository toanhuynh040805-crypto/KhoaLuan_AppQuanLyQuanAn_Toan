using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Nhan vien - chi danh cho Role QuanLy.
// TODO: hien dang dung du lieu mau. Backend hien CHUA co endpoint
// GET/POST/PUT /api/users - can bo sung truoc khi noi that.
public partial class StaffControl : UserControl
{
    private record StaffSample(string Name, string Role, string Phone, string Email, string Code, bool IsWorking);

    private readonly List<StaffSample> _sampleStaff = new()
    {
        new("Nguyễn Văn A", "Quản lý", "0901 234 567", "nva.manager@gusto.vn", "EMP-001", true),
        new("Trần Thị B", "Thu ngân", "0988 765 432", "ttb.cashier@gusto.vn", "EMP-012", false),
        new("Lê Hoàng C", "Bếp trưởng", "0912 345 678", "lhc.chef@gusto.vn", "EMP-025", true),
    };

    public StaffControl()
    {
        InitializeComponent();
        RenderStaffCards();
    }

    private void RenderStaffCards()
    {
        flowStaffCards.Controls.Clear();
        foreach (var staff in _sampleStaff)
        {
            flowStaffCards.Controls.Add(BuildStaffCard(staff));
        }
        flowStaffCards.Controls.Add(BuildAddNewTile());
    }

    private Panel BuildStaffCard(StaffSample s)
    {
        var card = new Panel { Size = new Size(230, 220), BackColor = Color.White, Margin = new Padding(8) };

        var avatar = new Panel { Size = new Size(48, 48), Location = new Point(16, 16), BackColor = AppColors.SecondaryContainer };
        var lblName = new Label { Text = s.Name, Font = AppFonts.LabelLg, Location = new Point(74, 14), Size = new Size(150, 36) };
        var lblRole = new Label
        {
            Text = s.Role.ToUpper(), Font = AppFonts.LabelMd, ForeColor = AppColors.Primary,
            BackColor = AppColors.ErrorContainer, Location = new Point(74, 48), AutoSize = true, Padding = new Padding(6, 2, 6, 2),
        };

        var lblPhone = new Label { Text = "☎ " + s.Phone, Font = AppFonts.BodyMd, Location = new Point(16, 84), AutoSize = true };
        var lblEmail = new Label { Text = "✉ " + s.Email, Font = AppFonts.BodyMd, Location = new Point(16, 106), AutoSize = true };
        var lblCode = new Label { Text = "🪪 " + s.Code, Font = AppFonts.BodyMd, Location = new Point(16, 128), AutoSize = true };

        var dot = new Panel { Size = new Size(8, 8), Location = new Point(16, 168), BackColor = s.IsWorking ? AppColors.Success : AppColors.Outline };
        var lblStatus = new Label { Text = s.IsWorking ? "Đang ca làm" : "Nghỉ ngơi", Font = AppFonts.BodyMd, Location = new Point(30, 162), AutoSize = true };
        var lblPerm = new Label { Text = "Phân quyền", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(150, 162), AutoSize = true, Cursor = Cursors.Hand };

        card.Controls.Add(avatar);
        card.Controls.Add(lblName);
        card.Controls.Add(lblRole);
        card.Controls.Add(lblPhone);
        card.Controls.Add(lblEmail);
        card.Controls.Add(lblCode);
        card.Controls.Add(dot);
        card.Controls.Add(lblStatus);
        card.Controls.Add(lblPerm);
        card.Paint += (sender, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, card.Width - 1, card.Height - 1);

        return card;
    }

    private Panel BuildAddNewTile()
    {
        var tile = new Panel { Size = new Size(230, 220), BackColor = Color.FromArgb(253, 245, 242), Margin = new Padding(8), Cursor = Cursors.Hand };
        var lblPlus = new Label { Text = "+", Font = new Font("Segoe UI", 20), ForeColor = AppColors.Primary, Location = new Point(100, 70), AutoSize = true };
        var lblText = new Label { Text = "Thêm nhân sự mới", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(48, 110), AutoSize = true };
        tile.Controls.Add(lblPlus);
        tile.Controls.Add(lblText);
        // TODO: mo Form them nhan vien, goi POST /api/users khi Backend co endpoint
        tile.Click += (s, e) => MessageBox.Show("Mở form Thêm nhân viên mới (chưa nối API)");
        return tile;
    }
}
