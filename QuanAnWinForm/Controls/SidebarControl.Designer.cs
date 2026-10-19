using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

partial class SidebarControl
{
    private System.ComponentModel.IContainer components = null;

    private Panel panelHeader;
    private Panel panelLogo;
    private Label lblAppName;
    private Label lblBranch;
    private Panel panelDivider;
    private FlowLayoutPanel flowNavItems;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        panelHeader = new Panel();
        panelLogo = new Panel();
        lblAppName = new Label();
        lblBranch = new Label();
        panelDivider = new Panel();
        flowNavItems = new FlowLayoutPanel();
        panelHeader.SuspendLayout();
        SuspendLayout();
        // 
        // panelHeader
        // 
        panelHeader.Controls.Add(panelLogo);
        panelHeader.Controls.Add(lblAppName);
        panelHeader.Controls.Add(lblBranch);
        panelHeader.Dock = DockStyle.Top;
        panelHeader.Location = new Point(0, 0);
        panelHeader.Name = "panelHeader";
        panelHeader.Size = new Size(1546, 76);
        panelHeader.TabIndex = 2;
        // 
        // panelLogo
        // 
        panelLogo.BackColor = Color.FromArgb(172, 45, 0);
        panelLogo.Location = new Point(20, 18);
        panelLogo.Name = "panelLogo";
        panelLogo.Size = new Size(40, 40);
        panelLogo.TabIndex = 0;
        // 
        // lblAppName
        // 
        lblAppName.AutoSize = true;
        lblAppName.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblAppName.ForeColor = Color.FromArgb(172, 45, 0);
        lblAppName.Location = new Point(70, 18);
        lblAppName.Name = "lblAppName";
        lblAppName.Size = new Size(117, 30);
        lblAppName.TabIndex = 1;
        lblAppName.Text = "GustoPOS";
        // 
        // lblBranch
        // 
        lblBranch.AutoSize = true;
        lblBranch.Font = new Font("Segoe UI", 10F);
        lblBranch.ForeColor = Color.FromArgb(91, 65, 57);
        lblBranch.Location = new Point(70, 48);
        lblBranch.Name = "lblBranch";
        lblBranch.Size = new Size(136, 23);
        lblBranch.TabIndex = 2;
        lblBranch.Text = "Chi nhánh chính";
        // 
        // panelDivider
        // 
        panelDivider.BackColor = Color.FromArgb(224, 224, 224);
        panelDivider.Dock = DockStyle.Top;
        panelDivider.Location = new Point(0, 76);
        panelDivider.Name = "panelDivider";
        panelDivider.Size = new Size(1546, 1);
        panelDivider.TabIndex = 1;
        // 
        // flowNavItems
        // 
        flowNavItems.AutoSize = true;
        flowNavItems.Dock = DockStyle.Top;
        flowNavItems.FlowDirection = FlowDirection.TopDown;
        flowNavItems.Location = new Point(0, 77);
        flowNavItems.Name = "flowNavItems";
        flowNavItems.Padding = new Padding(12);
        flowNavItems.Size = new Size(1546, 24);
        flowNavItems.TabIndex = 0;
        flowNavItems.WrapContents = false;
        // 
        // SidebarControl
        // 
        BackColor = Color.White;
        Controls.Add(flowNavItems);
        Controls.Add(panelDivider);
        Controls.Add(panelHeader);
        Name = "SidebarControl";
        Size = new Size(1546, 747);
        panelHeader.ResumeLayout(false);
        panelHeader.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
