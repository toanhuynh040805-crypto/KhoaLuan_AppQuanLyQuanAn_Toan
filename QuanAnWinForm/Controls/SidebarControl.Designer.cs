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
        this.panelHeader = new Panel();
        this.panelLogo = new Panel();
        this.lblAppName = new Label();
        this.lblBranch = new Label();
        this.panelDivider = new Panel();
        this.flowNavItems = new FlowLayoutPanel();
        this.panelHeader.SuspendLayout();
        this.SuspendLayout();
        //
        // panelLogo
        //
        this.panelLogo.BackColor = AppColors.Primary;
        this.panelLogo.Location = new Point(20, 18);
        this.panelLogo.Size = new Size(40, 40);
        //
        // lblAppName
        //
        this.lblAppName.AutoSize = true;
        this.lblAppName.Font = AppFonts.HeadlineSm;
        this.lblAppName.ForeColor = AppColors.Primary;
        this.lblAppName.Location = new Point(70, 18);
        this.lblAppName.Text = "GustoPOS";
        //
        // lblBranch
        //
        this.lblBranch.AutoSize = true;
        this.lblBranch.Font = AppFonts.BodyMd;
        this.lblBranch.ForeColor = AppColors.OnSurfaceVariant;
        this.lblBranch.Location = new Point(70, 40);
        this.lblBranch.Text = "Chi nhánh chính";
        //
        // panelHeader
        //
        this.panelHeader.Controls.Add(this.panelLogo);
        this.panelHeader.Controls.Add(this.lblAppName);
        this.panelHeader.Controls.Add(this.lblBranch);
        this.panelHeader.Dock = DockStyle.Top;
        this.panelHeader.Height = 76;
        //
        // panelDivider
        //
        this.panelDivider.BackColor = AppColors.OutlineVariant;
        this.panelDivider.Dock = DockStyle.Top;
        this.panelDivider.Height = 1;
        //
        // flowNavItems
        //
        this.flowNavItems.AutoSize = true;
        this.flowNavItems.Dock = DockStyle.Top;
        this.flowNavItems.FlowDirection = FlowDirection.TopDown;
        this.flowNavItems.WrapContents = false;
        this.flowNavItems.Padding = new Padding(12, 12, 12, 12);
        //
        // SidebarControl
        //
        this.BackColor = AppColors.SidebarBg;
        this.Controls.Add(this.flowNavItems);
        this.Controls.Add(this.panelDivider);
        this.Controls.Add(this.panelHeader);
        this.Dock = DockStyle.Left;
        this.Width = 224;
        this.Name = "SidebarControl";
        this.Size = new Size(224, 750);
        this.panelHeader.ResumeLayout(false);
        this.panelHeader.PerformLayout();
        this.ResumeLayout(false);
    }
}
