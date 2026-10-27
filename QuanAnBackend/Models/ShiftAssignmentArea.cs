namespace QuanAnBackend.Models;

public class ShiftAssignmentArea
{
    public int ShiftAssignmentId { get; set; }
    public ShiftAssignment? ShiftAssignment { get; set; }

    public int AreaId { get; set; }
    public Area? Area { get; set; }
}
