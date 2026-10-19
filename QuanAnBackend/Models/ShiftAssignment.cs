namespace QuanAnBackend.Models;

public enum AssignmentStatus
{
    DaXepLich,
    DaDiemDanh,
    VangMat,
    DaHoanThanh
}

// Khop CHINH XAC bang ShiftAssignments trong QuanAnManagementDB_Final.sql.
public class ShiftAssignment
{
    public int ShiftAssignmentId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    // Khu vực phụ trách trong đúng ca này (nullable = chưa phân khu vực).
    // Cột được WinFormsCompatibilitySchemaService bổ sung cho database cũ.
    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public DateOnly WorkDate { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.DaXepLich;

    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
}
