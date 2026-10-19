using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class TableManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Button btnAddTable;
    private Button btnManageAreas;
    private Button btnReservations;
    private TextBox txtSearchTable;
    private ComboBox cbFilterStatus;
    private FlowLayoutPanel flowLegend;
    private FlowLayoutPanel flowTables;
    private Panel panelDetail;
    private Label lblDetailName;
    private Label lblDetailStatus;
    private Label lblDetailInfo;
    private Label lblServingTitle;
    private FlowLayoutPanel flowServingItems;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        btnAddTable = new Button();
        btnManageAreas = new Button();
        btnReservations = new Button();
        txtSearchTable = new TextBox();
        cbFilterStatus = new ComboBox();
        flowLegend = new FlowLayoutPanel();
        flowTables = new FlowLayoutPanel();
        panelDetail = new Panel();
        flowServingItems = new FlowLayoutPanel();
        lblServingTitle = new Label();
        lblDetailInfo = new Label();
        lblDetailStatus = new Label();
        lblDetailName = new Label();
        panelDetail.SuspendLayout();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(197, 50);
        lblTitle.TabIndex = 4;
        lblTitle.Text = "Sơ đồ bàn";
        // 
        // btnAddTable
        // 
        btnAddTable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddTable.BackColor = Color.FromArgb(172, 45, 0);
        btnAddTable.FlatAppearance.BorderSize = 0;
        btnAddTable.FlatStyle = FlatStyle.Flat;
        btnAddTable.ForeColor = Color.White;
        btnAddTable.Location = new Point(620, 10);
        btnAddTable.Name = "btnAddTable";
        btnAddTable.Size = new Size(120, 34);
        btnAddTable.TabIndex = 3;
        btnAddTable.Text = "+  Thêm bàn";
        btnAddTable.UseVisualStyleBackColor = false;
        // 
        // btnManageAreas
        // 
        btnManageAreas.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnManageAreas.BackColor = Color.FromArgb(84, 96, 103);
        btnManageAreas.FlatAppearance.BorderSize = 0;
        btnManageAreas.FlatStyle = FlatStyle.Flat;
        btnManageAreas.ForeColor = Color.White;
        btnManageAreas.Location = new Point(750, 10);
        btnManageAreas.Name = "btnManageAreas";
        btnManageAreas.Size = new Size(140, 34);
        btnManageAreas.TabIndex = 2;
        btnManageAreas.Text = "Quản lý khu vực";
        btnManageAreas.UseVisualStyleBackColor = false;
        // 
        // btnReservations
        // 
        btnReservations.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnReservations.BackColor = Color.FromArgb(46, 125, 115);
        btnReservations.FlatAppearance.BorderSize = 0;
        btnReservations.FlatStyle = FlatStyle.Flat;
        btnReservations.ForeColor = Color.White;
        btnReservations.Location = new Point(490, 10);
        btnReservations.Name = "btnReservations";
        btnReservations.Size = new Size(120, 34);
        btnReservations.TabIndex = 6;
        btnReservations.Text = "Đặt bàn trước";
        btnReservations.UseVisualStyleBackColor = false;
        // 
        // txtSearchTable
        // 
        txtSearchTable.Font = new Font("Segoe UI", 10F);
        txtSearchTable.Location = new Point(0, 58);
        txtSearchTable.Name = "txtSearchTable";
        txtSearchTable.PlaceholderText = "Tìm theo tên bàn hoặc khu vực...";
        txtSearchTable.Size = new Size(280, 30);
        txtSearchTable.TabIndex = 7;
        // 
        // cbFilterStatus
        // 
        cbFilterStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        cbFilterStatus.Font = new Font("Segoe UI", 10F);
        cbFilterStatus.Location = new Point(290, 58);
        cbFilterStatus.Name = "cbFilterStatus";
        cbFilterStatus.Size = new Size(170, 31);
        cbFilterStatus.TabIndex = 8;
        // 
        // flowLegend
        // 
        flowLegend.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flowLegend.Location = new Point(0, 98);
        flowLegend.Name = "flowLegend";
        flowLegend.Size = new Size(880, 26);
        flowLegend.TabIndex = 1;
        // 
        // flowTables
        // 
        flowTables.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowTables.AutoScroll = true;
        flowTables.Location = new Point(0, 130);
        flowTables.Name = "flowTables";
        flowTables.Size = new Size(880, 630);
        flowTables.TabIndex = 0;
        // 
        // panelDetail
        // 
        panelDetail.BackColor = Color.White;
        panelDetail.Controls.Add(flowServingItems);
        panelDetail.Controls.Add(lblServingTitle);
        panelDetail.Controls.Add(lblDetailInfo);
        panelDetail.Controls.Add(lblDetailStatus);
        panelDetail.Controls.Add(lblDetailName);
        panelDetail.Dock = DockStyle.Right;
        panelDetail.Location = new Point(893, 0);
        panelDetail.Name = "panelDetail";
        panelDetail.Size = new Size(300, 771);
        panelDetail.TabIndex = 5;
        panelDetail.Visible = false;
        // 
        // flowServingItems
        // 
        flowServingItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowServingItems.AutoScroll = true;
        flowServingItems.FlowDirection = FlowDirection.TopDown;
        flowServingItems.Location = new Point(16, 299);
        flowServingItems.Name = "flowServingItems";
        flowServingItems.Size = new Size(268, 456);
        flowServingItems.TabIndex = 0;
        flowServingItems.WrapContents = false;
        // 
        // lblServingTitle
        // 
        lblServingTitle.AutoSize = true;
        lblServingTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblServingTitle.Location = new Point(16, 264);
        lblServingTitle.Name = "lblServingTitle";
        lblServingTitle.Size = new Size(160, 23);
        lblServingTitle.TabIndex = 1;
        lblServingTitle.Text = "Món đang phục vụ";
        // 
        // lblDetailInfo
        // 
        lblDetailInfo.AutoSize = true;
        lblDetailInfo.Font = new Font("Segoe UI", 10F);
        lblDetailInfo.ForeColor = Color.FromArgb(91, 65, 57);
        lblDetailInfo.Location = new Point(16, 76);
        lblDetailInfo.Name = "lblDetailInfo";
        lblDetailInfo.Size = new Size(0, 23);
        lblDetailInfo.TabIndex = 2;
        // 
        // lblDetailStatus
        // 
        lblDetailStatus.AutoSize = true;
        lblDetailStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblDetailStatus.ForeColor = Color.FromArgb(172, 45, 0);
        lblDetailStatus.Location = new Point(16, 48);
        lblDetailStatus.Name = "lblDetailStatus";
        lblDetailStatus.Size = new Size(0, 23);
        lblDetailStatus.TabIndex = 3;
        // 
        // lblDetailName
        // 
        lblDetailName.AutoSize = true;
        lblDetailName.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblDetailName.Location = new Point(16, 16);
        lblDetailName.Name = "lblDetailName";
        lblDetailName.Size = new Size(0, 37);
        lblDetailName.TabIndex = 4;
        // 
        // TableManagementControl
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(flowTables);
        Controls.Add(flowLegend);
        Controls.Add(cbFilterStatus);
        Controls.Add(txtSearchTable);
        Controls.Add(btnReservations);
        Controls.Add(btnManageAreas);
        Controls.Add(btnAddTable);
        Controls.Add(lblTitle);
        Controls.Add(panelDetail);
        Name = "TableManagementControl";
        Size = new Size(1193, 771);
        panelDetail.ResumeLayout(false);
        panelDetail.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private void AddLegendItem(FlowLayoutPanel parent, string label, Color color)
    {
        var dot = new Panel { Size = new Size(10, 10), BackColor = color, Margin = new Padding(0, 10, 4, 0) };
        var lbl = new Label { Text = label, Font = AppFonts.BodyMd, AutoSize = true, Margin = new Padding(0, 6, 16, 0) };
        parent.Controls.Add(dot);
        parent.Controls.Add(lbl);
    }
}