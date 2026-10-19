using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class RevenueControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private ComboBox cbPeriod;
    private Button btnRefresh;

    private StatCard cardRevenue;
    private StatCard cardProfit;
    private StatCard cardCost;
    private StatCard cardOrders;

    private TabControl tabReports;
    private TabPage tabRevenue;
    private TabPage tabItems;
    private TabPage tabEmployees;
    private TabPage tabCampaigns;

    private Panel panelChartCard;
    private Label lblChartTitle;
    private MiniBarChartPanel chartPanel;
    private Panel panelInvoicesCard;
    private Label lblInvoicesTitle;
    private DataGridView gridInvoices;

    private DataGridView gridTopItems;
    private DataGridView gridByEmployee;
    private DataGridView gridCampaigns;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblSubtitle = new Label();
        cbPeriod = new ComboBox();
        btnRefresh = new Button();
        cardRevenue = new StatCard();
        cardProfit = new StatCard();
        cardCost = new StatCard();
        cardOrders = new StatCard();
        tabReports = new TabControl();
        tabRevenue = new TabPage();
        panelChartCard = new Panel();
        lblChartTitle = new Label();
        chartPanel = new MiniBarChartPanel();
        panelInvoicesCard = new Panel();
        gridInvoices = new DataGridView();
        lblInvoicesTitle = new Label();
        dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn5 = new DataGridViewTextBoxColumn();
        tabItems = new TabPage();
        gridTopItems = new DataGridView();
        tabEmployees = new TabPage();
        gridByEmployee = new DataGridView();
        tabCampaigns = new TabPage();
        gridCampaigns = new DataGridView();
        tabReports.SuspendLayout();
        tabRevenue.SuspendLayout();
        panelChartCard.SuspendLayout();
        panelInvoicesCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridInvoices).BeginInit();
        tabItems.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridTopItems).BeginInit();
        tabEmployees.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridByEmployee).BeginInit();
        tabCampaigns.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridCampaigns).BeginInit();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(372, 50);
        lblTitle.TabIndex = 7;
        lblTitle.Text = "Thống kê & Báo cáo";
        // 
        // lblSubtitle
        // 
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(16, 50);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(150, 23);
        lblSubtitle.TabIndex = 6;
        lblSubtitle.Text = "Ngày ..";
        // 
        // cbPeriod
        // 
        cbPeriod.DropDownStyle = ComboBoxStyle.DropDownList;
        cbPeriod.Font = new Font("Segoe UI", 9.5F);
        cbPeriod.Items.AddRange(new object[] { "Hôm nay", "7 ngày qua", "30 ngày qua", "Tháng này" });
        cbPeriod.Location = new Point(1160, 16);
        cbPeriod.Name = "cbPeriod";
        cbPeriod.Size = new Size(150, 30);
        cbPeriod.TabIndex = 8;
        // 
        // btnRefresh
        // 
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.Font = new Font("Segoe UI", 9.5F);
        btnRefresh.Location = new Point(1320, 16);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(100, 32);
        btnRefresh.TabIndex = 9;
        btnRefresh.Text = "⟳ Làm mới";
        // 
        // cardRevenue
        // 
        cardRevenue.BackColor = Color.White;
        cardRevenue.Location = new Point(16, 77);
        cardRevenue.Name = "cardRevenue";
        cardRevenue.Padding = new Padding(16);
        cardRevenue.Size = new Size(220, 130);
        cardRevenue.TabIndex = 5;
        // 
        // cardProfit
        // 
        cardProfit.BackColor = Color.White;
        cardProfit.Location = new Point(246, 77);
        cardProfit.Name = "cardProfit";
        cardProfit.Padding = new Padding(16);
        cardProfit.Size = new Size(220, 130);
        cardProfit.TabIndex = 4;
        // 
        // cardCost
        // 
        cardCost.BackColor = Color.White;
        cardCost.Location = new Point(476, 77);
        cardCost.Name = "cardCost";
        cardCost.Padding = new Padding(16);
        cardCost.Size = new Size(220, 130);
        cardCost.TabIndex = 3;
        // 
        // cardOrders
        // 
        cardOrders.BackColor = Color.White;
        cardOrders.Location = new Point(706, 77);
        cardOrders.Name = "cardOrders";
        cardOrders.Padding = new Padding(16);
        cardOrders.Size = new Size(220, 130);
        cardOrders.TabIndex = 2;
        // 
        // tabReports
        // 
        tabReports.Controls.Add(tabRevenue);
        tabReports.Controls.Add(tabItems);
        tabReports.Controls.Add(tabEmployees);
        tabReports.Controls.Add(tabCampaigns);
        tabReports.Font = new Font("Segoe UI", 10F);
        tabReports.Location = new Point(16, 229);
        tabReports.Name = "tabReports";
        tabReports.SelectedIndex = 0;
        tabReports.Size = new Size(1060, 596);
        tabReports.TabIndex = 10;
        // 
        // tabRevenue
        // 
        tabRevenue.Controls.Add(panelChartCard);
        tabRevenue.Controls.Add(panelInvoicesCard);
        tabRevenue.Location = new Point(4, 33);
        tabRevenue.Name = "tabRevenue";
        tabRevenue.Padding = new Padding(8);
        tabRevenue.Size = new Size(1052, 559);
        tabRevenue.TabIndex = 0;
        tabRevenue.Text = "Doanh thu";
        tabRevenue.UseVisualStyleBackColor = true;
        // 
        // panelChartCard
        // 
        panelChartCard.BackColor = Color.White;
        panelChartCard.Controls.Add(lblChartTitle);
        panelChartCard.Controls.Add(chartPanel);
        panelChartCard.Location = new Point(8, 8);
        panelChartCard.Name = "panelChartCard";
        panelChartCard.Size = new Size(1030, 320);
        panelChartCard.TabIndex = 1;
        // 
        // lblChartTitle
        // 
        lblChartTitle.AutoSize = true;
        lblChartTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblChartTitle.Location = new Point(16, 16);
        lblChartTitle.Name = "lblChartTitle";
        lblChartTitle.Size = new Size(203, 30);
        lblChartTitle.TabIndex = 0;
        lblChartTitle.Text = "Biểu đồ doanh thu";
        // 
        // chartPanel
        // 
        chartPanel.BackColor = Color.White;
        chartPanel.HighlightIndex = -1;
        chartPanel.Location = new Point(16, 50);
        chartPanel.Name = "chartPanel";
        chartPanel.Size = new Size(998, 254);
        chartPanel.TabIndex = 1;
        // 
        // panelInvoicesCard
        // 
        panelInvoicesCard.BackColor = Color.White;
        panelInvoicesCard.Controls.Add(gridInvoices);
        panelInvoicesCard.Controls.Add(lblInvoicesTitle);
        panelInvoicesCard.Location = new Point(8, 336);
        panelInvoicesCard.Name = "panelInvoicesCard";
        panelInvoicesCard.Size = new Size(1030, 216);
        panelInvoicesCard.TabIndex = 0;
        // 
        // gridInvoices
        // 
        gridInvoices.AllowUserToAddRows = false;
        gridInvoices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridInvoices.BackgroundColor = Color.White;
        gridInvoices.BorderStyle = BorderStyle.None;
        gridInvoices.ColumnHeadersHeight = 29;
        gridInvoices.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4, dataGridViewTextBoxColumn5 });
        gridInvoices.Location = new Point(16, 56);
        gridInvoices.Name = "gridInvoices";
        gridInvoices.ReadOnly = true;
        gridInvoices.RowHeadersVisible = false;
        gridInvoices.RowHeadersWidth = 51;
        gridInvoices.Size = new Size(998, 146);
        gridInvoices.TabIndex = 0;
        // 
        // lblInvoicesTitle
        // 
        lblInvoicesTitle.AutoSize = true;
        lblInvoicesTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblInvoicesTitle.Location = new Point(16, 16);
        lblInvoicesTitle.Name = "lblInvoicesTitle";
        lblInvoicesTitle.Size = new Size(191, 30);
        lblInvoicesTitle.TabIndex = 1;
        lblInvoicesTitle.Text = "Hóa đơn gần đây";
        // 
        // dataGridViewTextBoxColumn1
        // 
        dataGridViewTextBoxColumn1.HeaderText = "Mã Hóa Đơn";
        dataGridViewTextBoxColumn1.MinimumWidth = 6;
        dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
        dataGridViewTextBoxColumn1.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn2
        // 
        dataGridViewTextBoxColumn2.HeaderText = "Bàn / Kênh";
        dataGridViewTextBoxColumn2.MinimumWidth = 6;
        dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
        dataGridViewTextBoxColumn2.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn3
        // 
        dataGridViewTextBoxColumn3.HeaderText = "Thời gian";
        dataGridViewTextBoxColumn3.MinimumWidth = 6;
        dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
        dataGridViewTextBoxColumn3.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn4
        // 
        dataGridViewTextBoxColumn4.HeaderText = "Số tiền";
        dataGridViewTextBoxColumn4.MinimumWidth = 6;
        dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
        dataGridViewTextBoxColumn4.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn5
        // 
        dataGridViewTextBoxColumn5.HeaderText = "Trạng thái";
        dataGridViewTextBoxColumn5.MinimumWidth = 6;
        dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
        dataGridViewTextBoxColumn5.ReadOnly = true;
        // 
        // tabItems
        // 
        tabItems.Controls.Add(gridTopItems);
        tabItems.Location = new Point(4, 33);
        tabItems.Name = "tabItems";
        tabItems.Padding = new Padding(8);
        tabItems.Size = new Size(1052, 559);
        tabItems.TabIndex = 1;
        tabItems.Text = "Món ăn bán chạy";
        tabItems.UseVisualStyleBackColor = true;
        // 
        // gridTopItems
        // 
        gridTopItems.AllowUserToAddRows = false;
        gridTopItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridTopItems.BackgroundColor = Color.White;
        gridTopItems.BorderStyle = BorderStyle.FixedSingle;
        gridTopItems.ColumnHeadersHeight = 32;
        gridTopItems.Location = new Point(8, 8);
        gridTopItems.Name = "gridTopItems";
        gridTopItems.ReadOnly = true;
        gridTopItems.RowHeadersVisible = false;
        gridTopItems.Size = new Size(1036, 543);
        gridTopItems.TabIndex = 0;
        // 
        // tabEmployees
        // 
        tabEmployees.Controls.Add(gridByEmployee);
        tabEmployees.Location = new Point(4, 33);
        tabEmployees.Name = "tabEmployees";
        tabEmployees.Padding = new Padding(8);
        tabEmployees.Size = new Size(1052, 559);
        tabEmployees.TabIndex = 2;
        tabEmployees.Text = "Theo nhân viên";
        tabEmployees.UseVisualStyleBackColor = true;
        // 
        // gridByEmployee
        // 
        gridByEmployee.AllowUserToAddRows = false;
        gridByEmployee.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridByEmployee.BackgroundColor = Color.White;
        gridByEmployee.BorderStyle = BorderStyle.FixedSingle;
        gridByEmployee.ColumnHeadersHeight = 32;
        gridByEmployee.Location = new Point(8, 8);
        gridByEmployee.Name = "gridByEmployee";
        gridByEmployee.ReadOnly = true;
        gridByEmployee.RowHeadersVisible = false;
        gridByEmployee.Size = new Size(1036, 543);
        gridByEmployee.TabIndex = 0;
        // 
        // tabCampaigns
        // 
        tabCampaigns.Controls.Add(gridCampaigns);
        tabCampaigns.Location = new Point(4, 33);
        tabCampaigns.Name = "tabCampaigns";
        tabCampaigns.Padding = new Padding(8);
        tabCampaigns.Size = new Size(1052, 559);
        tabCampaigns.TabIndex = 3;
        tabCampaigns.Text = "Khuyến mãi";
        tabCampaigns.UseVisualStyleBackColor = true;
        // 
        // gridCampaigns
        // 
        gridCampaigns.AllowUserToAddRows = false;
        gridCampaigns.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridCampaigns.BackgroundColor = Color.White;
        gridCampaigns.BorderStyle = BorderStyle.FixedSingle;
        gridCampaigns.ColumnHeadersHeight = 32;
        gridCampaigns.Location = new Point(8, 8);
        gridCampaigns.Name = "gridCampaigns";
        gridCampaigns.ReadOnly = true;
        gridCampaigns.RowHeadersVisible = false;
        gridCampaigns.Size = new Size(1036, 543);
        gridCampaigns.TabIndex = 0;
        // 
        // RevenueControl
        // 
        AutoScroll = true;
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tabReports);
        Controls.Add(cardOrders);
        Controls.Add(cardCost);
        Controls.Add(cardProfit);
        Controls.Add(cardRevenue);
        Controls.Add(btnRefresh);
        Controls.Add(cbPeriod);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        Name = "RevenueControl";
        Size = new Size(1438, 860);
        tabReports.ResumeLayout(false);
        tabRevenue.ResumeLayout(false);
        panelChartCard.ResumeLayout(false);
        panelChartCard.PerformLayout();
        panelInvoicesCard.ResumeLayout(false);
        panelInvoicesCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)gridInvoices).EndInit();
        tabItems.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridTopItems).EndInit();
        tabEmployees.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridByEmployee).EndInit();
        tabCampaigns.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridCampaigns).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
}