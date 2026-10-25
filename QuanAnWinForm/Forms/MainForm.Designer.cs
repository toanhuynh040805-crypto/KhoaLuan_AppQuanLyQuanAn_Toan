using QuanAnWinForm.Controls;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private SidebarControl sidebar;
    private HeaderBar headerBar;
    private Panel panelContent;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        sidebar = new SidebarControl();
        headerBar = new HeaderBar();
        panelContent = new Panel();
        SuspendLayout();
        // 
        // sidebar
        // 
        sidebar.BackColor = Color.White;
        sidebar.Dock = DockStyle.Left;
        sidebar.Location = new Point(0, 0);
        sidebar.Name = "sidebar";
        sidebar.Size = new Size(224, 850);
        sidebar.TabIndex = 2;
        // 
        // headerBar
        // 
        headerBar.BackColor = Color.White;
        headerBar.Dock = DockStyle.Top;
        headerBar.Location = new Point(224, 0);
        headerBar.Name = "headerBar";
        headerBar.Size = new Size(1176, 77);
        headerBar.TabIndex = 1;
        // 
        // panelContent
        // 
        panelContent.BackColor = Color.FromArgb(249, 249, 249);
        panelContent.Dock = DockStyle.Fill;
        panelContent.Location = new Point(224, 77);
        panelContent.Name = "panelContent";
        panelContent.Padding = new Padding(32, 24, 32, 24);
        panelContent.Size = new Size(1176, 773);
        panelContent.TabIndex = 0;
        // 
        // MainForm
        // 
        BackColor = Color.FromArgb(249, 249, 249);
        ClientSize = new Size(1400, 850);
        Controls.Add(panelContent);
        Controls.Add(headerBar);
        Controls.Add(sidebar);
        MinimumSize = new Size(1200, 750);
        Name = "MainForm";
        Text = "GustoPOS - Hệ thống quản lý quán ăn";
        WindowState = FormWindowState.Maximized;
        ResumeLayout(false);
    }
}