using QuanAnWinForm.Controls;

namespace QuanAnWinForm.UserControls;

partial class StaffControl
{
    private System.ComponentModel.IContainer components = null;

    private TabControl tabsMain;
    private TabPage tabEmployees;
    private TabPage tabShifts;
    private TabPage tabTasks;
    private TabPage tabHistory;

    // -- Tab Nhan vien --
    private TableLayoutPanel tlEmployees;
    private TableLayoutPanel tlEmpHeader;
    private FlowLayoutPanel flowEmpTitles;
    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private FlowLayoutPanel flowStats;
    private StatCard cardTotalStaff;
    private StatCard cardWorking;
    private FlowLayoutPanel flowStaffCards;

    // -- Tab Ca lam: cot trai = dinh nghia ca, cot phai = danh sach nhan vien theo ngay --
    private TableLayoutPanel tlShifts;
    private TableLayoutPanel tlShiftLeft;
    private Label lblShiftsTitle;
    private Button btnAddShift;
    private FlowLayoutPanel flowShiftDefs;
    private TableLayoutPanel tlShiftRight;
    private TableLayoutPanel tlRosterHeader;
    private Label lblRosterTitle;
    private FlowLayoutPanel flowAssignDate;
    private Label lblAssignDate;
    private DateTimePicker dtpAssignDate;
    private Button btnAddAssignment;
    private FlowLayoutPanel flowAssignments;

    // -- Tab Nhiem vu --
    private TableLayoutPanel tlTasks;
    private TableLayoutPanel tlTasksHeader;
    private Label lblTasksTitle;
    private Button btnAddTask;
    private FlowLayoutPanel flowTaskFilter;
    private Label lblTaskFilter;
    private ComboBox cbTaskFilter;
    private FlowLayoutPanel flowTasks;

    // -- Tab Lich su ca lam --
    private TableLayoutPanel tlHistory;
    private Label lblHistoryTitle;
    private FlowLayoutPanel flowHistoryFilter;
    private Label lblHistoryEmpLabel;
    private ComboBox cbHistoryEmployee;
    private Label lblHistoryFromLabel;
    private DateTimePicker dtpHistoryFrom;
    private Label lblHistoryToLabel;
    private DateTimePicker dtpHistoryTo;
    private Button btnHistoryQuick7;
    private Button btnHistoryQuick30;
    private FlowLayoutPanel flowHistory;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        tabsMain = new TabControl();
        tabEmployees = new TabPage();
        tabShifts = new TabPage();
        tabTasks = new TabPage();
        tabHistory = new TabPage();

        tlEmployees = new TableLayoutPanel();
        tlEmpHeader = new TableLayoutPanel();
        flowEmpTitles = new FlowLayoutPanel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnAddNew = new Button();
        flowStats = new FlowLayoutPanel();
        cardTotalStaff = new StatCard();
        cardWorking = new StatCard();
        flowStaffCards = new FlowLayoutPanel();

        tlShifts = new TableLayoutPanel();
        tlShiftLeft = new TableLayoutPanel();
        lblShiftsTitle = new Label();
        btnAddShift = new Button();
        flowShiftDefs = new FlowLayoutPanel();
        tlShiftRight = new TableLayoutPanel();
        tlRosterHeader = new TableLayoutPanel();
        lblRosterTitle = new Label();
        flowAssignDate = new FlowLayoutPanel();
        lblAssignDate = new Label();
        dtpAssignDate = new DateTimePicker();
        btnAddAssignment = new Button();
        flowAssignments = new FlowLayoutPanel();

        tlTasks = new TableLayoutPanel();
        tlTasksHeader = new TableLayoutPanel();
        lblTasksTitle = new Label();
        btnAddTask = new Button();
        flowTaskFilter = new FlowLayoutPanel();
        lblTaskFilter = new Label();
        cbTaskFilter = new ComboBox();
        flowTasks = new FlowLayoutPanel();

