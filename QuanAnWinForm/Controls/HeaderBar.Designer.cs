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
    private Label lblNotifyBadge;
    private Panel panelDivider;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        txtSearch = new TextBox();
        flowRight = new FlowLayoutPanel();
        panelUserInfo = new Panel();
        panelAvatar = new Panel();
        lblAvatarInitial = new Label();
        lblUserName = new Label();
        lblUserRole = new Label();
        btnHelp = new Button();
        btnNotify = new Button();
        lblNotifyBadge = new Label();
        panelDivider = new Panel();
        flowRight.SuspendLayout();
        panelUserInfo.SuspendLayout();
        panelAvatar.SuspendLayout();
        btnNotify.SuspendLayout();
        SuspendLayout();
        // 
        // txtSearch
        // 
        txtSearch.BorderStyle = BorderStyle.FixedSingle;
        txtSearch.Font = new Font("Segoe UI", 10F);
        txtSearch.Location = new Point(24, 14);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "Tìm kiếm...";
        txtSearch.Size = new Size(320, 30);
        txtSearch.TabIndex = 0;
        // 
        // flowRight
        // 
        flowRight.Controls.Add(panelUserInfo);
        flowRight.Controls.Add(btnHelp);
        flowRight.Controls.Add(btnNotify);
        flowRight.Dock = DockStyle.Right;
        flowRight.FlowDirection = FlowDirection.RightToLeft;
        flowRight.Location = new Point(1289, 0);
        flowRight.Name = "flowRight";
        flowRight.Padding = new Padding(0, 10, 24, 0);
        flowRight.Size = new Size(420, 63);
        flowRight.TabIndex = 1;
        // 
        // panelUserInfo
        // 
        panelUserInfo.Controls.Add(panelAvatar);
        panelUserInfo.Controls.Add(lblUserName);
        panelUserInfo.Controls.Add(lblUserRole);
        panelUserInfo.Location = new Point(243, 13);
        panelUserInfo.Name = "panelUserInfo";
        panelUserInfo.Size = new Size(150, 40);
        panelUserInfo.TabIndex = 0;
        // 
        // panelAvatar
        // 
        panelAvatar.BackColor = Color.FromArgb(215, 228, 236);
        panelAvatar.Controls.Add(lblAvatarInitial);
        panelAvatar.Location = new Point(114, 2);
        panelAvatar.Name = "panelAvatar";
        panelAvatar.Size = new Size(36, 36);
        panelAvatar.TabIndex = 0;
        // 
        // lblAvatarInitial
        // 
        lblAvatarInitial.Dock = DockStyle.Fill;
        lblAvatarInitial.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblAvatarInitial.ForeColor = Color.FromArgb(84, 96, 103);
        lblAvatarInitial.Location = new Point(0, 0);
        lblAvatarInitial.Name = "lblAvatarInitial";
        lblAvatarInitial.Size = new Size(36, 36);
        lblAvatarInitial.TabIndex = 0;
        lblAvatarInitial.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblUserName
        // 
        lblUserName.AutoSize = true;
        lblUserName.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblUserName.Location = new Point(0, 4);
        lblUserName.Name = "lblUserName";
        lblUserName.Size = new Size(0, 23);
        lblUserName.TabIndex = 1;
        // 
        // lblUserRole
        // 
        lblUserRole.AutoSize = true;
        lblUserRole.Font = new Font("Segoe UI", 10F);
        lblUserRole.ForeColor = Color.FromArgb(91, 65, 57);
        lblUserRole.Location = new Point(0, 22);
        lblUserRole.Name = "lblUserRole";
        lblUserRole.Size = new Size(0, 23);
        lblUserRole.TabIndex = 2;
        // 
        // btnHelp
        // 
        btnHelp.Cursor = Cursors.Hand;
        btnHelp.FlatAppearance.BorderSize = 0;
        btnHelp.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 243, 243);
        btnHelp.FlatStyle = FlatStyle.Flat;
        btnHelp.ForeColor = Color.FromArgb(91, 65, 57);
        btnHelp.Location = new Point(198, 12);
        btnHelp.Margin = new Padding(6, 2, 6, 2);
        btnHelp.Name = "btnHelp";
        btnHelp.Size = new Size(36, 36);
        btnHelp.TabIndex = 1;
        btnHelp.Text = "?";
        // 
        // btnNotify
        // 
        btnNotify.Controls.Add(lblNotifyBadge);
        btnNotify.Cursor = Cursors.Hand;
        btnNotify.FlatAppearance.BorderSize = 0;
        btnNotify.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 243, 243);
        btnNotify.FlatStyle = FlatStyle.Flat;
        btnNotify.ForeColor = Color.FromArgb(91, 65, 57);
        btnNotify.Location = new Point(150, 12);
        btnNotify.Margin = new Padding(6, 2, 6, 2);
        btnNotify.Name = "btnNotify";
        btnNotify.Size = new Size(36, 36);
        btnNotify.TabIndex = 2;
        btnNotify.Text = "🔔";
        // 
        // lblNotifyBadge
        // 
        lblNotifyBadge.BackColor = Color.FromArgb(211, 47, 47);
        lblNotifyBadge.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
        lblNotifyBadge.ForeColor = Color.White;
        lblNotifyBadge.Location = new Point(20, 0);
        lblNotifyBadge.Name = "lblNotifyBadge";
        lblNotifyBadge.Size = new Size(16, 16);
        lblNotifyBadge.TabIndex = 0;
        lblNotifyBadge.Text = "0";
        lblNotifyBadge.TextAlign = ContentAlignment.MiddleCenter;
        lblNotifyBadge.Visible = false;
        // 
        // panelDivider
        // 
        panelDivider.BackColor = Color.FromArgb(224, 224, 224);
        panelDivider.Dock = DockStyle.Bottom;
        panelDivider.Location = new Point(0, 63);
        panelDivider.Name = "panelDivider";
        panelDivider.Size = new Size(1709, 1);
        panelDivider.TabIndex = 2;
        // 
        // HeaderBar
        // 
        BackColor = Color.White;
        Controls.Add(txtSearch);
        Controls.Add(flowRight);
        Controls.Add(panelDivider);
        Name = "HeaderBar";
        Size = new Size(1709, 64);
        flowRight.ResumeLayout(false);
        panelUserInfo.ResumeLayout(false);
        panelUserInfo.PerformLayout();
        panelAvatar.ResumeLayout(false);
        btnNotify.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}