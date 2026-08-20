using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

partial class TableManagementControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private FlowLayoutPanel flowLegend;
    private FlowLayoutPanel flowTables;
    private Panel panelDetail;
    private Label lblDetailName;
    private Label lblDetailStatus;
    private Label lblDetailInfo;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new Label();
        this.flowLegend = new FlowLayoutPanel();
        this.flowTables = new FlowLayoutPanel();
        this.panelDetail = new Panel();
        this.lblDetailName = new Label();
        this.lblDetailStatus = new Label();
        this.lblDetailInfo = new Label();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Text = "Sơ đồ bàn";
        this.lblTitle.Font = AppFonts.HeadlineLg;
        this.lblTitle.Location = new Point(0, 0);
        this.lblTitle.AutoSize = true;
        //
        // flowLegend
        //
        this.flowLegend.Location = new Point(0, 44);
        this.flowLegend.Size = new Size(500, 30);
        AddLegendItem(this.flowLegend, "Trống", AppColors.TableTrong);
        AddLegendItem(this.flowLegend, "Có khách", AppColors.TableCoKhach);
        AddLegendItem(this.flowLegend, "Đã đặt", AppColors.Warning);
        //
        // flowTables
        //
        this.flowTables.Location = new Point(0, 84);
        this.flowTables.Size = new Size(760, 600);
        this.flowTables.AutoScroll = true;
        //
        // lblDetailName
        //
        this.lblDetailName.Font = AppFonts.HeadlineMd;
        this.lblDetailName.Location = new Point(16, 16);
        this.lblDetailName.AutoSize = true;
        //
        // lblDetailStatus
        //
        this.lblDetailStatus.Font = AppFonts.LabelLg;
        this.lblDetailStatus.ForeColor = AppColors.Primary;
        this.lblDetailStatus.Location = new Point(16, 48);
        this.lblDetailStatus.AutoSize = true;
        //
        // lblDetailInfo
        //
        this.lblDetailInfo.Font = AppFonts.BodyMd;
        this.lblDetailInfo.ForeColor = AppColors.OnSurfaceVariant;
        this.lblDetailInfo.Location = new Point(16, 76);
        this.lblDetailInfo.AutoSize = true;
        //
        // panelDetail
        //
        this.panelDetail.BackColor = Color.White;
        this.panelDetail.Dock = DockStyle.Right;
        this.panelDetail.Width = 300;
        this.panelDetail.Visible = false;
        this.panelDetail.Controls.Add(this.lblDetailInfo);
        this.panelDetail.Controls.Add(this.lblDetailStatus);
        this.panelDetail.Controls.Add(this.lblDetailName);
        //
        // TableManagementControl
        //
        this.BackColor = AppColors.Background;
        this.Controls.Add(this.flowTables);
        this.Controls.Add(this.flowLegend);
        this.Controls.Add(this.lblTitle);
        this.Controls.Add(this.panelDetail);
        this.Name = "TableManagementControl";
        this.Size = new Size(1060, 700);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void AddLegendItem(FlowLayoutPanel parent, string label, Color color)
    {
        var dot = new Panel { Size = new Size(10, 10), BackColor = color, Margin = new Padding(0, 10, 4, 0) };
        var lbl = new Label { Text = label, Font = AppFonts.BodyMd, AutoSize = true, Margin = new Padding(0, 6, 16, 0) };
        parent.Controls.Add(dot);
        parent.Controls.Add(lbl);
    }
}
