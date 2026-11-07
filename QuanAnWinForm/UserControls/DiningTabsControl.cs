using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Khung 2 tab: "Ăn tại chỗ" (Sơ đồ bàn - TableManagementControl)
//              "Mang về"    (Danh sách đơn mang về - TakeawayOrdersControl)
// Control dựng hoàn toàn bằng code (không có Designer).
public partial class DiningTabsControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync()
    {
        var child = _content.Controls
            .OfType<UserControl>()
            .FirstOrDefault();

        return child is IRefreshableContent refreshable
            ? refreshable.RefreshDataAsync()
            : Task.CompletedTask;
    }


    private readonly int _userId;
    private readonly Panel _tabBar = new();
    private readonly Panel _content = new();
    private readonly Button _btnDineIn = new() { Text = "Ăn tại chỗ" };
    private readonly Button _btnTakeaway = new() { Text = "Mang về" };
    private bool _showingTakeaway;

    // Bấm "Sang bán hàng" trên 1 bàn ở tab Ăn tại chỗ (tham số: TableId)
    public event Action<int>? GoToSalesRequested;

    // Mở màn Thanh toán ở chế độ Mang về. Tham số: 0 = tạo đơn mới, > 0 = thanh toán đơn đó (OrderId)
    public event Action<int>? GoToTakeawaySalesRequested;

    public DiningTabsControl() : this(0, false) { }

    public DiningTabsControl(int userId, bool startOnTakeaway = false)
    {
        _userId = userId;
        _showingTakeaway = startOnTakeaway;
        Dock = DockStyle.Fill;
        BackColor = AppColors.Background;

        _tabBar.Dock = DockStyle.Top;
        _tabBar.Height = 52;
        _tabBar.BackColor = Color.White;

        foreach (var b in new[] { _btnDineIn, _btnTakeaway })
        {
            b.Size = new Size(150, 34);
            b.Font = AppFonts.LabelLg;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            _tabBar.Controls.Add(b);
        }
        _btnDineIn.Location = new Point(24, 9);
        _btnTakeaway.Location = new Point(24 + 150 + 8, 9);
        _btnDineIn.Click += (s, e) => ShowTab(takeaway: false);
        _btnTakeaway.Click += (s, e) => ShowTab(takeaway: true);

        _content.Dock = DockStyle.Fill;

        // Thứ tự add: vùng Fill trước, thanh tab (Top) sau để thanh tab nằm trên cùng
        Controls.Add(_content);
        Controls.Add(_tabBar);

        Load += (s, e) => ShowTab(_showingTakeaway, force: true);
    }

    private void ShowTab(bool takeaway, bool force = false)
    {
        if (!force && _showingTakeaway == takeaway) return;
        _showingTakeaway = takeaway;

        StyleTab(_btnDineIn, !takeaway);
        StyleTab(_btnTakeaway, takeaway);

        // Bỏ control cũ (control cũ tự hủy đăng ký realtime khi Dispose)
        foreach (var old in _content.Controls.Cast<Control>().ToList())
        {
            _content.Controls.Remove(old);
            old.Dispose();
        }

        UserControl view;
        if (takeaway)
        {
            var orders = new TakeawayOrdersControl(_userId);
            orders.CreateRequested += () => GoToTakeawaySalesRequested?.Invoke(0);
            orders.PayRequested += orderId => GoToTakeawaySalesRequested?.Invoke(orderId);
            view = orders;
        }
        else
        {
            var tables = new TableManagementControl(_userId);
            tables.GoToSalesRequested += tableId => GoToSalesRequested?.Invoke(tableId);
            view = tables;
        }

        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
    }

    private static void StyleTab(Button btn, bool selected)
    {
        btn.UseVisualStyleBackColor = false;
        if (selected)
        {
            btn.BackColor = AppColors.Primary;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
        }
        else
        {
            btn.BackColor = Color.White;
            btn.ForeColor = AppColors.OnSurface;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = AppColors.OutlineVariant;
        }
    }
}