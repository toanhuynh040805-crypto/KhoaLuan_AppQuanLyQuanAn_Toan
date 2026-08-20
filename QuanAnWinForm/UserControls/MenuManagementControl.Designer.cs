using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class MenuManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private FlowLayoutPanel flowCategoryTabs;
    private FlowLayoutPanel flowMenuCards;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.lblSubtitle = new Label();
        this.btnAddNew = new Button();
        this.flowCategoryTabs = new FlowLayoutPanel();
        this.flowMenuCards = new FlowLayoutPanel();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Text = "Quản lý Thực đơn";
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        //
        // lblSubtitle
        //
        this.lblSubtitle.Text = "Cập nhật và quản lý danh sách món ăn, giá cả và trạng thái.";
        this.lblSubtitle.Font = AppFonts.BodyMd;
        this.lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubtitle.Location = new Point(0, 36);
        this.lblSubtitle.AutoSize = true;
        //
        // btnAddNew
        //
        this.btnAddNew.Text = "+  Thêm món mới";
        this.btnAddNew.Location = new Point(880, 8);
        this.btnAddNew.Size = new Size(160, 40);
        this.btnAddNew.FlatStyle = FlatStyle.Flat;
        this.btnAddNew.BackColor = AppColors.Primary;
        this.btnAddNew.ForeColor = Color.White;
        this.btnAddNew.FlatAppearance.BorderSize = 0;
        //
        // flowCategoryTabs
        //
        this.flowCategoryTabs.Location = new Point(0, 76);
        this.flowCategoryTabs.Size = new Size(700, 34);
        AddTab(this.flowCategoryTabs, "Tất cả món", true);
        AddTab(this.flowCategoryTabs, "Món chính", false);
        AddTab(this.flowCategoryTabs, "Khai vị", false);
        AddTab(this.flowCategoryTabs, "Đồ uống", false);
        AddTab(this.flowCategoryTabs, "Tráng miệng", false);
        //
        // flowMenuCards
        //
        this.flowMenuCards.Location = new Point(0, 120);
        this.flowMenuCards.Size = new Size(1060, 600);
        this.flowMenuCards.AutoScroll = true;
        //
        // MenuManagementControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.flowMenuCards);
        this.Controls.Add(this.flowCategoryTabs);
        this.Controls.Add(this.btnAddNew);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.Name = "MenuManagementControl";
        this.Size = new Size(1060, 700);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void AddTab(FlowLayoutPanel parent, string label, bool selected)
    {
        var lbl = new Label
        {
            Text = label,
            Font = selected ? AppFonts.LabelLg : AppFonts.BodyMd,
            ForeColor = selected ? AppColors.Primary : AppColors.OnSurfaceVariant,
            AutoSize = true,
            Margin = new Padding(0, 6, 24, 6),
            Cursor = Cursors.Hand,
        };
        parent.Controls.Add(lbl);
    }
}
