using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel panelCard;
    private Panel panelLogo;
    private Label lblLogoIcon;
    private Label lblHeading;
    private Label lblSubheading;
    private Label lblUsernameCaption;
    private TextBox txtUsername;
    private Label lblPasswordCaption;
    private TextBox txtPassword;
    private Label lblError;
    private Button btnLogin;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.panelCard = new Panel();
        this.panelLogo = new Panel();
        this.lblLogoIcon = new Label();
        this.lblHeading = new Label();
        this.lblSubheading = new Label();
        this.lblUsernameCaption = new Label();
        this.txtUsername = new TextBox();
        this.lblPasswordCaption = new Label();
        this.txtPassword = new TextBox();
        this.lblError = new Label();
        this.btnLogin = new Button();
        this.panelCard.SuspendLayout();
        this.SuspendLayout();
        //
        // panelLogo
        //
        this.panelLogo.BackColor = AppColors.Primary;
        this.panelLogo.Location = new Point(150, 24);
        this.panelLogo.Size = new Size(56, 56);
        this.panelLogo.Controls.Add(this.lblLogoIcon);
        //
        // lblLogoIcon
        //
        this.lblLogoIcon.Dock = DockStyle.Fill;
        this.lblLogoIcon.TextAlign = ContentAlignment.MiddleCenter;
        this.lblLogoIcon.Font = new Font("Segoe UI", 20);
        this.lblLogoIcon.ForeColor = Color.White;
        this.lblLogoIcon.Text = "🍴";
        //
        // lblHeading
        //
        this.lblHeading.Font = AppFonts.HeadlineMd;
        this.lblHeading.Location = new Point(0, 92);
        this.lblHeading.Size = new Size(356, 30);
        this.lblHeading.TextAlign = ContentAlignment.MiddleCenter;
        this.lblHeading.Text = "Đăng nhập Hệ thống";
        //
        // lblSubheading
        //
        this.lblSubheading.Font = AppFonts.BodyMd;
        this.lblSubheading.ForeColor = AppColors.OnSurfaceVariant;
        this.lblSubheading.Location = new Point(0, 124);
        this.lblSubheading.Size = new Size(356, 22);
        this.lblSubheading.TextAlign = ContentAlignment.MiddleCenter;
        this.lblSubheading.Text = "Dành cho nhân viên quán ăn";
        //
        // lblUsernameCaption
        //
        this.lblUsernameCaption.Font = AppFonts.LabelLg;
        this.lblUsernameCaption.Location = new Point(28, 168);
        this.lblUsernameCaption.AutoSize = true;
        this.lblUsernameCaption.Text = "Tài khoản";
        //
        // txtUsername
        //
        this.txtUsername.Location = new Point(28, 190);
        this.txtUsername.Size = new Size(300, 30);
        this.txtUsername.Font = AppFonts.BodyLg;
        this.txtUsername.BorderStyle = BorderStyle.FixedSingle;
        this.txtUsername.PlaceholderText = "VD: admin";
        //
        // lblPasswordCaption
        //
        this.lblPasswordCaption.Font = AppFonts.LabelLg;
        this.lblPasswordCaption.Location = new Point(28, 232);
        this.lblPasswordCaption.AutoSize = true;
        this.lblPasswordCaption.Text = "Mật khẩu";
        //
        // txtPassword
        //
        this.txtPassword.Location = new Point(28, 254);
        this.txtPassword.Size = new Size(300, 30);
        this.txtPassword.Font = AppFonts.BodyLg;
        this.txtPassword.BorderStyle = BorderStyle.FixedSingle;
        this.txtPassword.PasswordChar = '●';
        //
        // lblError
        //
        this.lblError.ForeColor = AppColors.Error;
        this.lblError.Font = AppFonts.BodyMd;
        this.lblError.Location = new Point(28, 292);
        this.lblError.Size = new Size(300, 20);
        this.lblError.Visible = false;
        //
        // btnLogin
        //
        this.btnLogin.Text = "Vào ca";
        this.btnLogin.Location = new Point(28, 320);
        this.btnLogin.Size = new Size(300, 46);
        this.btnLogin.FlatStyle = FlatStyle.Flat;
        this.btnLogin.BackColor = AppColors.Primary;
        this.btnLogin.ForeColor = Color.White;
        this.btnLogin.Font = AppFonts.LabelLg;
        this.btnLogin.FlatAppearance.BorderSize = 0;
        this.btnLogin.Cursor = Cursors.Hand;
        //
        // panelCard
        //
        this.panelCard.BackColor = Color.White;
        this.panelCard.Location = new Point(0, 0);
        this.panelCard.Size = new Size(356, 400);
        this.panelCard.Controls.Add(this.panelLogo);
        this.panelCard.Controls.Add(this.lblHeading);
        this.panelCard.Controls.Add(this.lblSubheading);
        this.panelCard.Controls.Add(this.lblUsernameCaption);
        this.panelCard.Controls.Add(this.txtUsername);
        this.panelCard.Controls.Add(this.lblPasswordCaption);
        this.panelCard.Controls.Add(this.txtPassword);
        this.panelCard.Controls.Add(this.lblError);
        this.panelCard.Controls.Add(this.btnLogin);
        this.panelCard.Anchor = AnchorStyles.None;
        //
        // LoginForm
        //
        this.ClientSize = new Size(900, 600);
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.panelCard);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Đăng nhập - Hệ thống quản lý quán ăn";
        this.Name = "LoginForm";
        this.panelCard.ResumeLayout(false);
        this.panelCard.PerformLayout();
        this.ResumeLayout(false);
    }
}
