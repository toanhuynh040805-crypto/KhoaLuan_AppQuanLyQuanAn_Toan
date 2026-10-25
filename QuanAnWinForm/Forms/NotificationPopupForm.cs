using QuanAnWinForm.Models;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms
{
    // Popup thông báo: danh sách vẽ tay (owner-draw) -> không bao giờ bị chồng chữ / thanh cuộn ngang,
    // có tab "Tất cả | Chưa đọc", hover, chấm báo chưa đọc, nội dung tối đa 2 dòng.
    public partial class NotificationPopupForm : Form
    {
        public event Action<NotificationDto>? NotificationItemClicked;
        public event Action? MarkAllReadClicked;

        private const int PopupWidth = 420;
        private const int HeaderHeight = 96;
        private const int RowHeight = 84;
        private const int MaxVisibleRows = 5;

        private readonly Panel _header;
        private readonly Label _lblMarkAllRead;
        private readonly Label _tabAll;
        private readonly Label _tabUnread;
        private readonly BufferedListBox _list;
        private readonly Label _lblEmpty;

        private List<NotificationDto> _items = new();
        private List<NotificationDto> _visible = new();
        private bool _unreadOnly;
        private int _hoverIndex = -1;

        // DonHang, DatBan, CaLam, HeThong, KhuyenMai
        private static readonly string[] TypeGlyphs = { "🍽", "📅", "🕒", "⚙", "🏷" };
        private static readonly Color[] TypeColors =
        {
            Color.FromArgb(172, 45, 0),
            Color.FromArgb(0, 104, 92),
            Color.FromArgb(84, 96, 103),
            Color.FromArgb(214, 138, 0),
            Color.FromArgb(123, 31, 162),
        };
        private static readonly Color[] TypeTints =
        {
            Color.FromArgb(253, 236, 231),
            Color.FromArgb(224, 242, 241),
            Color.FromArgb(232, 238, 242),
            Color.FromArgb(255, 243, 224),
            Color.FromArgb(243, 229, 245),
        };

        private static readonly Font FTitleBold = new("Segoe UI", 10F, FontStyle.Bold);
        private static readonly Font FTitle = new("Segoe UI", 10F, FontStyle.Regular);
        private static readonly Font FBody = new("Segoe UI", 9F, FontStyle.Regular);
        private static readonly Font FSmall = new("Segoe UI", 8.5F, FontStyle.Regular);
        private static readonly Font FEmoji = new("Segoe UI Emoji", 13F, FontStyle.Regular);

        private static readonly Color UnreadBg = Color.FromArgb(255, 247, 240);
        private static readonly Color MutedText = Color.FromArgb(96, 104, 110);

        public NotificationPopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Padding = new Padding(1);
            BackColor = AppColors.OutlineVariant; // viền mỏng 1px nhờ Padding
            Size = new Size(PopupWidth, HeaderHeight + RowHeight * MaxVisibleRows + 2);

            // Dùng Hide() thay vì Close(): MainForm giữ 1 instance duy nhất và mở lại nhiều lần.
            Deactivate += (s, e) => Hide();

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // ---------- Header ----------
            _header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = Color.White };

            var lblTitle = new Label
            {
                Text = "Thông báo",
                Font = AppFonts.HeadlineMd,
                Location = new Point(20, 12),
                AutoSize = true,
            };

            _lblMarkAllRead = new Label
            {
                Text = "Đánh dấu đã đọc tất cả",
                Font = AppFonts.BodyMd,
                ForeColor = AppColors.Primary,
                Cursor = Cursors.Hand,
                AutoSize = true,
            };
            _lblMarkAllRead.Location = new Point(PopupWidth - 2 - 20 - _lblMarkAllRead.PreferredWidth, 20);
            _lblMarkAllRead.Click += (s, e) => MarkAllReadClicked?.Invoke();

            _tabAll = MakeTab("Tất cả", 12, 100);
            _tabUnread = MakeTab("Chưa đọc", _tabAll.Right + 4, 120);
            _tabAll.Click += (s, e) => SetFilter(false);
            _tabUnread.Click += (s, e) => SetFilter(true);

            _header.Controls.Add(lblTitle);
            _header.Controls.Add(_lblMarkAllRead);
            _header.Controls.Add(_tabAll);
            _header.Controls.Add(_tabUnread);
            _header.Paint += Header_Paint;

            // ---------- Danh sách ----------
            _list = new BufferedListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = RowHeight,
                IntegralHeight = false,
                SelectionMode = SelectionMode.None,
                HorizontalScrollbar = false,
                BackColor = Color.White,
            };
            _list.DrawItem += List_DrawItem;
            _list.MouseMove += List_MouseMove;
            _list.MouseLeave += (s, e) => SetHover(-1);
            _list.MouseUp += List_MouseUp;
            _list.MouseEnter += (s, e) => _list.Focus(); // để lăn chuột cuộn được ngay

            _lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.BodyMd,
                ForeColor = MutedText,
                BackColor = Color.White,
                Visible = false,
            };

            inner.Controls.Add(_list);
            inner.Controls.Add(_lblEmpty);
            inner.Controls.Add(_header);
            Controls.Add(inner);

            UpdateTabStyles();
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
            _items = items;
            ApplyFilter();
        }

        // ------------------------------------------------------------------
        private Label MakeTab(string text, int left, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(width, 32),
                Location = new Point(left, 54),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Font = AppFonts.LabelLg,
            };
        }

        private void SetFilter(bool unreadOnly)
        {
            if (_unreadOnly == unreadOnly) return;
            _unreadOnly = unreadOnly;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            int unread = _items.Count(n => !n.IsRead);
            _tabUnread.Text = unread > 0 ? $"Chưa đọc ({unread})" : "Chưa đọc";
            _lblMarkAllRead.Visible = unread > 0;
            UpdateTabStyles();

            _visible = _unreadOnly ? _items.Where(n => !n.IsRead).ToList() : _items.ToList();
            _hoverIndex = -1;

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var n in _visible) _list.Items.Add(n);
            _list.EndUpdate();

            bool empty = _visible.Count == 0;
            _lblEmpty.Visible = empty;
            _list.Visible = !empty;
            if (empty)
                _lblEmpty.Text = _unreadOnly ? "Bạn đã đọc hết thông báo 🎉" : "Chưa có thông báo nào.";

            // Popup co giãn theo số dòng (tối đa 5 dòng, nhiều hơn thì cuộn).
            int bodyHeight = empty ? 150 : Math.Min(_visible.Count, MaxVisibleRows) * RowHeight;
            Height = HeaderHeight + bodyHeight + 2;
        }

        private void UpdateTabStyles()
        {
            foreach (var (tab, selected) in new[] { (_tabAll, !_unreadOnly), (_tabUnread, _unreadOnly) })
            {
                tab.ForeColor = selected ? AppColors.Primary : MutedText;
            }
            _header.Invalidate();
        }

        private void Header_Paint(object? sender, PaintEventArgs e)
        {
            // đường kẻ dưới header
            using var line = new Pen(AppColors.OutlineVariant);
            e.Graphics.DrawLine(line, 0, _header.Height - 1, _header.Width, _header.Height - 1);

            // gạch chân tab đang chọn
            var tab = _unreadOnly ? _tabUnread : _tabAll;
            using var brush = new SolidBrush(AppColors.Primary);
            e.Graphics.FillRectangle(brush, tab.Left + 10, _header.Height - 3, tab.Width - 20, 3);
        }

        // ------------------------------------------------------------------
        private void SetHover(int index)
        {
            if (_hoverIndex == index) return;
            int old = _hoverIndex;
            _hoverIndex = index;
            if (old >= 0 && old < _list.Items.Count) _list.Invalidate(_list.GetItemRectangle(old));
            if (index >= 0 && index < _list.Items.Count) _list.Invalidate(_list.GetItemRectangle(index));
            _list.Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        }

        private void List_MouseMove(object? sender, MouseEventArgs e)
        {
            int idx = _list.IndexFromPoint(e.Location);
            SetHover(idx >= 0 && idx < _visible.Count ? idx : -1);
        }

        private void List_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int idx = _list.IndexFromPoint(e.Location);
            if (idx >= 0 && idx < _visible.Count)
                NotificationItemClicked?.Invoke(_visible[idx]);
        }

        private void List_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _visible.Count) return;

            var n = _visible[e.Index];
            var g = e.Graphics;
            var r = e.Bounds;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool hover = e.Index == _hoverIndex;
            var bg = hover ? AppColors.SurfaceLow : (n.IsRead ? Color.White : UnreadBg);
            using (var bgBrush = new SolidBrush(bg)) g.FillRectangle(bgBrush, r);

            // vạch màu bên trái cho thông báo chưa đọc
            if (!n.IsRead)
            {
                using var stripe = new SolidBrush(AppColors.Primary);
                g.FillRectangle(stripe, r.X, r.Y, 3, r.Height);
            }

            // icon tròn theo loại
            int t = Math.Clamp(n.NotificationType, 0, TypeGlyphs.Length - 1);
            var iconRect = new Rectangle(r.X + 18, r.Y + 18, 40, 40);
            using (var tint = new SolidBrush(TypeTints[t])) g.FillEllipse(tint, iconRect);
            TextRenderer.DrawText(g, TypeGlyphs[t], FEmoji, iconRect, TypeColors[t],
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            const int textLeft = 70;
            int rightEdge = r.Right - 16;

            // thời gian (góc phải dòng tiêu đề)
            string time = TimeAgo(n.CreatedAt);
            int timeW = TextRenderer.MeasureText(g, time, FSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, time, FSmall,
                new Rectangle(rightEdge - timeW, r.Y + 14, timeW, 18),
                n.IsRead ? MutedText : AppColors.Primary,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // tiêu đề (1 dòng, cắt "...")
            var titleRect = new Rectangle(r.X + textLeft, r.Y + 12, rightEdge - timeW - 10 - (r.X + textLeft), 22);
            TextRenderer.DrawText(g, n.Title, n.IsRead ? FTitle : FTitleBold, titleRect, AppColors.OnSurface,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            // nội dung (tối đa 2 dòng, cắt "...")
            int contentRight = rightEdge - (n.IsRead ? 0 : 16);
            var contentRect = new Rectangle(r.X + textLeft, r.Y + 36, contentRight - (r.X + textLeft), 38);
            TextRenderer.DrawText(g, n.Content, FBody, contentRect, MutedText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            // chấm chưa đọc
            if (!n.IsRead)
            {
                using var dot = new SolidBrush(AppColors.Primary);
                g.FillEllipse(dot, rightEdge - 8, r.Y + 50, 8, 8);
            }

            // đường phân cách
            using var sep = new Pen(AppColors.OutlineVariant);
            g.DrawLine(sep, r.X + textLeft, r.Bottom - 1, r.Right, r.Bottom - 1);
        }

        private static string TimeAgo(DateTime createdAt)
        {
            var span = DateTime.Now - createdAt;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            return createdAt.ToString("HH:mm dd/MM");
        }

        // ListBox chống nhấp nháy khi vẽ tay + hover.
        private sealed class BufferedListBox : ListBox
        {
            public BufferedListBox()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.ResizeRedraw, true);
            }
        }
    }
}