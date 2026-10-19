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
        panelCard = new Panel();
        panelLogo = new Panel();
        lblLogoIcon = new Label();
        lblHeading = new Label();
        lblSubheading = new Label();
        lblUsernameCaption = new Label();
        txtUsername = new TextBox();
        lblPasswordCaption = new Label();
        txtPassword = new TextBox();
        lblError = new Label();
        btnLogin = new Button();
        panelCard.SuspendLayout();
        panelLogo.SuspendLayout();
        SuspendLayout();
        // 
        // panelCard
        // 
        panelCard.Anchor = AnchorStyles.None;
        panelCard.BackColor = Color.White;
        panelCard.Controls.Add(panelLogo);
        panelCard.Controls.Add(lblHeading);
        panelCard.Controls.Add(lblSubheading);
        panelCard.Controls.Add(lblUsernameCaption);
        panelCard.Controls.Add(txtUsername);
        panelCard.Controls.Add(lblPasswordCaption);
        panelCard.Controls.Add(txtPassword);
        panelCard.Controls.Add(lblError);
        panelCard.Controls.Add(btnLogin);
        panelCard.Location = new Point(0, 0);
        panelCard.Name = "panelCard";
        panelCard.Size = new Size(356, 400);
        panelCard.TabIndex = 0;
        // 
        // panelLogo
        // 
        panelLogo.BackColor = Color.FromArgb(172, 45, 0);
        panelLogo.Controls.Add(lblLogoIcon);
        panelLogo.Location = new Point(150, 24);
        panelLogo.Name = "panelLogo";
        panelLogo.Size = new Size(56, 56);
        panelLogo.TabIndex = 0;
        // 
        // lblLogoIcon
        // 
        lblLogoIcon.Dock = DockStyle.Fill;
        lblLogoIcon.Font = new Font("Segoe UI", 20F);
        lblLogoIcon.ForeColor = Color.White;
        lblLogoIcon.Location = new Point(0, 0);
        lblLogoIcon.Name = "lblLogoIcon";
        lblLogoIcon.Size = new Size(56, 56);
        lblLogoIcon.TabIndex = 0;
        lblLogoIcon.Text = "🍴";
        lblLogoIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblHeading
        // 
        lblHeading.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblHeading.Location = new Point(0, 92);
        lblHeading.Name = "lblHeading";
        lblHeading.Size = new Size(356, 41);
        lblHeading.TabIndex = 1;
        lblHeading.Text = "Đăng nhập Hệ thống";
        lblHeading.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblSubheading
        // 
        lblSubheading.Font = new Font("Segoe UI", 10F);
        lblSubheading.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubheading.Location = new Point(-3, 133);
        lblSubheading.Name = "lblSubheading";
        lblSubheading.Size = new Size(356, 22);
        lblSubheading.TabIndex = 2;
        lblSubheading.Text = "Dành cho nhân viên quán ăn";
        lblSubheading.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblUsernameCaption
        // 
        lblUsernameCaption.AutoSize = true;
        lblUsernameCaption.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblUsernameCaption.Location = new Point(28, 164);
        lblUsernameCaption.Name = "lblUsernameCaption";
        lblUsernameCaption.Size = new Size(87, 23);
        lblUsernameCaption.TabIndex = 3;
        lblUsernameCaption.Text = "Tài khoản";
        // 
        // txtUsername
        // 
        txtUsername.BorderStyle = BorderStyle.FixedSingle;
        txtUsername.Font = new Font("Segoe UI", 11F);
        txtUsername.Location = new Point(28, 190);
        txtUsername.Name = "txtUsername";
        txtUsername.PlaceholderText = "VD: admin";
        txtUsername.Size = new Size(300, 32);
        txtUsername.TabIndex = 4;
        // 
        // lblPasswordCaption
        // 
        lblPasswordCaption.AutoSize = true;
        lblPasswordCaption.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblPasswordCaption.Location = new Point(28, 232);
        lblPasswordCaption.Name = "lblPasswordCaption";
        lblPasswordCaption.Size = new Size(86, 23);
        lblPasswordCaption.TabIndex = 5;
        lblPasswordCaption.Text = "Mật khẩu";
        // 
        // txtPassword
        // 
        txtPassword.BorderStyle = BorderStyle.FixedSingle;
        txtPassword.Font = new Font("Segoe UI", 11F);
        txtPassword.Location = new Point(28, 258);
        txtPassword.Name = "txtPassword";
        txtPassword.PasswordChar = '●';
        txtPassword.Size = new Size(300, 32);
        txtPassword.TabIndex = 6;
        // 
        // lblError
        // 
        lblError.Font = new Font("Segoe UI", 10F);
        lblError.ForeColor = Color.FromArgb(186, 26, 26);
        lblError.Location = new Point(28, 292);
        lblError.Name = "lblError";
        lblError.Size = new Size(300, 20);
        lblError.TabIndex = 7;
        lblError.Visible = false;
        // 
        // btnLogin
        // 
        btnLogin.BackColor = Color.FromArgb(172, 45, 0);
        btnLogin.Cursor = Cursors.Hand;
        btnLogin.FlatAppearance.BorderSize = 0;
        btnLogin.FlatStyle = FlatStyle.Flat;
        btnLogin.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnLogin.ForeColor = Color.White;
        btnLogin.Location = new Point(28, 320);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(300, 46);
        btnLogin.TabIndex = 8;
        btnLogin.Text = "Vào ca";
        btnLogin.UseVisualStyleBackColor = false;
        // 
        // LoginForm
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        ClientSize = new Size(900, 600);
        Controls.Add(panelCard);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Đăng nhập - Hệ thống quản lý quán ăn";
        panelCard.ResumeLayout(false);
        panelCard.PerformLayout();
        panelLogo.ResumeLayout(false);
        ResumeLayout(false);
    }
}
