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
        lblTitle = new Label();
        panelTabsContainer = new Panel();
        flowSettingsTabs = new FlowLayoutPanel();
        panelSettingsContent = new Panel();
        panelTabsContainer.SuspendLayout();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(172, 45, 0);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(309, 50);
        lblTitle.TabIndex = 2;
        lblTitle.Text = "Cài đặt hệ thống";
        // 
        // panelTabsContainer
        // 
        panelTabsContainer.Controls.Add(flowSettingsTabs);
        panelTabsContainer.Location = new Point(0, 50);
        panelTabsContainer.Name = "panelTabsContainer";
        panelTabsContainer.Size = new Size(240, 400);
        panelTabsContainer.TabIndex = 1;
        // 
        // flowSettingsTabs
        // 
        flowSettingsTabs.Dock = DockStyle.Fill;
        flowSettingsTabs.FlowDirection = FlowDirection.TopDown;
        flowSettingsTabs.Location = new Point(0, 0);
        flowSettingsTabs.Name = "flowSettingsTabs";
        flowSettingsTabs.Size = new Size(240, 400);
        flowSettingsTabs.TabIndex = 0;
        flowSettingsTabs.WrapContents = false;
        // 
        // panelSettingsContent
        // 
        panelSettingsContent.BackColor = Color.White;
        panelSettingsContent.Location = new Point(256, 50);
        panelSettingsContent.Name = "panelSettingsContent";
        panelSettingsContent.Size = new Size(783, 400);
        panelSettingsContent.TabIndex = 0;
        // 
        // SettingsControl
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(panelSettingsContent);
        Controls.Add(panelTabsContainer);
        Controls.Add(lblTitle);
        Name = "SettingsControl";
        Size = new Size(1141, 582);
        panelTabsContainer.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
