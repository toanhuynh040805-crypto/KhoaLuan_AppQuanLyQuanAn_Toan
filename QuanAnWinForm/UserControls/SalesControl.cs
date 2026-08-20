using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;
using QRCoder;

namespace QuanAnWinForm.UserControls;

// Man hinh Ban hang / Thanh toan (POS) - man hinh chinh cua Role Thu ngan.
// Ben trai: chon mon theo danh muc. Ben phai: gio hang cua ban dang chon + thanh toan.
public partial class SalesControl : UserControl
{
    private readonly ApiService _api = new();
    private readonly int _userId;

    private List<MenuItemDto> _allMenuItems = new();
    private readonly Dictionary<int, (MenuItemDto Item, int Qty)> _cart = new();
    private int? _selectedTableId;

    public SalesControl() : this(0) { }

    public SalesControl(int userId)
    {
        _userId = userId;
        InitializeComponent();

        btnAllCategory.Click += (s, e) => FilterCategory(null);
        Load += async (s, e) => await LoadDataAsync();
        btnVietQr.Click += async (s, e) => await ProcessPaymentAsync(method: 1);
        btnThanhToan.Click += async (s, e) => await ProcessPaymentAsync(method: 0);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _allMenuItems = await _api.GetMenuAsync();
            var tables = await _api.GetTablesAsync();

            cbTable.Items.Clear();
            foreach (var t in tables) cbTable.Items.Add(t);
            cbTable.DisplayMember = "TableName";
            if (tables.Count > 0) cbTable.SelectedIndex = 0;

            RenderMenuTiles(_allMenuItems);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được dữ liệu: {ex.Message}");
        }
    }

    private void CbTable_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cbTable.SelectedItem is TableDto t)
        {
            _selectedTableId = t.Id;
            lblTableName.Text = t.TableName;
            // TODO: goi GET /api/orders lay don dang mo cua ban nay de load lai gio hang neu co
            _cart.Clear();
            RenderCart();
        }
    }

    private void FilterCategory(int? categoryId)
    {
        var filtered = categoryId is null ? _allMenuItems : _allMenuItems.Where(m => m.DepartmentId == categoryId).ToList();
        RenderMenuTiles(filtered);
    }

    private void RenderMenuTiles(List<MenuItemDto> items)
    {
        flowMenuTiles.Controls.Clear();
        foreach (var item in items)
        {
            var tile = BuildMenuTile(item);
            flowMenuTiles.Controls.Add(tile);
        }
    }

    private Panel BuildMenuTile(MenuItemDto item)
    {
        var tile = new Panel
        {
            Size = new Size(150, 170),
            BackColor = Color.White,
            Margin = new Padding(8),
            Cursor = Cursors.Hand,
        };
        var thumb = new Panel { Size = new Size(150, 100), Location = new Point(0, 0), BackColor = AppColors.SurfaceContainer };
        var lblName = new Label { Text = item.Name, Font = AppFonts.LabelLg, Location = new Point(8, 106), Size = new Size(134, 34) };
        var lblPrice = new Label { Text = $"{item.Price:N0}đ", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(8, 142), AutoSize = true };

        tile.Controls.Add(thumb);
        tile.Controls.Add(lblName);
        tile.Controls.Add(lblPrice);
        tile.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, tile.Width - 1, tile.Height - 1);

        void addToCart(object? s, EventArgs e) => AddToCart(item);
        tile.Click += addToCart;
        thumb.Click += addToCart;
        lblName.Click += addToCart;
        lblPrice.Click += addToCart;

        return tile;
    }

    private void AddToCart(MenuItemDto item)
    {
        if (_selectedTableId is null)
        {
            MessageBox.Show("Vui lòng chọn bàn trước");
            return;
        }

        if (_cart.ContainsKey(item.Id))
        {
            var (i, qty) = _cart[item.Id];
            _cart[item.Id] = (i, qty + 1);
        }
        else
        {
            _cart[item.Id] = (item, 1);
        }
        RenderCart();
    }

    private void RenderCart()
    {
        flowCartItems.Controls.Clear();
        decimal subtotal = 0;

        foreach (var (item, qty) in _cart.Values)
        {
            subtotal += item.Price * qty;
            flowCartItems.Controls.Add(BuildCartRow(item, qty));
        }

        decimal vat = subtotal * 0.08m;
        decimal total = subtotal + vat;

        lblSubtotalValue.Text = $"{subtotal:N0}đ";
        lblVatValue.Text = $"{vat:N0}đ";
        lblTotalValue.Text = $"{total:N0}đ";
    }

    private Panel BuildCartRow(MenuItemDto item, int qty)
    {
        var row = new Panel { Size = new Size(360, 56), Margin = new Padding(0, 0, 0, 6) };

        var lblName = new Label { Text = item.Name, Font = AppFonts.LabelLg, Location = new Point(0, 4), AutoSize = true };
        var lblLine = new Label { Text = $"{qty} x {item.Price:N0}đ", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(0, 26), AutoSize = true };
        var lblSum = new Label { Text = $"{item.Price * qty:N0}đ", Font = AppFonts.LabelLg, Location = new Point(280, 14), AutoSize = true };

        var btnMinus = new Button { Text = "-", Size = new Size(24, 24), Location = new Point(230, 12), FlatStyle = FlatStyle.Flat };
        btnMinus.Click += (s, e) =>
        {
            var (i, q) = _cart[item.Id];
            if (q <= 1) _cart.Remove(item.Id); else _cart[item.Id] = (i, q - 1);
            RenderCart();
        };

        row.Controls.Add(lblName);
        row.Controls.Add(lblLine);
        row.Controls.Add(lblSum);
        row.Controls.Add(btnMinus);
        return row;
    }

    private async Task ProcessPaymentAsync(int method)
    {
        // TODO: hien tai dang gia lap - can thay bang luong that:
        // 1. Goi POST /api/orders/add-items de tao Order + OrderItems that tu _cart
        // 2. Lay OrderId tra ve, goi POST /api/payments voi OrderId + method that
        // Hien Backend Payments dang yeu cau OrderId co san, chua ho tro tao truc tiep tu gio hang.
        if (_cart.Count == 0)
        {
            MessageBox.Show("Giỏ hàng đang trống");
            return;
        }

        if (method == 1)
        {
            ShowQrPlaceholder();
        }
        else
        {
            MessageBox.Show("Đã xác nhận thanh toán tiền mặt (demo - chưa nối API thật)");
            _cart.Clear();
            RenderCart();
        }
    }

    private void ShowQrPlaceholder()
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode($"DEMO-VIETQR-{lblTotalValue.Text}", QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(8);
        using var ms = new MemoryStream(bytes);

        using var dialog = new Form { Text = "Thanh toán VietQR", Width = 300, Height = 340, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
        var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = Image.FromStream(ms) };
        dialog.Controls.Add(pic);
        dialog.ShowDialog();
    }
}
