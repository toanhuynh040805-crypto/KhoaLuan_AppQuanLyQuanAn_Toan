namespace QuanAnBackend.Models;

// Khớp bảng MenuItems trong QuanAnManagementDB_Final.sql.
// Không map Combo/Ingredient (chưa có module đó trong dự án).
public class MenuItem
{
    public int MenuItemId { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? StationId { get; set; } // món này do bộ phận nào chế biến (Bếp/Quầy nước)
    public Station? Station { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Unit { get; set; } = "Phan";
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFreeRefill { get; set; } = false;
    public int FreeRefillLimit { get; set; } = 2;
    public decimal? RefillPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
