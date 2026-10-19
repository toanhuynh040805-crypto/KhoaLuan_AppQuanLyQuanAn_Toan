namespace QuanAnWinForm.UserControls;

partial class OrderManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblFilterDate;
    private DateTimePicker dtpFilterDate;
    private Label lblFilterStatus;
    private ComboBox cbFilterStatus;
    private FlowLayoutPanel flowOrders;
    private Panel panelDetail;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblFilterDate = new Label();
        dtpFilterDate = new DateTimePicker();
        lblFilterStatus = new Label();
        cbFilterStatus = new ComboBox();
        flowOrders = new FlowLayoutPanel();
        panelDetail = new Panel();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(141, 37);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Đơn hàng";
        // 
        // lblFilterDate
        // 
        lblFilterDate.AutoSize = true;
        lblFilterDate.Font = new Font("Segoe UI", 10F);
        lblFilterDate.ForeColor = Color.FromArgb(91, 65, 57);
        lblFilterDate.Location = new Point(0, 64);
        lblFilterDate.Name = "lblFilterDate";
        lblFilterDate.Size = new Size(54, 23);
        lblFilterDate.TabIndex = 1;
        lblFilterDate.Text = "Ngày:";
        // 
        // dtpFilterDate
        // 
        dtpFilterDate.Format = DateTimePickerFormat.Short;
        dtpFilterDate.Location = new Point(69, 59);
        dtpFilterDate.Margin = new Padding(3, 4, 3, 4);
        dtpFilterDate.Name = "dtpFilterDate";
        dtpFilterDate.Size = new Size(148, 27);
        dtpFilterDate.TabIndex = 2;
        // 
        // lblFilterStatus
        // 
        lblFilterStatus.AutoSize = true;
        lblFilterStatus.Font = new Font("Segoe UI", 10F);
        lblFilterStatus.ForeColor = Color.FromArgb(91, 65, 57);
        lblFilterStatus.Location = new Point(235, 64);
        lblFilterStatus.Name = "lblFilterStatus";
        lblFilterStatus.Size = new Size(91, 23);
        lblFilterStatus.TabIndex = 3;
        lblFilterStatus.Text = "Trạng thái:";
        // 
        // cbFilterStatus
        // 
        cbFilterStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        cbFilterStatus.Font = new Font("Segoe UI", 10F);
        cbFilterStatus.Location = new Point(343, 59);
        cbFilterStatus.Margin = new Padding(3, 4, 3, 4);
        cbFilterStatus.Name = "cbFilterStatus";
        cbFilterStatus.Size = new Size(194, 31);
        cbFilterStatus.TabIndex = 4;
        // 
        // flowOrders
        // 
        flowOrders.AutoScroll = true;
        flowOrders.FlowDirection = FlowDirection.TopDown;
        flowOrders.Location = new Point(11, 98);
        flowOrders.Margin = new Padding(3, 4, 3, 4);
        flowOrders.Name = "flowOrders";
        flowOrders.Size = new Size(831, 788);
        flowOrders.TabIndex = 5;
        flowOrders.WrapContents = false;
        // 
        // panelDetail
        // 
        panelDetail.BackColor = Color.White;
        panelDetail.Dock = DockStyle.Right;
        panelDetail.Location = new Point(864, 32);
        panelDetail.Margin = new Padding(3, 4, 3, 4);
        panelDetail.Name = "panelDetail";
        panelDetail.Padding = new Padding(27, 32, 27, 32);
        panelDetail.Size = new Size(480, 936);
        panelDetail.TabIndex = 6;
        panelDetail.Visible = false;
        // 
        // OrderManagementControl
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(flowOrders);
        Controls.Add(cbFilterStatus);
        Controls.Add(lblFilterStatus);
        Controls.Add(dtpFilterDate);
        Controls.Add(lblFilterDate);
        Controls.Add(lblTitle);
        Controls.Add(panelDetail);
        Margin = new Padding(3, 4, 3, 4);
        Name = "OrderManagementControl";
        Padding = new Padding(27, 32, 27, 32);
        Size = new Size(1371, 1000);
        ResumeLayout(false);
        PerformLayout();
    }
}