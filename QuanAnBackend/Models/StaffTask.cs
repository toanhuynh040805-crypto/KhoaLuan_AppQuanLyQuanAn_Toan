namespace QuanAnBackend.Models;

public enum StaffTaskStatus
{
    ChuaLam,
    DangLam,
    HoanThanh
}

// Khớp bảng Tasks trong QuanAnManagementDB_Final.sql.
// Đặt tên class là StaffTask (không phải Task) để tránh đụng
// System.Threading.Tasks.Task; ánh xạ bảng thật bằng ToTable("Tasks")
// trong AppDbContext.
public class StaffTask
{
    public int TaskId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public StaffTaskStatus Status { get; set; } = StaffTaskStatus.ChuaLam;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
