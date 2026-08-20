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
    private FlowLayoutPanel flowCartItems;
    private Panel panelTotals;
    private Label lblSubtotalCaption, lblSubtotalValue;
    private Label lblVatCaption, lblVatValue;
    private Label lblTotalCaption, lblTotalValue;
    private Button btnVietQr;
    private Button btnThanhToan;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.panelLeft = new Panel();
        this.flowCategoryTabs = new FlowLayoutPanel();
        this.btnAllCategory = new Button();
        this.flowMenuTiles = new FlowLayoutPanel();

        this.panelRight = new Panel();
        this.lblTableLabel = new Label();
        this.cbTable = new ComboBox();
        this.lblTableName = new Label();
        this.flowCartItems = new FlowLayoutPanel();
        this.panelTotals = new Panel();
        this.lblSubtotalCaption = new Label();
        this.lblSubtotalValue = new Label();
        this.lblVatCaption = new Label();
        this.lblVatValue = new Label();
        this.lblTotalCaption = new Label();
        this.lblTotalValue = new Label();
        this.btnVietQr = new Button();
        this.btnThanhToan = new Button();
        this.SuspendLayout();
        //
        // btnAllCategory + flowCategoryTabs
        //
        this.btnAllCategory.Text = "Tất cả";
        this.btnAllCategory.Size = new Size(90, 34);
        this.btnAllCategory.FlatStyle = FlatStyle.Flat;
        this.btnAllCategory.BackColor = AppColors.Primary;
        this.btnAllCategory.ForeColor = Color.White;
        this.btnAllCategory.FlatAppearance.BorderSize = 0;
        this.flowCategoryTabs.Location = new Point(0, 0);
        this.flowCategoryTabs.Size = new Size(700, 44);
        this.flowCategoryTabs.Controls.Add(this.btnAllCategory);
        //
        // flowMenuTiles
        //
        this.flowMenuTiles.Location = new Point(0, 52);
        this.flowMenuTiles.Size = new Size(700, 700);
        this.flowMenuTiles.AutoScroll = true;
        //
        // panelLeft
        //
        this.panelLeft.Dock = DockStyle.Fill;
        this.panelLeft.Controls.Add(this.flowMenuTiles);
        this.panelLeft.Controls.Add(this.flowCategoryTabs);
        //
        // lblTableLabel
        //
        this.lblTableLabel.Text = "Chọn bàn:";
        this.lblTableLabel.Font = AppFonts.BodyMd;
        this.lblTableLabel.Location = new Point(16, 16);
        this.lblTableLabel.AutoSize = true;
        //
        // cbTable
        //
        this.cbTable.Location = new Point(90, 12);
        this.cbTable.Width = 200;
        this.cbTable.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cbTable.SelectedIndexChanged += CbTable_SelectedIndexChanged;
        //
        // lblTableName
        //
        this.lblTableName.Font = AppFonts.HeadlineSm;
        this.lblTableName.ForeColor = AppColors.Primary;
        this.lblTableName.Location = new Point(16, 46);
        this.lblTableName.AutoSize = true;
        this.lblTableName.Text = "Chưa chọn bàn";
        //
        // flowCartItems
        //
        this.flowCartItems.FlowDirection = FlowDirection.TopDown;
        this.flowCartItems.WrapContents = false;
        this.flowCartItems.Location = new Point(16, 84);
        this.flowCartItems.Size = new Size(370, 420);
        this.flowCartItems.AutoScroll = true;
        //
        // lblSubtotalCaption / Value
        //
        this.lblSubtotalCaption.Text = "Tạm tính";
        this.lblSubtotalCaption.Font = AppFonts.BodyMd;
        this.lblSubtotalCaption.Location = new Point(0, 0);
        this.lblSubtotalCaption.AutoSize = true;
        this.lblSubtotalValue.Text = "0đ";
        this.lblSubtotalValue.Font = AppFonts.BodyMd;
        this.lblSubtotalValue.Location = new Point(300, 0);
        this.lblSubtotalValue.AutoSize = true;
        //
        // lblVatCaption / Value
        //
        this.lblVatCaption.Text = "VAT (8%)";
        this.lblVatCaption.Font = AppFonts.BodyMd;
        this.lblVatCaption.Location = new Point(0, 24);
        this.lblVatCaption.AutoSize = true;
        this.lblVatValue.Text = "0đ";
        this.lblVatValue.Font = AppFonts.BodyMd;
        this.lblVatValue.Location = new Point(300, 24);
        this.lblVatValue.AutoSize = true;
        //
        // lblTotalCaption / Value
        //
        this.lblTotalCaption.Text = "Tổng cộng";
        this.lblTotalCaption.Font = AppFonts.HeadlineSm;
        this.lblTotalCaption.Location = new Point(0, 56);
        this.lblTotalCaption.AutoSize = true;
        this.lblTotalValue.Text = "0đ";
        this.lblTotalValue.Font = AppFonts.HeadlineSm;
        this.lblTotalValue.ForeColor = AppColors.Primary;
        this.lblTotalValue.Location = new Point(260, 56);
        this.lblTotalValue.AutoSize = true;
        //
        // btnVietQr
        //
        this.btnVietQr.Text = "VietQR";
        this.btnVietQr.Location = new Point(0, 96);
        this.btnVietQr.Size = new Size(370, 42);
        this.btnVietQr.FlatStyle = FlatStyle.Flat;
        this.btnVietQr.ForeColor = AppColors.Secondary;
        this.btnVietQr.FlatAppearance.BorderColor = AppColors.Secondary;
        //
        // btnThanhToan
        //
        this.btnThanhToan.Text = "Thanh Toán";
        this.btnThanhToan.Location = new Point(0, 146);
        this.btnThanhToan.Size = new Size(370, 46);
        this.btnThanhToan.FlatStyle = FlatStyle.Flat;
        this.btnThanhToan.BackColor = AppColors.Primary;
        this.btnThanhToan.ForeColor = Color.White;
        this.btnThanhToan.Font = AppFonts.LabelLg;
        this.btnThanhToan.FlatAppearance.BorderSize = 0;
        //
        // panelTotals
        //
        this.panelTotals.Location = new Point(16, 520);
        this.panelTotals.Size = new Size(370, 200);
        this.panelTotals.Controls.Add(this.lblSubtotalCaption);
        this.panelTotals.Controls.Add(this.lblSubtotalValue);
        this.panelTotals.Controls.Add(this.lblVatCaption);
        this.panelTotals.Controls.Add(this.lblVatValue);
        this.panelTotals.Controls.Add(this.lblTotalCaption);
        this.panelTotals.Controls.Add(this.lblTotalValue);
        this.panelTotals.Controls.Add(this.btnVietQr);
        this.panelTotals.Controls.Add(this.btnThanhToan);
        //
        // panelRight
        //
        this.panelRight.Dock = DockStyle.Right;
        this.panelRight.Width = 400;
        this.panelRight.BackColor = Color.White;
        this.panelRight.Padding = new Padding(0);
        this.panelRight.Controls.Add(this.panelTotals);
        this.panelRight.Controls.Add(this.flowCartItems);
        this.panelRight.Controls.Add(this.lblTableName);
        this.panelRight.Controls.Add(this.cbTable);
        this.panelRight.Controls.Add(this.lblTableLabel);
        //
        // SalesControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.panelLeft);
        this.Controls.Add(this.panelRight);
        this.Name = "SalesControl";
        this.Size = new Size(1100, 750);
        this.ResumeLayout(false);
    }
}
