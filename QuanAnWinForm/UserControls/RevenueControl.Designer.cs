using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class RevenueControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private StatCard cardRevenue;
    private StatCard cardProfit;
    private StatCard cardCost;
    private StatCard cardOrders;
    private Panel panelChartCard;
    private Label lblChartTitle;
    private MiniBarChartPanel chartPanel;
    private Panel panelInvoicesCard;
    private Label lblInvoicesTitle;
    private DataGridView gridInvoices;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.lblSubtitle = new Label();
        this.cardRevenue = new StatCard();
        this.cardProfit = new StatCard();
        this.cardCost = new StatCard();
        this.cardOrders = new StatCard();
        this.panelChartCard = new Panel();
        this.lblChartTitle = new Label();
        this.chartPanel = new MiniBarChartPanel();
        this.panelInvoicesCard = new Panel();
        this.lblInvoicesTitle = new Label();
        this.gridInvoices = new DataGridView();
        ((System.ComponentModel.ISupportInitialize)(this.gridInvoices)).BeginInit();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Text = "Thống kê doanh thu";
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        //
        // lblSubtitle
        //
        this.lblSubtitle.Text = "Cập nhật lúc " + DateTime.Now.ToString("HH:mm, dd/MM/yyyy");
        this.lblSubtitle.Font = AppFonts.BodyMd;
        this.lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubtitle.Location = new Point(0, 36);
        this.lblSubtitle.AutoSize = true;
        //
        // 4 stat card
        //
        this.cardRevenue.Configure("💰", AppColors.Primary, AppColors.ErrorContainer, "Tổng doanh thu", "124,500,000 đ", "+12.5%", true);
        this.cardRevenue.Location = new Point(0, 76);
        this.cardProfit.Configure("💵", AppColors.Tertiary, Color.FromArgb(230, 245, 240), "Lợi nhuận ròng", "45,200,000 đ", "+8.2%", true);
        this.cardProfit.Location = new Point(230, 76);
        this.cardCost.Configure("🧾", AppColors.Error, AppColors.ErrorContainer, "Tổng chi phí", "79,300,000 đ", "-2.1%", false);
        this.cardCost.Location = new Point(460, 76);
        this.cardOrders.Configure("🛍", AppColors.Secondary, AppColors.SecondaryContainer, "Số lượng đơn hàng", "1,452", "+15.0%", true);
        this.cardOrders.Location = new Point(690, 76);
        //
        // lblChartTitle
        //
        this.lblChartTitle.Text = "Biểu đồ doanh thu";
        this.lblChartTitle.Font = AppFonts.HeadlineSm;
        this.lblChartTitle.Location = new Point(16, 16);
        this.lblChartTitle.AutoSize = true;
        //
        // chartPanel
        //
        this.chartPanel.Location = new Point(16, 50);
        this.chartPanel.Size = new Size(1000, 260);
        //
        // panelChartCard
        //
        this.panelChartCard.BackColor = Color.White;
        this.panelChartCard.Location = new Point(0, 230);
        this.panelChartCard.Size = new Size(1060, 330);
        this.panelChartCard.Controls.Add(this.lblChartTitle);
        this.panelChartCard.Controls.Add(this.chartPanel);
        //
        // lblInvoicesTitle
        //
        this.lblInvoicesTitle.Text = "Hóa đơn gần đây";
        this.lblInvoicesTitle.Font = AppFonts.HeadlineSm;
        this.lblInvoicesTitle.Location = new Point(16, 16);
        this.lblInvoicesTitle.AutoSize = true;
        //
        // gridInvoices
        //
        this.gridInvoices.Location = new Point(16, 56);
        this.gridInvoices.Size = new Size(1028, 180);
        this.gridInvoices.ReadOnly = true;
        this.gridInvoices.AllowUserToAddRows = false;
        this.gridInvoices.RowHeadersVisible = false;
        this.gridInvoices.BackgroundColor = Color.White;
        this.gridInvoices.BorderStyle = BorderStyle.None;
        this.gridInvoices.Columns.Add("MaHoaDon", "Mã Hóa Đơn");
        this.gridInvoices.Columns.Add("BanKenh", "Bàn / Kênh");
        this.gridInvoices.Columns.Add("ThoiGian", "Thời gian");
        this.gridInvoices.Columns.Add("SoTien", "Số tiền");
        this.gridInvoices.Columns.Add("TrangThai", "Trạng thái");
        this.gridInvoices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        //
        // panelInvoicesCard
        //
        this.panelInvoicesCard.BackColor = Color.White;
        this.panelInvoicesCard.Location = new Point(0, 576);
        this.panelInvoicesCard.Size = new Size(1060, 260);
        this.panelInvoicesCard.Controls.Add(this.gridInvoices);
        this.panelInvoicesCard.Controls.Add(this.lblInvoicesTitle);
        //
        // RevenueControl
        //
        this.AutoScroll = true;
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.panelInvoicesCard);
        this.Controls.Add(this.panelChartCard);
        this.Controls.Add(this.cardOrders);
        this.Controls.Add(this.cardCost);
        this.Controls.Add(this.cardProfit);
        this.Controls.Add(this.cardRevenue);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.Name = "RevenueControl";
        this.Size = new Size(1060, 860);
        ((System.ComponentModel.ISupportInitialize)(this.gridInvoices)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
