using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

public class StatCard : Panel
{
    private const int PadX = 18;

    private readonly Panel _iconBox;
    private readonly Label _lblIcon;
    private readonly Label _lblTrend;
    private readonly Label _lblTitle;
    private readonly Label _lblValue;

    public StatCard()
    {
        Size = new Size(240, 140);
        MinimumSize = new Size(170, 110);
        BackColor = Color.White;
        DoubleBuffered = true;

        _iconBox = new Panel { Size = new Size(38, 38), Location = new Point(PadX, 16) };
        _lblIcon = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 12),
        };
        _iconBox.Controls.Add(_lblIcon);

        _lblTrend = new Label
        {
            AutoSize = true,
            Font = AppFonts.LabelMd,
            Padding = new Padding(4, 2, 4, 2),
            Visible = false,
        };

        _lblTitle = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Height = 22,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblValue = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Height = 40,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = AppColors.OnSurface,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Controls.Add(_iconBox);
        Controls.Add(_lblTrend);
        Controls.Add(_lblTitle);
        Controls.Add(_lblValue);

        Resize += (s, e) => DoLayout();
        Paint += (s, e) =>
        {
            using var pen = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        DoLayout();
    }

    private void DoLayout()
    {
        int innerWidth = Math.Max(10, Width - PadX * 2);

        _lblTitle.SetBounds(PadX, 62, innerWidth, 22);
        _lblValue.SetBounds(PadX, 84, innerWidth, 40);

        if (_lblTrend.Visible)
        {
            // Nhãn xu hướng canh sát mép phải, không bao giờ đè lên icon.
            int maxLeft = _iconBox.Right + 8;
            int left = Math.Max(maxLeft, Width - PadX - _lblTrend.PreferredWidth);
            _lblTrend.Location = new Point(left, 22);
        }
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

        DoLayout();
    }
}