        tlHistory = new TableLayoutPanel();
        lblHistoryTitle = new Label();
        flowHistoryFilter = new FlowLayoutPanel();
        lblHistoryEmpLabel = new Label();
        cbHistoryEmployee = new ComboBox();
        lblHistoryFromLabel = new Label();
        dtpHistoryFrom = new DateTimePicker();
        lblHistoryToLabel = new Label();
        dtpHistoryTo = new DateTimePicker();
        btnHistoryQuick7 = new Button();
        btnHistoryQuick30 = new Button();
        flowHistory = new FlowLayoutPanel();

        tabsMain.SuspendLayout();
        tabEmployees.SuspendLayout();
        tlEmployees.SuspendLayout();
        tlEmpHeader.SuspendLayout();
        flowEmpTitles.SuspendLayout();
        flowStats.SuspendLayout();
        tabShifts.SuspendLayout();
        tlShifts.SuspendLayout();
        tlShiftLeft.SuspendLayout();
        tlShiftRight.SuspendLayout();
        tlRosterHeader.SuspendLayout();
        flowAssignDate.SuspendLayout();
        tabTasks.SuspendLayout();
        tlTasks.SuspendLayout();
        tlTasksHeader.SuspendLayout();
        flowTaskFilter.SuspendLayout();
        tabHistory.SuspendLayout();
        tlHistory.SuspendLayout();
        flowHistoryFilter.SuspendLayout();
        SuspendLayout();

        // =============================================================
        // tabsMain
        // =============================================================
        tabsMain.Controls.Add(tabEmployees);
        tabsMain.Controls.Add(tabShifts);
        tabsMain.Controls.Add(tabTasks);
        tabsMain.Controls.Add(tabHistory);
        tabsMain.Dock = DockStyle.Fill;
        tabsMain.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        tabsMain.Name = "tabsMain";
        tabsMain.Padding = new Point(16, 6);
        tabsMain.SelectedIndex = 0;
        tabsMain.TabIndex = 0;

        // =============================================================
        // TAB 1: NHAN VIEN
        // =============================================================
        tabEmployees.BackColor = Color.FromArgb(249, 249, 249);
        tabEmployees.Controls.Add(tlEmployees);
        tabEmployees.Name = "tabEmployees";
        tabEmployees.Padding = new Padding(24, 16, 24, 16);
        tabEmployees.TabIndex = 0;
        tabEmployees.Text = "Nhân viên";

        tlEmployees.ColumnCount = 1;
        tlEmployees.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlEmployees.RowCount = 3;
        tlEmployees.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlEmployees.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlEmployees.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlEmployees.Controls.Add(tlEmpHeader, 0, 0);
        tlEmployees.Controls.Add(flowStats, 0, 1);
        tlEmployees.Controls.Add(flowStaffCards, 0, 2);
        tlEmployees.Dock = DockStyle.Fill;
        tlEmployees.Name = "tlEmployees";
        tlEmployees.TabIndex = 0;

        tlEmpHeader.AutoSize = true;
        tlEmpHeader.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tlEmpHeader.ColumnCount = 2;
        tlEmpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlEmpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlEmpHeader.RowCount = 1;
        tlEmpHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlEmpHeader.Controls.Add(flowEmpTitles, 0, 0);
        tlEmpHeader.Controls.Add(btnAddNew, 1, 0);
        tlEmpHeader.Dock = DockStyle.Fill;
        tlEmpHeader.Margin = new Padding(0);
        tlEmpHeader.Name = "tlEmpHeader";
        tlEmpHeader.TabIndex = 0;

        flowEmpTitles.AutoSize = true;
        flowEmpTitles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowEmpTitles.Controls.Add(lblTitle);
        flowEmpTitles.Controls.Add(lblSubtitle);
        flowEmpTitles.FlowDirection = FlowDirection.TopDown;
        flowEmpTitles.Margin = new Padding(0);
        flowEmpTitles.Name = "flowEmpTitles";
        flowEmpTitles.TabIndex = 0;
        flowEmpTitles.WrapContents = false;

        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblTitle.Margin = new Padding(0);
        lblTitle.Name = "lblTitle";
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Quản lý Nhân viên";

        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Margin = new Padding(3, 0, 0, 0);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Quản lý danh sách, vai trò và phân quyền hệ thống";

