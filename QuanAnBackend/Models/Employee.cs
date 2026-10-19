namespace QuanAnBackend.Models;

public enum EmployeeStatus
{
    DangLamViec,
    NghiViec,
    TamNghi
}

// Khớp bảng Employees trong QuanAnManagementDB_Final.sql.
// Tách riêng khỏi Users: Users lo đăng nhập/định danh, Employee lo thông
// tin nghiệp vụ (mã NV, lương, vị trí, bộ phận phụ trách...).
public class Employee
{
    public int EmployeeId { get; set; }

    public int UserId { get; set; } // 1-1 với Users
    public User? User { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty; // Bep, PhucVu, ThuNgan, QuanLy
    public DateTime HireDate { get; set; }
    public decimal? Salary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.DangLamViec;

    public int? StationId { get; set; } // bộ phận phụ trách (Bếp/Quầy nước), nếu có
    public Station? Station { get; set; }

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<StaffTask> Tasks { get; set; } = new List<StaffTask>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
