using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh Quan ly Thuc don - chi danh cho Role QuanLy.
// Gom 2 nghiep vu theo dung de cuong:
//  1) Quan ly loai do an, loai thuc uong (Categories) - nut "Quan ly loai mon".
//  2) Quan ly mon an, thuc uong (MenuItems) - CRUD day du + loc theo loai mon.
// Da noi that toan bo API: GET/POST/PUT/DELETE /api/menu va /api/categories
// (truoc day chi co GET, Them/Sua/Xoa la demo bang MessageBox).
public partial class MenuManagementControl : UserControl
{
    private readonly ApiService _api = new();

    private List<CategoryDto> _categories = new();
    private List<StationDto> _stations = new();
    private List<MenuItemDto> _allItems = new();
    private int? _selectedCategoryId; // null = "Tất cả món"
    private string _searchText = "";
    private static readonly string[] UnitOptions = { "Phần", "Suất", "Tô", "Dĩa", "Ly", "Chai", "Lon", "Cái" };

    public MenuManagementControl()
    {
        InitializeComponent();
        // Designer neo (Anchor) theo kich thuoc 1200x760 nen khi cua so lon hon se chua trong ben phai.
        // Bo Anchor, tu can lai theo kich thuoc that moi khi doi cua so.
        foreach (Control c in new Control[] { flowCategoryTabs, flowMenuCards, btnAddNew, btnManageCategories })
            c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Resize += (s, e) => FitLayout();
        Load += (s, e) => FitLayout();
        Disposed += (s, e) => _tip.Dispose();

        btnAddNew.Click += (s, e) => OpenEditItemDialog(null);
        btnManageCategories.Click += (s, e) => OpenManageCategoriesDialog();
        txtSearch.TextChanged += (s, e) => { _searchText = txtSearch.Text.Trim(); RenderGrid(); };
        Load += async (s, e) => await LoadAllAsync();
    }

