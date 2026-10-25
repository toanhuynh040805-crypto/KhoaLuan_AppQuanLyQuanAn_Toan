using System.ComponentModel;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Panel dang "the" (card): nen trang + vien mong, tuy chon co vach mau o canh
// trai/tren va vien net dut. Dung chung cho cac dong/the o man Nhan vien.
public partial class CardPanel : Panel
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = AppColors.OutlineVariant;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color StripeColor { get; set; } = Color.Transparent;

    // Do day vach mau (px). 0 = khong ve.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int StripeWidth { get; set; }

    // true: vach nam o canh TREN (ngang); false: canh TRAI (doc).
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool StripeOnTop { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Dashed { get; set; }

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using (var pen = new Pen(BorderColor))
        {
            if (Dashed) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        if (StripeWidth > 0 && StripeColor != Color.Transparent)
        {
            using var brush = new SolidBrush(StripeColor);
            if (StripeOnTop) e.Graphics.FillRectangle(brush, 0, 0, Width, StripeWidth);
            else e.Graphics.FillRectangle(brush, 0, 0, StripeWidth, Height);
        }
    }
}