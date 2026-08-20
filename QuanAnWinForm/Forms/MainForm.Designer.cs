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
        this.sidebar = new SidebarControl();
        this.headerBar = new HeaderBar();
        this.panelContent = new Panel();
        this.SuspendLayout();
        //
        // panelContent
        //
        this.panelContent.Dock = DockStyle.Fill;
        this.panelContent.BackColor = AppColors.Background;
        this.panelContent.Padding = new Padding(32, 24, 32, 24);
        //
        // MainForm
        //
        this.ClientSize = new Size(1400, 850);
        this.Controls.Add(this.panelContent);
        this.Controls.Add(this.headerBar);
        this.Controls.Add(this.sidebar);
        this.BackColor = AppColors.Background;
        this.MinimumSize = new Size(1200, 750);
        this.WindowState = FormWindowState.Maximized;
        this.Text = "GustoPOS - Hệ thống quản lý quán ăn";
        this.Name = "MainForm";
        this.ResumeLayout(false);
    }
}
