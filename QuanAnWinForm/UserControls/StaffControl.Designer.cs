using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class StaffControl
{
    private System.ComponentModel.IContainer components = null;

    private TabControl tabsMain;
    private TabPage tabEmployees;
    private TabPage tabShifts;
    private TabPage tabTasks;
    private TabPage tabHistory;

    // -- Tab Nhân viên --
    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private StatCard cardTotalStaff;
    private StatCard cardWorking;
    private FlowLayoutPanel flowStaffCards;

    // -- Tab Ca làm: cột trái = định nghĩa ca, cột phải = 1 danh sách toàn bộ
    // nhân viên cho ngày đang chọn (ai đã có ca / ai chưa có ca đều nằm chung) --
    private Label lblShiftsTitle;
    private Button btnAddShift;
    private FlowLayoutPanel flowShiftDefs;
    private Panel dividerShifts;
    private Label lblRosterTitle;
    private Label lblAssignDate;
    private DateTimePicker dtpAssignDate;
    private Button btnAddAssignment;
    private FlowLayoutPanel flowAssignments;

    // -- Tab Nhiệm vụ --
    private Label lblTasksTitle;
    private Button btnAddTask;
    private Label lblTaskFilter;
    private ComboBox cbTaskFilter;
    private FlowLayoutPanel flowTasks;

    // -- Tab Lịch sử ca làm: lọc theo nhân viên + khoảng ngày, hiển thị dạng
    // timeline (nhóm theo từng ngày, ngày gần nhất lên trên) giống các app hiện đại --
    private Label lblHistoryTitle;
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
        flowStaffCards = new FlowLayoutPanel();
        cardWorking = new StatCard();
        cardTotalStaff = new StatCard();
        btnAddNew = new Button();
        lblSubtitle = new Label();
        lblTitle = new Label();
        tabShifts = new TabPage();
        flowAssignments = new FlowLayoutPanel();
        btnAddAssignment = new Button();
        dtpAssignDate = new DateTimePicker();
        lblAssignDate = new Label();
        lblRosterTitle = new Label();
        dividerShifts = new Panel();
        flowShiftDefs = new FlowLayoutPanel();
        btnAddShift = new Button();
        lblShiftsTitle = new Label();
        tabTasks = new TabPage();
        flowTasks = new FlowLayoutPanel();
        cbTaskFilter = new ComboBox();
        lblTaskFilter = new Label();
        btnAddTask = new Button();
        lblTasksTitle = new Label();
        tabHistory = new TabPage();
        flowHistory = new FlowLayoutPanel();
        btnHistoryQuick30 = new Button();
        btnHistoryQuick7 = new Button();
        dtpHistoryTo = new DateTimePicker();
        lblHistoryToLabel = new Label();
        dtpHistoryFrom = new DateTimePicker();
        lblHistoryFromLabel = new Label();
        cbHistoryEmployee = new ComboBox();
        lblHistoryEmpLabel = new Label();
        lblHistoryTitle = new Label();
        tabsMain.SuspendLayout();
        tabEmployees.SuspendLayout();
        tabShifts.SuspendLayout();
        tabTasks.SuspendLayout();
        tabHistory.SuspendLayout();
        SuspendLayout();
        // 
        // tabsMain
        // 
        tabsMain.Controls.Add(tabEmployees);
        tabsMain.Controls.Add(tabShifts);
        tabsMain.Controls.Add(tabTasks);
        tabsMain.Controls.Add(tabHistory);
        tabsMain.Dock = DockStyle.Fill;
        tabsMain.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        tabsMain.Location = new Point(0, 0);
        tabsMain.Name = "tabsMain";
        tabsMain.SelectedIndex = 0;
        tabsMain.Size = new Size(1200, 760);
        tabsMain.TabIndex = 0;
        // 
        // tabEmployees
        // 
        tabEmployees.BackColor = Color.FromArgb(249, 249, 249);
        tabEmployees.Controls.Add(flowStaffCards);
        tabEmployees.Controls.Add(cardWorking);
        tabEmployees.Controls.Add(cardTotalStaff);
        tabEmployees.Controls.Add(btnAddNew);
        tabEmployees.Controls.Add(lblSubtitle);
        tabEmployees.Controls.Add(lblTitle);
        tabEmployees.Location = new Point(4, 32);
        tabEmployees.Name = "tabEmployees";
        tabEmployees.Padding = new Padding(20, 16, 20, 16);
        tabEmployees.Size = new Size(1192, 724);
        tabEmployees.TabIndex = 0;
        tabEmployees.Text = "Nhân viên";
        // 
        // flowStaffCards
        // 
        flowStaffCards.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowStaffCards.AutoScroll = true;
        flowStaffCards.Location = new Point(0, 246);
        flowStaffCards.Name = "flowStaffCards";
        flowStaffCards.Size = new Size(1160, 438);
        flowStaffCards.TabIndex = 0;
        // 
        // cardWorking
        // 
        cardWorking.BackColor = Color.White;
        cardWorking.Location = new Point(248, 110);
        cardWorking.Name = "cardWorking";
        cardWorking.Padding = new Padding(16);
        cardWorking.Size = new Size(220, 130);
        cardWorking.TabIndex = 1;
        // 
        // cardTotalStaff
        // 
        cardTotalStaff.BackColor = Color.White;
        cardTotalStaff.Location = new Point(13, 110);
        cardTotalStaff.Name = "cardTotalStaff";
        cardTotalStaff.Padding = new Padding(16);
        cardTotalStaff.Size = new Size(220, 130);
        cardTotalStaff.TabIndex = 2;
        // 
        // btnAddNew
        // 
        btnAddNew.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddNew.BackColor = Color.FromArgb(172, 45, 0);
        btnAddNew.FlatAppearance.BorderSize = 0;
        btnAddNew.FlatStyle = FlatStyle.Flat;
        btnAddNew.ForeColor = Color.White;
        btnAddNew.Location = new Point(960, 8);
        btnAddNew.Name = "btnAddNew";
        btnAddNew.Size = new Size(200, 40);
        btnAddNew.TabIndex = 3;
        btnAddNew.Text = "+  Thêm nhân viên mới";
        btnAddNew.UseVisualStyleBackColor = false;
        // 
        // lblSubtitle
        // 
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(13, 67);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(402, 23);
        lblSubtitle.TabIndex = 4;
        lblSubtitle.Text = "Quản lý danh sách, vai trò và phân quyền hệ thống";
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(13, 8);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(341, 50);
        lblTitle.TabIndex = 5;
        lblTitle.Text = "Quản lý Nhân viên";
        // 
        // tabShifts
        // 
        tabShifts.BackColor = Color.FromArgb(249, 249, 249);
        tabShifts.Controls.Add(flowAssignments);
        tabShifts.Controls.Add(btnAddAssignment);
        tabShifts.Controls.Add(dtpAssignDate);
        tabShifts.Controls.Add(lblAssignDate);
        tabShifts.Controls.Add(lblRosterTitle);
        tabShifts.Controls.Add(dividerShifts);
        tabShifts.Controls.Add(flowShiftDefs);
        tabShifts.Controls.Add(btnAddShift);
        tabShifts.Controls.Add(lblShiftsTitle);
        tabShifts.Location = new Point(4, 32);
        tabShifts.Name = "tabShifts";
        tabShifts.Padding = new Padding(20, 16, 20, 16);
        tabShifts.Size = new Size(1192, 724);
        tabShifts.TabIndex = 1;
        tabShifts.Text = "Ca làm";
        // 
        // flowAssignments
        // 
        flowAssignments.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowAssignments.AutoScroll = true;
        flowAssignments.FlowDirection = FlowDirection.TopDown;
        flowAssignments.Location = new Point(336, 68);
        flowAssignments.Name = "flowAssignments";
        flowAssignments.Size = new Size(856, 614);
        flowAssignments.TabIndex = 0;
        flowAssignments.WrapContents = false;
        // 
        // btnAddAssignment
        // 
        btnAddAssignment.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddAssignment.BackColor = Color.FromArgb(0, 104, 92);
        btnAddAssignment.FlatAppearance.BorderSize = 0;
        btnAddAssignment.FlatStyle = FlatStyle.Flat;
        btnAddAssignment.ForeColor = Color.White;
        btnAddAssignment.Location = new Point(1040, 22);
        btnAddAssignment.Name = "btnAddAssignment";
        btnAddAssignment.Size = new Size(152, 34);
        btnAddAssignment.TabIndex = 1;
        btnAddAssignment.Text = "+  Phân công";
        btnAddAssignment.UseVisualStyleBackColor = false;
        // 
        // dtpAssignDate
        // 
        dtpAssignDate.Format = DateTimePickerFormat.Short;
        dtpAssignDate.Location = new Point(378, 24);
        dtpAssignDate.Name = "dtpAssignDate";
        dtpAssignDate.Size = new Size(150, 30);
        dtpAssignDate.TabIndex = 2;
        // 
        // lblAssignDate
        // 
        lblAssignDate.AutoSize = true;
        lblAssignDate.Font = new Font("Segoe UI", 10F);
        lblAssignDate.ForeColor = Color.FromArgb(91, 65, 57);
        lblAssignDate.Location = new Point(336, 28);
        lblAssignDate.Name = "lblAssignDate";
        lblAssignDate.Size = new Size(54, 23);
        lblAssignDate.TabIndex = 3;
        lblAssignDate.Text = "Ngày:";
        // 
        // lblRosterTitle
        // 
        lblRosterTitle.AutoSize = true;
        lblRosterTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblRosterTitle.Location = new Point(336, 0);
        lblRosterTitle.Name = "lblRosterTitle";
        lblRosterTitle.Size = new Size(335, 30);
        lblRosterTitle.TabIndex = 8;
        lblRosterTitle.Text = "Danh sách nhân viên theo ngày";
        // 
        // dividerShifts
        // 
        dividerShifts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        dividerShifts.BackColor = Color.FromArgb(224, 224, 224);
        dividerShifts.Location = new Point(312, 0);
        dividerShifts.Name = "dividerShifts";
        dividerShifts.Size = new Size(1, 682);
        dividerShifts.TabIndex = 7;
        // 
        // flowShiftDefs
        // 
        flowShiftDefs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        flowShiftDefs.AutoScroll = true;
        flowShiftDefs.FlowDirection = FlowDirection.TopDown;
        flowShiftDefs.Location = new Point(0, 82);
        flowShiftDefs.Name = "flowShiftDefs";
        flowShiftDefs.Size = new Size(280, 600);
        flowShiftDefs.TabIndex = 4;
        flowShiftDefs.WrapContents = false;
        // 
        // btnAddShift
        // 
        btnAddShift.BackColor = Color.FromArgb(172, 45, 0);
        btnAddShift.FlatAppearance.BorderSize = 0;
        btnAddShift.FlatStyle = FlatStyle.Flat;
        btnAddShift.ForeColor = Color.White;
        btnAddShift.Location = new Point(0, 34);
        btnAddShift.Name = "btnAddShift";
        btnAddShift.Size = new Size(280, 36);
        btnAddShift.TabIndex = 5;
        btnAddShift.Text = "+  Thêm ca làm";
        btnAddShift.UseVisualStyleBackColor = false;
        // 
        // lblShiftsTitle
        // 
        lblShiftsTitle.AutoSize = true;
        lblShiftsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblShiftsTitle.Location = new Point(0, 0);
        lblShiftsTitle.Name = "lblShiftsTitle";
        lblShiftsTitle.Size = new Size(244, 30);
        lblShiftsTitle.TabIndex = 6;
        lblShiftsTitle.Text = "Các ca làm trong ngày";
        // 
        // tabTasks
        // 
        tabTasks.BackColor = Color.FromArgb(249, 249, 249);
        tabTasks.Controls.Add(flowTasks);
        tabTasks.Controls.Add(cbTaskFilter);
        tabTasks.Controls.Add(lblTaskFilter);
        tabTasks.Controls.Add(btnAddTask);
        tabTasks.Controls.Add(lblTasksTitle);
        tabTasks.Location = new Point(4, 32);
        tabTasks.Name = "tabTasks";
        tabTasks.Padding = new Padding(20, 16, 20, 16);
        tabTasks.Size = new Size(1192, 724);
        tabTasks.TabIndex = 2;
        tabTasks.Text = "Nhiệm vụ";
        // 
        // flowTasks
        // 
        flowTasks.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowTasks.AutoScroll = true;
        flowTasks.FlowDirection = FlowDirection.TopDown;
        flowTasks.Location = new Point(0, 88);
        flowTasks.Name = "flowTasks";
        flowTasks.Size = new Size(1160, 600);
        flowTasks.TabIndex = 0;
        flowTasks.WrapContents = false;
        // 
        // cbTaskFilter
        // 
        cbTaskFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        cbTaskFilter.Location = new Point(140, 40);
        cbTaskFilter.Name = "cbTaskFilter";
        cbTaskFilter.Size = new Size(260, 31);
        cbTaskFilter.TabIndex = 1;
        // 
        // lblTaskFilter
        // 
        lblTaskFilter.AutoSize = true;
        lblTaskFilter.Font = new Font("Segoe UI", 10F);
        lblTaskFilter.ForeColor = Color.FromArgb(91, 65, 57);
        lblTaskFilter.Location = new Point(0, 44);
        lblTaskFilter.Name = "lblTaskFilter";
        lblTaskFilter.Size = new Size(160, 23);
        lblTaskFilter.TabIndex = 4;
        lblTaskFilter.Text = "Lọc theo nhân viên:";
        // 
        // btnAddTask
        // 
        btnAddTask.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddTask.BackColor = Color.FromArgb(172, 45, 0);
        btnAddTask.FlatAppearance.BorderSize = 0;
        btnAddTask.FlatStyle = FlatStyle.Flat;
        btnAddTask.ForeColor = Color.White;
        btnAddTask.Location = new Point(980, 4);
        btnAddTask.Name = "btnAddTask";
        btnAddTask.Size = new Size(180, 36);
        btnAddTask.TabIndex = 2;
        btnAddTask.Text = "+  Giao việc mới";
        btnAddTask.UseVisualStyleBackColor = false;
        // 
        // lblTasksTitle
        // 
        lblTasksTitle.AutoSize = true;
        lblTasksTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTasksTitle.Location = new Point(0, 0);
        lblTasksTitle.Name = "lblTasksTitle";
        lblTasksTitle.Size = new Size(271, 37);
        lblTasksTitle.TabIndex = 3;
        lblTasksTitle.Text = "Nhiệm vụ nhân viên";
        // 
        // tabHistory
        // 
        tabHistory.BackColor = Color.FromArgb(249, 249, 249);
        tabHistory.Controls.Add(flowHistory);
        tabHistory.Controls.Add(btnHistoryQuick30);
        tabHistory.Controls.Add(btnHistoryQuick7);
        tabHistory.Controls.Add(dtpHistoryTo);
        tabHistory.Controls.Add(lblHistoryToLabel);
        tabHistory.Controls.Add(dtpHistoryFrom);
        tabHistory.Controls.Add(lblHistoryFromLabel);
        tabHistory.Controls.Add(cbHistoryEmployee);
        tabHistory.Controls.Add(lblHistoryEmpLabel);
        tabHistory.Controls.Add(lblHistoryTitle);
        tabHistory.Location = new Point(4, 32);
        tabHistory.Name = "tabHistory";
        tabHistory.Padding = new Padding(20, 16, 20, 16);
        tabHistory.Size = new Size(1192, 724);
        tabHistory.TabIndex = 3;
        tabHistory.Text = "Lịch sử ca làm";
        // 
        // lblHistoryTitle
        // 
        lblHistoryTitle.AutoSize = true;
        lblHistoryTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblHistoryTitle.Location = new Point(0, 0);
        lblHistoryTitle.Name = "lblHistoryTitle";
        lblHistoryTitle.Size = new Size(300, 37);
        lblHistoryTitle.TabIndex = 0;
        lblHistoryTitle.Text = "Lịch sử ca làm";
        // 
        // lblHistoryEmpLabel
        // 
        lblHistoryEmpLabel.AutoSize = true;
        lblHistoryEmpLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryEmpLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryEmpLabel.Location = new Point(0, 46);
        lblHistoryEmpLabel.Name = "lblHistoryEmpLabel";
        lblHistoryEmpLabel.Size = new Size(90, 23);
        lblHistoryEmpLabel.TabIndex = 1;
        lblHistoryEmpLabel.Text = "Nhân viên:";
        // 
        // cbHistoryEmployee
        // 
        cbHistoryEmployee.DropDownStyle = ComboBoxStyle.DropDownList;
        cbHistoryEmployee.Location = new Point(96, 42);
        cbHistoryEmployee.Name = "cbHistoryEmployee";
        cbHistoryEmployee.Size = new Size(230, 31);
        cbHistoryEmployee.TabIndex = 2;
        // 
        // lblHistoryFromLabel
        // 
        lblHistoryFromLabel.AutoSize = true;
        lblHistoryFromLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryFromLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryFromLabel.Location = new Point(340, 46);
        lblHistoryFromLabel.Name = "lblHistoryFromLabel";
        lblHistoryFromLabel.Size = new Size(70, 23);
        lblHistoryFromLabel.TabIndex = 3;
        lblHistoryFromLabel.Text = "Từ ngày:";
        // 
        // dtpHistoryFrom
        // 
        dtpHistoryFrom.Format = DateTimePickerFormat.Short;
        dtpHistoryFrom.Location = new Point(416, 42);
        dtpHistoryFrom.Name = "dtpHistoryFrom";
        dtpHistoryFrom.Size = new Size(130, 30);
        dtpHistoryFrom.TabIndex = 4;
        // 
        // lblHistoryToLabel
        // 
        lblHistoryToLabel.AutoSize = true;
        lblHistoryToLabel.Font = new Font("Segoe UI", 10F);
        lblHistoryToLabel.ForeColor = Color.FromArgb(91, 65, 57);
        lblHistoryToLabel.Location = new Point(560, 46);
        lblHistoryToLabel.Name = "lblHistoryToLabel";
        lblHistoryToLabel.Size = new Size(80, 23);
        lblHistoryToLabel.TabIndex = 5;
        lblHistoryToLabel.Text = "Đến ngày:";
        // 
        // dtpHistoryTo
        // 
        dtpHistoryTo.Format = DateTimePickerFormat.Short;
        dtpHistoryTo.Location = new Point(646, 42);
        dtpHistoryTo.Name = "dtpHistoryTo";
        dtpHistoryTo.Size = new Size(130, 30);
        dtpHistoryTo.TabIndex = 6;
        // 
        // btnHistoryQuick7
        // 
        btnHistoryQuick7.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
        btnHistoryQuick7.FlatStyle = FlatStyle.Flat;
        btnHistoryQuick7.Font = new Font("Segoe UI", 9F);
        btnHistoryQuick7.ForeColor = Color.FromArgb(91, 65, 57);
        btnHistoryQuick7.Location = new Point(796, 40);
        btnHistoryQuick7.Name = "btnHistoryQuick7";
        btnHistoryQuick7.Size = new Size(100, 34);
        btnHistoryQuick7.TabIndex = 7;
        btnHistoryQuick7.Text = "7 ngày qua";
        btnHistoryQuick7.UseVisualStyleBackColor = true;
        // 
        // btnHistoryQuick30
        // 
        btnHistoryQuick30.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
        btnHistoryQuick30.FlatStyle = FlatStyle.Flat;
        btnHistoryQuick30.Font = new Font("Segoe UI", 9F);
        btnHistoryQuick30.ForeColor = Color.FromArgb(91, 65, 57);
        btnHistoryQuick30.Location = new Point(904, 40);
        btnHistoryQuick30.Name = "btnHistoryQuick30";
        btnHistoryQuick30.Size = new Size(100, 34);
        btnHistoryQuick30.TabIndex = 8;
        btnHistoryQuick30.Text = "30 ngày qua";
        btnHistoryQuick30.UseVisualStyleBackColor = true;
        // 
        // flowHistory
        // 
        flowHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowHistory.AutoScroll = true;
        flowHistory.FlowDirection = FlowDirection.TopDown;
        flowHistory.Location = new Point(0, 92);
        flowHistory.Name = "flowHistory";
        flowHistory.Size = new Size(1160, 596);
        flowHistory.TabIndex = 9;
        flowHistory.WrapContents = false;
        // 
        // StaffControl
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(tabsMain);
        Name = "StaffControl";
        Size = new Size(1200, 760);
        tabsMain.ResumeLayout(false);
        tabEmployees.ResumeLayout(false);
        tabEmployees.PerformLayout();
        tabShifts.ResumeLayout(false);
        tabShifts.PerformLayout();
        tabTasks.ResumeLayout(false);
        tabTasks.PerformLayout();
        tabHistory.ResumeLayout(false);
        tabHistory.PerformLayout();
        ResumeLayout(false);
    }
}