    private void FitLayout()
    {
        const int margin = 19;
        int w = Math.Max(300, ClientSize.Width - margin * 2);
        flowCategoryTabs.Width = w;
        flowMenuCards.SetBounds(margin, flowMenuCards.Top, w, Math.Max(100, ClientSize.Height - flowMenuCards.Top - 16));
        btnAddNew.Left = ClientSize.Width - margin - btnAddNew.Width;
        btnManageCategories.Left = btnAddNew.Left - 16 - btnManageCategories.Width;
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _categories = await _api.GetCategoriesAsync();
            _stations = await _api.GetStationsAsync();
            RenderCategoryTabs();
            await LoadMenuAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được dữ liệu thực đơn: {ex.Message}");
        }
    }

    private async Task LoadMenuAsync()
    {
        try
        {
            _allItems = await _api.GetMenuAsync();
            RenderGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được thực đơn: {ex.Message}");
        }
    }

    // =====================================================================
    // TAB LỌC THEO LOẠI MÓN
    // =====================================================================

    private void RenderCategoryTabs()
    {
        flowCategoryTabs.Controls.Clear();
        flowCategoryTabs.Controls.Add(BuildTab("Tất cả món", null));
        foreach (var c in _categories)
            flowCategoryTabs.Controls.Add(BuildTab(c.Name, c.Id));
    }

    private Label BuildTab(string text, int? categoryId)
    {
        bool selected = _selectedCategoryId == categoryId;
        var lbl = new Label
        {
            Text = text,
            Font = selected ? AppFonts.LabelLg : AppFonts.BodyMd,
            ForeColor = selected ? AppColors.Primary : AppColors.OnSurfaceVariant,
            AutoSize = true,
            Margin = new Padding(0, 6, 24, 6),
            Cursor = Cursors.Hand,
            Tag = categoryId,
        };
        lbl.Click += (s, e) =>
        {
            _selectedCategoryId = categoryId;
            RenderCategoryTabs();
            RenderGrid();
        };
        return lbl;
    }

    // =====================================================================
    // LƯỚI MÓN ĂN / THỨC UỐNG
    // =====================================================================

    private void RenderGrid()
    {
        IEnumerable<MenuItemDto> items = _allItems;

        if (_selectedCategoryId is not null)
            items = items.Where(i => i.CategoryId == _selectedCategoryId);

        if (!string.IsNullOrWhiteSpace(_searchText))
            items = items.Where(i => i.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        flowMenuCards.SuspendLayout();
        // Dispose the cu (Controls.Clear() khong dispose) de giai phong anh nen cua tung the.
        foreach (Control old in flowMenuCards.Controls.Cast<Control>().ToList())
        {
            flowMenuCards.Controls.Remove(old);
            old.Dispose();
        }
        foreach (var item in items)
            flowMenuCards.Controls.Add(BuildMenuCard(item));
        flowMenuCards.Controls.Add(BuildAddNewTile());
        flowMenuCards.ResumeLayout();
    }

    // Kich thuoc the mon dung chung cho ca BuildMenuCard va BuildAddNewTile, de 2
    // loai the luon thang hang nhau trong FlowLayoutPanel.
    private const int CardWidth = 236;
    // Chieu cao 1 dong chu (theo font that) -> chieu cao the tinh tu do, khong hardcode pixel.
    private static readonly int CardLineH = TextRenderer.MeasureText("Ag", AppFonts.LabelLg).Height;
    private static readonly int CardHeight = 148 + CardLineH * 2 + 2 + 2 + CardLineH + 8 + 32 + CardPad;
    private readonly ToolTip _tip = new();
    private const int CardPad = 12;

    private Panel BuildMenuCard(MenuItemDto item)
    {
        var card = new Panel { Size = new Size(CardWidth, CardHeight), BackColor = Color.White, Margin = new Padding(8) };
        var thumb = new Panel { Size = new Size(CardWidth, 140), Location = new Point(0, 0), BackColor = AppColors.SurfaceContainer };

        // Nhan loai mon nen ban trong suot de van doc duoc khi nam de len anh mon.
        var lblCategory = new Label { Text = item.CategoryName, Font = AppFonts.LabelMd, ForeColor = AppColors.OnSurface, BackColor = Color.FromArgb(210, 255, 255, 255), Padding = new Padding(4, 1, 4, 1), Location = new Point(CardPad, 10), AutoSize = true };
        thumb.Controls.Add(lblCategory);

        // Anh mon: tai bat dong bo (co cache trong ApiService), chua co anh thi hien chu "Chua co anh".
        thumb.Disposed += (s, e) => thumb.BackgroundImage?.Dispose();
        Label? lblNoImage = null;
        if (string.IsNullOrWhiteSpace(item.ImageUrl))
        {
            lblNoImage = new Label { Text = "Chưa có ảnh", Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, AutoSize = true };
            thumb.Controls.Add(lblNoImage);
            var noImgSize = TextRenderer.MeasureText(lblNoImage.Text, lblNoImage.Font);
            lblNoImage.Location = new Point((CardWidth - noImgSize.Width) / 2, 60);
        }
        else
        {
            _ = LoadThumbAsync(thumb, item.ImageUrl);
        }

        // Badge "Con hang/Het hang": do AutoSize=true nen phai TAO XONG (co Text +
        // Padding) roi moi do Width that de tinh Location ep sat le phai cua thumb.
        // Truoc day hardcode X=140 nen voi thumb rong 220 va chu "Con hang" no se
        // tran ra ngoai/qua sat mep, gay nhin roi.
        var badge = new Label
        {
            Text = item.IsAvailable ? "Còn hàng" : "Hết hàng",
            Font = AppFonts.LabelMd,
            ForeColor = Color.White,
            BackColor = item.IsAvailable ? AppColors.Primary : AppColors.Outline,
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Cursor = Cursors.Hand,
        };
        thumb.Controls.Add(badge);
        badge.Location = new Point(thumb.Width - badge.Width - CardPad, 10);
        // Bam vao nhan "Con hang/Het hang" de doi trang thai nhanh, khong can mo form Sua.
        badge.Click += async (s, e) => await ToggleAvailableAsync(item);

        // Bo cuc the: ten mon (toi da 2 dong, dai thi "...") -> hang gia + bo phan -> nut Sua/Xoa.
        // Chieu cao tinh theo font that (CardLineH) nen khong bi cat chu o moi do phan giai.
        int nameY = 148;
        int nameH = CardLineH * 2 + 2;
        int rowY = nameY + nameH + 2;
        int btnY = rowY + CardLineH + 8;

        var lblName = new Label
        {
            Text = item.Name,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.OnSurface,
            Location = new Point(CardPad, nameY),
            Size = new Size(CardWidth - CardPad * 2, nameH),
            AutoSize = false,
            AutoEllipsis = true,
        };
        _tip.SetToolTip(lblName, item.Name); // re chuot vao xem ten day du

        var priceText = $"{item.Price:N0}đ";
        var priceSize = TextRenderer.MeasureText(priceText, AppFonts.LabelLg);
        var lblPrice = new Label
        {
            Text = priceText,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.Primary,
            AutoSize = true,
            Location = new Point(CardPad, rowY),
        };

        // Bo phan che bien: canh phai cung hang voi gia, phan con lai sau gia, qua dai thi "...".
        int stationX = CardPad + priceSize.Width + 8;
        var lblStation = new Label
        {
            Text = string.IsNullOrEmpty(item.DepartmentName) ? "Chưa gán bộ phận" : item.DepartmentName,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            Location = new Point(stationX, rowY),
            Size = new Size(Math.Max(40, CardWidth - CardPad - stationX), CardLineH),
            TextAlign = ContentAlignment.TopRight,
            AutoEllipsis = true,
        };
        var btnEdit = new Button { Text = "Sửa", Location = new Point(CardPad, btnY), Size = new Size(100, 32), FlatStyle = FlatStyle.Flat };
        var btnDelete = new Button { Text = "Xóa", Location = new Point(CardPad + 108, btnY), Size = new Size(100, 32), FlatStyle = FlatStyle.Flat, ForeColor = AppColors.Error };

        btnEdit.Click += (s, e) => OpenEditItemDialog(item);
        btnDelete.Click += async (s, e) => await ConfirmDeleteAsync(item);

        card.Controls.Add(thumb);
        card.Controls.Add(lblName);
        card.Controls.Add(lblPrice);
        card.Controls.Add(lblStation);
        card.Controls.Add(btnEdit);
        card.Controls.Add(btnDelete);
        card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, card.Width - 1, card.Height - 1);
        return card;
    }

    private async Task LoadThumbAsync(Panel thumb, string? imageUrl)
    {
        var img = await _api.LoadMenuImageAsync(imageUrl);
        if (img is null) return;
        using (img)
        {
            if (thumb.IsDisposed) return;
            var old = thumb.BackgroundImage;
            thumb.BackgroundImage = CropToFill(img, thumb.Size);
            thumb.BackgroundImageLayout = ImageLayout.None;
            old?.Dispose();
        }
    }

    // Cat anh cho day vua khung (kieu "cover"), giu ty le, khong bi meo.
    private static Bitmap CropToFill(Image src, Size target)
    {
        var bmp = new Bitmap(target.Width, target.Height);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale = Math.Max((float)target.Width / src.Width, (float)target.Height / src.Height);
        float w = target.Width / scale, h = target.Height / scale;
        g.DrawImage(src, new Rectangle(Point.Empty, target),
            (src.Width - w) / 2, (src.Height - h) / 2, w, h, GraphicsUnit.Pixel);
        return bmp;
    }

    private Panel BuildAddNewTile()
    {
        var tile = new Panel { Size = new Size(CardWidth, CardHeight), BackColor = Color.FromArgb(253, 245, 242), Margin = new Padding(8), Cursor = Cursors.Hand };
        var lblPlus = new Label { Text = "+", Font = new Font("Segoe UI", 24), ForeColor = AppColors.Primary, AutoSize = true };
        var lblText = new Label { Text = "Thêm món ăn mới", Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, AutoSize = true };
        tile.Controls.Add(lblPlus);
        tile.Controls.Add(lblText);
        // Can giua theo chieu ngang thay vi hardcode X (95 / 55) chi dung cho dung
        // 1 kich thuoc the cu 220px; neu doi CardWidth sau nay se tu dong can lai.
        lblPlus.Location = new Point((CardWidth - lblPlus.Width) / 2, 96);
        lblText.Location = new Point((CardWidth - lblText.Width) / 2, 140);
        tile.Click += (s, e) => OpenEditItemDialog(null);
        return tile;
    }

    private async Task ToggleAvailableAsync(MenuItemDto item)
    {
        try
        {
            await _api.SetMenuItemAvailableAsync(item.Id, !item.IsAvailable);
            await LoadMenuAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không cập nhật được trạng thái: {ex.Message}");
        }
    }

    // =====================================================================
    // THÊM / SỬA MÓN ĂN, THỨC UỐNG
    // =====================================================================

    private void OpenEditItemDialog(MenuItemDto? existing)
    {
        if (_categories.Count == 0)
        {
            MessageBox.Show("Chưa có loại món nào, hãy bấm \"Quản lý loại món\" để tạo loại món trước.");
            return;
        }

        using var dialog = new Form
        {
            Text = existing is null ? "Thêm món mới" : "Sửa món",
            Width = 380,
            Height = 600,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblName = new Label { Text = "Tên món *", Font = AppFonts.BodyMd, Location = new Point(20, 16), AutoSize = true };
        var txtName = new TextBox { Location = new Point(20, 36), Width = 320, Font = AppFonts.BodyMd, Text = existing?.Name ?? "" };

        var lblPrice = new Label { Text = "Giá bán (đ) *", Font = AppFonts.BodyMd, Location = new Point(20, 72), AutoSize = true };
        var numPrice = new NumericUpDown { Location = new Point(20, 92), Width = 150, Minimum = 0, Maximum = 100_000_000, Increment = 1000, ThousandsSeparator = true, Value = existing?.Price ?? 0 };

        var lblUnit = new Label { Text = "Đơn vị tính", Font = AppFonts.BodyMd, Location = new Point(190, 72), AutoSize = true };
        var cbUnit = new ComboBox { Location = new Point(190, 92), Width = 150, Font = AppFonts.BodyMd, DropDownStyle = ComboBoxStyle.DropDownList };
        cbUnit.Items.AddRange(UnitOptions);
        var preselectUnit = existing is null ? -1 : Array.IndexOf(UnitOptions, existing.Unit);
        cbUnit.SelectedIndex = preselectUnit >= 0 ? preselectUnit : 0;

        var lblCategory = new Label { Text = "Loại món *", Font = AppFonts.BodyMd, Location = new Point(20, 128), AutoSize = true };
        var cbCategory = new ComboBox { Location = new Point(20, 148), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbCategory.Items.AddRange(_categories.Cast<object>().ToArray());
        var preselectCategory = existing is null ? 0 : _categories.FindIndex(c => c.Id == existing.CategoryId);
        cbCategory.SelectedIndex = preselectCategory >= 0 ? preselectCategory : 0;

        var lblStation = new Label { Text = "Bộ phận chế biến", Font = AppFonts.BodyMd, Location = new Point(20, 184), AutoSize = true };
        var cbStation = new ComboBox { Location = new Point(20, 204), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        cbStation.Items.Add("(Không chọn)");
        cbStation.Items.AddRange(_stations.Cast<object>().ToArray());
        var preselectStation = existing?.DepartmentId is int depId and > 0 ? _stations.FindIndex(s => s.Id == depId) : -1;
        cbStation.SelectedIndex = preselectStation >= 0 ? preselectStation + 1 : 0;

        var lblDescription = new Label { Text = "Mô tả", Font = AppFonts.BodyMd, Location = new Point(20, 240), AutoSize = true };
        var txtDescription = new TextBox { Location = new Point(20, 260), Width = 320, Height = 70, Font = AppFonts.BodyMd, Multiline = true, Text = existing?.Description ?? "" };

        var chkAvailable = new CheckBox { Text = "Đang bán (Còn hàng)", Font = AppFonts.BodyMd, Location = new Point(20, 340), AutoSize = true, Checked = existing?.IsAvailable ?? true };

        dialog.Controls.Add(lblName);
        dialog.Controls.Add(txtName);
        dialog.Controls.Add(lblPrice);
        dialog.Controls.Add(numPrice);
        dialog.Controls.Add(lblUnit);
        dialog.Controls.Add(cbUnit);
        dialog.Controls.Add(lblCategory);
        dialog.Controls.Add(cbCategory);
        dialog.Controls.Add(lblStation);
        dialog.Controls.Add(cbStation);
        dialog.Controls.Add(lblDescription);
        dialog.Controls.Add(txtDescription);
        dialog.Controls.Add(chkAvailable);

        // ---------- Anh mon ----------
        // pendingImagePath: file vua chon (chua upload, se upload sau khi luu mon thanh cong).
        // removeImage: nguoi dung bam "Go anh" tren mon dang co anh.
        string? pendingImagePath = null;
        var removeImage = false;

        var lblImage = new Label { Text = "Ảnh món (jpg, png, webp - tối đa 5 MB)", Font = AppFonts.BodyMd, Location = new Point(20, 372), AutoSize = true };
        var picPreview = new PictureBox { Location = new Point(20, 394), Size = new Size(150, 100), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = AppColors.SurfaceContainer };
        var btnPickImage = new Button { Text = "Chọn ảnh...", Location = new Point(190, 394), Size = new Size(150, 32), FlatStyle = FlatStyle.Flat };
        var btnClearImage = new Button { Text = "Gỡ ảnh", Location = new Point(190, 434), Size = new Size(150, 32), FlatStyle = FlatStyle.Flat, ForeColor = AppColors.Error };
        dialog.Controls.Add(lblImage);
        dialog.Controls.Add(picPreview);
        dialog.Controls.Add(btnPickImage);
        dialog.Controls.Add(btnClearImage);
        dialog.Disposed += (s, e) => picPreview.Image?.Dispose();

        // Mon dang co anh -> tai anh hien tai de xem truoc.
        if (!string.IsNullOrWhiteSpace(existing?.ImageUrl))
        {
            _ = LoadCurrentPreviewAsync();
        }

        async Task LoadCurrentPreviewAsync()
        {
            var img = await _api.LoadMenuImageAsync(existing!.ImageUrl);
            if (img is null) return;
            // Nguoi dung da chon anh khac / go anh / dong dialog trong luc cho tai -> bo qua.
            if (picPreview.IsDisposed || pendingImagePath != null || removeImage) { img.Dispose(); return; }
            picPreview.Image = img;
        }

        btnPickImage.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Chọn ảnh món",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            if (new FileInfo(ofd.FileName).Length > 5 * 1024 * 1024)
            {
                MessageBox.Show("Ảnh quá lớn, vui lòng chọn ảnh tối đa 5 MB.");
                return;
            }

            try
            {
                // Doc qua bo nho de khong khoa file goc tren dia.
                using var ms = new MemoryStream(File.ReadAllBytes(ofd.FileName));
                using var raw = Image.FromStream(ms);
                var old = picPreview.Image;
                picPreview.Image = new Bitmap(raw);
                old?.Dispose();
                pendingImagePath = ofd.FileName;
                removeImage = false;
            }
            catch
            {
                MessageBox.Show("Không đọc được file ảnh này (WebP có thể chưa được Windows Forms hỗ trợ xem trước). Hãy thử ảnh JPG hoặc PNG.");
            }
        };

        btnClearImage.Click += (s, e) =>
        {
            var old = picPreview.Image;
            picPreview.Image = null;
            old?.Dispose();
            pendingImagePath = null;
            removeImage = !string.IsNullOrWhiteSpace(existing?.ImageUrl);
        };

        var btnSave = new Button { Text = "Lưu", Location = new Point(130, 510), Size = new Size(120, 34), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        btnSave.FlatAppearance.BorderSize = 0;
        dialog.Controls.Add(btnSave);
        dialog.AcceptButton = btnSave;

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(txtName.Text) || cbCategory.SelectedItem is not CategoryDto category)
        {
            MessageBox.Show("Vui lòng nhập tên món và chọn loại món");
            return;
        }

        int? stationId = cbStation.SelectedIndex > 0 && cbStation.SelectedItem is StationDto station ? station.Id : null;

        _ = SaveMenuItemAsync(
            existing?.Id,
            txtName.Text.Trim(),
            numPrice.Value,
            category.Id,
            stationId,
            cbUnit.SelectedItem?.ToString(),   // trước là txtUnit.Text.Trim()
            txtDescription.Text.Trim(),
            chkAvailable.Checked,
            pendingImagePath,
            removeImage);
    }

    private async Task SaveMenuItemAsync(int? id, string name, decimal price, int categoryId, int? stationId, string? unit, string? description, bool isAvailable, string? newImagePath = null, bool removeImage = false)
    {
        int itemId;
        try
        {
            if (id.HasValue)
            {
                await _api.UpdateMenuItemAsync(id.Value, name, price, categoryId, stationId, unit, description, isAvailable);
                itemId = id.Value;
            }
            else
            {
                itemId = await _api.CreateMenuItemAsync(name, price, categoryId, stationId, unit, description, isAvailable);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được món: {ex.Message}");
            return;
        }

        // Mon da luu xong -> xu ly anh rieng, loi anh khong lam mat du lieu mon vua luu.
        try
        {
            if (newImagePath != null && itemId > 0)
                await _api.UploadMenuItemImageAsync(itemId, newImagePath);
            else if (removeImage && itemId > 0)
                await _api.DeleteMenuItemImageAsync(itemId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Đã lưu món nhưng chưa cập nhật được ảnh: {ex.Message}");
        }

        await LoadMenuAsync();
    }

    private async Task ConfirmDeleteAsync(MenuItemDto item)
    {
        var result = MessageBox.Show($"Xóa món \"{item.Name}\"?", "Xác nhận", MessageBoxButtons.YesNo);
        if (result != DialogResult.Yes) return;

        try
        {
            await _api.DeleteMenuItemAsync(item.Id);
            await LoadMenuAsync();
        }
        catch (Exception ex)
        {
            // Backend chan xoa neu mon da tung xuat hien trong don hang - hien thi
            // nguyen van message loi tra ve (vd goi y doi sang "Het hang" thay vi xoa).
            MessageBox.Show($"Không xóa được: {ex.Message}");
        }
    }

    // =====================================================================
    // QUẢN LÝ LOẠI MÓN (Categories)
    // =====================================================================

    private void OpenManageCategoriesDialog()
    {
        using var dialog = new Form
        {
            Text = "Quản lý loại món",
            Width = 440,
            Height = 500,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var flowCategories = new FlowLayoutPanel { Location = new Point(16, 16), Size = new Size(390, 320), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        dialog.Controls.Add(flowCategories);

        var txtNewCategory = new TextBox { Location = new Point(16, 350), Width = 190, Font = AppFonts.BodyMd, PlaceholderText = "Tên loại món mới" };
        var cbNewType = new ComboBox { Location = new Point(214, 350), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        cbNewType.Items.AddRange(new object[] { "Món ăn", "Thức uống" });
        cbNewType.SelectedIndex = 0;
        var btnAddCategory = new Button { Text = "+  Thêm", Location = new Point(332, 348), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };
        btnAddCategory.FlatAppearance.BorderSize = 0;
        dialog.Controls.Add(txtNewCategory);
        dialog.Controls.Add(cbNewType);
        dialog.Controls.Add(btnAddCategory);

        var btnClose = new Button { Text = "Đóng", Location = new Point(160, 420), Size = new Size(120, 32), FlatStyle = FlatStyle.Flat, BackColor = AppColors.Secondary, ForeColor = Color.White, DialogResult = DialogResult.OK };
        dialog.Controls.Add(btnClose);
        dialog.AcceptButton = btnClose;

        async Task RefreshCategoryListAsync()
        {
            _categories = await _api.GetCategoriesAsync();
            flowCategories.Controls.Clear();
            foreach (var category in _categories)
                flowCategories.Controls.Add(BuildCategoryRow(category, RefreshCategoryListAsync));
        }

        btnAddCategory.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtNewCategory.Text))
            {
                MessageBox.Show("Vui lòng nhập tên loại món");
                return;
            }
            try
            {
                await _api.CreateCategoryAsync(txtNewCategory.Text.Trim(), cbNewType.SelectedIndex);
                txtNewCategory.Clear();
                await RefreshCategoryListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thêm được loại món: {ex.Message}");
            }
        };

        _ = RefreshCategoryListAsync();
        dialog.ShowDialog();

        // Dong dialog xong thi lam moi lai tab loc + luoi mon (ten loai mon co the da
        // doi, hoac 1 loai mon dang duoc chon co the da bi xoa).
        _selectedCategoryId = _categories.Any(c => c.Id == _selectedCategoryId) ? _selectedCategoryId : null;
        RenderCategoryTabs();
        _ = LoadMenuAsync();
    }

    private Panel BuildCategoryRow(CategoryDto category, Func<Task> onChanged)
    {
        var row = new Panel { Size = new Size(380, 44), BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
        row.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(AppColors.OutlineVariant), 0, 0, row.Width - 1, row.Height - 1);

        var lblName = new Label { Text = category.Name, Font = AppFonts.LabelLg, Location = new Point(10, 12), AutoSize = true };
        var lblType = new Label { Text = category.TypeLabel, Font = AppFonts.LabelMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(180, 14), AutoSize = true };
        var lblEdit = new Label { Text = "Sửa", Font = AppFonts.LabelMd, ForeColor = AppColors.Primary, Location = new Point(280, 14), AutoSize = true, Cursor = Cursors.Hand };
        var lblDelete = new Label { Text = "Xóa", Font = AppFonts.LabelMd, ForeColor = AppColors.Error, Location = new Point(330, 14), AutoSize = true, Cursor = Cursors.Hand };

        lblEdit.Click += async (s, e) =>
        {
            var (newName, newType) = PromptForCategory(category.Name, category.Type);
            if (newName is null) return; // nguoi dung bam Huy
            try
            {
                await _api.UpdateCategoryAsync(category.Id, newName, newType);
                await onChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không sửa được: {ex.Message}");
            }
        };

        lblDelete.Click += async (s, e) =>
        {
            if (MessageBox.Show($"Xóa loại món \"{category.Name}\"?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                await _api.DeleteCategoryAsync(category.Id);
                await onChanged();
            }
            catch (Exception ex)
            {
                // Backend chan xoa neu loai mon dang co mon an/thuc uong ben trong
                MessageBox.Show($"Không xóa được: {ex.Message}");
            }
        };

        row.Controls.Add(lblName);
        row.Controls.Add(lblType);
        row.Controls.Add(lblEdit);
        row.Controls.Add(lblDelete);
        return row;
    }

    // Dialog nhap Ten + Loai (Mon an/Thuc uong), dung cho thao tac "Sua loai mon".
    private (string? name, int type) PromptForCategory(string initialName, int initialType)
    {
        using var dialog = new Form { Text = "Sửa loại món", Width = 340, Height = 220, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

        var lblName = new Label { Text = "Tên loại món", Font = AppFonts.BodyMd, Location = new Point(20, 20), AutoSize = true };
        var txt = new TextBox { Location = new Point(20, 40), Width = 280, Font = AppFonts.BodyMd, Text = initialName };

        var lblType = new Label { Text = "Loại", Font = AppFonts.BodyMd, Location = new Point(20, 80), AutoSize = true };
        var cbType = new ComboBox { Location = new Point(20, 100), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
        cbType.Items.AddRange(new object[] { "Món ăn", "Thức uống" });
        cbType.SelectedIndex = initialType == 1 ? 1 : 0;

        var btnOk = new Button { Text = "Lưu", Location = new Point(110, 140), Size = new Size(100, 32), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = AppColors.Primary, ForeColor = Color.White };

        dialog.Controls.Add(lblName);
        dialog.Controls.Add(txt);
        dialog.Controls.Add(lblType);
        dialog.Controls.Add(cbType);
        dialog.Controls.Add(btnOk);
        dialog.AcceptButton = btnOk;

        if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(txt.Text))
            return (null, 0);

        return (txt.Text.Trim(), cbType.SelectedIndex);
    }


}