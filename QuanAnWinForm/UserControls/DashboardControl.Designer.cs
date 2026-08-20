using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class DashboardControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private FlowLayoutPanel flowStatCards;
    private StatCard cardRevenue;
    private StatCard cardOrders;
    private StatCard cardTables;
    private StatCard cardNewCustomers;
    private Panel panelChartCard;
    private Label lblChartTitle;
    private MiniBarChartPanel chartPanel;
    private Panel panelTopItemsCard;
    private Label lblTopItemsTitle;
    private FlowLayoutPanel flowTopItems;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.lblSubtitle = new Label();
        this.flowStatCards = new FlowLayoutPanel();
        this.cardRevenue = new StatCard();
        this.cardOrders = new StatCard();
        this.cardTables = new StatCard();
        this.cardNewCustomers = new StatCard();
        this.panelChartCard = new Panel();
        this.lblChartTitle = new Label();
        this.chartPanel = new MiniBarChartPanel();
        this.panelTopItemsCard = new Panel();
        this.lblTopItemsTitle = new Label();
        this.flowTopItems = new FlowLayoutPanel();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.Text = "Tổng quan hôm nay";
        //
        // lblSubtitle
        //
        this.lblSubtitle.AutoSize = true;
        this.lblSubtitle.Font = AppFonts.BodyMd;
        this.lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubtitle.Location = new Point(0, 36);
        this.lblSubtitle.Text = "Cập nhật lúc " + DateTime.Now.ToString("HH:mm, dd/MM/yyyy");
        //
        // cardRevenue / cardOrders / cardTables / cardNewCustomers
        //
        this.cardRevenue.Configure("💰", AppColors.Primary, AppColors.ErrorContainer, "Doanh thu trong ngày", "24,500,000 đ", "+12.5%", true);
        this.cardOrders.Configure("🧾", AppColors.Secondary, AppColors.SecondaryContainer, "Số lượng đơn hàng", "156", "+5.2%", true);
        this.cardTables.Configure("🍽", AppColors.Tertiary, Color.FromArgb(230, 245, 240), "Bàn đang có khách", "18 / 32", "0%", true);
        this.cardNewCustomers.Configure("👥", AppColors.Secondary, AppColors.SurfaceContainer, "Khách hàng thành viên mới", "24", "-2.1%", false);
        //
        // flowStatCards
        //
        this.flowStatCards.Location = new Point(0, 70);
        this.flowStatCards.Size = new Size(1200, 140);
        this.flowStatCards.Controls.Add(this.cardRevenue);
        this.flowStatCards.Controls.Add(this.cardOrders);
        this.flowStatCards.Controls.Add(this.cardTables);
        this.flowStatCards.Controls.Add(this.cardNewCustomers);
        //
        // lblChartTitle
        //
        this.lblChartTitle.AutoSize = true;
        this.lblChartTitle.Font = AppFonts.HeadlineSm;
        this.lblChartTitle.Location = new Point(16, 16);
        this.lblChartTitle.Text = "Biểu đồ tăng trưởng doanh thu";
        //
        // chartPanel
        //
        this.chartPanel.Location = new Point(16, 50);
        this.chartPanel.Size = new Size(650, 280);
        //
        // panelChartCard
        //
        this.panelChartCard.BackColor = Color.White;
        this.panelChartCard.Location = new Point(0, 230);
        this.panelChartCard.Size = new Size(700, 350);
        this.panelChartCard.Controls.Add(this.lblChartTitle);
        this.panelChartCard.Controls.Add(this.chartPanel);
        //
        // lblTopItemsTitle
        //
        this.lblTopItemsTitle.AutoSize = true;
        this.lblTopItemsTitle.Font = AppFonts.HeadlineSm;
        this.lblTopItemsTitle.Location = new Point(16, 16);
        this.lblTopItemsTitle.Text = "Món bán chạy nhất";
        //
        // flowTopItems
        //
        this.flowTopItems.FlowDirection = FlowDirection.TopDown;
        this.flowTopItems.WrapContents = false;
        this.flowTopItems.Location = new Point(16, 56);
        this.flowTopItems.Size = new Size(300, 280);
        BuildTopItemRow(this.flowTopItems, "Beef Steak Premium", "45 phần đã bán", "13.5M");
        BuildTopItemRow(this.flowTopItems, "Caesar Salad Classic", "38 phần đã bán", "5.7M");
        BuildTopItemRow(this.flowTopItems, "Signature Cocktail", "22 ly đã bán", "3.3M");
        //
        // panelTopItemsCard
        //
        this.panelTopItemsCard.BackColor = Color.White;
        this.panelTopItemsCard.Location = new Point(716, 230);
        this.panelTopItemsCard.Size = new Size(340, 350);
        this.panelTopItemsCard.Controls.Add(this.lblTopItemsTitle);
        this.panelTopItemsCard.Controls.Add(this.flowTopItems);
        //
        // DashboardControl
        //
        this.AutoScroll = true;
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.panelTopItemsCard);
        this.Controls.Add(this.panelChartCard);
        this.Controls.Add(this.flowStatCards);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.Name = "DashboardControl";
        this.Size = new Size(1060, 620);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void BuildTopItemRow(FlowLayoutPanel parent, string name, string sold, string revenue)
    {
        var row = new Panel { Size = new Size(300, 60), Margin = new Padding(0, 0, 0, 8) };
        var thumb = new Panel { Size = new Size(44, 44), Location = new Point(0, 8), BackColor = AppColors.SurfaceContainer };
        var lblName = new Label { Text = name, Font = AppFonts.LabelLg, Location = new Point(56, 6), AutoSize = true };
        var lblSold = new Label { Text = sold, Font = AppFonts.BodyMd, ForeColor = AppColors.OnSurfaceVariant, Location = new Point(56, 26), AutoSize = true };
        var lblRevenue = new Label { Text = revenue, Font = AppFonts.LabelLg, ForeColor = AppColors.Primary, Location = new Point(250, 16), AutoSize = true };
        row.Controls.Add(thumb);
        row.Controls.Add(lblName);
        row.Controls.Add(lblSold);
        row.Controls.Add(lblRevenue);
        parent.Controls.Add(row);
    }
}
