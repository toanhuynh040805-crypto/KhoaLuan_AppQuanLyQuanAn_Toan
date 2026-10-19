namespace QuanAnBackend.Models;

// Khớp bảng Shifts trong QuanAnManagementDB_Final.sql
public class Shift
{
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
}
