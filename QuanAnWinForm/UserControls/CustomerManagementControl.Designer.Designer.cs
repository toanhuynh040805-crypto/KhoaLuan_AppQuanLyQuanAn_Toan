using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class CustomerManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private TextBox txtSearch;

    private QuanAnWinForm.Controls.StatCard cardTotalCustomers;
    private QuanAnWinForm.Controls.StatCard cardVipCustomers;

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

    private Button btnViewDetail;
    private Button btnEdit;
    private Button btnDelete;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnAddNew = new Button();
        txtSearch = new TextBox();
        cardTotalCustomers = new QuanAnWinForm.Controls.StatCard();
        cardVipCustomers = new QuanAnWinForm.Controls.StatCard();
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
        btnViewDetail = new Button();
        btnEdit = new Button();
        btnDelete = new Button();
        ((System.ComponentModel.ISupportInitialize)gridCustomers).BeginInit();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(19, 15);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(330, 50);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Quản lý Khách hàng";
        // 
        // lblSubtitle
        // 
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(19, 65);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(600, 23);
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Quản lý thông tin khách hàng và chương trình khách hàng thân thiết (tích điểm, hạng thành viên).";
        // 
        // btnAddNew
        // 
        btnAddNew.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddNew.BackColor = AppColors.Primary;
        btnAddNew.FlatAppearance.BorderSize = 0;
        btnAddNew.FlatStyle = FlatStyle.Flat;
        btnAddNew.ForeColor = Color.White;
        btnAddNew.Location = new Point(1000, 20);
        btnAddNew.Name = "btnAddNew";
        btnAddNew.Size = new Size(180, 40);
        btnAddNew.TabIndex = 2;
        btnAddNew.Text = "+  Thêm khách hàng";
        btnAddNew.UseVisualStyleBackColor = false;
        // 
        // cardTotalCustomers
        // 
        cardTotalCustomers.BackColor = Color.White;
        cardTotalCustomers.Location = new Point(19, 100);
        cardTotalCustomers.Name = "cardTotalCustomers";
        cardTotalCustomers.Padding = new Padding(12);
        cardTotalCustomers.Size = new Size(200, 105);
        cardTotalCustomers.TabIndex = 4;
        // 
        // cardVipCustomers
        // 
        cardVipCustomers.BackColor = Color.White;
        cardVipCustomers.Location = new Point(235, 100);
        cardVipCustomers.Name = "cardVipCustomers";
        cardVipCustomers.Padding = new Padding(12);
        cardVipCustomers.Size = new Size(200, 105);
        cardVipCustomers.TabIndex = 5;
        // 
        // txtSearch
        // 
        txtSearch.Font = new Font("Segoe UI", 10F);
        txtSearch.Location = new Point(19, 220);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "Tìm theo tên hoặc số điện thoại...";
        txtSearch.Size = new Size(360, 30);
        txtSearch.TabIndex = 3;
        // 
        // gridCustomers
        // 
        gridCustomers.AllowUserToAddRows = false;
        gridCustomers.AllowUserToDeleteRows = false;
        gridCustomers.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridCustomers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridCustomers.BackgroundColor = Color.White;
        gridCustomers.BorderStyle = BorderStyle.FixedSingle;
        gridCustomers.ColumnHeadersHeight = 32;
        gridCustomers.Columns.AddRange(new DataGridViewColumn[] { colId, colName, colPhone, colEmail, colDob, colPoint, colLevel, colOrders, colSpent });
        gridCustomers.Location = new Point(19, 260);
        gridCustomers.MultiSelect = false;
        gridCustomers.Name = "gridCustomers";
        gridCustomers.ReadOnly = true;
        gridCustomers.RowHeadersVisible = false;
        gridCustomers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridCustomers.Size = new Size(1161, 410);
        gridCustomers.TabIndex = 6;
        // 
        // colId
        // 
        colId.HeaderText = "Mã";
        colId.MinimumWidth = 50;
        colId.Name = "colId";
        colId.ReadOnly = true;
        colId.FillWeight = 40;
        // 
        // colName
        // 
        colName.HeaderText = "Họ tên";
        colName.MinimumWidth = 120;
        colName.Name = "colName";
        colName.ReadOnly = true;
        colName.FillWeight = 130;
        // 
        // colPhone
        // 
        colPhone.HeaderText = "SĐT";
        colPhone.MinimumWidth = 100;
        colPhone.Name = "colPhone";
        colPhone.ReadOnly = true;
        colPhone.FillWeight = 90;
        // 
        // colEmail
        // 
        colEmail.HeaderText = "Email";
        colEmail.MinimumWidth = 100;
        colEmail.Name = "colEmail";
        colEmail.ReadOnly = true;
        colEmail.FillWeight = 130;
        // 
        // colDob
        // 
        colDob.HeaderText = "Ngày sinh";
        colDob.MinimumWidth = 90;
        colDob.Name = "colDob";
        colDob.ReadOnly = true;
        colDob.FillWeight = 90;
        // 
        // colPoint
        // 
        colPoint.HeaderText = "Điểm";
        colPoint.MinimumWidth = 70;
        colPoint.Name = "colPoint";
        colPoint.ReadOnly = true;
        colPoint.FillWeight = 70;
        // 
        // colLevel
        // 
        colLevel.HeaderText = "Hạng";
        colLevel.MinimumWidth = 90;
        colLevel.Name = "colLevel";
        colLevel.ReadOnly = true;
        colLevel.FillWeight = 90;
        // 
        // colOrders
        // 
        colOrders.HeaderText = "Số đơn";
        colOrders.MinimumWidth = 70;
        colOrders.Name = "colOrders";
        colOrders.ReadOnly = true;
        colOrders.FillWeight = 70;
        // 
        // colSpent
        // 
        colSpent.HeaderText = "Tổng chi tiêu";
        colSpent.MinimumWidth = 110;
        colSpent.Name = "colSpent";
        colSpent.ReadOnly = true;
        colSpent.FillWeight = 110;
        // 
        // btnViewDetail
        // 
        btnViewDetail.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnViewDetail.BackColor = AppColors.Tertiary;
        btnViewDetail.FlatAppearance.BorderSize = 0;
        btnViewDetail.FlatStyle = FlatStyle.Flat;
        btnViewDetail.ForeColor = Color.White;
        btnViewDetail.Location = new Point(19, 682);
        btnViewDetail.Name = "btnViewDetail";
        btnViewDetail.Size = new Size(190, 38);
        btnViewDetail.TabIndex = 7;
        btnViewDetail.Text = "Chi tiết && Điểm tích lũy";
        btnViewDetail.UseVisualStyleBackColor = false;
        // 
        // btnEdit
        // 
        btnEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnEdit.BackColor = AppColors.Secondary;
        btnEdit.FlatAppearance.BorderSize = 0;
        btnEdit.FlatStyle = FlatStyle.Flat;
        btnEdit.ForeColor = Color.White;
        btnEdit.Location = new Point(219, 682);
        btnEdit.Name = "btnEdit";
        btnEdit.Size = new Size(110, 38);
        btnEdit.TabIndex = 8;
        btnEdit.Text = "Sửa";
        btnEdit.UseVisualStyleBackColor = false;
        // 
        // btnDelete
        // 
        btnDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnDelete.FlatAppearance.BorderColor = AppColors.Error;
        btnDelete.FlatStyle = FlatStyle.Flat;
        btnDelete.ForeColor = AppColors.Error;
        btnDelete.Location = new Point(339, 682);
        btnDelete.Name = "btnDelete";
        btnDelete.Size = new Size(110, 38);
        btnDelete.TabIndex = 9;
        btnDelete.Text = "Xóa";
        btnDelete.UseVisualStyleBackColor = true;
        // 
        // CustomerManagementControl
        // 
        AutoScroll = true;
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(btnDelete);
        Controls.Add(btnEdit);
        Controls.Add(btnViewDetail);
        Controls.Add(gridCustomers);
        Controls.Add(txtSearch);
        Controls.Add(cardVipCustomers);
        Controls.Add(cardTotalCustomers);
        Controls.Add(btnAddNew);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        Name = "CustomerManagementControl";
        Size = new Size(1200, 760);
        ((System.ComponentModel.ISupportInitialize)gridCustomers).EndInit();
        ResumeLayout(false);
        PerformLayout();
        //
        cardTotalCustomers.BringToFront();
        cardVipCustomers.BringToFront();
    }
}