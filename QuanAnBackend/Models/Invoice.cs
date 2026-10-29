namespace QuanAnBackend.Models;

public enum InvoiceStatus
{
    ChuaThanhToan,
    DaThanhToan,
    DaHuy
}

// Khớp bảng Invoices trong QuanAnManagementDB_Final.sql.
// Các cột liên quan Khuyến mãi/Voucher/VIP giữ nguyên trong DB nhưng
// KHÔNG tính toán ở backend này (chưa có module Khuyến mãi/VIP) -> luôn
// lưu mặc định 0. Khi nào module đó được làm, chỉ cần cập nhật lại logic
// tính ở PaymentsController mà không cần đổi schema.
public class Invoice
{
    public int InvoiceId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public int? AppliedCampaignId { get; set; }
    public DiscountCampaign? AppliedCampaign { get; set; }
    public int? VoucherId { get; set; }
    public decimal TaxAmount { get; set; } = 0;
    public decimal VIPRoomFee { get; set; } = 0;
    public decimal VIPMinimumGap { get; set; } = 0;
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.ChuaThanhToan;

    // Người thực sự xác nhận thanh toán / nhận tiền (Thu ngân).
    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }

    // Người gửi yêu cầu thanh toán (Phục vụ).
    public int? RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    public byte[]? RowVer { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}