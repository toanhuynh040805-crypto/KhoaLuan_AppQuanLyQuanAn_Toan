using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

partial class HeaderBar
{
    private System.ComponentModel.IContainer components = null;

    private TextBox txtSearch;
    private FlowLayoutPanel flowRight;
    private Panel panelUserInfo;
    private Panel panelAvatar;
    private Label lblAvatarInitial;
    private Label lblUserName;
    private Label lblUserRole;
    private Button btnHelp;
    private Button btnNotify;
    private Panel panelDivider;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.txtSearch = new TextBox();
        this.flowRight = new FlowLayoutPanel();
        this.panelUserInfo = new Panel();
        this.panelAvatar = new Panel();
        this.lblAvatarInitial = new Label();
        this.lblUserName = new Label();
        this.lblUserRole = new Label();
        this.btnHelp = new Button();
        this.btnNotify = new Button();
        this.panelDivider = new Panel();
        this.SuspendLayout();
        //
        // txtSearch
        //
        this.txtSearch.Location = new Point(24, 14);
        this.txtSearch.Size = new Size(320, 30);
        this.txtSearch.Font = AppFonts.BodyMd;
        this.txtSearch.BorderStyle = BorderStyle.FixedSingle;
        this.txtSearch.PlaceholderText = "Tìm kiếm...";
        //
        // panelAvatar
        //
        this.panelAvatar.BackColor = AppColors.SecondaryContainer;
        this.panelAvatar.Location = new Point(114, 2);
        this.panelAvatar.Size = new Size(36, 36);
        this.panelAvatar.Controls.Add(this.lblAvatarInitial);
        //
        // lblAvatarInitial
        //
        this.lblAvatarInitial.Dock = DockStyle.Fill;
        this.lblAvatarInitial.TextAlign = ContentAlignment.MiddleCenter;
        this.lblAvatarInitial.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        this.lblAvatarInitial.ForeColor = AppColors.Secondary;
        //
        // lblUserName
        //
        this.lblUserName.AutoSize = true;
        this.lblUserName.Font = AppFonts.LabelLg;
        this.lblUserName.Location = new Point(0, 4);
        //
        // lblUserRole
        //
        this.lblUserRole.AutoSize = true;
        this.lblUserRole.Font = AppFonts.BodyMd;
        this.lblUserRole.ForeColor = AppColors.OnSurfaceVariant;
        this.lblUserRole.Location = new Point(0, 22);
        //
        // panelUserInfo
        //
        this.panelUserInfo.Size = new Size(150, 40);
        this.panelUserInfo.Controls.Add(this.panelAvatar);
        this.panelUserInfo.Controls.Add(this.lblUserName);
        this.panelUserInfo.Controls.Add(this.lblUserRole);
        //
        // btnHelp
        //
        this.btnHelp.Text = "?";
        this.btnHelp.Size = new Size(36, 36);
        this.btnHelp.FlatStyle = FlatStyle.Flat;
        this.btnHelp.ForeColor = AppColors.OnSurfaceVariant;
        this.btnHelp.Margin = new Padding(6, 2, 6, 2);
        this.btnHelp.Cursor = Cursors.Hand;
        this.btnHelp.FlatAppearance.BorderSize = 0;
        this.btnHelp.FlatAppearance.MouseOverBackColor = AppColors.SurfaceLow;
        //
        // btnNotify
        //
        this.btnNotify.Text = "🔔";
        this.btnNotify.Size = new Size(36, 36);
        this.btnNotify.FlatStyle = FlatStyle.Flat;
        this.btnNotify.ForeColor = AppColors.OnSurfaceVariant;
        this.btnNotify.Margin = new Padding(6, 2, 6, 2);
        this.btnNotify.Cursor = Cursors.Hand;
        this.btnNotify.FlatAppearance.BorderSize = 0;
        this.btnNotify.FlatAppearance.MouseOverBackColor = AppColors.SurfaceLow;
        //
        // flowRight
        //
        this.flowRight.FlowDirection = FlowDirection.RightToLeft;
        this.flowRight.Dock = DockStyle.Right;
        this.flowRight.Width = 420;
        this.flowRight.Padding = new Padding(0, 10, 24, 0);
        this.flowRight.Controls.Add(this.panelUserInfo);
        this.flowRight.Controls.Add(this.btnHelp);
        this.flowRight.Controls.Add(this.btnNotify);
        //
        // panelDivider
        //
        this.panelDivider.BackColor = AppColors.OutlineVariant;
        this.panelDivider.Dock = DockStyle.Bottom;
        this.panelDivider.Height = 1;
        //
        // HeaderBar
        //
        this.BackColor = Color.White;
        this.Controls.Add(this.txtSearch);
        this.Controls.Add(this.flowRight);
        this.Controls.Add(this.panelDivider);
        this.Dock = DockStyle.Top;
        this.Height = 64;
        this.Name = "HeaderBar";
        this.Size = new Size(1200, 64);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
