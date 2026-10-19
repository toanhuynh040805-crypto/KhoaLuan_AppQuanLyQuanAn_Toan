using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanAnWinForm.Models;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms
{
    public partial class NotificationPopupForm : Form
    {
        public event Action<NotificationDto>? NotificationItemClicked;
        public event Action? MarkAllReadClicked;

        private readonly FlowLayoutPanel _flowList;
        private readonly Label _lblEmpty;

        private static readonly string[] TypeIcons = { "🍽️", "📅", "🕒", "⚙️", "🏷️" }; // DonHang, DatBan, CaLam, HeThong, KhuyenMai

        public NotificationPopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(380, 440);
            Padding = new Padding(1);
            BackColor = AppColors.OutlineVariant; // tao vien mong 1px nho Padding
                                                  // Dung Hide() thay vi Close() - Close() tren 1 Form hien bang Show() (khong
                                                  // phai ShowDialog()) se TU DONG Dispose luon, trong khi MainForm dang giu
                                                  // 1 instance DUY NHAT (_notificationPopup) de dung lai nhieu lan xuyen suot
                                                  // phien lam viec -> dung Close() se gay ObjectDisposedException o lan mo tiep theo.
            Deactivate += (s, e) => Hide();

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            var lblTitle = new Label { Text = "Thông báo", Font = AppFonts.HeadlineSm, Location = new Point(16, 12), AutoSize = true };
            var lblMarkAllRead = new Label
            {
                Text = "Đánh dấu đã đọc tất cả",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Primary,
                Cursor = Cursors.Hand,
                AutoSize = true,
            };
            lblMarkAllRead.Location = new Point(Width - 20 - lblMarkAllRead.PreferredWidth, 18);
            lblMarkAllRead.Click += (s, e) => MarkAllReadClicked?.Invoke();

            _flowList = new FlowLayoutPanel
            {
                Location = new Point(0, 48),
                Size = new Size(Width - 2, Height - 50),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };

            _lblEmpty = new Label { Text = "Chưa có thông báo nào.", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(16, 16), AutoSize = true };

            inner.Controls.Add(lblTitle);
            inner.Controls.Add(lblMarkAllRead);
            inner.Controls.Add(_flowList);
            Controls.Add(inner);
        }

        public void ShowAt(Point screenLocation, List<NotificationDto> items)
        {
            Location = screenLocation;
            Render(items);
            Show();
            Activate();
        }

        public void Render(List<NotificationDto> items)
        {
            _flowList.Controls.Clear();
            if (items.Count == 0)
            {
                _flowList.Controls.Add(_lblEmpty);
                return;
            }

            foreach (var n in items) _flowList.Controls.Add(BuildRow(n));
        }

        private Panel BuildRow(NotificationDto n)
        {
            var row = new Panel { Size = new Size(360, 64), Cursor = Cursors.Hand, BackColor = n.IsRead ? Color.White : Color.FromArgb(255, 247, 240) };
            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(AppColors.OutlineVariant), 0, row.Height - 1, row.Width, row.Height - 1);

            var icon = TypeIcons[Math.Clamp(n.NotificationType, 0, TypeIcons.Length - 1)];
            var lblIcon = new Label { Text = icon, Font = new Font("Segoe UI Emoji", 14F), Location = new Point(12, 14), Size = new Size(30, 30), TextAlign = ContentAlignment.MiddleCenter };
            var lblTitle = new Label { Text = n.Title, Font = n.IsRead ? AppFonts.BodyMd : AppFonts.LabelLg, Location = new Point(48, 8), Size = new Size(300, 20), AutoEllipsis = true };
            var lblContent = new Label { Text = n.Content, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(48, 28), Size = new Size(300, 18), AutoEllipsis = true };
            var lblTime = new Label { Text = TimeAgo(n.CreatedAt), Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(48, 46), AutoSize = true };

            row.Controls.Add(lblIcon);
            row.Controls.Add(lblTitle);
            row.Controls.Add(lblContent);
            row.Controls.Add(lblTime);

            void Fire(object? s, EventArgs e) => NotificationItemClicked?.Invoke(n);
            row.Click += Fire;
            lblTitle.Click += Fire;
            lblContent.Click += Fire;

            return row;
        }

        private static string TimeAgo(DateTime createdAt)
        {
            var span = DateTime.Now - createdAt;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            return createdAt.ToString("HH:mm dd/MM");
        }
    }
}
