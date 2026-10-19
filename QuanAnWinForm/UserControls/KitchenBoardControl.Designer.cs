namespace QuanAnWinForm.UserControls;

partial class KitchenBoardControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Label lblSubtitle;
    private Panel panelLegend;
    private Panel dotNew;
    private Panel dotWaiting;
    private Panel dotLate;
    private Label lblLegendNew;
    private Label lblLegendWaiting;
    private Label lblLegendLate;
    private Label lblEmptyState;
    private FlowLayoutPanel flowColumns;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblSubtitle = new Label();
        panelLegend = new Panel();
        dotNew = new Panel();
        dotWaiting = new Panel();
        dotLate = new Panel();
        lblLegendNew = new Label();
        lblLegendWaiting = new Label();
        lblLegendLate = new Label();
        lblEmptyState = new Label();
        flowColumns = new FlowLayoutPanel();
        SuspendLayout();

        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(263, 50);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Màn hình Bếp";

        lblSubtitle.AutoSize = true;
        lblSubtitle.Font = new Font("Segoe UI", 10F);
        lblSubtitle.ForeColor = Color.FromArgb(91, 65, 57);
        lblSubtitle.Location = new Point(0, 52);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(402, 23);
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Hiển thị món ăn: Mới gọi (Bắt đầu) -> Đang chế biến (Xong)";

        panelLegend.Location = new Point(0, 84);
        panelLegend.Name = "panelLegend";
        panelLegend.Size = new Size(680, 26);
        panelLegend.TabIndex = 2;
        panelLegend.Controls.Add(dotNew);
        panelLegend.Controls.Add(dotWaiting);
        panelLegend.Controls.Add(dotLate);
        panelLegend.Controls.Add(lblLegendNew);
        panelLegend.Controls.Add(lblLegendWaiting);
        panelLegend.Controls.Add(lblLegendLate);

        dotNew.BackColor = Color.FromArgb(25, 131, 116);
        dotNew.Location = new Point(0, 7);
        dotNew.Name = "dotNew";
        dotNew.Size = new Size(12, 12);

        dotWaiting.BackColor = Color.FromArgb(245, 158, 11);
        dotWaiting.Location = new Point(200, 7);
        dotWaiting.Name = "dotWaiting";
        dotWaiting.Size = new Size(12, 12);

        dotLate.BackColor = Color.FromArgb(186, 26, 26);
        dotLate.Location = new Point(440, 7);
        dotLate.Name = "dotLate";
        dotLate.Size = new Size(12, 12);

        lblLegendNew.AutoSize = true;
        lblLegendNew.Font = new Font("Segoe UI", 9F);
        lblLegendNew.ForeColor = Color.FromArgb(91, 65, 57);
        lblLegendNew.Location = new Point(20, 4);
        lblLegendNew.Name = "lblLegendNew";
        lblLegendNew.Size = new Size(140, 20);
        lblLegendNew.TabIndex = 0;
        lblLegendNew.Text = "Mới (dưới 5 phút)";

        lblLegendWaiting.AutoSize = true;
        lblLegendWaiting.Font = new Font("Segoe UI", 9F);
        lblLegendWaiting.ForeColor = Color.FromArgb(91, 65, 57);
        lblLegendWaiting.Location = new Point(220, 4);
        lblLegendWaiting.Name = "lblLegendWaiting";
        lblLegendWaiting.Size = new Size(160, 20);
        lblLegendWaiting.TabIndex = 1;
        lblLegendWaiting.Text = "Đang chờ (5-10 phút)";

        lblLegendLate.AutoSize = true;
        lblLegendLate.Font = new Font("Segoe UI", 9F);
        lblLegendLate.ForeColor = Color.FromArgb(91, 65, 57);
        lblLegendLate.Location = new Point(460, 4);
        lblLegendLate.Name = "lblLegendLate";
        lblLegendLate.Size = new Size(140, 20);
        lblLegendLate.TabIndex = 2;
        lblLegendLate.Text = "Trễ (trên 10 phút)";

        lblEmptyState.AutoSize = true;
        lblEmptyState.Font = new Font("Segoe UI", 12F);
        lblEmptyState.ForeColor = Color.FromArgb(120, 120, 120);
        lblEmptyState.Location = new Point(0, 140);
        lblEmptyState.Name = "lblEmptyState";
        lblEmptyState.Size = new Size(360, 27);
        lblEmptyState.TabIndex = 3;
        lblEmptyState.Text = "✓ Không có món nào đang chờ chế biến.";
        lblEmptyState.Visible = false;

        flowColumns.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flowColumns.AutoScroll = true;
        flowColumns.FlowDirection = FlowDirection.LeftToRight;
        flowColumns.WrapContents = true;
        flowColumns.Location = new Point(0, 120);
        flowColumns.Name = "flowColumns";
        flowColumns.Size = new Size(1060, 620);
        flowColumns.TabIndex = 4;

        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(flowColumns);
        Controls.Add(lblEmptyState);
        Controls.Add(panelLegend);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        Name = "KitchenBoardControl";
        Size = new Size(1060, 750);
        ResumeLayout(false);
        PerformLayout();
    }
}