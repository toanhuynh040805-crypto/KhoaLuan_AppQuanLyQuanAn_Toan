using QuanAnWinForm.Controls;

namespace QuanAnWinForm.UserControls;

partial class PromotionsControl
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel tlRoot;
    private TableLayoutPanel tlHeader;
    private FlowLayoutPanel flowTitles;
    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddCampaign;

    private TableLayoutPanel tlStats;
    private StatCard cardTotal;
    private StatCard cardRunning;
    private StatCard cardUpcoming;
    private StatCard cardEnded;

    private FlowLayoutPanel flowFilter;
    private Button btnFilterAll;
    private Button btnFilterRunning;
    private Button btnFilterUpcoming;
    private Button btnFilterEnded;

    private FlowLayoutPanel flowCampaigns;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        tlRoot = new TableLayoutPanel();
        tlHeader = new TableLayoutPanel();
        flowTitles = new FlowLayoutPanel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnAddCampaign = new Button();
        tlStats = new TableLayoutPanel();
        cardTotal = new StatCard();
        cardRunning = new StatCard();
        cardUpcoming = new StatCard();
        cardEnded = new StatCard();
        flowFilter = new FlowLayoutPanel();
        btnFilterAll = new Button();
        btnFilterRunning = new Button();
        btnFilterUpcoming = new Button();
        btnFilterEnded = new Button();
        flowCampaigns = new FlowLayoutPanel();

        tlRoot.SuspendLayout();
        tlHeader.SuspendLayout();
        flowTitles.SuspendLayout();
        tlStats.SuspendLayout();
        flowFilter.SuspendLayout();
        SuspendLayout();

        // tlRoot: tieu de / the thong ke / bo loc / danh sach
        tlRoot.ColumnCount = 1;
        tlRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlRoot.RowCount = 4;
        tlRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlRoot.Controls.Add(tlHeader, 0, 0);
        tlRoot.Controls.Add(tlStats, 0, 1);
        tlRoot.Controls.Add(flowFilter, 0, 2);
        tlRoot.Controls.Add(flowCampaigns, 0, 3);
        tlRoot.Dock = DockStyle.Fill;
        tlRoot.Name = "tlRoot";
        tlRoot.TabIndex = 0;

        // Tieu de + nut them
        tlHeader.AutoSize = true;
        tlHeader.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tlHeader.ColumnCount = 2;
        tlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlHeader.RowCount = 1;
        tlHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlHeader.Controls.Add(flowTitles, 0, 0);
        tlHeader.Controls.Add(btnAddCampaign, 1, 0);
        tlHeader.Dock = DockStyle.Fill;
        tlHeader.Margin = new Padding(0, 0, 0, 16);
        tlHeader.Name = "tlHeader";
        tlHeader.TabIndex = 0;

        flowTitles.AutoSize = true;
        flowTitles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowTitles.Controls.Add(lblTitle);
        flowTitles.Controls.Add(lblSubtitle);
        flowTitles.FlowDirection = FlowDirection.TopDown;
        flowTitles.Margin = new Padding(0);
        flowTitles.Name = "flowTitles";
        flowTitles.TabIndex = 0;
        flowTitles.WrapContents = false;

        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblTitle.Margin = new Padding(0);
        lblTitle.Name = "lblTitle";
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Khuyến mãi";

        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Margin = new Padding(3, 0, 0, 0);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Tạo và quản lý các chiến dịch giảm giá theo % cho hóa đơn";

        btnAddCampaign.Anchor = AnchorStyles.Right;
        btnAddCampaign.AutoSize = true;
        btnAddCampaign.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnAddCampaign.BackColor = Color.FromArgb(172, 45, 0);
        btnAddCampaign.Cursor = Cursors.Hand;
        btnAddCampaign.FlatAppearance.BorderSize = 0;
        btnAddCampaign.FlatStyle = FlatStyle.Flat;
        btnAddCampaign.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddCampaign.ForeColor = Color.White;
        btnAddCampaign.Margin = new Padding(0);
        btnAddCampaign.Name = "btnAddCampaign";
        btnAddCampaign.Padding = new Padding(14, 6, 14, 6);
        btnAddCampaign.TabIndex = 1;
        btnAddCampaign.Text = "+  Thêm chiến dịch";
        btnAddCampaign.UseVisualStyleBackColor = false;

        // 4 the thong ke chia deu chieu ngang
        tlStats.ColumnCount = 4;
        tlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlStats.RowCount = 1;
        tlStats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlStats.Controls.Add(cardTotal, 0, 0);
        tlStats.Controls.Add(cardRunning, 1, 0);
        tlStats.Controls.Add(cardUpcoming, 2, 0);
        tlStats.Controls.Add(cardEnded, 3, 0);
        tlStats.Dock = DockStyle.Fill;
        tlStats.Margin = new Padding(0);
        tlStats.Name = "tlStats";
        tlStats.TabIndex = 1;

        cardTotal.BackColor = Color.White;
        cardTotal.Dock = DockStyle.Fill;
        cardTotal.Margin = new Padding(0, 0, 12, 12);
        cardTotal.Name = "cardTotal";
        cardTotal.TabIndex = 0;
        cardRunning.BackColor = Color.White;
        cardRunning.Dock = DockStyle.Fill;
        cardRunning.Margin = new Padding(0, 0, 12, 12);
        cardRunning.Name = "cardRunning";
        cardRunning.TabIndex = 1;
        cardUpcoming.BackColor = Color.White;
        cardUpcoming.Dock = DockStyle.Fill;
        cardUpcoming.Margin = new Padding(0, 0, 12, 12);
        cardUpcoming.Name = "cardUpcoming";
        cardUpcoming.TabIndex = 2;
        cardEnded.BackColor = Color.White;
        cardEnded.Dock = DockStyle.Fill;
        cardEnded.Margin = new Padding(0, 0, 0, 12);
        cardEnded.Name = "cardEnded";
        cardEnded.TabIndex = 3;

        // Bo loc nhanh (chip)
        flowFilter.AutoSize = true;
        flowFilter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowFilter.Controls.Add(btnFilterAll);
        flowFilter.Controls.Add(btnFilterRunning);
        flowFilter.Controls.Add(btnFilterUpcoming);
        flowFilter.Controls.Add(btnFilterEnded);
        flowFilter.Dock = DockStyle.Fill;
        flowFilter.Margin = new Padding(0, 4, 0, 14);
        flowFilter.Name = "flowFilter";
        flowFilter.TabIndex = 2;
        flowFilter.WrapContents = false;

        ConfigureChip(btnFilterAll, "btnFilterAll", "Tất cả", 0);
        ConfigureChip(btnFilterRunning, "btnFilterRunning", "Đang diễn ra", 1);
        ConfigureChip(btnFilterUpcoming, "btnFilterUpcoming", "Sắp diễn ra", 2);
        ConfigureChip(btnFilterEnded, "btnFilterEnded", "Đã kết thúc", 3);

        // Danh sach chien dich
        flowCampaigns.AutoScroll = true;
        flowCampaigns.Dock = DockStyle.Fill;
        flowCampaigns.FlowDirection = FlowDirection.TopDown;
        flowCampaigns.Margin = new Padding(0);
        flowCampaigns.Name = "flowCampaigns";
        flowCampaigns.TabIndex = 3;
        flowCampaigns.WrapContents = false;

        // PromotionsControl
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tlRoot);
        Name = "PromotionsControl";
        Padding = new Padding(28, 20, 28, 20);
        Size = new Size(1371, 1000);

        tlRoot.ResumeLayout(false);
        tlRoot.PerformLayout();
        tlHeader.ResumeLayout(false);
        tlHeader.PerformLayout();
        flowTitles.ResumeLayout(false);
        flowTitles.PerformLayout();
        tlStats.ResumeLayout(false);
        flowFilter.ResumeLayout(false);
        flowFilter.PerformLayout();
        ResumeLayout(false);
    }

    private static void ConfigureChip(Button chip, string name, string text, int tabIndex)
    {
        chip.AutoSize = true;
        chip.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        chip.BackColor = Color.White;
        chip.Cursor = Cursors.Hand;
        chip.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
        chip.FlatStyle = FlatStyle.Flat;
        chip.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        chip.ForeColor = Color.FromArgb(91, 65, 57);
        chip.Margin = new Padding(0, 0, 8, 0);
        chip.Name = name;
        chip.Padding = new Padding(12, 4, 12, 4);
        chip.TabIndex = tabIndex;
        chip.Text = text;
        chip.UseVisualStyleBackColor = false;
    }
}