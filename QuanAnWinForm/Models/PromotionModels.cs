namespace QuanAnWinForm.Models;

public class DiscountCampaignDto
{
    public int Id { get; set; }
    public string CampaignName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool RequireMembership { get; set; }
    public bool IsActive { get; set; }
    public string? Note { get; set; }
}

// Dung rieng cho ComboBox chon khuyen mai luc thanh toan (chi can Id/Ten/%).
public class ActiveCampaignDto
{
    public int Id { get; set; }
    public string CampaignName { get; set; } = "";
    public decimal DiscountPercent { get; set; }

    public override string ToString() => $"{CampaignName} (-{DiscountPercent:0.#}%)";
}