        btnAddNew.Anchor = AnchorStyles.Right;
        btnAddNew.AutoSize = true;
        btnAddNew.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnAddNew.BackColor = Color.FromArgb(172, 45, 0);
        btnAddNew.Cursor = Cursors.Hand;
        btnAddNew.FlatAppearance.BorderSize = 0;
        btnAddNew.FlatStyle = FlatStyle.Flat;
        btnAddNew.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddNew.ForeColor = Color.White;
        btnAddNew.Margin = new Padding(0);
        btnAddNew.Name = "btnAddNew";
        btnAddNew.Padding = new Padding(14, 6, 14, 6);
        btnAddNew.TabIndex = 1;
        btnAddNew.Text = "+  Thêm nhân viên mới";
        btnAddNew.UseVisualStyleBackColor = false;

        flowStats.AutoSize = true;
        flowStats.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowStats.Controls.Add(cardTotalStaff);
        flowStats.Controls.Add(cardWorking);
        flowStats.Dock = DockStyle.Fill;
        flowStats.Margin = new Padding(0, 16, 0, 16);
        flowStats.Name = "flowStats";
        flowStats.TabIndex = 1;
        flowStats.WrapContents = false;

        cardTotalStaff.BackColor = Color.White;
        cardTotalStaff.Margin = new Padding(0, 0, 16, 0);
        cardTotalStaff.Name = "cardTotalStaff";
        cardTotalStaff.Size = new Size(240, 140);
        cardTotalStaff.TabIndex = 0;

        cardWorking.BackColor = Color.White;
        cardWorking.Margin = new Padding(0);
        cardWorking.Name = "cardWorking";
        cardWorking.Size = new Size(240, 140);
        cardWorking.TabIndex = 1;

        flowStaffCards.AutoScroll = true;
        flowStaffCards.Dock = DockStyle.Fill;
        flowStaffCards.Margin = new Padding(0);
        flowStaffCards.Name = "flowStaffCards";
        flowStaffCards.TabIndex = 2;

        // =============================================================
        // TAB 2: CA LAM
        // =============================================================
        tabShifts.BackColor = Color.FromArgb(249, 249, 249);
        tabShifts.Controls.Add(tlShifts);
        tabShifts.Name = "tabShifts";
        tabShifts.Padding = new Padding(24, 16, 24, 16);
        tabShifts.TabIndex = 1;
        tabShifts.Text = "Ca làm";

        tlShifts.ColumnCount = 2;
        tlShifts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        tlShifts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        tlShifts.RowCount = 1;
        tlShifts.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlShifts.Controls.Add(tlShiftLeft, 0, 0);
        tlShifts.Controls.Add(tlShiftRight, 1, 0);
        tlShifts.Dock = DockStyle.Fill;
        tlShifts.Name = "tlShifts";
        tlShifts.TabIndex = 0;

        // --- cot trai ---
        tlShiftLeft.ColumnCount = 1;
        tlShiftLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlShiftLeft.RowCount = 3;
        tlShiftLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlShiftLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlShiftLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlShiftLeft.Controls.Add(lblShiftsTitle, 0, 0);
        tlShiftLeft.Controls.Add(btnAddShift, 0, 1);
        tlShiftLeft.Controls.Add(flowShiftDefs, 0, 2);
        tlShiftLeft.Dock = DockStyle.Fill;
        tlShiftLeft.Margin = new Padding(0, 0, 16, 0);
        tlShiftLeft.Name = "tlShiftLeft";
        tlShiftLeft.TabIndex = 0;

        lblShiftsTitle.AutoSize = true;
        lblShiftsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblShiftsTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblShiftsTitle.Margin = new Padding(0, 0, 0, 10);
        lblShiftsTitle.Name = "lblShiftsTitle";
        lblShiftsTitle.TabIndex = 0;
        lblShiftsTitle.Text = "Các ca làm trong ngày";

