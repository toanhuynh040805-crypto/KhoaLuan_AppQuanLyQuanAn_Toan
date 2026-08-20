using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Thong ke doanh thu - danh cho QuanLy va ThuNgan.
// TODO: hien dang dung du lieu mau. Can bo sung endpoint GET /api/reports/revenue
// ben Backend (group theo ngay/thang tu bang Payments) roi thay du lieu that vao day.
public partial class RevenueControl : UserControl
{
    public RevenueControl()
    {
        InitializeComponent();
        WireUpEvents();
        LoadSampleData();
    }

    // Xem giai thich chi tiet trong DashboardControl.cs: lambda khong duoc dat trong .Designer.cs.
    private void WireUpEvents()
    {
        panelChartCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelChartCard.Width - 1, panelChartCard.Height - 1);
        panelInvoicesCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelInvoicesCard.Width - 1, panelInvoicesCard.Height - 1);
    }

    private void LoadSampleData()
    {
        chartPanel.Data = new List<(string Label, double Value)>
        {
            ("T2", 12_000_000), ("T3", 18_000_000), ("T4", 9_000_000), ("T5", 24_000_000),
            ("T6", 15_000_000), ("T7", 28_000_000), ("CN", 32_000_000),
        };
        chartPanel.Invalidate();

        gridInvoices.Rows.Clear();
        gridInvoices.Rows.Add("#INV-8042", "Bàn 12 (Tầng 1)", "14:15, 24/10", "1,250,000 đ", "Đã thanh toán");
        gridInvoices.Rows.Add("#INV-8041", "Mang về", "14:02, 24/10", "450,000 đ", "Đã thanh toán");
        gridInvoices.Rows.Add("#INV-8040", "Bàn 05 (Tầng 1)", "13:48, 24/10", "3,400,000 đ", "Đã thanh toán");
    }
}
