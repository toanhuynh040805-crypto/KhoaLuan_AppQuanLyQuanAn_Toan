using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Bep / Quay nuoc dang KDS (Kitchen Display System) kieu cot ngang,
// moi cot la 1 phieu don. Dung chung cho ca Bep va Quay nuoc, chi khac
// departmentName truyen vao - giong y het co che phan luong da lam ben Backend.
public partial class KitchenBoardControl : UserControl
{
    private readonly string _departmentName;
    private readonly SignalRService _signalR = new();

    public KitchenBoardControl() : this("Bep", "Màn hình Bếp") { }

    public KitchenBoardControl(string departmentName, string title)
    {
        _departmentName = departmentName;
        InitializeComponent();
        lblTitle.Text = title;

        Load += async (s, e) => await ConnectSignalRAsync();
        Disposed += async (s, e) => await _signalR.DisconnectAsync();
    }

    private async Task ConnectSignalRAsync()
    {
        _signalR.OnNewTicket += payload =>
        {
            if (InvokeRequired) Invoke(() => AddTicketColumn(payload));
            else AddTicketColumn(payload);
        };

        try
        {
            await _signalR.ConnectAndJoinDepartmentAsync(_departmentName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không kết nối được realtime: {ex.Message}");
        }
    }

    private void AddTicketColumn(NewTicketPayload payload)
    {
        var column = BuildTicketColumn(payload);
        flowColumns.Controls.Add(column);
        flowColumns.Controls.SetChildIndex(column, 0); // phieu moi nhat o ben trai
    }

    private Panel BuildTicketColumn(NewTicketPayload payload)
    {
        // TODO: mau vien theo do gap (binh thuong/dang cho/tre) can tinh tu
        // thoi gian thuc te - hien mac dinh mau xanh "Binh thuong" cho phieu moi.
        var borderColor = AppColors.Tertiary;

        var column = new Panel { Size = new Size(260, 640), Margin = new Padding(8), BackColor = Color.White };
        column.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(borderColor, 2), 1, 1, column.Width - 3, column.Height - 3);

        var header = new Panel { Size = new Size(260, 44), BackColor = borderColor };
        var lblTable = new Label { Text = payload.TableName, ForeColor = Color.White, Font = AppFonts.HeadlineSm, Location = new Point(10, 6), AutoSize = true };
        var lblOrderId = new Label { Text = $"#ORD-{payload.OrderId}", ForeColor = Color.White, Font = AppFonts.BodyMd, Location = new Point(10, 26), AutoSize = true };
        header.Controls.Add(lblTable);
        header.Controls.Add(lblOrderId);

        var itemsList = new FlowLayoutPanel { Location = new Point(0, 50), Size = new Size(260, 540), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        foreach (var item in payload.Items)
        {
            itemsList.Controls.Add(BuildItemRow(item));
        }

        var btnDoneAll = new Button
        {
            Text = "✓  Hoàn thành tất cả",
            Location = new Point(10, 596),
            Size = new Size(240, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = borderColor,
            ForeColor = Color.White,
        };
        btnDoneAll.FlatAppearance.BorderSize = 0;
        // TODO: goi API cap nhat KitchenStatus = HoanThanh cho toan bo item trong phieu nay
        btnDoneAll.Click += (s, e) => { column.Visible = false; };

        column.Controls.Add(header);
        column.Controls.Add(itemsList);
        column.Controls.Add(btnDoneAll);
        return column;
    }

    private Panel BuildItemRow(TicketItem item)
    {
        var row = new Panel { Size = new Size(240, 56), Margin = new Padding(4) };
        var lblQty = new Label { Text = $"{item.Quantity}x", Font = AppFonts.HeadlineSm, ForeColor = AppColors.Primary, Location = new Point(0, 4), AutoSize = true };
        var lblName = new Label { Text = item.MenuItemName, Font = AppFonts.LabelLg, Location = new Point(40, 4), AutoSize = true };
        var chkDone = new CheckBox { Location = new Point(210, 6), Size = new Size(20, 20) };
        // TODO: goi API cap nhat trang thai rieng item nay khi tick

        row.Controls.Add(lblQty);
        row.Controls.Add(lblName);
        row.Controls.Add(chkDone);

        if (!string.IsNullOrEmpty(item.Note))
        {
            var lblNote = new Label { Text = item.Note, Font = AppFonts.BodyMd, ForeColor = AppColors.Error, Location = new Point(40, 26), AutoSize = true };
            row.Controls.Add(lblNote);
        }
        return row;
    }
}
