namespace QuanAnBackend.Models;

public enum KitchenStatus
{
    MoiGoi,
    DangCheBien,
    DaCheBien,
    DaPhucVu,
    DaHuy
}

// Khop CHINH XAC bang OrderItems trong QuanAnManagementDB_Final.sql.
// MenuItemId co the NULL khi dong nay la Combo; ComboId co the NULL khi la mon le.
public class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int? MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    public int? ComboId { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public KitchenStatus Status { get; set; } = KitchenStatus.MoiGoi;

    // Các cột này được WinFormsCompatibilitySchemaService bổ sung tự động
    // cho database cũ để giữ lịch sử hủy món của cả WinForms và Mobile.
    public string? CancelReason { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }

    public bool IsRefillRequest { get; set; } = false;

    // Món được khách yêu cầu mang về (dùng cho khách ăn tại chỗ gọi thêm mang về).
    // Món thuộc đơn OrderType = MangVe mặc định là mang về dù cờ này = false.
    public bool IsTakeaway { get; set; } = false;
    public int? RefillSequenceNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public byte[]? RowVer { get; set; }

    public ICollection<KitchenTicket> KitchenTickets { get; set; } = new List<KitchenTicket>();
    public ICollection<DishStatusHistory> StatusHistory { get; set; } = new List<DishStatusHistory>();
}