namespace QuanAnBackend.Models;

// Khớp bảng LoyaltyPointHistory trong QuanAnManagementDB_Final.sql.
public enum LoyaltyPointType
{
    TichDiem,   // cộng điểm (thanh toán hóa đơn, hoặc Quản lý cộng tay)
    DoiDiem     // trừ điểm (đổi điểm giảm giá hóa đơn, hoặc Quản lý trừ tay)
}

public class LoyaltyPointHistory
{
    public int HistoryId { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Số điểm THAY ĐỔI trong lần ghi nhận này - luôn lưu đúng dấu thực tế
    // (TichDiem > 0, DoiDiem < 0) để có thể SUM trực tiếp ra số dư, không cần
    // biết Type mới tính được đã thay đổi bao nhiêu.
    public int Points { get; set; }
    public LoyaltyPointType Type { get; set; }

    public int? RelatedInvoiceId { get; set; }
    public Invoice? RelatedInvoice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}