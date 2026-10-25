using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class CustomerManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel tlpRoot;
    private TableLayoutPanel tlpHeader;
    private Panel panelTitles;
    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;

    private TableLayoutPanel tlpStats;
    private QuanAnWinForm.Controls.StatCard cardTotalCustomers;
    private QuanAnWinForm.Controls.StatCard cardVipCustomers;
    private QuanAnWinForm.Controls.StatCard cardNewCustomers;
    private QuanAnWinForm.Controls.StatCard cardWebAccounts;

    private TableLayoutPanel tlpToolbar;
    private Panel panelSearch;
    private TextBox txtSearch;
    private FlowLayoutPanel flowActions;
    private Button btnViewDetail;
    private Button btnEdit;
    private Button btnDelete;
    private Button btnToggleWeb;

    private Panel panelGridCard;
    private DataGridView gridCustomers;
    private DataGridViewTextBoxColumn colId;
    private DataGridViewTextBoxColumn colName;
    private DataGridViewTextBoxColumn colPhone;
    private DataGridViewTextBoxColumn colEmail;
    private DataGridViewTextBoxColumn colDob;
    private DataGridViewTextBoxColumn colPoint;
    private DataGridViewTextBoxColumn colLevel;
    private DataGridViewTextBoxColumn colOrders;
    private DataGridViewTextBoxColumn colSpent;
    private DataGridViewTextBoxColumn colWebAccount;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        DataGridViewCellStyle styleHeader = new DataGridViewCellStyle();
        DataGridViewCellStyle styleCell = new DataGridViewCellStyle();
        DataGridViewCellStyle styleAlt = new DataGridViewCellStyle();
        DataGridViewCellStyle styleRight = new DataGridViewCellStyle();

        tlpRoot = new TableLayoutPanel();
        tlpHeader = new TableLayoutPanel();
        panelTitles = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnAddNew = new Button();
        tlpStats = new TableLayoutPanel();
        cardTotalCustomers = new QuanAnWinForm.Controls.StatCard();
        cardVipCustomers = new QuanAnWinForm.Controls.StatCard();
        cardNewCustomers = new QuanAnWinForm.Controls.StatCard();
        cardWebAccounts = new QuanAnWinForm.Controls.StatCard();
        tlpToolbar = new TableLayoutPanel();
        panelSearch = new Panel();
        txtSearch = new TextBox();
        flowActions = new FlowLayoutPanel();
        btnViewDetail = new Button();
        btnEdit = new Button();
        btnDelete = new Button();
        btnToggleWeb = new Button();
        panelGridCard = new Panel();
        gridCustomers = new DataGridView();
        colId = new DataGridViewTextBoxColumn();
        colName = new DataGridViewTextBoxColumn();
        colPhone = new DataGridViewTextBoxColumn();
        colEmail = new DataGridViewTextBoxColumn();
        colDob = new DataGridViewTextBoxColumn();
        colPoint = new DataGridViewTextBoxColumn();
        colLevel = new DataGridViewTextBoxColumn();
        colOrders = new DataGridViewTextBoxColumn();
        colSpent = new DataGridViewTextBoxColumn();
        colWebAccount = new DataGridViewTextBoxColumn();
        ((System.ComponentModel.ISupportInitialize)gridCustomers).BeginInit();
        tlpRoot.SuspendLayout();
        tlpHeader.SuspendLayout();
        panelTitles.SuspendLayout();
        tlpStats.SuspendLayout();
        tlpToolbar.SuspendLayout();
        panelSearch.SuspendLayout();
        flowActions.SuspendLayout();
        panelGridCard.SuspendLayout();
        SuspendLayout();
        //
        // tlpRoot : tiêu đề | thẻ thống kê | thanh công cụ | bảng
        //
        tlpRoot.ColumnCount = 1;
        tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpRoot.RowCount = 4;
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 156F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpRoot.Controls.Add(tlpHeader, 0, 0);
        tlpRoot.Controls.Add(tlpStats, 0, 1);
        tlpRoot.Controls.Add(tlpToolbar, 0, 2);
        tlpRoot.Controls.Add(panelGridCard, 0, 3);
        tlpRoot.Dock = DockStyle.Fill;
        tlpRoot.Margin = new Padding(0);
        tlpRoot.Name = "tlpRoot";
        //
        // tlpHeader : tiêu đề bên trái, nút "Thêm khách hàng" bên phải
        //
        tlpHeader.ColumnCount = 2;
        tlpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        tlpHeader.RowCount = 1;
        tlpHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpHeader.Controls.Add(panelTitles, 0, 0);
        tlpHeader.Controls.Add(btnAddNew, 1, 0);
        tlpHeader.Dock = DockStyle.Fill;
        tlpHeader.Margin = new Padding(0);
        tlpHeader.Name = "tlpHeader";
        //
        // panelTitles
        //
        panelTitles.Controls.Add(lblTitle);
        panelTitles.Controls.Add(lblSubtitle);
        panelTitles.Dock = DockStyle.Fill;
        panelTitles.Margin = new Padding(0);
        panelTitles.Name = "panelTitles";
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(-2, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Text = "Quản lý Khách hàng";
        //
        // lblSubtitle
        //
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(1, 50);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Text = "Quản lý thông tin khách hàng và chương trình khách hàng thân thiết (tích điểm, hạng thành viên).";
        //
        // btnAddNew
        //
        btnAddNew.Anchor = AnchorStyles.Right;
        btnAddNew.BackColor = AppColors.Primary;
        btnAddNew.Cursor = Cursors.Hand;
        btnAddNew.FlatAppearance.BorderSize = 0;
        btnAddNew.FlatStyle = FlatStyle.Flat;
        btnAddNew.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddNew.ForeColor = Color.White;
        btnAddNew.Margin = new Padding(0);
        btnAddNew.Name = "btnAddNew";
        btnAddNew.Size = new Size(200, 44);
        btnAddNew.TabIndex = 2;
        btnAddNew.Text = "+  Thêm khách hàng";
        btnAddNew.UseVisualStyleBackColor = false;
        //
        // tlpStats : 4 thẻ chia đều chiều ngang
        //
        tlpStats.ColumnCount = 4;
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.RowCount = 1;
        tlpStats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpStats.Controls.Add(cardTotalCustomers, 0, 0);
        tlpStats.Controls.Add(cardVipCustomers, 1, 0);
        tlpStats.Controls.Add(cardNewCustomers, 2, 0);
        tlpStats.Controls.Add(cardWebAccounts, 3, 0);
        tlpStats.Dock = DockStyle.Fill;
        tlpStats.Margin = new Padding(0, 0, 0, 16);
        tlpStats.Name = "tlpStats";
        //
        // 4 thẻ thống kê
        //
        cardTotalCustomers.BackColor = Color.White;
        cardTotalCustomers.Dock = DockStyle.Fill;
        cardTotalCustomers.Margin = new Padding(0, 0, 16, 0);
        cardTotalCustomers.Name = "cardTotalCustomers";
        cardVipCustomers.BackColor = Color.White;
        cardVipCustomers.Dock = DockStyle.Fill;
        cardVipCustomers.Margin = new Padding(0, 0, 16, 0);
        cardVipCustomers.Name = "cardVipCustomers";
        cardNewCustomers.BackColor = Color.White;
        cardNewCustomers.Dock = DockStyle.Fill;
        cardNewCustomers.Margin = new Padding(0, 0, 16, 0);
        cardNewCustomers.Name = "cardNewCustomers";
        cardWebAccounts.BackColor = Color.White;
        cardWebAccounts.Dock = DockStyle.Fill;
        cardWebAccounts.Margin = new Padding(0);
        cardWebAccounts.Name = "cardWebAccounts";
        //
        // tlpToolbar : ô tìm kiếm (trái) | khoảng trống | nhóm nút thao tác (phải)
        //
        tlpToolbar.ColumnCount = 3;
        tlpToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380F));
        tlpToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlpToolbar.RowCount = 1;
        tlpToolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpToolbar.Controls.Add(panelSearch, 0, 0);
        tlpToolbar.Controls.Add(flowActions, 2, 0);
        tlpToolbar.Dock = DockStyle.Fill;
        tlpToolbar.Margin = new Padding(0);
        tlpToolbar.Name = "tlpToolbar";
        //
        // panelSearch
        //
        panelSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        panelSearch.BackColor = Color.White;
        panelSearch.Controls.Add(txtSearch);
        panelSearch.Margin = new Padding(0);
        panelSearch.Name = "panelSearch";
        panelSearch.Size = new Size(372, 40);
        //
        // txtSearch
        //
        txtSearch.BackColor = Color.White;
        txtSearch.BorderStyle = BorderStyle.None;
        txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtSearch.Font = new Font("Segoe UI", 10.5F);
        txtSearch.Location = new Point(42, 9);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "Tìm theo tên hoặc số điện thoại...";
        txtSearch.Size = new Size(320, 23);
        txtSearch.TabIndex = 3;
        //
        // flowActions
        //
        flowActions.AutoSize = true;
        flowActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowActions.Controls.Add(btnViewDetail);
        flowActions.Controls.Add(btnEdit);
        flowActions.Controls.Add(btnDelete);
        flowActions.Controls.Add(btnToggleWeb);
        flowActions.Dock = DockStyle.Fill;
        flowActions.FlowDirection = FlowDirection.LeftToRight;
        flowActions.Margin = new Padding(0);
        flowActions.Name = "flowActions";
        flowActions.WrapContents = false;
        //
        // btnViewDetail
        //
        btnViewDetail.BackColor = AppColors.Tertiary;
        btnViewDetail.Cursor = Cursors.Hand;
        btnViewDetail.FlatAppearance.BorderSize = 0;
        btnViewDetail.FlatStyle = FlatStyle.Flat;
        btnViewDetail.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnViewDetail.ForeColor = Color.White;
        btnViewDetail.Margin = new Padding(0, 8, 8, 8);
        btnViewDetail.Name = "btnViewDetail";
        btnViewDetail.Size = new Size(212, 40);
        btnViewDetail.TabIndex = 7;
        btnViewDetail.Text = "Chi tiết && Điểm tích lũy";
        btnViewDetail.UseVisualStyleBackColor = false;
        //
        // btnEdit
        //
        btnEdit.BackColor = Color.White;
        btnEdit.Cursor = Cursors.Hand;
        btnEdit.FlatAppearance.BorderColor = AppColors.Secondary;
        btnEdit.FlatAppearance.BorderSize = 1;
        btnEdit.FlatStyle = FlatStyle.Flat;
        btnEdit.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnEdit.ForeColor = AppColors.Secondary;
        btnEdit.Margin = new Padding(0, 8, 8, 8);
        btnEdit.Name = "btnEdit";
        btnEdit.Size = new Size(90, 40);
        btnEdit.TabIndex = 8;
        btnEdit.Text = "Sửa";
        btnEdit.UseVisualStyleBackColor = false;
        //
        // btnDelete
        //
        btnDelete.BackColor = Color.White;
        btnDelete.Cursor = Cursors.Hand;
        btnDelete.FlatAppearance.BorderColor = AppColors.Error;
        btnDelete.FlatAppearance.BorderSize = 1;
        btnDelete.FlatStyle = FlatStyle.Flat;
        btnDelete.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnDelete.ForeColor = AppColors.Error;
        btnDelete.Margin = new Padding(0, 8, 8, 8);
        btnDelete.Name = "btnDelete";
        btnDelete.Size = new Size(90, 40);
        btnDelete.TabIndex = 9;
        btnDelete.Text = "Xóa";
        btnDelete.UseVisualStyleBackColor = false;
        //
        // btnToggleWeb
        //
        btnToggleWeb.BackColor = Color.FromArgb(238, 238, 238);
        btnToggleWeb.Cursor = Cursors.Hand;
        btnToggleWeb.Enabled = false;
        btnToggleWeb.FlatAppearance.BorderSize = 0;
        btnToggleWeb.FlatStyle = FlatStyle.Flat;
        btnToggleWeb.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnToggleWeb.ForeColor = Color.White;
        btnToggleWeb.Margin = new Padding(0, 8, 0, 8);
        btnToggleWeb.Name = "btnToggleWeb";
        btnToggleWeb.Size = new Size(176, 40);
        btnToggleWeb.TabIndex = 10;
        btnToggleWeb.Text = "Khóa/Mở TK web";
        btnToggleWeb.UseVisualStyleBackColor = false;
        //
        // panelGridCard : khung viền mảnh quanh bảng
        //
        panelGridCard.BackColor = AppColors.OutlineVariant;
        panelGridCard.Controls.Add(gridCustomers);
        panelGridCard.Dock = DockStyle.Fill;
        panelGridCard.Margin = new Padding(0);
        panelGridCard.Name = "panelGridCard";
        panelGridCard.Padding = new Padding(1);
        //
        // kiểu ô
        //
        styleHeader.Alignment = DataGridViewContentAlignment.MiddleLeft;
        styleHeader.BackColor = Color.FromArgb(246, 246, 246);
        styleHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        styleHeader.ForeColor = Color.FromArgb(84, 96, 103);
        styleHeader.Padding = new Padding(8, 0, 8, 0);
        styleHeader.SelectionBackColor = Color.FromArgb(246, 246, 246);
        styleHeader.SelectionForeColor = Color.FromArgb(84, 96, 103);
        styleHeader.WrapMode = DataGridViewTriState.False;

        styleCell.Alignment = DataGridViewContentAlignment.MiddleLeft;
        styleCell.BackColor = Color.White;
        styleCell.Font = new Font("Segoe UI", 10F);
        styleCell.ForeColor = Color.FromArgb(26, 28, 28);
        styleCell.Padding = new Padding(8, 0, 8, 0);
        styleCell.SelectionBackColor = Color.FromArgb(253, 236, 231);
        styleCell.SelectionForeColor = Color.FromArgb(26, 28, 28);
        styleCell.WrapMode = DataGridViewTriState.False;

        styleAlt.BackColor = Color.FromArgb(251, 251, 251);
        styleAlt.SelectionBackColor = Color.FromArgb(253, 236, 231);
        styleAlt.SelectionForeColor = Color.FromArgb(26, 28, 28);

        styleRight.Alignment = DataGridViewContentAlignment.MiddleRight;
        styleRight.Padding = new Padding(8, 0, 16, 0);
        //
        // gridCustomers
        //
        gridCustomers.AllowUserToAddRows = false;
        gridCustomers.AllowUserToDeleteRows = false;
        gridCustomers.AllowUserToResizeRows = false;
        gridCustomers.AlternatingRowsDefaultCellStyle = styleAlt;
        gridCustomers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridCustomers.BackgroundColor = Color.White;
        gridCustomers.BorderStyle = BorderStyle.None;
        gridCustomers.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        gridCustomers.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        gridCustomers.ColumnHeadersDefaultCellStyle = styleHeader;
        gridCustomers.ColumnHeadersHeight = 44;
        gridCustomers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        gridCustomers.Columns.AddRange(new DataGridViewColumn[] { colId, colName, colPhone, colEmail, colDob, colPoint, colLevel, colOrders, colSpent, colWebAccount });
        gridCustomers.DefaultCellStyle = styleCell;
        gridCustomers.Dock = DockStyle.Fill;
        gridCustomers.EnableHeadersVisualStyles = false;
        gridCustomers.GridColor = Color.FromArgb(226, 226, 226);
        gridCustomers.MultiSelect = false;
        gridCustomers.Name = "gridCustomers";
        gridCustomers.ReadOnly = true;
        gridCustomers.RowHeadersVisible = false;
        gridCustomers.RowTemplate.Height = 46;
        gridCustomers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridCustomers.TabIndex = 6;
        //
        // các cột
        //
        colId.FillWeight = 40F;
        colId.HeaderText = "Mã";
        colId.MinimumWidth = 50;
        colId.Name = "colId";
        colId.ReadOnly = true;
        colName.FillWeight = 130F;
        colName.HeaderText = "Họ tên";
        colName.MinimumWidth = 120;
        colName.Name = "colName";
        colName.ReadOnly = true;
        colPhone.FillWeight = 100F;
        colPhone.HeaderText = "SĐT";
        colPhone.MinimumWidth = 100;
        colPhone.Name = "colPhone";
        colPhone.ReadOnly = true;
        colEmail.FillWeight = 170F;
        colEmail.HeaderText = "Email";
        colEmail.MinimumWidth = 120;
        colEmail.Name = "colEmail";
        colEmail.ReadOnly = true;
        colDob.FillWeight = 90F;
        colDob.HeaderText = "Ngày sinh";
        colDob.MinimumWidth = 90;
        colDob.Name = "colDob";
        colDob.ReadOnly = true;
        colPoint.DefaultCellStyle = styleRight;
        colPoint.FillWeight = 70F;
        colPoint.HeaderText = "Điểm";
        colPoint.MinimumWidth = 70;
        colPoint.Name = "colPoint";
        colPoint.ReadOnly = true;
        colLevel.FillWeight = 110F;
        colLevel.HeaderText = "Hạng";
        colLevel.MinimumWidth = 100;
        colLevel.Name = "colLevel";
        colLevel.ReadOnly = true;
        colOrders.DefaultCellStyle = styleRight;
        colOrders.FillWeight = 70F;
        colOrders.HeaderText = "Số đơn";
        colOrders.MinimumWidth = 70;
        colOrders.Name = "colOrders";
        colOrders.ReadOnly = true;
        colSpent.DefaultCellStyle = styleRight;
        colSpent.FillWeight = 115F;
        colSpent.HeaderText = "Tổng chi tiêu";
        colSpent.MinimumWidth = 110;
        colSpent.Name = "colSpent";
        colSpent.ReadOnly = true;
        colWebAccount.FillWeight = 130F;
        colWebAccount.HeaderText = "Tài khoản web";
        colWebAccount.MinimumWidth = 120;
        colWebAccount.Name = "colWebAccount";
        colWebAccount.ReadOnly = true;
        //
        // CustomerManagementControl
        //
        AutoScroll = true;
        AutoScrollMinSize = new Size(1040, 620);
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tlpRoot);
        Name = "CustomerManagementControl";
        Padding = new Padding(24, 20, 24, 24);
        Size = new Size(1356, 875);
        ((System.ComponentModel.ISupportInitialize)gridCustomers).EndInit();
        tlpRoot.ResumeLayout(false);
        tlpHeader.ResumeLayout(false);
        panelTitles.ResumeLayout(false);
        panelTitles.PerformLayout();
        tlpStats.ResumeLayout(false);
        tlpToolbar.ResumeLayout(false);
        tlpToolbar.PerformLayout();
        panelSearch.ResumeLayout(false);
        panelSearch.PerformLayout();
        flowActions.ResumeLayout(false);
        panelGridCard.ResumeLayout(false);
        ResumeLayout(false);
    }
}