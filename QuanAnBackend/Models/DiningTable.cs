namespace QuanAnBackend.Models;

public enum TableType
{
    Thuong,
    VIP
}

public enum TableStatus
{
    Trong,
    DangPhucVu,
    DaDat,
    DangDonDep
}

// Khớp bảng DiningTables trong QuanAnManagementDB_Final.sql.
// Tên class đổi từ RestaurantTable -> DiningTable cho khớp tên bảng thật.
public class DiningTable
{
    public int TableId { get; set; }
    public string TableName { get; set; } = string.Empty;

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    public int Capacity { get; set; } = 4;
    public TableType TableType { get; set; } = TableType.Thuong;
    public TableStatus Status { get; set; } = TableStatus.Trong;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
