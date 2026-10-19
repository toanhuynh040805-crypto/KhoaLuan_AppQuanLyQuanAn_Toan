using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class MenuManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private Button btnManageCategories;
    private TextBox txtSearch;
    private FlowLayoutPanel flowCategoryTabs;
    private FlowLayoutPanel flowMenuCards;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblSubtitle = new Label();
        btnAddNew = new Button();
        btnManageCategories = new Button();
        txtSearch = new TextBox();
        flowCategoryTabs = new FlowLayoutPanel();
        flowMenuCards = new FlowLayoutPanel();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(19, 15);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(330, 50);
        lblTitle.TabIndex = 6;
        lblTitle.Text = "Quản lý Thực đơn";
        // 
        // lblSubtitle
        // 
        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(19, 65);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(558, 23);
        lblSubtitle.TabIndex = 5;
        lblSubtitle.Text = "Cập nhật và quản lý danh sách món ăn, thức uống, giá cả và trạng thái.";
        // 
        // btnAddNew
        // 
        btnAddNew.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddNew.BackColor = Color.FromArgb(172, 45, 0);
        btnAddNew.FlatAppearance.BorderSize = 0;
        btnAddNew.FlatStyle = FlatStyle.Flat;
        btnAddNew.ForeColor = Color.White;
        btnAddNew.Location = new Point(880, 8);
        btnAddNew.Name = "btnAddNew";
        btnAddNew.Size = new Size(160, 40);
        btnAddNew.TabIndex = 4;
        btnAddNew.Text = "+  Thêm món mới";
        btnAddNew.UseVisualStyleBackColor = false;
        // 
        // btnManageCategories
        // 
        btnManageCategories.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnManageCategories.BackColor = Color.FromArgb(84, 96, 103);
        btnManageCategories.FlatAppearance.BorderSize = 0;
        btnManageCategories.FlatStyle = FlatStyle.Flat;
        btnManageCategories.ForeColor = Color.White;
        btnManageCategories.Location = new Point(696, 8);
        btnManageCategories.Name = "btnManageCategories";
        btnManageCategories.Size = new Size(168, 40);
        btnManageCategories.TabIndex = 3;
        btnManageCategories.Text = "Quản lý loại món";
        btnManageCategories.UseVisualStyleBackColor = false;
        // 
        // txtSearch
        // 
        txtSearch.Font = new Font("Segoe UI", 10F);
        txtSearch.Location = new Point(19, 104);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "Tìm món theo tên...";
        txtSearch.Size = new Size(320, 30);
        txtSearch.TabIndex = 2;
        // 
        // flowCategoryTabs
        // 
        flowCategoryTabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flowCategoryTabs.Location = new Point(19, 140);
        flowCategoryTabs.Name = "flowCategoryTabs";
        flowCategoryTabs.Size = new Size(1060, 34);
        flowCategoryTabs.TabIndex = 1;
        // 
        // flowMenuCards
        // 
        flowMenuCards.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowMenuCards.AutoScroll = true;
        flowMenuCards.Location = new Point(19, 192);
        flowMenuCards.Name = "flowMenuCards";
        flowMenuCards.Size = new Size(1060, 518);
        flowMenuCards.TabIndex = 0;
        // 
        // MenuManagementControl
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(flowMenuCards);
        Controls.Add(flowCategoryTabs);
        Controls.Add(txtSearch);
        Controls.Add(btnManageCategories);
        Controls.Add(btnAddNew);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        Name = "MenuManagementControl";
        Size = new Size(1200, 760);
        ResumeLayout(false);
        PerformLayout();
    }
}