        btnAddShift.AutoSize = true;
        btnAddShift.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnAddShift.BackColor = Color.FromArgb(172, 45, 0);
        btnAddShift.Cursor = Cursors.Hand;
        btnAddShift.Dock = DockStyle.Fill;
        btnAddShift.FlatAppearance.BorderSize = 0;
        btnAddShift.FlatStyle = FlatStyle.Flat;
        btnAddShift.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddShift.ForeColor = Color.White;
        btnAddShift.Margin = new Padding(0, 0, 0, 12);
        btnAddShift.Name = "btnAddShift";
        btnAddShift.Padding = new Padding(0, 6, 0, 6);
        btnAddShift.TabIndex = 1;
        btnAddShift.Text = "+  Thêm ca làm";
        btnAddShift.UseVisualStyleBackColor = false;

        flowShiftDefs.AutoScroll = true;
        flowShiftDefs.Dock = DockStyle.Fill;
        flowShiftDefs.FlowDirection = FlowDirection.TopDown;
        flowShiftDefs.Margin = new Padding(0);
        flowShiftDefs.Name = "flowShiftDefs";
        flowShiftDefs.TabIndex = 2;
        flowShiftDefs.WrapContents = false;

        // --- cot phai ---
        tlShiftRight.ColumnCount = 1;
        tlShiftRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlShiftRight.RowCount = 2;
        tlShiftRight.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlShiftRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlShiftRight.Controls.Add(tlRosterHeader, 0, 0);
        tlShiftRight.Controls.Add(flowAssignments, 0, 1);
        tlShiftRight.Dock = DockStyle.Fill;
        tlShiftRight.Margin = new Padding(0);
        tlShiftRight.Name = "tlShiftRight";
        tlShiftRight.TabIndex = 1;

        tlRosterHeader.AutoSize = true;
        tlRosterHeader.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tlRosterHeader.ColumnCount = 2;
        tlRosterHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlRosterHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlRosterHeader.RowCount = 2;
        tlRosterHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlRosterHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlRosterHeader.Controls.Add(lblRosterTitle, 0, 0);
        tlRosterHeader.SetColumnSpan(lblRosterTitle, 2);
        tlRosterHeader.Controls.Add(flowAssignDate, 0, 1);
        tlRosterHeader.Controls.Add(btnAddAssignment, 1, 1);
        tlRosterHeader.Dock = DockStyle.Fill;
        tlRosterHeader.Margin = new Padding(0);
        tlRosterHeader.Name = "tlRosterHeader";
        tlRosterHeader.TabIndex = 0;

        lblRosterTitle.AutoSize = true;
        lblRosterTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblRosterTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblRosterTitle.Margin = new Padding(0, 0, 0, 10);
        lblRosterTitle.Name = "lblRosterTitle";
        lblRosterTitle.TabIndex = 0;
        lblRosterTitle.Text = "Danh sách nhân viên theo ngày";

        flowAssignDate.Anchor = AnchorStyles.Left;
        flowAssignDate.AutoSize = true;
        flowAssignDate.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowAssignDate.Controls.Add(lblAssignDate);
        flowAssignDate.Controls.Add(dtpAssignDate);
        flowAssignDate.Margin = new Padding(0, 0, 0, 12);
        flowAssignDate.Name = "flowAssignDate";
        flowAssignDate.TabIndex = 1;
        flowAssignDate.WrapContents = false;

        lblAssignDate.Anchor = AnchorStyles.Left;
        lblAssignDate.AutoSize = true;
        lblAssignDate.Font = new Font("Segoe UI", 10F);
        lblAssignDate.ForeColor = Color.FromArgb(91, 65, 57);
        lblAssignDate.Margin = new Padding(0, 0, 8, 0);
        lblAssignDate.Name = "lblAssignDate";
        lblAssignDate.TabIndex = 0;
        lblAssignDate.Text = "Ngày:";

