namespace QuanAnBackend.Models;

// Khớp bảng Areas trong QuanAnManagementDB_Final.sql
public class Area
{
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty; // Tầng 1, Sân vườn, Phòng VIP...
    public string? Description { get; set; }

    public ICollection<DiningTable> Tables { get; set; } = new List<DiningTable>();
}
