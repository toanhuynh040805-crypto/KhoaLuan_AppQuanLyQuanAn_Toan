using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// The thong ke nho (icon + nhan xu huong % + tieu de + gia tri lon).
// Dung chung cho Dashboard, Doanh thu, Nhan vien.
public class StatCard : Panel
{
    private readonly Panel _iconBox;
    private readonly Label _lblIcon;
    private readonly Label _lblTrend;
    private readonly Label _lblTitle;
    private readonly Label _lblValue;

    public StatCard()
    {
        Size = new Size(220, 130);
        BackColor = Color.White;
        Padding = new Padding(16);

        _iconBox = new Panel { Size = new Size(40, 40), Location = new Point(16, 16) };
        _lblIcon = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14) };
        _iconBox.Controls.Add(_lblIcon);

        _lblTrend = new Label
        {
            AutoSize = true,
            Font = AppFonts.LabelMd,
            Location = new Point(140, 24),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _lblTitle = new Label
        {
            AutoSize = true,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(16, 68),
        };

        _lblValue = new Label
        {
            AutoSize = true,
            Font = AppFonts.HeadlineMd,
            ForeColor = AppColors.OnSurface,
            Location = new Point(16, 88),
        };

        Controls.Add(_iconBox);
        Controls.Add(_lblTrend);
        Controls.Add(_lblTitle);
        Controls.Add(_lblValue);

        Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    public void Configure(string icon, Color iconColor, Color iconBg, string title, string value, string? trendText = null, bool trendUp = true)
    {
        _lblIcon.Text = icon;
        _lblIcon.ForeColor = iconColor;
        _iconBox.BackColor = iconBg;
        _lblTitle.Text = title;
        _lblValue.Text = value;

        if (trendText != null)
        {
            _lblTrend.Text = (trendUp ? "▲ " : "▼ ") + trendText;
            _lblTrend.ForeColor = trendUp ? AppColors.Success : AppColors.Error;
            _lblTrend.BackColor = trendUp ? Color.FromArgb(230, 245, 240) : AppColors.ErrorContainer;
            _lblTrend.Visible = true;
        }
        else
        {
            _lblTrend.Visible = false;
        }
    }
}
