namespace QuanAnBackend.Models;

// Khớp bảng DiscountCampaigns trong QuanAnManagementDB_Final.sql.
//
// Điều kiện áp dụng được lưu ở bảng DiscountCampaignConditions (xem
// DiscountCampaignCondition.cs): 1 chiến dịch có 0..N điều kiện, hóa đơn phải thỏa
// TẤT CẢ. Mỗi hóa đơn chỉ áp dụng tối đa 1 chiến dịch.
//
// Ba cột RequireMembership / MinVisitCount / VisitLookbackDays là cột CŨ của schema
// gốc, được giữ lại để tương thích DB. Từ nay backend luôn ghi false/null cho 3 cột
// này (điều kiện khách quen được lưu thành 1 dòng VisitCount trong bảng điều kiện).
public class DiscountCampaign
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DiscountPercent { get; set; } // 10.00 nghia la 10%

    // Cột cũ - luôn false/null, xem ghi chú đầu file.
    public bool RequireMembership { get; set; } = false;
    public int? MinVisitCount { get; set; }
    public int? VisitLookbackDays { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }

    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<DiscountCampaignCondition> Conditions { get; set; }
        = new List<DiscountCampaignCondition>();
}