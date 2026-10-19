namespace QuanAnBackend.Models;

// Khớp bảng Stations trong QuanAnManagementDB_Final.sql.
// Tên class đổi từ Department -> Station cho khớp tên bảng thật.
public class Station
{
    public int StationId { get; set; }
    public string StationName { get; set; } = string.Empty; // "Bep", "QuayNuoc"

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<KitchenTicket> KitchenTickets { get; set; } = new List<KitchenTicket>();
}
