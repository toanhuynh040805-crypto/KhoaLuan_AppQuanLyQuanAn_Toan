using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class OrderManagementControl
{
    private System.ComponentModel.IContainer components = null;

    // ---- Khung chính ----
    private TableLayoutPanel tlpRoot;
    private Panel panelTitles;
    private Label lblTitle;
    private Label lblSubtitle;

    // ---- Thẻ thống kê ----
    private TableLayoutPanel tlpStats;
    private StatCard cardTotal;
    private StatCard cardOpen;
    private StatCard cardPaid;
    private StatCard cardRevenue;

    // ---- Thanh lọc ----
    private TableLayoutPanel tlpToolbar;
    private FlowLayoutPanel flowFilters;
    private Label lblFilterDate;
    private DateTimePicker dtpFilterDate;
    private Label lblFilterStatus;
    private ComboBox cbFilterStatus;
    private Button btnToday;
    private FlowLayoutPanel flowToolbarRight;
    private Label lblCount;
    private Button btnRefresh;

    // ---- Thân trang: danh sách (trái) + chi tiết (phải) ----
    private TableLayoutPanel tlpBody;
    private Panel panelListCard;
    private Panel panelListHeader;
    private Panel panelOrderScroll;
    private Label lblEmptyList;

    private Panel panelDetail;
    private Panel panelDetailEmpty;
    private Label lblDetailEmpty;
    private Panel panelDetailContent;
    private Panel panelItems;
    private Panel panelItemsHeader;
    private Label lblItemsCaption;
    private Panel panelDetailHead;
    private Label lblDetailTitle;
    private Panel pillDetailStatus;
    private TableLayoutPanel tlpInfo;
    private Label lblInfoPlaceCaption;
    private Label lblInfoPlace;
    private Label lblInfoStaffCaption;
    private Label lblInfoStaff;
    private Label lblInfoTimeCaption;
    private Label lblInfoTime;
    private Label lblInfoSourceCaption;
    private Label lblInfoSource;
    private Panel panelDetailFooter;
    private Label lblTotalCaption;
    private Label lblTotalValue;
    private Button btnCancelOrder;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        tlpRoot = new TableLayoutPanel();
        panelTitles = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        tlpStats = new TableLayoutPanel();
        cardTotal = new StatCard();
        cardOpen = new StatCard();
        cardPaid = new StatCard();
        cardRevenue = new StatCard();
        tlpToolbar = new TableLayoutPanel();
        flowFilters = new FlowLayoutPanel();
        lblFilterDate = new Label();
        dtpFilterDate = new DateTimePicker();
        lblFilterStatus = new Label();
        cbFilterStatus = new ComboBox();
        btnToday = new Button();
        flowToolbarRight = new FlowLayoutPanel();
        lblCount = new Label();
        btnRefresh = new Button();
        tlpBody = new TableLayoutPanel();
        panelListCard = new Panel();
        panelListHeader = new Panel();
        panelOrderScroll = new Panel();
        lblEmptyList = new Label();
        panelDetail = new Panel();
        panelDetailEmpty = new Panel();
        lblDetailEmpty = new Label();
        panelDetailContent = new Panel();
        panelItems = new Panel();
        panelItemsHeader = new Panel();
        lblItemsCaption = new Label();
        panelDetailHead = new Panel();
        lblDetailTitle = new Label();
        pillDetailStatus = new Panel();
        tlpInfo = new TableLayoutPanel();
        lblInfoPlaceCaption = new Label();
        lblInfoPlace = new Label();
        lblInfoStaffCaption = new Label();
        lblInfoStaff = new Label();
        lblInfoTimeCaption = new Label();
        lblInfoTime = new Label();
        lblInfoSourceCaption = new Label();
        lblInfoSource = new Label();
        panelDetailFooter = new Panel();
        lblTotalCaption = new Label();
        lblTotalValue = new Label();
        btnCancelOrder = new Button();

        tlpRoot.SuspendLayout();
        panelTitles.SuspendLayout();
        tlpStats.SuspendLayout();
        tlpToolbar.SuspendLayout();
        flowFilters.SuspendLayout();
        flowToolbarRight.SuspendLayout();
        tlpBody.SuspendLayout();
        panelListCard.SuspendLayout();
        panelDetail.SuspendLayout();
        panelDetailEmpty.SuspendLayout();
        panelDetailContent.SuspendLayout();
        panelDetailHead.SuspendLayout();
        tlpInfo.SuspendLayout();
        panelDetailFooter.SuspendLayout();
        SuspendLayout();
        //
        // tlpRoot : 4 hàng = tiêu đề / thẻ thống kê / thanh lọc / thân trang (chiếm phần còn lại)
        //
        tlpRoot.ColumnCount = 1;
        tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpRoot.Controls.Add(panelTitles, 0, 0);
        tlpRoot.Controls.Add(tlpStats, 0, 1);
        tlpRoot.Controls.Add(tlpToolbar, 0, 2);
        tlpRoot.Controls.Add(tlpBody, 0, 3);
        tlpRoot.Dock = DockStyle.Fill;
        tlpRoot.Margin = new Padding(0);
        tlpRoot.Name = "tlpRoot";
        tlpRoot.RowCount = 4;
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpRoot.TabIndex = 0;
        //
        // panelTitles
        //
        panelTitles.Controls.Add(lblSubtitle);
        panelTitles.Controls.Add(lblTitle);
        panelTitles.Dock = DockStyle.Fill;
        panelTitles.Margin = new Padding(0);
        panelTitles.Name = "panelTitles";
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.ForeColor = AppColors.OnSurface;
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Text = "Đơn hàng";
        //
        // lblSubtitle
        //
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        lblSubtitle.Location = new Point(3, 46);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Text = "Theo dõi đơn hàng theo ngày, xem chi tiết món và xử lý hủy đơn / hủy món.";
        //
        // tlpStats : 4 thẻ chia đều chiều ngang
        //
        tlpStats.ColumnCount = 4;
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpStats.Controls.Add(cardTotal, 0, 0);
        tlpStats.Controls.Add(cardOpen, 1, 0);
        tlpStats.Controls.Add(cardPaid, 2, 0);
        tlpStats.Controls.Add(cardRevenue, 3, 0);
        tlpStats.Dock = DockStyle.Fill;
        tlpStats.Margin = new Padding(0, 0, 0, 16);
        tlpStats.Name = "tlpStats";
        tlpStats.RowCount = 1;
        tlpStats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        //
        // 4 thẻ thống kê
        //
        cardTotal.Dock = DockStyle.Fill;
        cardTotal.Margin = new Padding(0, 0, 12, 0);
        cardTotal.Name = "cardTotal";
        cardOpen.Dock = DockStyle.Fill;
        cardOpen.Margin = new Padding(0, 0, 12, 0);
        cardOpen.Name = "cardOpen";
        cardPaid.Dock = DockStyle.Fill;
        cardPaid.Margin = new Padding(0, 0, 12, 0);
        cardPaid.Name = "cardPaid";
        cardRevenue.Dock = DockStyle.Fill;
        cardRevenue.Margin = new Padding(0);
        cardRevenue.Name = "cardRevenue";
        //
        // tlpToolbar : bên trái = bộ lọc, bên phải = số lượng + làm mới
        //
        tlpToolbar.ColumnCount = 2;
        tlpToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlpToolbar.Controls.Add(flowFilters, 0, 0);
        tlpToolbar.Controls.Add(flowToolbarRight, 1, 0);
        tlpToolbar.Dock = DockStyle.Fill;
        tlpToolbar.Margin = new Padding(0);
        tlpToolbar.Name = "tlpToolbar";
        tlpToolbar.RowCount = 1;
        tlpToolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        //
        // flowFilters (Anchor = Left => các control tự canh giữa theo chiều dọc)
        //
        flowFilters.Controls.Add(lblFilterDate);
        flowFilters.Controls.Add(dtpFilterDate);
        flowFilters.Controls.Add(lblFilterStatus);
        flowFilters.Controls.Add(cbFilterStatus);
        flowFilters.Controls.Add(btnToday);
        flowFilters.Dock = DockStyle.Fill;
        flowFilters.FlowDirection = FlowDirection.LeftToRight;
        flowFilters.Margin = new Padding(0);
        flowFilters.Name = "flowFilters";
        flowFilters.WrapContents = false;
        //
        // lblFilterDate
        //
        lblFilterDate.Anchor = AnchorStyles.Left;
        lblFilterDate.AutoSize = true;
        lblFilterDate.Font = new Font("Segoe UI", 10F);
        lblFilterDate.ForeColor = AppColors.OnSurfaceVariant;
        lblFilterDate.Margin = new Padding(0, 0, 8, 0);
        lblFilterDate.Name = "lblFilterDate";
        lblFilterDate.Text = "Ngày:";
        //
        // dtpFilterDate
        //
        dtpFilterDate.Anchor = AnchorStyles.Left;
        dtpFilterDate.Font = new Font("Segoe UI", 10F);
        dtpFilterDate.Format = DateTimePickerFormat.Short;
        dtpFilterDate.Margin = new Padding(0, 0, 24, 0);
        dtpFilterDate.Name = "dtpFilterDate";
        dtpFilterDate.Size = new Size(140, 25);
        //
        // lblFilterStatus
        //
        lblFilterStatus.Anchor = AnchorStyles.Left;
        lblFilterStatus.AutoSize = true;
        lblFilterStatus.Font = new Font("Segoe UI", 10F);
        lblFilterStatus.ForeColor = AppColors.OnSurfaceVariant;
        lblFilterStatus.Margin = new Padding(0, 0, 8, 0);
        lblFilterStatus.Name = "lblFilterStatus";
        lblFilterStatus.Text = "Trạng thái:";
        //
        // cbFilterStatus
        //
        cbFilterStatus.Anchor = AnchorStyles.Left;
        cbFilterStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        cbFilterStatus.Font = new Font("Segoe UI", 10F);
        cbFilterStatus.Margin = new Padding(0, 0, 24, 0);
        cbFilterStatus.Name = "cbFilterStatus";
        cbFilterStatus.Size = new Size(190, 25);
        //
        // btnToday
        //
        btnToday.Anchor = AnchorStyles.Left;
        btnToday.BackColor = Color.White;
        btnToday.Cursor = Cursors.Hand;
        btnToday.FlatAppearance.BorderColor = Color.FromArgb(208, 208, 208);
        btnToday.FlatStyle = FlatStyle.Flat;
        btnToday.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnToday.ForeColor = AppColors.OnSurface;
        btnToday.Margin = new Padding(0);
        btnToday.Name = "btnToday";
        btnToday.Size = new Size(96, 34);
        btnToday.Text = "Hôm nay";
        btnToday.UseVisualStyleBackColor = false;
        //
        // flowToolbarRight
        //
        flowToolbarRight.AutoSize = true;
        flowToolbarRight.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowToolbarRight.Controls.Add(lblCount);
        flowToolbarRight.Controls.Add(btnRefresh);
        flowToolbarRight.Dock = DockStyle.Fill;
        flowToolbarRight.FlowDirection = FlowDirection.LeftToRight;
        flowToolbarRight.Margin = new Padding(0);
        flowToolbarRight.Name = "flowToolbarRight";
        flowToolbarRight.WrapContents = false;
        //
        // lblCount
        //
        lblCount.Anchor = AnchorStyles.Left;
        lblCount.AutoSize = true;
        lblCount.Font = new Font("Segoe UI", 10F);
        lblCount.ForeColor = AppColors.OnSurfaceVariant;
        lblCount.Margin = new Padding(0, 0, 16, 0);
        lblCount.Name = "lblCount";
        lblCount.Text = "0 đơn hàng";
        //
        // btnRefresh
        //
        btnRefresh.Anchor = AnchorStyles.Left;
        btnRefresh.BackColor = Color.White;
        btnRefresh.Cursor = Cursors.Hand;
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(208, 208, 208);
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnRefresh.ForeColor = AppColors.OnSurface;
        btnRefresh.Margin = new Padding(0);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(110, 34);
        btnRefresh.Text = "↻ Làm mới";
        btnRefresh.UseVisualStyleBackColor = false;
        //
        // tlpBody : cột trái co giãn theo cửa sổ, cột phải (chi tiết) rộng cố định
        //
        tlpBody.ColumnCount = 2;
        tlpBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 436F));
        tlpBody.Controls.Add(panelListCard, 0, 0);
        tlpBody.Controls.Add(panelDetail, 1, 0);
        tlpBody.Dock = DockStyle.Fill;
        tlpBody.Margin = new Padding(0);
        tlpBody.Name = "tlpBody";
        tlpBody.RowCount = 1;
        tlpBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        //
        // panelListCard : khung trắng viền mảnh chứa tiêu đề cột + danh sách đơn
        //
        panelListCard.BackColor = Color.White;
        panelListCard.Controls.Add(lblEmptyList);
        panelListCard.Controls.Add(panelOrderScroll);
        panelListCard.Controls.Add(panelListHeader);
        panelListCard.Dock = DockStyle.Fill;
        panelListCard.Margin = new Padding(0);
        panelListCard.Name = "panelListCard";
        panelListCard.Padding = new Padding(1);
        //
        // panelListHeader : hàng tiêu đề cột (tự vẽ trong code)
        //
        panelListHeader.BackColor = Color.FromArgb(246, 246, 246);
        panelListHeader.Dock = DockStyle.Top;
        panelListHeader.Name = "panelListHeader";
        panelListHeader.Size = new Size(100, 40);
        //
        // panelOrderScroll : vùng cuộn chứa các dòng đơn
        //
        panelOrderScroll.AutoScroll = true;
        panelOrderScroll.BackColor = Color.White;
        panelOrderScroll.Dock = DockStyle.Fill;
        panelOrderScroll.Name = "panelOrderScroll";
        //
        // lblEmptyList
        //
        lblEmptyList.BackColor = Color.White;
        lblEmptyList.Dock = DockStyle.Fill;
        lblEmptyList.Font = new Font("Segoe UI", 11F);
        lblEmptyList.ForeColor = AppColors.OnSurfaceVariant;
        lblEmptyList.Name = "lblEmptyList";
        lblEmptyList.Text = "Không có đơn hàng nào.";
        lblEmptyList.TextAlign = ContentAlignment.MiddleCenter;
        lblEmptyList.Visible = false;
        //
        // panelDetail : khung chi tiết đơn (luôn hiện, chưa chọn thì hiện hướng dẫn)
        //
        panelDetail.BackColor = Color.White;
        panelDetail.Controls.Add(panelDetailContent);
        panelDetail.Controls.Add(panelDetailEmpty);
        panelDetail.Dock = DockStyle.Fill;
        panelDetail.Margin = new Padding(16, 0, 0, 0);
        panelDetail.Name = "panelDetail";
        panelDetail.Padding = new Padding(1);
        //
        // panelDetailEmpty
        //
        panelDetailEmpty.BackColor = Color.White;
        panelDetailEmpty.Controls.Add(lblDetailEmpty);
        panelDetailEmpty.Dock = DockStyle.Fill;
        panelDetailEmpty.Name = "panelDetailEmpty";
        //
        // lblDetailEmpty
        //
        lblDetailEmpty.Dock = DockStyle.Fill;
        lblDetailEmpty.Font = new Font("Segoe UI", 11F);
        lblDetailEmpty.ForeColor = AppColors.OnSurfaceVariant;
        lblDetailEmpty.Name = "lblDetailEmpty";
        lblDetailEmpty.Text = "Chọn một đơn hàng ở danh sách bên trái\r\nđể xem chi tiết món và thao tác.";
        lblDetailEmpty.TextAlign = ContentAlignment.MiddleCenter;
        //
        // panelDetailContent : thứ tự Add = Fill trước, rồi các vùng Top/Bottom
        //
        panelDetailContent.BackColor = Color.White;
        panelDetailContent.Controls.Add(panelItems);
        panelDetailContent.Controls.Add(panelItemsHeader);
        panelDetailContent.Controls.Add(panelDetailHead);
        panelDetailContent.Controls.Add(panelDetailFooter);
        panelDetailContent.Dock = DockStyle.Fill;
        panelDetailContent.Name = "panelDetailContent";
        panelDetailContent.Padding = new Padding(20, 18, 20, 14);
        panelDetailContent.Visible = false;
        //
        // panelItems : danh sách món (cuộn)
        //
        panelItems.AutoScroll = true;
        panelItems.BackColor = Color.White;
        panelItems.Dock = DockStyle.Fill;
        panelItems.Name = "panelItems";
        //
        // panelItemsHeader
        //
        panelItemsHeader.BackColor = Color.White;
        panelItemsHeader.Controls.Add(lblItemsCaption);
        panelItemsHeader.Dock = DockStyle.Top;
        panelItemsHeader.Name = "panelItemsHeader";
        panelItemsHeader.Size = new Size(100, 34);
        //
        // lblItemsCaption
        //
        lblItemsCaption.Dock = DockStyle.Fill;
        lblItemsCaption.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        lblItemsCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblItemsCaption.Name = "lblItemsCaption";
        lblItemsCaption.Text = "MÓN ĐÃ GỌI";
        lblItemsCaption.TextAlign = ContentAlignment.MiddleLeft;
        //
        // panelDetailHead
        //
        panelDetailHead.BackColor = Color.White;
        panelDetailHead.Controls.Add(lblDetailTitle);
        panelDetailHead.Controls.Add(pillDetailStatus);
        panelDetailHead.Controls.Add(tlpInfo);
        panelDetailHead.Dock = DockStyle.Top;
        panelDetailHead.Name = "panelDetailHead";
        panelDetailHead.Size = new Size(394, 164);
        //
        // lblDetailTitle
        //
        lblDetailTitle.AutoSize = true;
        lblDetailTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblDetailTitle.ForeColor = AppColors.OnSurface;
        lblDetailTitle.Location = new Point(0, 0);
        lblDetailTitle.Name = "lblDetailTitle";
        lblDetailTitle.Text = "Đơn #0";
        //
        // pillDetailStatus : nhãn trạng thái bo tròn (tự vẽ trong code)
        //
        pillDetailStatus.BackColor = Color.White;
        pillDetailStatus.Location = new Point(120, 4);
        pillDetailStatus.Name = "pillDetailStatus";
        pillDetailStatus.Size = new Size(100, 24);
        //
        // tlpInfo : 4 dòng thông tin (nhãn | giá trị)
        //
        tlpInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        tlpInfo.ColumnCount = 2;
        tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpInfo.Controls.Add(lblInfoPlaceCaption, 0, 0);
        tlpInfo.Controls.Add(lblInfoPlace, 1, 0);
        tlpInfo.Controls.Add(lblInfoStaffCaption, 0, 1);
        tlpInfo.Controls.Add(lblInfoStaff, 1, 1);
        tlpInfo.Controls.Add(lblInfoTimeCaption, 0, 2);
        tlpInfo.Controls.Add(lblInfoTime, 1, 2);
        tlpInfo.Controls.Add(lblInfoSourceCaption, 0, 3);
        tlpInfo.Controls.Add(lblInfoSource, 1, 3);
        tlpInfo.Location = new Point(0, 46);
        tlpInfo.Margin = new Padding(0);
        tlpInfo.Name = "tlpInfo";
        tlpInfo.RowCount = 4;
        tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tlpInfo.Size = new Size(394, 104);
        //
        // các nhãn thông tin
        //
        lblInfoPlaceCaption.Dock = DockStyle.Fill;
        lblInfoPlaceCaption.Font = new Font("Segoe UI", 10F);
        lblInfoPlaceCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblInfoPlaceCaption.Margin = new Padding(0);
        lblInfoPlaceCaption.Name = "lblInfoPlaceCaption";
        lblInfoPlaceCaption.Text = "Vị trí";
        lblInfoPlaceCaption.TextAlign = ContentAlignment.MiddleLeft;
        lblInfoPlace.AutoEllipsis = true;
        lblInfoPlace.Dock = DockStyle.Fill;
        lblInfoPlace.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblInfoPlace.ForeColor = AppColors.OnSurface;
        lblInfoPlace.Margin = new Padding(0);
        lblInfoPlace.Name = "lblInfoPlace";
        lblInfoPlace.TextAlign = ContentAlignment.MiddleLeft;

        lblInfoStaffCaption.Dock = DockStyle.Fill;
        lblInfoStaffCaption.Font = new Font("Segoe UI", 10F);
        lblInfoStaffCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblInfoStaffCaption.Margin = new Padding(0);
        lblInfoStaffCaption.Name = "lblInfoStaffCaption";
        lblInfoStaffCaption.Text = "Nhân viên";
        lblInfoStaffCaption.TextAlign = ContentAlignment.MiddleLeft;
        lblInfoStaff.AutoEllipsis = true;
        lblInfoStaff.Dock = DockStyle.Fill;
        lblInfoStaff.Font = new Font("Segoe UI", 10F);
        lblInfoStaff.ForeColor = AppColors.OnSurface;
        lblInfoStaff.Margin = new Padding(0);
        lblInfoStaff.Name = "lblInfoStaff";
        lblInfoStaff.TextAlign = ContentAlignment.MiddleLeft;

        lblInfoTimeCaption.Dock = DockStyle.Fill;
        lblInfoTimeCaption.Font = new Font("Segoe UI", 10F);
        lblInfoTimeCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblInfoTimeCaption.Margin = new Padding(0);
        lblInfoTimeCaption.Name = "lblInfoTimeCaption";
        lblInfoTimeCaption.Text = "Thời gian";
        lblInfoTimeCaption.TextAlign = ContentAlignment.MiddleLeft;
        lblInfoTime.AutoEllipsis = true;
        lblInfoTime.Dock = DockStyle.Fill;
        lblInfoTime.Font = new Font("Segoe UI", 10F);
        lblInfoTime.ForeColor = AppColors.OnSurface;
        lblInfoTime.Margin = new Padding(0);
        lblInfoTime.Name = "lblInfoTime";
        lblInfoTime.TextAlign = ContentAlignment.MiddleLeft;

        lblInfoSourceCaption.Dock = DockStyle.Fill;
        lblInfoSourceCaption.Font = new Font("Segoe UI", 10F);
        lblInfoSourceCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblInfoSourceCaption.Margin = new Padding(0);
        lblInfoSourceCaption.Name = "lblInfoSourceCaption";
        lblInfoSourceCaption.Text = "Nguồn";
        lblInfoSourceCaption.TextAlign = ContentAlignment.MiddleLeft;
        lblInfoSource.AutoEllipsis = true;
        lblInfoSource.Dock = DockStyle.Fill;
        lblInfoSource.Font = new Font("Segoe UI", 10F);
        lblInfoSource.ForeColor = AppColors.OnSurface;
        lblInfoSource.Margin = new Padding(0);
        lblInfoSource.Name = "lblInfoSource";
        lblInfoSource.TextAlign = ContentAlignment.MiddleLeft;
        //
        // panelDetailFooter : tổng tiền + nút hủy đơn (chiều cao đổi theo việc có hiện nút hay không)
        //
        panelDetailFooter.BackColor = Color.White;
        panelDetailFooter.Controls.Add(lblTotalCaption);
        panelDetailFooter.Controls.Add(lblTotalValue);
        panelDetailFooter.Controls.Add(btnCancelOrder);
        panelDetailFooter.Dock = DockStyle.Bottom;
        panelDetailFooter.Name = "panelDetailFooter";
        panelDetailFooter.Size = new Size(394, 112);
        //
        // lblTotalCaption
        //
        lblTotalCaption.AutoSize = true;
        lblTotalCaption.Font = new Font("Segoe UI", 11F);
        lblTotalCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblTotalCaption.Location = new Point(0, 20);
        lblTotalCaption.Name = "lblTotalCaption";
        lblTotalCaption.Text = "Tổng cộng";
        //
        // lblTotalValue
        //
        lblTotalValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblTotalValue.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTotalValue.ForeColor = AppColors.Primary;
        lblTotalValue.Location = new Point(144, 12);
        lblTotalValue.Name = "lblTotalValue";
        lblTotalValue.Size = new Size(250, 36);
        lblTotalValue.Text = "0đ";
        lblTotalValue.TextAlign = ContentAlignment.MiddleRight;
        //
        // btnCancelOrder
        //
        btnCancelOrder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        btnCancelOrder.BackColor = AppColors.Error;
        btnCancelOrder.Cursor = Cursors.Hand;
        btnCancelOrder.FlatAppearance.BorderSize = 0;
        btnCancelOrder.FlatStyle = FlatStyle.Flat;
        btnCancelOrder.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnCancelOrder.ForeColor = Color.White;
        btnCancelOrder.Location = new Point(0, 62);
        btnCancelOrder.Name = "btnCancelOrder";
        btnCancelOrder.Size = new Size(394, 42);
        btnCancelOrder.Text = "Hủy toàn bộ đơn";
        btnCancelOrder.UseVisualStyleBackColor = false;
        btnCancelOrder.Visible = false;
        //
        // OrderManagementControl
        // (Padding = 0 vì MainForm.panelContent đã có lề 32/24 — trước đây lề bị cộng đôi)
        //
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tlpRoot);
        Name = "OrderManagementControl";
        Padding = new Padding(0);
        Size = new Size(1356, 875);

        tlpRoot.ResumeLayout(false);
        panelTitles.ResumeLayout(false);
        panelTitles.PerformLayout();
        tlpStats.ResumeLayout(false);
        tlpToolbar.ResumeLayout(false);
        tlpToolbar.PerformLayout();
        flowFilters.ResumeLayout(false);
        flowFilters.PerformLayout();
        flowToolbarRight.ResumeLayout(false);
        flowToolbarRight.PerformLayout();
        tlpBody.ResumeLayout(false);
        panelListCard.ResumeLayout(false);
        panelDetail.ResumeLayout(false);
        panelDetailEmpty.ResumeLayout(false);
        panelDetailContent.ResumeLayout(false);
        panelDetailHead.ResumeLayout(false);
        panelDetailHead.PerformLayout();
        tlpInfo.ResumeLayout(false);
        panelDetailFooter.ResumeLayout(false);
        panelDetailFooter.PerformLayout();
        ResumeLayout(false);
    }
}