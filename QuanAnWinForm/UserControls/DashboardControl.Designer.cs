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
        lblTitle = new Label();
        lblSubtitle = new Label();
        flowStatCards = new FlowLayoutPanel();
        cardRevenue = new StatCard();
        cardOrders = new StatCard();
        cardTables = new StatCard();
        cardNewCustomers = new StatCard();
        panelChartCard = new Panel();
        lblChartTitle = new Label();
        chartPanel = new MiniBarChartPanel();
        panelTopItemsCard = new Panel();
        lblTopItemsTitle = new Label();
        flowTopItems = new FlowLayoutPanel();
        flowStatCards.SuspendLayout();
        panelChartCard.SuspendLayout();
        panelTopItemsCard.SuspendLayout();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(21, 16);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(370, 50);
        lblTitle.TabIndex = 4;
        lblTitle.Text = "Tổng quan hôm nay";
        // 
        // lblSubtitle
        // 
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(25, 76);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(247, 23);
        lblSubtitle.TabIndex = 3;
        lblSubtitle.Text = "Cập nhật lúc 08:01, 20/09/2026";
        // 
        // flowStatCards
        // 
        flowStatCards.Controls.Add(cardRevenue);
        flowStatCards.Controls.Add(cardOrders);
        flowStatCards.Controls.Add(cardTables);
        flowStatCards.Controls.Add(cardNewCustomers);
        flowStatCards.Location = new Point(22, 115);
        flowStatCards.Name = "flowStatCards";
        flowStatCards.Size = new Size(1200, 140);
        flowStatCards.TabIndex = 2;
        // 
        // cardRevenue
        // 
        cardRevenue.BackColor = Color.White;
        cardRevenue.Location = new Point(3, 3);
        cardRevenue.Name = "cardRevenue";
        cardRevenue.Padding = new Padding(16);
        cardRevenue.Size = new Size(220, 130);
        cardRevenue.TabIndex = 0;
        // 
        // cardOrders
        // 
        cardOrders.BackColor = Color.White;
        cardOrders.Location = new Point(229, 3);
        cardOrders.Name = "cardOrders";
        cardOrders.Padding = new Padding(16);
        cardOrders.Size = new Size(220, 130);
        cardOrders.TabIndex = 1;
        // 
        // cardTables
        // 
        cardTables.BackColor = Color.White;
        cardTables.Location = new Point(455, 3);
        cardTables.Name = "cardTables";
        cardTables.Padding = new Padding(16);
        cardTables.Size = new Size(220, 130);
        cardTables.TabIndex = 2;
        // 
        // cardNewCustomers
        // 
        cardNewCustomers.BackColor = Color.White;
        cardNewCustomers.Location = new Point(681, 3);
        cardNewCustomers.Name = "cardNewCustomers";
        cardNewCustomers.Padding = new Padding(16);
        cardNewCustomers.Size = new Size(250, 130);
        cardNewCustomers.TabIndex = 3;
        // 
        // panelChartCard
        // 
        panelChartCard.BackColor = Color.White;
        panelChartCard.Controls.Add(lblChartTitle);
        panelChartCard.Controls.Add(chartPanel);
        panelChartCard.Location = new Point(21, 277);
        panelChartCard.Name = "panelChartCard";
        panelChartCard.Size = new Size(700, 350);
        panelChartCard.TabIndex = 1;
        // 
        // lblChartTitle
        // 
        lblChartTitle.AutoSize = true;
        lblChartTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblChartTitle.Location = new Point(16, 16);
        lblChartTitle.Name = "lblChartTitle";
        lblChartTitle.Size = new Size(337, 30);
        lblChartTitle.TabIndex = 0;
        lblChartTitle.Text = "Biểu đồ tăng trưởng doanh thu";
        // 
        // chartPanel
        // 
        chartPanel.BackColor = Color.White;
        chartPanel.HighlightIndex = -1;
        chartPanel.Location = new Point(16, 50);
        chartPanel.Name = "chartPanel";
        chartPanel.Size = new Size(650, 280);
        chartPanel.TabIndex = 1;
        // 
        // panelTopItemsCard
        // 
        panelTopItemsCard.BackColor = Color.White;
        panelTopItemsCard.Controls.Add(lblTopItemsTitle);
        panelTopItemsCard.Controls.Add(flowTopItems);
        panelTopItemsCard.Location = new Point(737, 277);
        panelTopItemsCard.Name = "panelTopItemsCard";
        panelTopItemsCard.Size = new Size(340, 350);
        panelTopItemsCard.TabIndex = 0;
        // 
        // lblTopItemsTitle
        // 
        lblTopItemsTitle.AutoSize = true;
        lblTopItemsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTopItemsTitle.Location = new Point(16, 16);
        lblTopItemsTitle.Name = "lblTopItemsTitle";
        lblTopItemsTitle.Size = new Size(212, 30);
        lblTopItemsTitle.TabIndex = 0;
        lblTopItemsTitle.Text = "Món bán chạy nhất";
        // 
        // flowTopItems
        // 
        flowTopItems.FlowDirection = FlowDirection.TopDown;
        flowTopItems.Location = new Point(16, 56);
        flowTopItems.Name = "flowTopItems";
        flowTopItems.Size = new Size(300, 280);
        flowTopItems.TabIndex = 1;
        flowTopItems.WrapContents = false;
        // 
        // DashboardControl
        // 
        AutoScroll = true;
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(panelTopItemsCard);
        Controls.Add(panelChartCard);
        Controls.Add(flowStatCards);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        Name = "DashboardControl";
        Size = new Size(1356, 875);
        flowStatCards.ResumeLayout(false);
        panelChartCard.ResumeLayout(false);
        panelChartCard.PerformLayout();
        panelTopItemsCard.ResumeLayout(false);
        panelTopItemsCard.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
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
