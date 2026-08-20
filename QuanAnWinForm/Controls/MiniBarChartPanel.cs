using System.ComponentModel;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Bieu do cot don gian ve bang GDI+, khong phu thuoc thu vien ngoai.
// Dung chung cho DashboardControl va RevenueControl.
// TODO: neu muon bieu do dep/tuong tac hon, thay bang LiveCharts2
// (NuGet: LiveChartsCore.SkiaSharpView.WinForms).
public class MiniBarChartPanel : Panel
{
    // QUAN TRONG: List<(string,double)> la kieu Tuple ma WinForms Designer KHONG the
    // serialize thanh code hop le -> neu de Designer tu dong luu gia tri property nay,
    // no se sinh ra code loi kieu "List<(T1, T2)>" khong ton tai (bien resources.GetObject).
    // 2 attribute duoi day bao Designer: bo qua property nay hoan toan (khong hien trong
    // Properties window, khong luu vao .Designer.cs) - Data chi duoc set bang code lúc runtime.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<(string Label, double Value)> Data { get; set; } = new();

    public int HighlightIndex { get; set; } = -1;

    public MiniBarChartPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Paint += OnPaint;
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (Data.Count == 0) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int marginLeft = 60, marginBottom = 30, marginTop = 10, marginRight = 10;
        int chartWidth = Width - marginLeft - marginRight;
        int chartHeight = Height - marginBottom - marginTop;
        if (chartWidth <= 0 || chartHeight <= 0) return;

        double maxValue = Data.Max(d => d.Value);
        int barCount = Data.Count;
        int slot = chartWidth / barCount;
        int barWidth = (int)(slot * 0.55);

        using var gridPen = new Pen(AppColors.OutlineVariant) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        for (int i = 0; i <= 4; i++)
        {
            int y = marginTop + chartHeight * i / 4;
            g.DrawLine(gridPen, marginLeft, y, marginLeft + chartWidth, y);
            var val = maxValue * (4 - i) / 4;
            using var lblFont = new Font("Segoe UI", 8);
            g.DrawString(FormatShort(val), lblFont, Brushes.Gray, 4, y - 7);
        }

        using var normalBrush = new SolidBrush(ColorTranslator.FromHtml("#D7E4EC"));
        using var highlightBrush = new SolidBrush(AppColors.Primary);
        using var labelFont = new Font("Segoe UI", 8);

        for (int i = 0; i < barCount; i++)
        {
            var (label, value) = Data[i];
            int barHeight = (int)(value / maxValue * chartHeight);
            int x = marginLeft + i * slot + (slot - barWidth) / 2;
            int y = marginTop + chartHeight - barHeight;

            var brush = i == HighlightIndex ? highlightBrush : normalBrush;
            g.FillRectangle(brush, x, y, barWidth, barHeight);

            var labelColor = i == HighlightIndex ? AppColors.Primary : Color.Gray;
            using var lp = new SolidBrush(labelColor);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, lp, x + barWidth / 2 - size.Width / 2, marginTop + chartHeight + 6);
        }
    }

    private static string FormatShort(double value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:0}M";
        if (value >= 1_000) return $"{value / 1_000:0}K";
        return value.ToString("0");
    }
}
