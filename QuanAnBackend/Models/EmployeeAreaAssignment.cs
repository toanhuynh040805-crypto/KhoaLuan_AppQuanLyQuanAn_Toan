namespace QuanAnBackend.Models;

// Phan cong nhan vien phuc vu theo khu vuc ban.
// WorkDate = null: phan cong mac dinh/lau dai.
// WorkDate co gia tri: chi ap dung cho ngay do.
public class EmployeeAreaAssignment
{
    public int EmployeeAreaAssignmentId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    public DateTime? WorkDate { get; set; }
    public bool IsActive { get; set; } = true;

    public int? AssignedBy { get; set; }
    public User? AssignedByUser { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.Now;
}
