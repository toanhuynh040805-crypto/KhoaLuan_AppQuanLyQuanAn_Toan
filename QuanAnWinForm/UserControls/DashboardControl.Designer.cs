using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class DashboardControl
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel tlpRoot;
    private Panel panelHeader;
    private Label lblTitle;
    private Label lblSubtitle;
    private TableLayoutPanel tlpStatCards;
    private StatCard cardRevenue;
    private StatCard cardOrders;
    private StatCard cardTables;
    private StatCard cardNewCustomers;
    private TableLayoutPanel tlpContent;
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
        tlpRoot = new TableLayoutPanel();
        panelHeader = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        tlpStatCards = new TableLayoutPanel();
        cardRevenue = new StatCard();
        cardOrders = new StatCard();
        cardTables = new StatCard();
        cardNewCustomers = new StatCard();
        tlpContent = new TableLayoutPanel();
        panelChartCard = new Panel();
        lblChartTitle = new Label();
        chartPanel = new MiniBarChartPanel();
        panelTopItemsCard = new Panel();
        lblTopItemsTitle = new Label();
        flowTopItems = new FlowLayoutPanel();
        tlpRoot.SuspendLayout();
        panelHeader.SuspendLayout();
        tlpStatCards.SuspendLayout();
        tlpContent.SuspendLayout();
        panelChartCard.SuspendLayout();
        panelTopItemsCard.SuspendLayout();
        SuspendLayout();
        //
        // tlpRoot : 3 hàng (tiêu đề | thẻ thống kê | biểu đồ + món bán chạy)
        //
        tlpRoot.ColumnCount = 1;
        tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpRoot.RowCount = 3;
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 156F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpRoot.Controls.Add(panelHeader, 0, 0);
        tlpRoot.Controls.Add(tlpStatCards, 0, 1);
        tlpRoot.Controls.Add(tlpContent, 0, 2);
        tlpRoot.Dock = DockStyle.Fill;
        tlpRoot.Margin = new Padding(0);
        tlpRoot.Name = "tlpRoot";
        tlpRoot.TabIndex = 0;
        //
        // panelHeader
        //
        panelHeader.Controls.Add(lblTitle);
        panelHeader.Controls.Add(lblSubtitle);
        panelHeader.Dock = DockStyle.Fill;
        panelHeader.Margin = new Padding(0);
        panelHeader.Name = "panelHeader";
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(-2, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Text = "Tổng quan hôm nay";
        //
        // lblSubtitle
        //
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(1, 50);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Text = "Cập nhật lúc 08:01, 20/09/2026";
        //
        // tlpStatCards : 4 thẻ chia đều chiều ngang
        //
        tlpStatCards.ColumnCount = 4;
        tlpStatCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStatCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStatCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStatCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStatCards.RowCount = 1;
        tlpStatCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpStatCards.Controls.Add(cardRevenue, 0, 0);
        tlpStatCards.Controls.Add(cardOrders, 1, 0);
        tlpStatCards.Controls.Add(cardTables, 2, 0);
        tlpStatCards.Controls.Add(cardNewCustomers, 3, 0);
        tlpStatCards.Dock = DockStyle.Fill;
        tlpStatCards.Margin = new Padding(0, 0, 0, 16);
        tlpStatCards.Name = "tlpStatCards";
        //
        // 4 thẻ thống kê
        //
        cardRevenue.Dock = DockStyle.Fill;
        cardRevenue.Margin = new Padding(0, 0, 16, 0);
        cardRevenue.Name = "cardRevenue";
        cardOrders.Dock = DockStyle.Fill;
        cardOrders.Margin = new Padding(0, 0, 16, 0);
        cardOrders.Name = "cardOrders";
        cardTables.Dock = DockStyle.Fill;
        cardTables.Margin = new Padding(0, 0, 16, 0);
        cardTables.Name = "cardTables";
        cardNewCustomers.Dock = DockStyle.Fill;
        cardNewCustomers.Margin = new Padding(0);
        cardNewCustomers.Name = "cardNewCustomers";
        //
        // tlpContent : biểu đồ (2/3) + món bán chạy (1/3), lấp đầy phần còn lại
        //
        tlpContent.ColumnCount = 2;
        tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        tlpContent.RowCount = 1;
        tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpContent.Controls.Add(panelChartCard, 0, 0);
        tlpContent.Controls.Add(panelTopItemsCard, 1, 0);
        tlpContent.Dock = DockStyle.Fill;
        tlpContent.Margin = new Padding(0);
        tlpContent.Name = "tlpContent";
        //
        // panelChartCard
        //
        panelChartCard.BackColor = Color.White;
        panelChartCard.Controls.Add(chartPanel);
        panelChartCard.Controls.Add(lblChartTitle);
        panelChartCard.Dock = DockStyle.Fill;
        panelChartCard.Margin = new Padding(0, 0, 16, 0);
        panelChartCard.Name = "panelChartCard";
        panelChartCard.Padding = new Padding(20, 60, 20, 20);
        //
        // lblChartTitle
        //
        lblChartTitle.AutoSize = true;
        lblChartTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblChartTitle.Location = new Point(18, 16);
        lblChartTitle.Name = "lblChartTitle";
        lblChartTitle.Text = "Biểu đồ tăng trưởng doanh thu";
        //
        // chartPanel
        //
        chartPanel.BackColor = Color.White;
        chartPanel.Dock = DockStyle.Fill;
        chartPanel.HighlightIndex = -1;
        chartPanel.Name = "chartPanel";
        //
        // panelTopItemsCard
        //
        panelTopItemsCard.BackColor = Color.White;
        panelTopItemsCard.Controls.Add(flowTopItems);
        panelTopItemsCard.Controls.Add(lblTopItemsTitle);
        panelTopItemsCard.Dock = DockStyle.Fill;
        panelTopItemsCard.Margin = new Padding(0);
        panelTopItemsCard.Name = "panelTopItemsCard";
        panelTopItemsCard.Padding = new Padding(20, 60, 20, 16);
        //
        // lblTopItemsTitle
        //
        lblTopItemsTitle.AutoSize = true;
        lblTopItemsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTopItemsTitle.Location = new Point(18, 16);
        lblTopItemsTitle.Name = "lblTopItemsTitle";
        lblTopItemsTitle.Text = "Món bán chạy nhất";
        //
        // flowTopItems : mỗi dòng luôn rộng bằng khung -> giá tiền không bị cắt
        //
        flowTopItems.AutoScroll = false;
        flowTopItems.Dock = DockStyle.Fill;
        flowTopItems.FlowDirection = FlowDirection.TopDown;
        flowTopItems.Name = "flowTopItems";
        flowTopItems.WrapContents = false;
        //
        // DashboardControl
        //
        AutoScaleMode = AutoScaleMode.None;
        AutoScroll = true;
        AutoScrollMinSize = new Size(900, 640);
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tlpRoot);
        Name = "DashboardControl";
        Padding = new Padding(24, 20, 24, 24);
        Size = new Size(1356, 875);
        tlpRoot.ResumeLayout(false);
        panelHeader.ResumeLayout(false);
        panelHeader.PerformLayout();
        tlpStatCards.ResumeLayout(false);
        tlpContent.ResumeLayout(false);
        panelChartCard.ResumeLayout(false);
        panelChartCard.PerformLayout();
        panelTopItemsCard.ResumeLayout(false);
        panelTopItemsCard.PerformLayout();
        ResumeLayout(false);
    }

    private void BuildTopItemRow(FlowLayoutPanel parent, string name, string sold, string revenue, string? imageUrl)
    {
        const int thumbSize = 48;
        const int textLeft = thumbSize + 12;

        var row = new Panel
        {
            Size = new Size(Math.Max(200, parent.ClientSize.Width), 64),
            Margin = new Padding(0, 0, 0, 6),
        };
        var thumb = new Panel { Size = new Size(thumbSize, thumbSize), Location = new Point(0, 8), BackColor = AppColors.SurfaceContainer };
        var lblPlaceholder = new Label
        {
            Text = "🍽",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 14),
            ForeColor = AppColors.Outline,
        };
        thumb.Controls.Add(lblPlaceholder);
        thumb.Disposed += (s, e) => thumb.BackgroundImage?.Dispose();
        var lblName = new Label
        {
            Text = name,
            Font = AppFonts.LabelLg,
            AutoSize = false,
            AutoEllipsis = true,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var lblSold = new Label
        {
            Text = sold,
            Font = AppFonts.BodyMd,
            ForeColor = AppColors.OnSurfaceVariant,
            AutoSize = true,
            Location = new Point(textLeft, 33),
        };
        var lblRevenue = new Label
        {
            Text = revenue,
            Font = AppFonts.LabelLg,
            ForeColor = AppColors.Primary,
            AutoSize = true,
        };

        void LayoutRow()
        {
            // Giá tiền canh sát mép phải; tên món chiếm phần còn lại và tự cắt "..." nếu quá dài.
            lblRevenue.Location = new Point(row.Width - lblRevenue.PreferredWidth, 20);
            int nameWidth = Math.Max(40, lblRevenue.Left - textLeft - 8);
            lblName.SetBounds(textLeft, 10, nameWidth, 22);
        }

        row.Controls.Add(thumb);
        row.Controls.Add(lblName);
        row.Controls.Add(lblSold);
        row.Controls.Add(lblRevenue);
        row.Resize += (s, e) => LayoutRow();
        LayoutRow();

        parent.Controls.Add(row);

        if (!string.IsNullOrWhiteSpace(imageUrl))
            _ = LoadTopItemThumbAsync(thumb, lblPlaceholder, imageUrl);
    }
}