namespace QuanAnBackend.Models;

public enum PaymentMethod
{
    TienMat,
    TheNganHang,
    ViDienTu,
    ChuyenKhoan
}

public enum PaymentStatus
{
    ThanhCong,
    ThatBai,
    HoanTien
}

// Khớp bảng Payments trong QuanAnManagementDB_Final.sql.
// LƯU Ý: Payment giờ gắn với Invoice (không gắn thẳng với Order như code
// cũ) - đúng theo thiết kế "1 Order có thể tách nhiều Invoice, 1 Invoice
// có thể có nhiều lần Payment (thử lại khi thất bại...)".
public class Payment
{
    public int PaymentId { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentTime { get; set; } = DateTime.Now;
    public PaymentStatus Status { get; set; } = PaymentStatus.ThanhCong;
    public string? TransactionRef { get; set; }
}