        dtpAssignDate.Anchor = AnchorStyles.Left;
        dtpAssignDate.CustomFormat = "dd/MM/yyyy";
        dtpAssignDate.Font = new Font("Segoe UI", 10F);
        dtpAssignDate.Format = DateTimePickerFormat.Custom;
        dtpAssignDate.Margin = new Padding(0);
        dtpAssignDate.Name = "dtpAssignDate";
        dtpAssignDate.Size = new Size(150, 30);
        dtpAssignDate.TabIndex = 1;

        btnAddAssignment.Anchor = AnchorStyles.Right;
        btnAddAssignment.AutoSize = true;
        btnAddAssignment.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnAddAssignment.BackColor = Color.FromArgb(0, 104, 92);
        btnAddAssignment.Cursor = Cursors.Hand;
        btnAddAssignment.FlatAppearance.BorderSize = 0;
        btnAddAssignment.FlatStyle = FlatStyle.Flat;
        btnAddAssignment.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddAssignment.ForeColor = Color.White;
        btnAddAssignment.Margin = new Padding(0, 0, 0, 12);
        btnAddAssignment.Name = "btnAddAssignment";
        btnAddAssignment.Padding = new Padding(14, 6, 14, 6);
        btnAddAssignment.TabIndex = 2;
        btnAddAssignment.Text = "+  Phân công";
        btnAddAssignment.UseVisualStyleBackColor = false;

        flowAssignments.AutoScroll = true;
        flowAssignments.Dock = DockStyle.Fill;
        flowAssignments.FlowDirection = FlowDirection.TopDown;
        flowAssignments.Margin = new Padding(0);
        flowAssignments.Name = "flowAssignments";
        flowAssignments.TabIndex = 1;
        flowAssignments.WrapContents = false;

        // =============================================================
        // TAB 3: NHIEM VU
        // =============================================================
        tabTasks.BackColor = Color.FromArgb(249, 249, 249);
        tabTasks.Controls.Add(tlTasks);
        tabTasks.Name = "tabTasks";
        tabTasks.Padding = new Padding(24, 16, 24, 16);
        tabTasks.TabIndex = 2;
        tabTasks.Text = "Nhiệm vụ";

        tlTasks.ColumnCount = 1;
        tlTasks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlTasks.RowCount = 3;
        tlTasks.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlTasks.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlTasks.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlTasks.Controls.Add(tlTasksHeader, 0, 0);
        tlTasks.Controls.Add(flowTaskFilter, 0, 1);
        tlTasks.Controls.Add(flowTasks, 0, 2);
        tlTasks.Dock = DockStyle.Fill;
        tlTasks.Name = "tlTasks";
        tlTasks.TabIndex = 0;

        tlTasksHeader.AutoSize = true;
        tlTasksHeader.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tlTasksHeader.ColumnCount = 2;
        tlTasksHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlTasksHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlTasksHeader.RowCount = 1;
        tlTasksHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlTasksHeader.Controls.Add(lblTasksTitle, 0, 0);
        tlTasksHeader.Controls.Add(btnAddTask, 1, 0);
        tlTasksHeader.Dock = DockStyle.Fill;
        tlTasksHeader.Margin = new Padding(0);
        tlTasksHeader.Name = "tlTasksHeader";
        tlTasksHeader.TabIndex = 0;

        lblTasksTitle.Anchor = AnchorStyles.Left;
        lblTasksTitle.AutoSize = true;
        lblTasksTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTasksTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblTasksTitle.Margin = new Padding(0);
        lblTasksTitle.Name = "lblTasksTitle";
        lblTasksTitle.TabIndex = 0;
        lblTasksTitle.Text = "Nhiệm vụ nhân viên";

