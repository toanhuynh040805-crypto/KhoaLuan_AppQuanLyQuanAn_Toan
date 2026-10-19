namespace QuanAnBackend.Models;

public enum KitchenTicketStatus
{
    ChoXuLy,
    DangCheBien,
    HoanThanh
}

// Khớp bảng KitchenTickets trong QuanAnManagementDB_Final.sql (thay cho PrintTicket cũ)
public class KitchenTicket
{
    public int KitchenTicketId { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int? StationId { get; set; }
    public Station? Station { get; set; }

    public DateTime SentTime { get; set; } = DateTime.Now;

    public int? ChefId { get; set; }
    public Employee? Chef { get; set; }

    public KitchenTicketStatus Status { get; set; } = KitchenTicketStatus.ChoXuLy;
    public DateTime? CompletedTime { get; set; }
}
