namespace QuanAnBackend.Models;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
