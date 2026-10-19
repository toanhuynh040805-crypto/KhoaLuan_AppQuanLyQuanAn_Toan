using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class SalesControl
{
    private System.ComponentModel.IContainer components = null;

    private Panel panelLeft;
    private FlowLayoutPanel flowCategoryTabs;
    private Button btnAllCategory;
    private FlowLayoutPanel flowMenuTiles;

    private Panel panelRight;
    private Label lblTableLabel;
    private ComboBox cbTable;
    private Label lblTableName;
    private TextBox txtCustomerPhone;
    private Button btnFindCustomer;
    private Button btnClearCustomer;
    private Label lblCustomerInfo;
    private FlowLayoutPanel flowCartItems;
    private Panel panelTotals;
    private Label lblSubtotalCaption, lblSubtotalValue;
    private Label lblVatCaption, lblVatValue;
    private Label lblCampaignCaption;
    private ComboBox cbCampaign;
    private Label lblDiscountCaption, lblDiscountValue;
    private Label lblTotalCaption, lblTotalValue;
    private Button btnVietQr;
    private Button btnThanhToan;
    private Label lblInvoiceHistory;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        panelLeft = new Panel();
        flowMenuTiles = new FlowLayoutPanel();
        flowCategoryTabs = new FlowLayoutPanel();
        btnAllCategory = new Button();
        panelRight = new Panel();
        panelTotals = new Panel();
        lblSubtotalCaption = new Label();
        lblSubtotalValue = new Label();
        lblVatCaption = new Label();
        lblVatValue = new Label();
        lblCampaignCaption = new Label();
        cbCampaign = new ComboBox();
        lblDiscountCaption = new Label();
        lblDiscountValue = new Label();
        lblTotalCaption = new Label();
        lblTotalValue = new Label();
        btnVietQr = new Button();
        btnThanhToan = new Button();
        flowCartItems = new FlowLayoutPanel();
        lblCustomerInfo = new Label();
        btnClearCustomer = new Button();
        btnFindCustomer = new Button();
        txtCustomerPhone = new TextBox();
        lblTableName = new Label();
        cbTable = new ComboBox();
        lblTableLabel = new Label();
        lblInvoiceHistory = new Label();
        panelLeft.SuspendLayout();
        flowCategoryTabs.SuspendLayout();
        panelRight.SuspendLayout();
        panelTotals.SuspendLayout();
        SuspendLayout();
        // 
        // panelLeft
        // 
        panelLeft.Controls.Add(flowMenuTiles);
        panelLeft.Controls.Add(flowCategoryTabs);
        panelLeft.Dock = DockStyle.Fill;
        panelLeft.Location = new Point(0, 0);
        panelLeft.Name = "panelLeft";
        panelLeft.Size = new Size(700, 750);
        panelLeft.TabIndex = 0;
        // 
        // flowMenuTiles
        // 
        flowMenuTiles.AutoScroll = true;
        flowMenuTiles.Location = new Point(0, 52);
        flowMenuTiles.Name = "flowMenuTiles";
        flowMenuTiles.Size = new Size(700, 700);
        flowMenuTiles.TabIndex = 0;
        // 
        // flowCategoryTabs
        // 
        flowCategoryTabs.Controls.Add(btnAllCategory);
        flowCategoryTabs.Location = new Point(0, 0);
        flowCategoryTabs.Name = "flowCategoryTabs";
        flowCategoryTabs.Size = new Size(700, 44);
        flowCategoryTabs.TabIndex = 1;
        // 
        // btnAllCategory
        // 
        btnAllCategory.BackColor = Color.FromArgb(172, 45, 0);
        btnAllCategory.FlatAppearance.BorderSize = 0;
        btnAllCategory.FlatStyle = FlatStyle.Flat;
        btnAllCategory.ForeColor = Color.White;
        btnAllCategory.Location = new Point(3, 3);
        btnAllCategory.Name = "btnAllCategory";
        btnAllCategory.Size = new Size(90, 34);
        btnAllCategory.TabIndex = 0;
        btnAllCategory.Text = "Tất cả";
        btnAllCategory.UseVisualStyleBackColor = false;
        // 
        // panelRight
        // 
        panelRight.BackColor = Color.White;
        panelRight.Controls.Add(panelTotals);
        panelRight.Controls.Add(flowCartItems);
        panelRight.Controls.Add(lblCustomerInfo);
        panelRight.Controls.Add(btnClearCustomer);
        panelRight.Controls.Add(btnFindCustomer);
        panelRight.Controls.Add(txtCustomerPhone);
        panelRight.Controls.Add(lblTableName);
        panelRight.Controls.Add(cbTable);
        panelRight.Controls.Add(lblTableLabel);
        panelRight.Controls.Add(lblInvoiceHistory);
        panelRight.Dock = DockStyle.Right;
        panelRight.Location = new Point(700, 0);
        panelRight.Name = "panelRight";
        panelRight.Size = new Size(400, 750);
        panelRight.TabIndex = 1;
        // 
        // panelTotals
        // 
        panelTotals.Controls.Add(lblSubtotalCaption);
        panelTotals.Controls.Add(lblSubtotalValue);
        panelTotals.Controls.Add(lblVatCaption);
        panelTotals.Controls.Add(lblVatValue);
        panelTotals.Controls.Add(lblCampaignCaption);
        panelTotals.Controls.Add(cbCampaign);
        panelTotals.Controls.Add(lblDiscountCaption);
        panelTotals.Controls.Add(lblDiscountValue);
        panelTotals.Controls.Add(lblTotalCaption);
        panelTotals.Controls.Add(lblTotalValue);
        panelTotals.Controls.Add(btnVietQr);
        panelTotals.Controls.Add(btnThanhToan);
        panelTotals.Location = new Point(16, 474);
        panelTotals.Name = "panelTotals";
        panelTotals.Size = new Size(370, 258);
        panelTotals.TabIndex = 0;
        // 
        // lblSubtotalCaption
        // 
        lblSubtotalCaption.AutoSize = true;
        lblSubtotalCaption.Font = new Font("Segoe UI", 10F);
        lblSubtotalCaption.Location = new Point(0, 0);
        lblSubtotalCaption.Name = "lblSubtotalCaption";
        lblSubtotalCaption.Size = new Size(78, 23);
        lblSubtotalCaption.TabIndex = 0;
        lblSubtotalCaption.Text = "Tạm tính";
        // 
        // lblSubtotalValue
        // 
        lblSubtotalValue.AutoSize = true;
        lblSubtotalValue.Font = new Font("Segoe UI", 10F);
        lblSubtotalValue.Location = new Point(300, 0);
        lblSubtotalValue.Name = "lblSubtotalValue";
        lblSubtotalValue.Size = new Size(29, 23);
        lblSubtotalValue.TabIndex = 1;
        lblSubtotalValue.Text = "0đ";
        // 
        // lblVatCaption
        // 
        lblVatCaption.AutoSize = true;
        lblVatCaption.Font = new Font("Segoe UI", 10F);
        lblVatCaption.Location = new Point(0, 24);
        lblVatCaption.Name = "lblVatCaption";
        lblVatCaption.Size = new Size(77, 23);
        lblVatCaption.TabIndex = 2;
        lblVatCaption.Text = "VAT (8%)";
        // 
        // lblVatValue
        // 
        lblVatValue.AutoSize = true;
        lblVatValue.Font = new Font("Segoe UI", 10F);
        lblVatValue.Location = new Point(300, 24);
        lblVatValue.Name = "lblVatValue";
        lblVatValue.Size = new Size(29, 23);
        lblVatValue.TabIndex = 3;
        lblVatValue.Text = "0đ";
        // 
        // lblCampaignCaption
        // 
        lblCampaignCaption.AutoSize = true;
        lblCampaignCaption.Font = new Font("Segoe UI", 9F);
        lblCampaignCaption.ForeColor = Color.FromArgb(84, 96, 103);
        lblCampaignCaption.Location = new Point(3, 48);
        lblCampaignCaption.Name = "lblCampaignCaption";
        lblCampaignCaption.Size = new Size(86, 20);
        lblCampaignCaption.TabIndex = 90;
        lblCampaignCaption.Text = "Khuyến mãi";
        // 
        // cbCampaign
        // 
        cbCampaign.DropDownStyle = ComboBoxStyle.DropDownList;
        cbCampaign.Font = new Font("Segoe UI", 9F);
        cbCampaign.Location = new Point(110, 48);
        cbCampaign.Name = "cbCampaign";
        cbCampaign.Size = new Size(260, 28);
        cbCampaign.TabIndex = 91;
        // 
        // lblDiscountCaption
        // 
        lblDiscountCaption.AutoSize = true;
        lblDiscountCaption.Font = new Font("Segoe UI", 10F);
        lblDiscountCaption.ForeColor = Color.FromArgb(172, 45, 0);
        lblDiscountCaption.Location = new Point(3, 82);
        lblDiscountCaption.Name = "lblDiscountCaption";
        lblDiscountCaption.Size = new Size(78, 23);
        lblDiscountCaption.TabIndex = 92;
        lblDiscountCaption.Text = "Giảm giá";
        lblDiscountCaption.Visible = false;
        // 
        // lblDiscountValue
        // 
        lblDiscountValue.AutoSize = true;
        lblDiscountValue.Font = new Font("Segoe UI", 10F);
        lblDiscountValue.ForeColor = Color.FromArgb(172, 45, 0);
        lblDiscountValue.Location = new Point(280, 82);
        lblDiscountValue.Name = "lblDiscountValue";
        lblDiscountValue.Size = new Size(36, 23);
        lblDiscountValue.TabIndex = 93;
        lblDiscountValue.Text = "-0đ";
        lblDiscountValue.Visible = false;
        // 
        // lblTotalCaption
        // 
        lblTotalCaption.AutoSize = true;
        lblTotalCaption.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTotalCaption.Location = new Point(0, 114);
        lblTotalCaption.Name = "lblTotalCaption";
        lblTotalCaption.Size = new Size(123, 30);
        lblTotalCaption.TabIndex = 4;
        lblTotalCaption.Text = "Tổng cộng";
        // 
        // lblTotalValue
        // 
        lblTotalValue.AutoSize = true;
        lblTotalValue.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTotalValue.ForeColor = Color.FromArgb(172, 45, 0);
        lblTotalValue.Location = new Point(260, 114);
        lblTotalValue.Name = "lblTotalValue";
        lblTotalValue.Size = new Size(40, 30);
        lblTotalValue.TabIndex = 5;
        lblTotalValue.Text = "0đ";
        // 
        // btnVietQr
        // 
        btnVietQr.FlatAppearance.BorderColor = Color.FromArgb(84, 96, 103);
        btnVietQr.FlatStyle = FlatStyle.Flat;
        btnVietQr.ForeColor = Color.FromArgb(84, 96, 103);
        btnVietQr.Location = new Point(0, 154);
        btnVietQr.Name = "btnVietQr";
        btnVietQr.Size = new Size(370, 42);
        btnVietQr.TabIndex = 6;
        btnVietQr.Text = "VietQR";
        // 
        // btnThanhToan
        // 
        btnThanhToan.BackColor = Color.FromArgb(172, 45, 0);
        btnThanhToan.FlatAppearance.BorderSize = 0;
        btnThanhToan.FlatStyle = FlatStyle.Flat;
        btnThanhToan.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnThanhToan.ForeColor = Color.White;
        btnThanhToan.Location = new Point(0, 204);
        btnThanhToan.Name = "btnThanhToan";
        btnThanhToan.Size = new Size(370, 46);
        btnThanhToan.TabIndex = 7;
        btnThanhToan.Text = "Thanh Toán";
        btnThanhToan.UseVisualStyleBackColor = false;
        // 
        // flowCartItems
        // 
        flowCartItems.AutoScroll = true;
        flowCartItems.FlowDirection = FlowDirection.TopDown;
        flowCartItems.Location = new Point(16, 132);
        flowCartItems.Name = "flowCartItems";
        flowCartItems.Size = new Size(370, 332);
        flowCartItems.TabIndex = 1;
        flowCartItems.WrapContents = false;
        // 
        // lblCustomerInfo
        // 
        lblCustomerInfo.AutoEllipsis = true;
        lblCustomerInfo.Font = new Font("Segoe UI", 8.5F);
        lblCustomerInfo.Location = new Point(16, 108);
        lblCustomerInfo.Name = "lblCustomerInfo";
        lblCustomerInfo.Size = new Size(370, 20);
        lblCustomerInfo.TabIndex = 13;
        lblCustomerInfo.Text = "Khách vãng lai (không tích điểm)";
        // 
        // btnClearCustomer
        // 
        btnClearCustomer.FlatStyle = FlatStyle.Flat;
        btnClearCustomer.Font = new Font("Segoe UI", 9F);
        btnClearCustomer.Location = new Point(228, 80);
        btnClearCustomer.Name = "btnClearCustomer";
        btnClearCustomer.Size = new Size(26, 26);
        btnClearCustomer.TabIndex = 12;
        btnClearCustomer.Text = "×";
        // 
        // btnFindCustomer
        // 
        btnFindCustomer.FlatStyle = FlatStyle.Flat;
        btnFindCustomer.Font = new Font("Segoe UI", 9F);
        btnFindCustomer.Location = new Point(172, 80);
        btnFindCustomer.Name = "btnFindCustomer";
        btnFindCustomer.Size = new Size(50, 26);
        btnFindCustomer.TabIndex = 11;
        btnFindCustomer.Text = "Tìm";
        // 
        // txtCustomerPhone
        // 
        txtCustomerPhone.Font = new Font("Segoe UI", 9F);
        txtCustomerPhone.Location = new Point(16, 80);
        txtCustomerPhone.Name = "txtCustomerPhone";
        txtCustomerPhone.PlaceholderText = "SĐT khách (không bắt buộc)";
        txtCustomerPhone.Size = new Size(150, 27);
        txtCustomerPhone.TabIndex = 10;
        // 
        // lblTableName
        // 
        lblTableName.AutoSize = true;
        lblTableName.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTableName.ForeColor = Color.FromArgb(172, 45, 0);
        lblTableName.Location = new Point(16, 46);
        lblTableName.Name = "lblTableName";
        lblTableName.Size = new Size(168, 30);
        lblTableName.TabIndex = 2;
        lblTableName.Text = "Chưa chọn bàn";
        // 
        // cbTable
        // 
        cbTable.DropDownStyle = ComboBoxStyle.DropDownList;
        cbTable.Location = new Point(111, 11);
        cbTable.Name = "cbTable";
        cbTable.Size = new Size(200, 28);
        cbTable.TabIndex = 3;
        cbTable.SelectedIndexChanged += CbTable_SelectedIndexChanged;
        // 
        // lblTableLabel
        // 
        lblTableLabel.AutoSize = true;
        lblTableLabel.Font = new Font("Segoe UI", 10F);
        lblTableLabel.Location = new Point(16, 16);
        lblTableLabel.Name = "lblTableLabel";
        lblTableLabel.Size = new Size(89, 23);
        lblTableLabel.TabIndex = 4;
        lblTableLabel.Text = "Chọn bàn:";
        // 
        // lblInvoiceHistory
        // 
        lblInvoiceHistory.AutoSize = true;
        lblInvoiceHistory.Cursor = Cursors.Hand;
        lblInvoiceHistory.Font = new Font("Segoe UI", 9F, FontStyle.Underline);
        lblInvoiceHistory.ForeColor = Color.FromArgb(46, 125, 115);
        lblInvoiceHistory.Location = new Point(318, 16);
        lblInvoiceHistory.Name = "lblInvoiceHistory";
        lblInvoiceHistory.Size = new Size(80, 20);
        lblInvoiceHistory.TabIndex = 5;
        lblInvoiceHistory.Text = "Lịch sử HĐ";
        // 
        // SalesControl
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(panelLeft);
        Controls.Add(panelRight);
        Name = "SalesControl";
        Size = new Size(1100, 750);
        panelLeft.ResumeLayout(false);
        flowCategoryTabs.ResumeLayout(false);
        panelRight.ResumeLayout(false);
        panelRight.PerformLayout();
        panelTotals.ResumeLayout(false);
        panelTotals.PerformLayout();
        ResumeLayout(false);
    }
}