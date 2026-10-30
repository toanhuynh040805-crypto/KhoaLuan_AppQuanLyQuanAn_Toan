using System.ComponentModel.DataAnnotations;

namespace QuanAnBackend.Models;

public class OrderReview
{
    public int ReviewId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public byte Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsVisible { get; set; } = true;

    [MaxLength(16)]
    public string ModerationStatus { get; set; } = "Public";
}
