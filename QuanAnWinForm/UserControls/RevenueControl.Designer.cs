using QuanAnWinForm.Controls;

namespace QuanAnWinForm.UserControls;

partial class RevenueControl
{
    private System.ComponentModel.IContainer components = null;

    // Khung chinh
    private TableLayoutPanel tlRoot;
    private Panel pnlHeader;
    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnExport;
    private Button btnRefresh;

    // Thanh loc
    private Panel pnlFilter;
    private FlowLayoutPanel flowFilter;
    private Label lblFilterCaption;
    private ComboBox cbPeriod;
    private DateTimePicker dtpDate;
    private ComboBox cbWeek;
    private ComboBox cbMonth;
    private ComboBox cbQuarter;
    private NumericUpDown nudYear;
    private Label lblRange;

    // The thong ke
    private TableLayoutPanel tlCards;
    private StatCard cardRevenue;
    private StatCard cardProfit;
    private StatCard cardCost;
    private StatCard cardOrders;

    // Tab
    private TabControl tabReports;
    private TabPage tabRevenue;
    private TabPage tabItems;
    private TabPage tabEmployees;
    private TabPage tabCampaigns;

    // Tab Doanh thu
    private TableLayoutPanel tlRevenue;
    private TableLayoutPanel tlLeft;
    private TableLayoutPanel tlRight;
    private Panel panelChartCard;
    private Label lblChartTitle;
    private MiniBarChartPanel chartPanel;
    private Panel panelInvoicesCard;
    private Label lblInvoicesTitle;
    private DataGridView gridInvoices;
    private Panel pnlInsightsCard;
    private Label lblInsightsTitle;
    private TableLayoutPanel tlInsights;
    private Panel pnlTop5Card;
    private Label lblTop5Title;
    private DataGridView gridTop5;

    // Cac tab con lai
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
        tlRoot = new TableLayoutPanel();
        pnlHeader = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnExport = new Button();
        btnRefresh = new Button();
        pnlFilter = new Panel();
        flowFilter = new FlowLayoutPanel();
        lblFilterCaption = new Label();
        cbPeriod = new ComboBox();
        dtpDate = new DateTimePicker();
        cbWeek = new ComboBox();
        cbMonth = new ComboBox();
        cbQuarter = new ComboBox();
        nudYear = new NumericUpDown();
        lblRange = new Label();
        tlCards = new TableLayoutPanel();
        cardRevenue = new StatCard();
        cardProfit = new StatCard();
        cardCost = new StatCard();
        cardOrders = new StatCard();
        tabReports = new TabControl();
        tabRevenue = new TabPage();
        tlRevenue = new TableLayoutPanel();
        tlLeft = new TableLayoutPanel();
        panelChartCard = new Panel();
        chartPanel = new MiniBarChartPanel();
        lblChartTitle = new Label();
        panelInvoicesCard = new Panel();
        gridInvoices = new DataGridView();
        lblInvoicesTitle = new Label();
        tlRight = new TableLayoutPanel();
        pnlInsightsCard = new Panel();
        tlInsights = new TableLayoutPanel();
        lblInsightsTitle = new Label();
        pnlTop5Card = new Panel();
        gridTop5 = new DataGridView();
        lblTop5Title = new Label();
        tabItems = new TabPage();
        gridTopItems = new DataGridView();
        tabEmployees = new TabPage();
        gridByEmployee = new DataGridView();
        tabCampaigns = new TabPage();
        gridCampaigns = new DataGridView();

