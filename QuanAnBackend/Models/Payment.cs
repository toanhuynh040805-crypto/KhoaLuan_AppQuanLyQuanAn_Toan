namespace QuanAnBackend.Models;

public enum PaymentMethod
{
    TienMat,
    VietQR
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public decimal TotalAmount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? QRCodeContent { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
}
