using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Tong quan (Dashboard) - chi danh cho Role QuanLy.
// TODO: hien dang dung du lieu mau. Can noi API thong ke that
// (GET /api/reports/dashboard) khi Backend bo sung endpoint tuong ung.
public partial class DashboardControl : UserControl
{
    public DashboardControl()
    {
        InitializeComponent();
        WireUpEvents();
        LoadSampleChart();
    }

    // Cac event handler dung lambda KHONG duoc dat trong .Designer.cs (WinForms Designer
    // khong parse duoc cu phap lambda -> loi "the designer cannot process the code").
    // Phai dang ky o day, trong code-behind, sau khi InitializeComponent() da chay xong.
    private void WireUpEvents()
    {
        panelChartCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelChartCard.Width - 1, panelChartCard.Height - 1);
        panelTopItemsCard.Paint += (s, e) =>
            e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, panelTopItemsCard.Width - 1, panelTopItemsCard.Height - 1);
    }

    private void LoadSampleChart()
    {
        // Du lieu mau khop voi thiet ke Stitch: doanh thu theo khung gio trong ngay
        chartPanel.Data = new List<(string Label, double Value)>
        {
            ("08:00", 8_000_000), ("10:00", 14_000_000), ("12:00", 22_000_000),
            ("14:00", 15_000_000), ("16:00", 27_000_000), ("18:00", 9_000_000), ("20:00", 6_000_000),
        };
        chartPanel.HighlightIndex = 4; // cot 16:00 duoc highlight giong thiet ke
        chartPanel.Invalidate();
    }
}