        tlRoot.SuspendLayout();
        pnlHeader.SuspendLayout();
        pnlFilter.SuspendLayout();
        flowFilter.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)nudYear).BeginInit();
        tlCards.SuspendLayout();
        tabReports.SuspendLayout();
        tabRevenue.SuspendLayout();
        tlRevenue.SuspendLayout();
        tlLeft.SuspendLayout();
        panelChartCard.SuspendLayout();
        panelInvoicesCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridInvoices).BeginInit();
        tlRight.SuspendLayout();
        pnlInsightsCard.SuspendLayout();
        pnlTop5Card.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridTop5).BeginInit();
        tabItems.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridTopItems).BeginInit();
        tabEmployees.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridByEmployee).BeginInit();
        tabCampaigns.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridCampaigns).BeginInit();
        SuspendLayout();
        //
        // tlRoot : 4 hang (tieu de / bo loc / the / noi dung)
        //
        tlRoot.ColumnCount = 1;
        tlRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlRoot.RowCount = 4;
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlRoot.Controls.Add(pnlHeader, 0, 0);
        tlRoot.Controls.Add(pnlFilter, 0, 1);
        tlRoot.Controls.Add(tlCards, 0, 2);
        tlRoot.Controls.Add(tabReports, 0, 3);
        tlRoot.Dock = DockStyle.Fill;
        tlRoot.Name = "tlRoot";
        tlRoot.TabIndex = 0;
        //
        // pnlHeader
        //
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        pnlHeader.Controls.Add(btnExport);
        pnlHeader.Controls.Add(btnRefresh);
        pnlHeader.Dock = DockStyle.Fill;
        pnlHeader.Margin = new Padding(0);
        pnlHeader.Name = "pnlHeader";
        pnlHeader.TabIndex = 0;
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Thống kê";
        //
        // lblSubtitle
        //
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.Location = new Point(4, 50);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Ngày ..";
        //
        // btnExport
        //
        btnExport.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnExport.Name = "btnExport";
        btnExport.Size = new Size(150, 38);
        btnExport.TabIndex = 2;
        btnExport.Text = "📥 Xuất Excel";
        btnExport.UseVisualStyleBackColor = false;
        //
        // btnRefresh
        //
        btnRefresh.Font = new Font("Segoe UI", 10F);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(110, 38);
        btnRefresh.TabIndex = 3;
        btnRefresh.Text = "⟳ Làm mới";
        btnRefresh.UseVisualStyleBackColor = false;
        //
        // pnlFilter (the trang chua bo loc)
        //
        pnlFilter.BackColor = Color.White;
        pnlFilter.Controls.Add(flowFilter);
        pnlFilter.Dock = DockStyle.Fill;
        pnlFilter.Margin = new Padding(0, 0, 0, 12);
        pnlFilter.Name = "pnlFilter";
        pnlFilter.Padding = new Padding(1);
        pnlFilter.TabIndex = 1;
        //
        // flowFilter
        //
        flowFilter.Controls.Add(lblFilterCaption);
        flowFilter.Controls.Add(cbPeriod);
        flowFilter.Controls.Add(dtpDate);
        flowFilter.Controls.Add(cbWeek);
        flowFilter.Controls.Add(cbMonth);
        flowFilter.Controls.Add(cbQuarter);
        flowFilter.Controls.Add(nudYear);
        flowFilter.Controls.Add(lblRange);
        flowFilter.Dock = DockStyle.Fill;
        flowFilter.FlowDirection = FlowDirection.LeftToRight;
        flowFilter.Name = "flowFilter";
        flowFilter.Padding = new Padding(14, 11, 14, 0);
        flowFilter.WrapContents = false;
        flowFilter.TabIndex = 0;
        //
        // lblFilterCaption
        //
        lblFilterCaption.AutoSize = false;
        lblFilterCaption.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblFilterCaption.Margin = new Padding(0, 0, 8, 0);
        lblFilterCaption.Name = "lblFilterCaption";
        lblFilterCaption.Size = new Size(74, 30);
        lblFilterCaption.Text = "Xem theo:";
        lblFilterCaption.TextAlign = ContentAlignment.MiddleLeft;
        //
        // cbPeriod
        //
        cbPeriod.DropDownStyle = ComboBoxStyle.DropDownList;
        cbPeriod.Font = new Font("Segoe UI", 10F);
        cbPeriod.Items.AddRange(new object[] { "Hôm nay", "Chọn ngày", "Theo tuần", "Theo tháng", "Theo quý" });
        cbPeriod.Margin = new Padding(0, 0, 10, 0);
        cbPeriod.Name = "cbPeriod";
        cbPeriod.Size = new Size(140, 31);
        cbPeriod.TabIndex = 0;
        //
        // dtpDate
        //
        dtpDate.CustomFormat = "dd/MM/yyyy";
        dtpDate.Font = new Font("Segoe UI", 10F);
        dtpDate.Format = DateTimePickerFormat.Custom;
        dtpDate.Margin = new Padding(0, 0, 6, 0);
        dtpDate.Name = "dtpDate";
        dtpDate.Size = new Size(130, 30);
        dtpDate.TabIndex = 2;
        //
        // cbWeek
        //
        cbWeek.DropDownStyle = ComboBoxStyle.DropDownList;
        cbWeek.DropDownHeight = 300;
        cbWeek.Font = new Font("Segoe UI", 10F);
        cbWeek.Margin = new Padding(0, 0, 6, 0);
        cbWeek.Name = "cbWeek";
        cbWeek.Size = new Size(210, 31);
        cbWeek.TabIndex = 7;
        //
        // cbMonth
        //
        cbMonth.DropDownStyle = ComboBoxStyle.DropDownList;
        cbMonth.Font = new Font("Segoe UI", 10F);
        cbMonth.Items.AddRange(new object[] { "Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6", "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12" });
        cbMonth.Margin = new Padding(0, 0, 6, 0);
        cbMonth.Name = "cbMonth";
        cbMonth.Size = new Size(110, 31);
        cbMonth.TabIndex = 3;
        //
        // cbQuarter
        //
        cbQuarter.DropDownStyle = ComboBoxStyle.DropDownList;
        cbQuarter.Font = new Font("Segoe UI", 10F);
        cbQuarter.Items.AddRange(new object[] { "Quý I (T1–T3)", "Quý II (T4–T6)", "Quý III (T7–T9)", "Quý IV (T10–T12)" });
        cbQuarter.Margin = new Padding(0, 0, 6, 0);
        cbQuarter.Name = "cbQuarter";
        cbQuarter.Size = new Size(160, 31);
        cbQuarter.TabIndex = 4;
        //
        // nudYear
        //
        nudYear.Font = new Font("Segoe UI", 10F);
        nudYear.Margin = new Padding(0, 0, 14, 0);
        nudYear.Maximum = new decimal(new int[] { 2100, 0, 0, 0 });
        nudYear.Minimum = new decimal(new int[] { 2000, 0, 0, 0 });
        nudYear.Name = "nudYear";
        nudYear.Size = new Size(80, 30);
        nudYear.TabIndex = 5;
        nudYear.Value = new decimal(new int[] { 2026, 0, 0, 0 });
        //
        // lblRange
        //
        lblRange.AutoSize = false;
        lblRange.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        lblRange.Margin = new Padding(0);
        lblRange.Name = "lblRange";
        lblRange.Size = new Size(520, 30);
        lblRange.Text = "";
        lblRange.TextAlign = ContentAlignment.MiddleLeft;
        //
        // tlCards : 4 the chia deu chieu ngang
        //
        tlCards.ColumnCount = 4;
        tlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlCards.RowCount = 1;
        tlCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlCards.Controls.Add(cardRevenue, 0, 0);
        tlCards.Controls.Add(cardProfit, 1, 0);
        tlCards.Controls.Add(cardCost, 2, 0);
        tlCards.Controls.Add(cardOrders, 3, 0);
        tlCards.Dock = DockStyle.Fill;
        tlCards.Margin = new Padding(0, 0, 0, 12);
        tlCards.Name = "tlCards";
        tlCards.TabIndex = 2;
        //
        // 4 StatCard
        //
        cardRevenue.BackColor = Color.White;
        cardRevenue.Dock = DockStyle.Fill;
        cardRevenue.Margin = new Padding(0, 0, 12, 0);
        cardRevenue.Name = "cardRevenue";
        cardRevenue.TabIndex = 0;
        cardProfit.BackColor = Color.White;
        cardProfit.Dock = DockStyle.Fill;
        cardProfit.Margin = new Padding(0, 0, 12, 0);
        cardProfit.Name = "cardProfit";
        cardProfit.TabIndex = 1;
        cardCost.BackColor = Color.White;
        cardCost.Dock = DockStyle.Fill;
        cardCost.Margin = new Padding(0, 0, 12, 0);
        cardCost.Name = "cardCost";
        cardCost.TabIndex = 2;
        cardOrders.BackColor = Color.White;
        cardOrders.Dock = DockStyle.Fill;
        cardOrders.Margin = new Padding(0);
        cardOrders.Name = "cardOrders";
        cardOrders.TabIndex = 3;
        //
        // tabReports
        //
        tabReports.Controls.Add(tabRevenue);
        tabReports.Controls.Add(tabItems);
        tabReports.Controls.Add(tabEmployees);
        tabReports.Controls.Add(tabCampaigns);
        tabReports.Dock = DockStyle.Fill;
        tabReports.Font = new Font("Segoe UI", 10F);
        tabReports.Margin = new Padding(0);
        tabReports.Name = "tabReports";
        tabReports.SelectedIndex = 0;
        tabReports.TabIndex = 3;
        //
        // tabRevenue
        //
        tabRevenue.BackColor = Color.FromArgb(249, 249, 249);
        tabRevenue.Controls.Add(tlRevenue);
        tabRevenue.Name = "tabRevenue";
        tabRevenue.Padding = new Padding(0, 12, 0, 0);
        tabRevenue.TabIndex = 0;
        tabRevenue.Text = "Doanh thu";
        tabRevenue.UseVisualStyleBackColor = false;
        //
        // tlRevenue : trai (bieu do + hoa don) | phai (diem noi bat + top mon)
        //
        tlRevenue.ColumnCount = 2;
        tlRevenue.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        tlRevenue.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        tlRevenue.RowCount = 1;
        tlRevenue.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlRevenue.Controls.Add(tlLeft, 0, 0);
        tlRevenue.Controls.Add(tlRight, 1, 0);
        tlRevenue.Dock = DockStyle.Fill;
        tlRevenue.Name = "tlRevenue";
        tlRevenue.TabIndex = 0;
        //
        // tlLeft
        //
        tlLeft.ColumnCount = 1;
        tlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlLeft.RowCount = 2;
        tlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        tlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        tlLeft.Controls.Add(panelChartCard, 0, 0);
        tlLeft.Controls.Add(panelInvoicesCard, 0, 1);
        tlLeft.Dock = DockStyle.Fill;
        tlLeft.Margin = new Padding(0, 0, 12, 0);
        tlLeft.Name = "tlLeft";
        tlLeft.TabIndex = 0;
        //
        // panelChartCard
        //
        panelChartCard.BackColor = Color.White;
        panelChartCard.Controls.Add(chartPanel);
        panelChartCard.Controls.Add(lblChartTitle);
        panelChartCard.Dock = DockStyle.Fill;
        panelChartCard.Margin = new Padding(0, 0, 0, 12);
        panelChartCard.Name = "panelChartCard";
        panelChartCard.Padding = new Padding(16);
        panelChartCard.TabIndex = 0;
        //
        // lblChartTitle
        //
        lblChartTitle.Dock = DockStyle.Top;
        lblChartTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblChartTitle.Height = 34;
        lblChartTitle.Name = "lblChartTitle";
        lblChartTitle.Text = "Biểu đồ doanh thu";
        lblChartTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // chartPanel
        //
        chartPanel.BackColor = Color.White;
        chartPanel.Dock = DockStyle.Fill;
        chartPanel.HighlightIndex = -1;
        chartPanel.Name = "chartPanel";
        chartPanel.TabIndex = 1;
        //
        // panelInvoicesCard
        //
        panelInvoicesCard.BackColor = Color.White;
        panelInvoicesCard.Controls.Add(gridInvoices);
        panelInvoicesCard.Controls.Add(lblInvoicesTitle);
        panelInvoicesCard.Dock = DockStyle.Fill;
        panelInvoicesCard.Margin = new Padding(0);
        panelInvoicesCard.Name = "panelInvoicesCard";
        panelInvoicesCard.Padding = new Padding(16);
        panelInvoicesCard.TabIndex = 1;
        //
        // lblInvoicesTitle
        //
        lblInvoicesTitle.Dock = DockStyle.Top;
        lblInvoicesTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblInvoicesTitle.Height = 34;
        lblInvoicesTitle.Name = "lblInvoicesTitle";
        lblInvoicesTitle.Text = "Hóa đơn gần đây";
        lblInvoicesTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // gridInvoices
        //
        gridInvoices.Dock = DockStyle.Fill;
        gridInvoices.Name = "gridInvoices";
        gridInvoices.TabIndex = 0;
        //
        // tlRight : diem noi bat (co dinh) + top 5 mon (gian het phan con lai)
        //
        tlRight.ColumnCount = 1;
        tlRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlRight.RowCount = 2;
        tlRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
        tlRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlRight.Controls.Add(pnlInsightsCard, 0, 0);
        tlRight.Controls.Add(pnlTop5Card, 0, 1);
        tlRight.Dock = DockStyle.Fill;
        tlRight.Margin = new Padding(0);
        tlRight.Name = "tlRight";
        tlRight.TabIndex = 1;
        //
        // pnlInsightsCard
        //
        pnlInsightsCard.BackColor = Color.White;
        pnlInsightsCard.Controls.Add(tlInsights);
        pnlInsightsCard.Controls.Add(lblInsightsTitle);
        pnlInsightsCard.Dock = DockStyle.Fill;
        pnlInsightsCard.Margin = new Padding(0, 0, 0, 12);
        pnlInsightsCard.Name = "pnlInsightsCard";
        pnlInsightsCard.Padding = new Padding(16);
        pnlInsightsCard.TabIndex = 0;
        //
        // lblInsightsTitle
        //
        lblInsightsTitle.Dock = DockStyle.Top;
        lblInsightsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblInsightsTitle.Height = 34;
        lblInsightsTitle.Name = "lblInsightsTitle";
        lblInsightsTitle.Text = "Điểm nổi bật";
        lblInsightsTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // tlInsights (cac dong duoc dung trong code - BuildInsightRows)
        //
        tlInsights.ColumnCount = 1;
        tlInsights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlInsights.Dock = DockStyle.Fill;
        tlInsights.Name = "tlInsights";
        tlInsights.TabIndex = 1;
        //
        // pnlTop5Card
        //
        pnlTop5Card.BackColor = Color.White;
        pnlTop5Card.Controls.Add(gridTop5);
        pnlTop5Card.Controls.Add(lblTop5Title);
        pnlTop5Card.Dock = DockStyle.Fill;
        pnlTop5Card.Margin = new Padding(0);
        pnlTop5Card.Name = "pnlTop5Card";
        pnlTop5Card.Padding = new Padding(16);
        pnlTop5Card.TabIndex = 1;
        //
        // lblTop5Title
        //
        lblTop5Title.Dock = DockStyle.Top;
        lblTop5Title.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTop5Title.Height = 34;
        lblTop5Title.Name = "lblTop5Title";
        lblTop5Title.Text = "Top 5 món bán chạy";
        lblTop5Title.TextAlign = ContentAlignment.MiddleLeft;
        //
        // gridTop5
        //
        gridTop5.Dock = DockStyle.Fill;
        gridTop5.Name = "gridTop5";
        gridTop5.TabIndex = 0;
        //
        // tabItems
        //
        tabItems.BackColor = Color.FromArgb(249, 249, 249);
        tabItems.Controls.Add(gridTopItems);
        tabItems.Name = "tabItems";
        tabItems.Padding = new Padding(0, 12, 0, 0);
        tabItems.TabIndex = 1;
        tabItems.Text = "Món ăn bán chạy";
        tabItems.UseVisualStyleBackColor = false;
        gridTopItems.Dock = DockStyle.Fill;
        gridTopItems.Name = "gridTopItems";
        gridTopItems.TabIndex = 0;
        //
        // tabEmployees
        //
        tabEmployees.BackColor = Color.FromArgb(249, 249, 249);
        tabEmployees.Controls.Add(gridByEmployee);
        tabEmployees.Name = "tabEmployees";
        tabEmployees.Padding = new Padding(0, 12, 0, 0);
        tabEmployees.TabIndex = 2;
        tabEmployees.Text = "Theo nhân viên";
        tabEmployees.UseVisualStyleBackColor = false;
        gridByEmployee.Dock = DockStyle.Fill;
        gridByEmployee.Name = "gridByEmployee";
        gridByEmployee.TabIndex = 0;
        //
        // tabCampaigns
        //
        tabCampaigns.BackColor = Color.FromArgb(249, 249, 249);
        tabCampaigns.Controls.Add(gridCampaigns);
        tabCampaigns.Name = "tabCampaigns";
        tabCampaigns.Padding = new Padding(0, 12, 0, 0);
        tabCampaigns.TabIndex = 3;
        tabCampaigns.Text = "Khuyến mãi";
        tabCampaigns.UseVisualStyleBackColor = false;
        gridCampaigns.Dock = DockStyle.Fill;
        gridCampaigns.Name = "gridCampaigns";
        gridCampaigns.TabIndex = 0;
        //
        // RevenueControl
        //
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tlRoot);
        Name = "RevenueControl";
        Padding = new Padding(24, 16, 24, 16);
        Size = new Size(1438, 860);

        tlRoot.ResumeLayout(false);
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        pnlFilter.ResumeLayout(false);
        flowFilter.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)nudYear).EndInit();
        tlCards.ResumeLayout(false);
        tabReports.ResumeLayout(false);
        tabRevenue.ResumeLayout(false);
        tlRevenue.ResumeLayout(false);
        tlLeft.ResumeLayout(false);
        panelChartCard.ResumeLayout(false);
        panelInvoicesCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridInvoices).EndInit();
        tlRight.ResumeLayout(false);
        pnlInsightsCard.ResumeLayout(false);
        pnlTop5Card.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridTop5).EndInit();
        tabItems.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridTopItems).EndInit();
        tabEmployees.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridByEmployee).EndInit();
        tabCampaigns.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridCampaigns).EndInit();
        ResumeLayout(false);
    }
}