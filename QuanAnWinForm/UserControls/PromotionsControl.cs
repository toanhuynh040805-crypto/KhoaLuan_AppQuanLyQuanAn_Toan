using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh "Khuyen mai" - danh cho Quan ly tao/sua/tat cac chien dich giam gia
// theo % ap dung chung cho moi khach (xem ghi chu pham vi o DiscountCampaign.cs
// ben Backend: CHUA ho tro dieu kien "khach quen" vi chua co module Khach hang).
// Chien dich dang hieu luc se xuat hien trong ComboBox chon khuyen mai luc thanh
// toan o SalesControl (GET /api/discountcampaigns/active).
public partial class PromotionsControl : UserControl
{
    private readonly ApiService _api = new();
    private readonly int _userId;
    private List<DiscountCampaignDto> _campaigns = new();

    public PromotionsControl() : this(0) { }

    public PromotionsControl(int userId)
    {
        _userId = userId;
        InitializeComponent();
        Load += async (s, e) => await LoadCampaignsAsync();
        btnAddCampaign.Click += (s, e) => OpenAddOrEditCampaignDialog(null);
    }

    private async Task LoadCampaignsAsync()
    {
        try
        {
            _campaigns = await _api.GetCampaignsAsync();
            RenderCampaignList();
        }
        catch (Exception ex)
        {
            flowCampaigns.Controls.Clear();
            flowCampaigns.Controls.Add(new Label { Text = $"Không tải được danh sách khuyến mãi: {ex.Message}", Font = AppFonts.BodyMd, ForeColor = AppColors.Error, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
        }
    }

    private void RenderCampaignList()
    {
        flowCampaigns.Controls.Clear();

        if (_campaigns.Count == 0)
        {
            flowCampaigns.Controls.Add(new Label { Text = "Chưa có chiến dịch khuyến mãi nào. Bấm \"+ Thêm chiến dịch\" để tạo mới.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true, Margin = new Padding(4, 12, 0, 0) });
            return;
        }

        foreach (var c in _campaigns.OrderByDescending(c => c.StartDate))
            flowCampaigns.Controls.Add(BuildCampaignRow(c));
    }

    // Trang thai hien thi: Sap dien ra (chua toi StartDate) / Dang dien ra (trong
    // khoang ngay VA IsActive) / Da het han (qua EndDate) / Da tat (IsActive=false).
    private static (string Label, Color Color) CampaignRuntimeStatus(DiscountCampaignDto c)
    {
        if (!c.IsActive) return ("Đã tắt", AppColors.Outline);
        var now = DateTime.Now;
        if (now < c.StartDate) return ("Sắp diễn ra", AppColors.Tertiary);
        if (now > c.EndDate) return ("Đã hết hạn", AppColors.Outline);
        return ("Đang diễn ra", AppColors.Success);
    }

    private Panel BuildCampaignRow(DiscountCampaignDto c)
    {
        var row = new Panel { Size = new Size(860, 92), BackColor = Color.White, Margin = new Padding(0, 0, 0, 10) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var (statusLabel, statusColor) = CampaignRuntimeStatus(c);
        var stripe = new Panel { Size = new Size(4, 92), Location = new Point(0, 0), BackColor = statusColor };

        var lblName = new Label { Text = c.CampaignName, Font = AppFonts.LabelLg, Location = new Point(16, 10), AutoSize = true };
        var lblPercent = new Label { Text = $"-{c.DiscountPercent:0.#}%", Font = AppFonts.HeadlineSm, ForeColor = AppColors.Primary, Location = new Point(16, 34), AutoSize = true };
        var lblDate = new Label { Text = $"{c.StartDate:dd/MM/yyyy} → {c.EndDate:dd/MM/yyyy}", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(120, 40), AutoSize = true };
        var lblStatus = new Label { Text = statusLabel, Font = AppFonts.LabelMd, ForeColor = statusColor, Location = new Point(16, 66), AutoSize = true };

        row.Controls.Add(stripe);
        row.Controls.Add(lblName);
        row.Controls.Add(lblPercent);
        row.Controls.Add(lblDate);
        row.Controls.Add(lblStatus);

        if (!string.IsNullOrWhiteSpace(c.Note))
        {
            var lblNote = new Label { Text = c.Note, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(120, 62), Size = new Size(340, 20), AutoEllipsis = true };
            row.Controls.Add(lblNote);
        }

        var btnEdit = new Button { Text = "Sửa", Location = new Point(600, 30), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White, Font = AppFonts.LabelMd };
        btnEdit.FlatAppearance.BorderSize = 0;
        btnEdit.Click += (s, e) => OpenAddOrEditCampaignDialog(c);
        row.Controls.Add(btnEdit);

        var toggleText = c.IsActive ? "Tắt" : "Bật lại";
        var btnToggle = new Button { Text = toggleText, Location = new Point(690, 30), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat, BackColor = c.IsActive ? AppColors.Error : AppColors.Success, ForeColor = Color.White, Font = AppFonts.LabelMd };
        btnToggle.FlatAppearance.BorderSize = 0;
        btnToggle.Click += async (s, e) =>
        {
            btnToggle.Enabled = false;
            try
            {
                await _api.SetCampaignActiveAsync(c.Id, !c.IsActive);
                await LoadCampaignsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không cập nhật được: {ex.Message}");
                btnToggle.Enabled = true;
            }
        };
        row.Controls.Add(btnToggle);

        var btnDelete = new Button { Text = "Xóa", Location = new Point(780, 30), Size = new Size(66, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.OutlineVariant, ForeColor = AppColors.OnSurface, Font = AppFonts.LabelMd };
        btnDelete.FlatAppearance.BorderSize = 0;
        btnDelete.Click += async (s, e) =>
        {
            if (MessageBox.Show($"Xóa chiến dịch \"{c.CampaignName}\"? Các hóa đơn đã áp dụng chiến dịch này trước đây vẫn giữ nguyên số tiền đã giảm.", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                await _api.DeleteCampaignAsync(c.Id);
                await LoadCampaignsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không xóa được: {ex.Message}");
            }
        };
        row.Controls.Add(btnDelete);

        return row;
    }

    // Dung 1 dialog cho ca Them moi (campaign == null) va Sua (campaign != null),
    // giong pattern da dung o TableManagementControl/StaffControl.
    private void OpenAddOrEditCampaignDialog(DiscountCampaignDto? campaign)
    {
        var isEdit = campaign is not null;
        using var dialog = new Form
        {
            Text = isEdit ? "Sửa chiến dịch khuyến mãi" : "Thêm chiến dịch khuyến mãi",
            Width = 380,
            Height = 420,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblName = new Label { Text = "Tên chiến dịch *", Font = AppFonts.BodyMd, Location = new Point(20, 16), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 36), Width = 320, Font = AppFonts.BodyMd, Text = campaign?.CampaignName ?? "" };

        var lblStart = new Label { Text = "Bắt đầu *", Font = AppFonts.BodyMd, Location = new Point(20, 76), AutoSize = true };
        var dtpStart = new DateTimePicker { Location = new Point(20, 96), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.StartDate ?? DateTime.Today };

        var lblEnd = new Label { Text = "Kết thúc *", Font = AppFonts.BodyMd, Location = new Point(190, 76), AutoSize = true };
        var dtpEnd = new DateTimePicker { Location = new Point(190, 96), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = campaign?.EndDate ?? DateTime.Today.AddDays(7) };

        var lblPercent = new Label { Text = "Phần trăm giảm (%) *", Font = AppFonts.BodyMd, Location = new Point(20, 136), AutoSize = true };
        var numPercent = new NumericUpDown { Location = new Point(20, 156), Width = 150, Minimum = 0, Maximum = 100, DecimalPlaces = 2, Value = campaign?.DiscountPercent ?? 10 };

        var lblNote = new Label { Text = "Ghi chú", Font = AppFonts.BodyMd, Location = new Point(20, 196), AutoSize = true };
        var txtNote = new TextBox { Location = new Point(20, 216), Width = 320, Height = 60, Font = AppFonts.BodyMd, Multiline = true, Text = campaign?.Note ?? "" };

        if (campaign is { RequireMembership: true })
        {
            var lblWarn = new Label
            {
                Text = "⚠ Chiến dịch này yêu cầu điều kiện khách quen - hệ thống hiện chưa hỗ trợ sửa loại này qua giao diện.",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Error,
                Location = new Point(20, 284),
                Size = new Size(320, 40),
            };
            dialog.Controls.Add(lblWarn);
        }

        dialog.Controls.Add(lblName);
        dialog.Controls.Add(txtName);
        dialog.Controls.Add(lblStart);
        dialog.Controls.Add(dtpStart);
        dialog.Controls.Add(lblEnd);
        dialog.Controls.Add(dtpEnd);
        dialog.Controls.Add(lblPercent);
        dialog.Controls.Add(numPercent);
        dialog.Controls.Add(lblNote);
        dialog.Controls.Add(txtNote);

        var btnSave = new Button { Text = isEdit ? "Lưu" : "Tạo chiến dịch", Location = new Point(110, 330), Size = new Size(150, 34), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        var name = txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập tên chiến dịch.");
            return;
        }
        if (dtpEnd.Value.Date <= dtpStart.Value.Date)
        {
            MessageBox.Show("Ngày kết thúc phải sau ngày bắt đầu.");
            return;
        }

        var note = string.IsNullOrWhiteSpace(txtNote.Text) ? null : txtNote.Text.Trim();

        if (isEdit)
            _ = UpdateCampaignAsync(campaign!.Id, name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note);
        else
            _ = CreateCampaignAsync(name, dtpStart.Value.Date, dtpEnd.Value.Date, numPercent.Value, note);
    }

    private async Task CreateCampaignAsync(string name, DateTime start, DateTime end, decimal percent, string? note)
    {
        try
        {
            await _api.CreateCampaignAsync(name, start, end, percent, note, _userId);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tạo được chiến dịch: {ex.Message}");
        }
    }

    private async Task UpdateCampaignAsync(int id, string name, DateTime start, DateTime end, decimal percent, string? note)
    {
        try
        {
            await _api.UpdateCampaignAsync(id, name, start, end, percent, note);
            await LoadCampaignsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được: {ex.Message}");
        }
    }
}