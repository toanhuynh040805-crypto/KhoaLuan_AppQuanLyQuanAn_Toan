namespace QuanAnBackend.Models;

public enum TableStatus
{
    Trong,
    DangPhucVu,
    DaDatTruoc
}

public class RestaurantTable
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Trong;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
