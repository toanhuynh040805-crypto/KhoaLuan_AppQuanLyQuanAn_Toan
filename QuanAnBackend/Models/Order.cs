namespace QuanAnBackend.Models;

public enum OrderStatus
{
    DangPhucVu,
    ChoThanhToan,
    HoanTat,
    DaHuy
}

public class Order
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.DangPhucVu;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
}
