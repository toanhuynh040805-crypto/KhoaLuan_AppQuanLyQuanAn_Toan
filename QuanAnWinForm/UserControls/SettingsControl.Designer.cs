using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class SettingsControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Panel panelTabsContainer;
    private FlowLayoutPanel flowSettingsTabs;
    private Panel panelSettingsContent;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.panelTabsContainer = new Panel();
        this.flowSettingsTabs = new FlowLayoutPanel();
        this.panelSettingsContent = new Panel();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Text = "Cài đặt hệ thống";
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.ForeColor = AppColors.Primary;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        //
        // flowSettingsTabs
        //
        this.flowSettingsTabs.FlowDirection = FlowDirection.TopDown;
        this.flowSettingsTabs.WrapContents = false;
        this.flowSettingsTabs.Dock = DockStyle.Fill;
        //
        // panelTabsContainer
        //
        this.panelTabsContainer.Location = new Point(0, 50);
        this.panelTabsContainer.Size = new Size(240, 400);
        this.panelTabsContainer.Controls.Add(this.flowSettingsTabs);
        //
        // panelSettingsContent
        //
        this.panelSettingsContent.Location = new Point(256, 50);
        this.panelSettingsContent.Size = new Size(800, 400);
        this.panelSettingsContent.BackColor = Color.White;
        //
        // SettingsControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.panelSettingsContent);
        this.Controls.Add(this.panelTabsContainer);
        this.Controls.Add(this.lblTitle);
        this.Name = "SettingsControl";
        this.Size = new Size(1060, 500);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
