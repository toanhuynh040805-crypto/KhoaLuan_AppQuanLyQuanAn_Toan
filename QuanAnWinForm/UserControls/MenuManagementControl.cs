using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Thuc don (CRUD mon an) - chi danh cho Role QuanLy.
// TODO: hien GET da noi that (/api/menu), nhung Them/Sua/Xoa (POST/PUT/DELETE)
// CHUA co endpoint tuong ung ben Backend - can bo sung truoc khi dung that.
public partial class MenuManagementControl : UserControl
{
    private readonly ApiService _api = new();

    public MenuManagementControl()
    {
        InitializeComponent();
        btnAddNew.Click += (s, e) => OpenEditDialog(null);
        Load += async (s, e) => await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        try
        {
            var items = await _api.GetMenuAsync();
            RenderGrid(items);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được thực đơn: {ex.Message}");
        }
    }

    private void RenderGrid(List<MenuItemDto> items)
    {
        flowMenuCards.Controls.Clear();
        foreach (var item in items)
        {
            flowMenuCards.Controls.Add(BuildMenuCard(item));
        }
        flowMenuCards.Controls.Add(BuildAddNewTile());
    }

    private Panel BuildMenuCard(MenuItemDto item)
    {
        var card = new Panel { Size = new Size(220, 260), BackColor = Color.White, Margin = new Padding(8) };
        var thumb = new Panel { Size = new Size(220, 140), Location = new Point(0, 0), BackColor = AppColors.SurfaceContainer };

        var badge = new Label
        {
            Text = item.IsAvailable ? "Còn hàng" : "Hết hàng",
            Font = AppFonts.LabelMd,
            ForeColor = Color.White,
            BackColor = item.IsAvailable ? AppColors.Primary : AppColors.Outline,
            Location = new Point(140, 10),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
        };
        thumb.Controls.Add(badge);

        var lblName = new Label { Text = item.Name, Font = AppFonts.LabelLg, Location = new Point(12, 152), AutoSize = true };
        var lblPrice = new Label { Text = $"{item.Price:N0}đ", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(160, 152), AutoSize = true };
        var btnEdit = new Button { Text = "Sửa", Location = new Point(12, 210), Size = new Size(90, 32), FlatStyle = FlatStyle.Flat };
        var btnDelete = new Button { Text = "Xóa", Location = new Point(112, 210), Size = new Size(90, 32), FlatStyle = FlatStyle.Flat, ForeColor = AppColors.Error };

        btnEdit.Click += (s, e) => OpenEditDialog(item);
        btnDelete.Click += (s, e) => ConfirmDelete(item);

        card.Controls.Add(thumb);
        card.Controls.Add(lblName);
        card.Controls.Add(lblPrice);
        card.Controls.Add(btnEdit);
        card.Controls.Add(btnDelete);
        card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, card.Width - 1, card.Height - 1);
        return card;
    }

    private Panel BuildAddNewTile()
    {
        var tile = new Panel { Size = new Size(220, 260), BackColor = Color.FromArgb(253, 245, 242), Margin = new Padding(8), Cursor = Cursors.Hand };
        var lblPlus = new Label { Text = "+", Font = new Font("Segoe UI", 24), ForeColor = AppColors.Primary, Location = new Point(95, 90), AutoSize = true };
        var lblText = new Label { Text = "Thêm món ăn mới", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(55, 140), AutoSize = true };
        tile.Controls.Add(lblPlus);
        tile.Controls.Add(lblText);
        tile.Click += (s, e) => OpenEditDialog(null);
        return tile;
    }

    private void OpenEditDialog(MenuItemDto? item)
    {
        // TODO: thay bang 1 Form nhap lieu day du (Ten, Gia, Danh muc, Bo phan, Anh, Trang thai)
        // roi goi POST/PUT /api/menu khi Backend co endpoint tuong ung.
        MessageBox.Show(item is null ? "Mở form Thêm món mới (chưa nối API)" : $"Mở form Sửa món: {item.Name} (chưa nối API)");
    }

    private void ConfirmDelete(MenuItemDto item)
    {
        var result = MessageBox.Show($"Xóa món \"{item.Name}\"?", "Xác nhận", MessageBoxButtons.YesNo);
        if (result == DialogResult.Yes)
        {
            // TODO: goi DELETE /api/menu/{id} khi co endpoint
            MessageBox.Show("Đã xóa (demo - chưa nối API)");
        }
    }
}
