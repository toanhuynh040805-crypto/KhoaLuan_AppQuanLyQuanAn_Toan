namespace QuanAnBackend.Models;

public enum KitchenStatus
{
    ChoXuLy,
    DaInPhieu,
    DangLam,
    HoanThanh,
    DaPhucVu
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    public int Quantity { get; set; }
    public string? Note { get; set; }
    public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.ChoXuLy;

    public ICollection<PrintTicket> PrintTickets { get; set; } = new List<PrintTicket>();
}
