namespace QuanAnWinForm.UserControls;

partial class PromotionsControl
{
    private System.ComponentModel.IContainer components = null;

    private Label lblTitle;
    private Button btnAddCampaign;
    private FlowLayoutPanel flowCampaigns;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        btnAddCampaign = new Button();
        flowCampaigns = new FlowLayoutPanel();
        SuspendLayout();
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(17, 9);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(168, 37);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Khuyến mãi";
        // 
        // btnAddCampaign
        // 
        btnAddCampaign.BackColor = Color.FromArgb(46, 125, 115);
        btnAddCampaign.FlatAppearance.BorderSize = 0;
        btnAddCampaign.FlatStyle = FlatStyle.Flat;
        btnAddCampaign.Font = new Font("Segoe UI", 10F);
        btnAddCampaign.ForeColor = Color.White;
        btnAddCampaign.Location = new Point(17, 59);
        btnAddCampaign.Margin = new Padding(3, 4, 3, 4);
        btnAddCampaign.Name = "btnAddCampaign";
        btnAddCampaign.Size = new Size(217, 48);
        btnAddCampaign.TabIndex = 1;
        btnAddCampaign.Text = "+  Thêm chiến dịch";
        btnAddCampaign.UseVisualStyleBackColor = false;
        // 
        // flowCampaigns
        // 
        flowCampaigns.AutoScroll = true;
        flowCampaigns.FlowDirection = FlowDirection.TopDown;
        flowCampaigns.Location = new Point(17, 125);
        flowCampaigns.Margin = new Padding(3, 4, 3, 4);
        flowCampaigns.Name = "flowCampaigns";
        flowCampaigns.Size = new Size(981, 762);
        flowCampaigns.TabIndex = 2;
        flowCampaigns.WrapContents = false;
        // 
        // PromotionsControl
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(249, 249, 249);
        Controls.Add(flowCampaigns);
        Controls.Add(btnAddCampaign);
        Controls.Add(lblTitle);
        Margin = new Padding(3, 4, 3, 4);
        Name = "PromotionsControl";
        Padding = new Padding(27, 32, 27, 32);
        Size = new Size(1371, 1000);
        ResumeLayout(false);
        PerformLayout();
    }
}