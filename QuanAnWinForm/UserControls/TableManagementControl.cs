using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly so do ban - cho phep Quan ly/Thu ngan xem tong quan
// tat ca ban, mau sac theo trang thai, bam vao xem chi tiet ben phai.
public partial class TableManagementControl : UserControl
{
    private readonly ApiService _api = new();
    private List<TableDto> _tables = new();

    public TableManagementControl()
    {
        InitializeComponent();
        Load += async (s, e) => await LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        try
        {
            _tables = await _api.GetTablesAsync();
            RenderTableGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được sơ đồ bàn: {ex.Message}");
        }
    }

    private void RenderTableGrid()
    {
        flowTables.Controls.Clear();
        foreach (var table in _tables)
        {
            flowTables.Controls.Add(BuildTableTile(table));
        }
    }

    private (Color border, Color bg, string label) StyleFor(int status) => status switch
    {
        1 => (AppColors.TableCoKhach, Color.White, "Có khách"),
        2 => (AppColors.Warning, Color.White, "Đã đặt"),
        _ => (AppColors.TableTrong, Color.FromArgb(235, 247, 243), "Trống"),
    };

    private Panel BuildTableTile(TableDto table)
    {
        var (border, bg, label) = StyleFor(table.Status);
        var tile = new Panel
        {
            Size = new Size(140, 110),
            BackColor = bg,
            Margin = new Padding(8),
            Cursor = Cursors.Hand,
            Tag = table,
        };
        tile.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(border, 2), 1, 1, tile.Width - 3, tile.Height - 3);

        var lblName = new Label { Text = table.TableName, Font = AppFonts.HeadlineSm, Location = new Point(12, 10), AutoSize = true };
        var lblCapacity = new Label { Text = $"{table.Capacity} khách", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(12, 36), AutoSize = true };
        var lblStatus = new Label { Text = label, Font = AppFonts.LabelMd, ForeColor = border, Location = new Point(12, 78), AutoSize = true };

        tile.Controls.Add(lblName);
        tile.Controls.Add(lblCapacity);
        tile.Controls.Add(lblStatus);

        void onClick(object? s, EventArgs e) => ShowDetail(table);
        tile.Click += onClick;
        lblName.Click += onClick;
        lblCapacity.Click += onClick;
        lblStatus.Click += onClick;

        return tile;
    }

    private void ShowDetail(TableDto table)
    {
        lblDetailName.Text = table.TableName;
        lblDetailInfo.Text = $"Sức chứa: {table.Capacity} khách";
        var (_, _, label) = StyleFor(table.Status);
        lblDetailStatus.Text = label;
        panelDetail.Visible = true;

        // TODO: khi bam "Sang Ban hang", nen chuyen tab sang SalesControl va
        // truyen san TableId nay (can them event o day de MainForm bat va xu ly).
    }
}
