namespace QuanAnBackend.Models;

// Khớp bảng DiscountCampaigns trong QuanAnManagementDB_Final.sql.
//
// LƯU Ý PHẠM VI: DB gốc cho phép 1 chiến dịch yêu cầu "khách quen" (RequireMembership
// = true, kèm MinVisitCount/VisitLookbackDays) - để kiểm tra đúng điều kiện này cần
// có module Khách hàng thành viên (theo dõi lịch sử ghé quán theo SĐT/tài khoản),
// MODULE ĐÓ CHƯA ĐƯỢC LÀM ở backend hiện tại. Vì vậy backend này chỉ tạo VÀ áp dụng
// được các chiến dịch RequireMembership = false (áp dụng chung cho mọi khách) - xem
// DiscountCampaignsController. Cột RequireMembership/MinVisitCount/VisitLookbackDays
// vẫn được giữ nguyên trong model để khớp đúng schema DB, chỉ là luôn ghi false/null
// từ phía backend này.
public class DiscountCampaign
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DiscountPercent { get; set; } // 10.00 nghia la 10%

    public bool RequireMembership { get; set; } = false;
    public int? MinVisitCount { get; set; }
    public int? VisitLookbackDays { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }

    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}