        btnAddTask.Anchor = AnchorStyles.Right;
        btnAddTask.AutoSize = true;
        btnAddTask.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnAddTask.BackColor = Color.FromArgb(172, 45, 0);
        btnAddTask.Cursor = Cursors.Hand;
        btnAddTask.FlatAppearance.BorderSize = 0;
        btnAddTask.FlatStyle = FlatStyle.Flat;
        btnAddTask.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnAddTask.ForeColor = Color.White;
        btnAddTask.Margin = new Padding(0);
        btnAddTask.Name = "btnAddTask";
        btnAddTask.Padding = new Padding(14, 6, 14, 6);
        btnAddTask.TabIndex = 1;
        btnAddTask.Text = "+  Giao việc mới";
        btnAddTask.UseVisualStyleBackColor = false;

        flowTaskFilter.AutoSize = true;
        flowTaskFilter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowTaskFilter.BackColor = Color.White;
        flowTaskFilter.Controls.Add(lblTaskFilter);
        flowTaskFilter.Controls.Add(cbTaskFilter);
        flowTaskFilter.Dock = DockStyle.Fill;
        flowTaskFilter.Margin = new Padding(0, 14, 0, 14);
        flowTaskFilter.Name = "flowTaskFilter";
        flowTaskFilter.Padding = new Padding(14, 10, 14, 10);
        flowTaskFilter.TabIndex = 1;
        flowTaskFilter.WrapContents = false;

        lblTaskFilter.Anchor = AnchorStyles.Left;
        lblTaskFilter.AutoSize = true;
        lblTaskFilter.Font = new Font("Segoe UI", 10F);
        lblTaskFilter.ForeColor = Color.FromArgb(91, 65, 57);
        lblTaskFilter.Margin = new Padding(0, 0, 8, 0);
        lblTaskFilter.Name = "lblTaskFilter";
        lblTaskFilter.TabIndex = 0;
        lblTaskFilter.Text = "Lọc theo nhân viên:";

        cbTaskFilter.Anchor = AnchorStyles.Left;
        cbTaskFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        cbTaskFilter.Font = new Font("Segoe UI", 10F);
        cbTaskFilter.Margin = new Padding(0);
        cbTaskFilter.Name = "cbTaskFilter";
        cbTaskFilter.Size = new Size(300, 31);
        cbTaskFilter.TabIndex = 1;

        flowTasks.AutoScroll = true;
        flowTasks.Dock = DockStyle.Fill;
        flowTasks.FlowDirection = FlowDirection.TopDown;
        flowTasks.Margin = new Padding(0);
        flowTasks.Name = "flowTasks";
        flowTasks.TabIndex = 2;
        flowTasks.WrapContents = false;

        // =============================================================
        // TAB 4: LICH SU CA LAM
        // =============================================================
        tabHistory.BackColor = Color.FromArgb(249, 249, 249);
        tabHistory.Controls.Add(tlHistory);
        tabHistory.Name = "tabHistory";
        tabHistory.Padding = new Padding(24, 16, 24, 16);
        tabHistory.TabIndex = 3;
        tabHistory.Text = "Lịch sử ca làm";

        tlHistory.ColumnCount = 1;
        tlHistory.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlHistory.RowCount = 3;
        tlHistory.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlHistory.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlHistory.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlHistory.Controls.Add(lblHistoryTitle, 0, 0);
        tlHistory.Controls.Add(flowHistoryFilter, 0, 1);
        tlHistory.Controls.Add(flowHistory, 0, 2);
        tlHistory.Dock = DockStyle.Fill;
        tlHistory.Name = "tlHistory";
        tlHistory.TabIndex = 0;

        lblHistoryTitle.AutoSize = true;
        lblHistoryTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblHistoryTitle.ForeColor = Color.FromArgb(26, 28, 28);
        lblHistoryTitle.Margin = new Padding(0);
        lblHistoryTitle.Name = "lblHistoryTitle";
        lblHistoryTitle.TabIndex = 0;
        lblHistoryTitle.Text = "Lịch sử ca làm";

