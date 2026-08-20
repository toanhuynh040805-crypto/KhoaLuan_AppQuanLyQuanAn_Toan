using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class StaffControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private Button btnAddNew;
    private StatCard cardTotalStaff;
    private StatCard cardWorking;
    private FlowLayoutPanel flowStaffCards;

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
        this.cardTotalStaff = new StatCard();
        this.cardWorking = new StatCard();
        this.flowStaffCards = new FlowLayoutPanel();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Text = "Quản lý Nhân viên";
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        //
        // lblSubtitle
        //
        this.lblSubtitle.Text = "Quản lý danh sách, vai trò và phân quyền hệ thống";
        this.lblSubtitle.Font = AppFonts.BodyMd;
        this.lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubtitle.Location = new Point(0, 36);
        this.lblSubtitle.AutoSize = true;
        //
        // btnAddNew
        //
        this.btnAddNew.Text = "+  Thêm nhân viên mới";
        this.btnAddNew.Location = new Point(830, 8);
        this.btnAddNew.Size = new Size(200, 40);
        this.btnAddNew.FlatStyle = FlatStyle.Flat;
        this.btnAddNew.BackColor = AppColors.Primary;
        this.btnAddNew.ForeColor = Color.White;
        this.btnAddNew.FlatAppearance.BorderSize = 0;
        //
        // cardTotalStaff / cardWorking
        //
        this.cardTotalStaff.Configure("👥", AppColors.Primary, AppColors.ErrorContainer, "TỔNG NHÂN SỰ", "42");
        this.cardTotalStaff.Location = new Point(0, 76);
        this.cardWorking.Configure("💼", AppColors.Tertiary, Color.FromArgb(230, 245, 240), "ĐANG LÀM VIỆC", "18");
        this.cardWorking.Location = new Point(230, 76);
        //
        // flowStaffCards
        //
        this.flowStaffCards.Location = new Point(0, 220);
        this.flowStaffCards.Size = new Size(1060, 500);
        this.flowStaffCards.AutoScroll = true;
        //
        // StaffControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.flowStaffCards);
        this.Controls.Add(this.cardWorking);
        this.Controls.Add(this.cardTotalStaff);
        this.Controls.Add(this.btnAddNew);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.Name = "StaffControl";
        this.Size = new Size(1060, 700);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
