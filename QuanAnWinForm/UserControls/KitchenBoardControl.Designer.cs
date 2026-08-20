using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class KitchenBoardControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private FlowLayoutPanel flowColumns;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.lblSubtitle = new Label();
        this.flowColumns = new FlowLayoutPanel();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        this.lblTitle.Text = "Màn hình Bếp";
        //
        // lblSubtitle
        //
        this.lblSubtitle.Text = "Đang hiển thị các đơn hàng cần chế biến (realtime)";
        this.lblSubtitle.Font = AppFonts.BodyMd;
        this.lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubtitle.Location = new Point(0, 36);
        this.lblSubtitle.AutoSize = true;
        //
        // flowColumns
        //
        this.flowColumns.Location = new Point(0, 76);
        this.flowColumns.Size = new Size(1060, 660);
        this.flowColumns.AutoScroll = true;
        this.flowColumns.WrapContents = false;
        //
        // KitchenBoardControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.flowColumns);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.Name = "KitchenBoardControl";
        this.Size = new Size(1060, 750);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