        flowHistoryFilter.AutoSize = true;
        flowHistoryFilter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowHistoryFilter.BackColor = Color.White;
        flowHistoryFilter.Controls.Add(lblHistoryEmpLabel);
        flowHistoryFilter.Controls.Add(cbHistoryEmployee);
        flowHistoryFilter.Controls.Add(lblHistoryFromLabel);
        flowHistoryFilter.Controls.Add(dtpHistoryFrom);
        flowHistoryFilter.Controls.Add(lblHistoryToLabel);
        flowHistoryFilter.Controls.Add(dtpHistoryTo);
        flowHistoryFilter.Controls.Add(btnHistoryQuick7);
        flowHistoryFilter.Controls.Add(btnHistoryQuick30);
        flowHistoryFilter.Dock = DockStyle.Fill;
        flowHistoryFilter.Margin = new Padding(0, 14, 0, 14);
        flowHistoryFilter.Name = "flowHistoryFilter";
        flowHistoryFilter.Padding = new Padding(14, 10, 14, 10);
        flowHistoryFilter.TabIndex = 1;
        flowHistoryFilter.WrapContents = false;

        lblHistoryEmpLabel.Anchor = AnchorStyles.Left;
        lblHistoryEmpLabel.AutoSize = true;
        lblHistoryEmpLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryEmpLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryEmpLabel.Margin = new Padding(0, 0, 8, 0);
        lblHistoryEmpLabel.Name = "lblHistoryEmpLabel";
        lblHistoryEmpLabel.TabIndex = 0;
        lblHistoryEmpLabel.Text = "Nhân viên:";

        cbHistoryEmployee.Anchor = AnchorStyles.Left;
        cbHistoryEmployee.DropDownStyle = ComboBoxStyle.DropDownList;
        cbHistoryEmployee.Font = new Font("Segoe UI", 10F);
        cbHistoryEmployee.Margin = new Padding(0, 0, 20, 0);
        cbHistoryEmployee.Name = "cbHistoryEmployee";
        cbHistoryEmployee.Size = new Size(250, 31);
        cbHistoryEmployee.TabIndex = 1;

        lblHistoryFromLabel.Anchor = AnchorStyles.Left;
        lblHistoryFromLabel.AutoSize = true;
        lblHistoryFromLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryFromLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryFromLabel.Margin = new Padding(0, 0, 8, 0);
        lblHistoryFromLabel.Name = "lblHistoryFromLabel";
        lblHistoryFromLabel.TabIndex = 2;
        lblHistoryFromLabel.Text = "Từ ngày:";

        dtpHistoryFrom.Anchor = AnchorStyles.Left;
        dtpHistoryFrom.CustomFormat = "dd/MM/yyyy";
        dtpHistoryFrom.Font = new Font("Segoe UI", 10F);
        dtpHistoryFrom.Format = DateTimePickerFormat.Custom;
        dtpHistoryFrom.Margin = new Padding(0, 0, 20, 0);
        dtpHistoryFrom.Name = "dtpHistoryFrom";
        dtpHistoryFrom.Size = new Size(140, 30);
        dtpHistoryFrom.TabIndex = 3;

        lblHistoryToLabel.Anchor = AnchorStyles.Left;
        lblHistoryToLabel.AutoSize = true;
        lblHistoryToLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryToLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryToLabel.Margin = new Padding(0, 0, 8, 0);
        lblHistoryToLabel.Name = "lblHistoryToLabel";
        lblHistoryToLabel.TabIndex = 4;
        lblHistoryToLabel.Text = "Đến ngày:";

        dtpHistoryTo.Anchor = AnchorStyles.Left;
        dtpHistoryTo.CustomFormat = "dd/MM/yyyy";
        dtpHistoryTo.Font = new Font("Segoe UI", 10F);
        dtpHistoryTo.Format = DateTimePickerFormat.Custom;
        dtpHistoryTo.Margin = new Padding(0, 0, 20, 0);
        dtpHistoryTo.Name = "dtpHistoryTo";
        dtpHistoryTo.Size = new Size(140, 30);
        dtpHistoryTo.TabIndex = 5;

