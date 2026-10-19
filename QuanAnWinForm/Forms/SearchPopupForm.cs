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
    public partial class SearchPopupForm : Form
    {
        public event Action<SearchResultDto>? ResultClicked;

        private readonly FlowLayoutPanel _flowList;

        private static readonly Dictionary<string, string> TypeLabels = new()
        {
            ["MenuItem"] = "🍽️ Món ăn",
            ["Table"] = "🪑 Bàn",
            ["Customer"] = "👤 Khách hàng",
            ["Employee"] = "🧑‍💼 Nhân viên",
            ["Order"] = "🧾 Đơn hàng",
            ["Reservation"] = "📅 Đặt bàn",
        };

        public SearchPopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(420, 480);
            Padding = new Padding(1);
            BackColor = AppColors.OutlineVariant;
            Deactivate += (s, e) => Hide();

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _flowList = new FlowLayoutPanel
            {
                Location = new Point(0, 0),
                Size = new Size(Width - 2, Height - 2),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 8),
            };

            inner.Controls.Add(_flowList);
            Controls.Add(inner);
        }

        public void ShowAt(Point screenLocation)
        {
            Location = screenLocation;
            RenderMessage("Nhập ít nhất 2 ký tự để tìm kiếm...");
            Show();
            Activate();
        }

        public void ShowLoading() => RenderMessage("Đang tìm...");

        private void RenderMessage(string text)
        {
            _flowList.Controls.Clear();
            _flowList.Controls.Add(new Label { Text = text, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(16, 16), AutoSize = true });
        }

        public void Render(List<SearchResultDto> items)
        {
            if (items.Count == 0)
            {
                RenderMessage("Không tìm thấy kết quả nào phù hợp.");
                return;
            }

            _flowList.Controls.Clear();
            foreach (var group in items.GroupBy(i => i.Type))
            {
                var header = new Label
                {
                    Text = TypeLabels.TryGetValue(group.Key, out var lbl) ? lbl : group.Key,
                    Font = AppFonts.LabelMd,
                    ForeColor = AppColors.Primary,
                    Location = new Point(16, 4),
                    AutoSize = true,
                    Margin = new Padding(0, 8, 0, 4),
                };
                _flowList.Controls.Add(header);

                foreach (var item in group) _flowList.Controls.Add(BuildRow(item));
            }
        }

        private Panel BuildRow(SearchResultDto item)
        {
            var row = new Panel { Size = new Size(400, 44), Cursor = Cursors.Hand, Margin = new Padding(0) };
            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(AppColors.OutlineVariant), 16, row.Height - 1, row.Width - 16, row.Height - 1);

            var lblTitle = new Label { Text = item.Title, Font = AppFonts.BodyMd, Location = new Point(16, 4), Size = new Size(370, 20), AutoEllipsis = true };
            var lblSubtitle = new Label { Text = item.Subtitle, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(16, 22), Size = new Size(370, 18), AutoEllipsis = true };

            row.Controls.Add(lblTitle);
            row.Controls.Add(lblSubtitle);

            void Fire(object? s, EventArgs e) => ResultClicked?.Invoke(item);
            row.Click += Fire;
            lblTitle.Click += Fire;
            lblSubtitle.Click += Fire;

            return row;
        }
    }
}
