namespace QuanAnBackend.Models;

public class PrintTicket
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public bool IsPrinted { get; set; } = false;
}