        btnHistoryQuick7.Anchor = AnchorStyles.Left;
        btnHistoryQuick7.AutoSize = true;
        btnHistoryQuick7.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnHistoryQuick7.BackColor = Color.White;
        btnHistoryQuick7.Cursor = Cursors.Hand;
        btnHistoryQuick7.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
        btnHistoryQuick7.FlatStyle = FlatStyle.Flat;
        btnHistoryQuick7.Font = new Font("Segoe UI", 9.5F);
        btnHistoryQuick7.ForeColor = Color.FromArgb(91, 65, 57);
        btnHistoryQuick7.Margin = new Padding(0, 0, 8, 0);
        btnHistoryQuick7.Name = "btnHistoryQuick7";
        btnHistoryQuick7.Padding = new Padding(10, 4, 10, 4);
        btnHistoryQuick7.TabIndex = 6;
        btnHistoryQuick7.Text = "7 ngày qua";
        btnHistoryQuick7.UseVisualStyleBackColor = false;

        btnHistoryQuick30.Anchor = AnchorStyles.Left;
        btnHistoryQuick30.AutoSize = true;
        btnHistoryQuick30.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnHistoryQuick30.BackColor = Color.White;
        btnHistoryQuick30.Cursor = Cursors.Hand;
        btnHistoryQuick30.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
        btnHistoryQuick30.FlatStyle = FlatStyle.Flat;
        btnHistoryQuick30.Font = new Font("Segoe UI", 9.5F);
        btnHistoryQuick30.ForeColor = Color.FromArgb(91, 65, 57);
        btnHistoryQuick30.Margin = new Padding(0);
        btnHistoryQuick30.Name = "btnHistoryQuick30";
        btnHistoryQuick30.Padding = new Padding(10, 4, 10, 4);
        btnHistoryQuick30.TabIndex = 7;
        btnHistoryQuick30.Text = "30 ngày qua";
        btnHistoryQuick30.UseVisualStyleBackColor = false;

        flowHistory.AutoScroll = true;
        flowHistory.Dock = DockStyle.Fill;
        flowHistory.FlowDirection = FlowDirection.TopDown;
        flowHistory.Margin = new Padding(0);
        flowHistory.Name = "flowHistory";
        flowHistory.TabIndex = 2;
        flowHistory.WrapContents = false;

        // =============================================================
        // StaffControl
        // =============================================================
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tabsMain);
        Name = "StaffControl";
        Size = new Size(1200, 760);

        tabsMain.ResumeLayout(false);
        tabEmployees.ResumeLayout(false);
        tlEmployees.ResumeLayout(false);
        tlEmployees.PerformLayout();
        tlEmpHeader.ResumeLayout(false);
        tlEmpHeader.PerformLayout();
        flowEmpTitles.ResumeLayout(false);
        flowEmpTitles.PerformLayout();
        flowStats.ResumeLayout(false);
        tabShifts.ResumeLayout(false);
        tlShifts.ResumeLayout(false);
        tlShiftLeft.ResumeLayout(false);
        tlShiftLeft.PerformLayout();
        tlShiftRight.ResumeLayout(false);
        tlShiftRight.PerformLayout();
        tlRosterHeader.ResumeLayout(false);
        tlRosterHeader.PerformLayout();
        flowAssignDate.ResumeLayout(false);
        flowAssignDate.PerformLayout();
        tabTasks.ResumeLayout(false);
        tlTasks.ResumeLayout(false);
        tlTasks.PerformLayout();
        tlTasksHeader.ResumeLayout(false);
        tlTasksHeader.PerformLayout();
        flowTaskFilter.ResumeLayout(false);
        flowTaskFilter.PerformLayout();
        tabHistory.ResumeLayout(false);
        tlHistory.ResumeLayout(false);
        tlHistory.PerformLayout();
        flowHistoryFilter.ResumeLayout(false);
        flowHistoryFilter.PerformLayout();
        ResumeLayout(false);
    }
}