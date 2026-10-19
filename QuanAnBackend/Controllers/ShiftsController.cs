using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using QuanAnBackend.Services;
namespace QuanAnBackend.Controllers;

public record ShiftRequest(string Name, TimeSpan StartTime, TimeSpan EndTime);
public record AssignShiftRequest(int UserId, int ShiftId, DateTime WorkDate, int? AreaId = null);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "QuanLy")]
public class ShiftsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    public ShiftsController(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Shifts.AsNoTracking()
            .Select(s => new { Id = s.ShiftId, Name = s.ShiftName, s.StartTime, s.EndTime })
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ShiftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Tên ca không được để trống" });

        var shift = new Shift { ShiftName = request.Name.Trim(), StartTime = request.StartTime, EndTime = request.EndTime };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return Ok(new { Id = shift.ShiftId, Name = shift.ShiftName, shift.StartTime, shift.EndTime });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ShiftRequest request)
    {
        var shift = await _db.Shifts.FindAsync(id);
        if (shift is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Tên ca không được để trống" });

        shift.ShiftName = request.Name.Trim();
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        await _db.SaveChangesAsync();
        return Ok(new { Id = shift.ShiftId, Name = shift.ShiftName, shift.StartTime, shift.EndTime });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var shift = await _db.Shifts.FindAsync(id);
        if (shift is null) return NotFound();

        var hasAssignments = await _db.ShiftAssignments.AnyAsync(a => a.ShiftId == id);
        if (hasAssignments)
            return BadRequest(new { message = "Ca làm đã có phân công, không thể xóa" });

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments([FromQuery] DateTime date)
    {
        var day = DateOnly.FromDateTime(date.Date);
        var assignments = await _db.ShiftAssignments
            .Include(a => a.Employee)!.ThenInclude(e => e!.User)
            .Include(a => a.Shift)
            .Include(a => a.Area)
            .Where(a => a.WorkDate == day)
            .AsNoTracking()
            .Select(a => new
            {
                Id = a.ShiftAssignmentId,
                UserId = a.Employee != null && a.Employee.User != null ? a.Employee.User.UserId : 0,
                UserName = a.Employee != null && a.Employee.User != null ? a.Employee.User.FullName : "",
                a.ShiftId,
                ShiftName = a.Shift != null ? a.Shift.ShiftName : "",
                ShiftStart = a.Shift != null ? a.Shift.StartTime : TimeSpan.Zero,
                ShiftEnd = a.Shift != null ? a.Shift.EndTime : TimeSpan.Zero,
                WorkDate = a.WorkDate.ToDateTime(TimeOnly.MinValue),
                a.AreaId,
                AreaName = a.Area != null ? a.Area.AreaName : null,
                Status = a.Status,
                a.CheckInTime,
                a.CheckOutTime
            })
            .ToListAsync();

        return Ok(assignments);
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign([FromBody] AssignShiftRequest request)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == request.UserId);
        if (employee is null)
            return BadRequest(new { message = "Tài khoản này chưa được gắn với hồ sơ nhân viên (Employees)" });

        if (!await _db.Shifts.AnyAsync(s => s.ShiftId == request.ShiftId))
            return BadRequest(new { message = "Ca làm không tồn tại" });

        if (request.AreaId.HasValue && !await _db.Areas.AnyAsync(a => a.AreaId == request.AreaId.Value))
            return BadRequest(new { message = "Khu vực không tồn tại" });

        var workDate = DateOnly.FromDateTime(request.WorkDate.Date);

        var exists = await _db.ShiftAssignments.AnyAsync(a =>
            a.EmployeeId == employee.EmployeeId && a.ShiftId == request.ShiftId && a.WorkDate == workDate);
        if (exists) return BadRequest(new { message = "Nhân viên đã được phân vào ca này trong ngày" });

        var assignment = new ShiftAssignment
        {
            EmployeeId = employee.EmployeeId,
            ShiftId = request.ShiftId,
            WorkDate = workDate,
            AreaId = request.AreaId,
            Status = AssignmentStatus.DaXepLich
        };
        _db.ShiftAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        var shiftName = await _db.Shifts.Where(s => s.ShiftId == request.ShiftId).Select(s => s.ShiftName).FirstOrDefaultAsync() ?? "";
        var areaName = await GetAreaNameAsync(request.AreaId);
        var message = $"Ca {shiftName} - ngày {request.WorkDate:dd/MM/yyyy}";
        if (!string.IsNullOrEmpty(areaName))
            message += $" · Khu vực: {areaName}";

        await _notifications.CreateAsync(
            "Bạn vừa được xếp ca mới",
            message,
            NotificationType.CaLam,
            targetUserId: request.UserId,
            relatedEntityType: "ShiftAssignment",
            relatedEntityId: assignment.ShiftAssignmentId);

        return Ok(new { Id = assignment.ShiftAssignmentId, UserId = request.UserId, assignment.ShiftId, WorkDate = request.WorkDate.Date, assignment.AreaId, Status = assignment.Status });
    }

    [HttpPut("assignments/{id}/area")]
    public async Task<IActionResult> UpdateAssignmentArea(int id, [FromBody] int? areaId)
    {
        var assignment = await _db.ShiftAssignments
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.ShiftAssignmentId == id);
        if (assignment is null) return NotFound();

        if (areaId.HasValue && !await _db.Areas.AnyAsync(a => a.AreaId == areaId.Value))
            return BadRequest(new { message = "Khu vực không tồn tại" });

        var oldAreaId = assignment.AreaId;
        assignment.AreaId = areaId;
        await _db.SaveChangesAsync();

        var isUpcoming = assignment.WorkDate >= DateOnly.FromDateTime(DateTime.Today);
        var targetUserId = assignment.Employee?.UserId;
        if (oldAreaId != areaId && isUpcoming && targetUserId.HasValue)
        {
            var newAreaName = await GetAreaNameAsync(areaId);
            var shiftName = assignment.Shift?.ShiftName ?? "";
            var workDateText = assignment.WorkDate.ToString("dd/MM/yyyy");

            string title, message;
            if (!string.IsNullOrEmpty(newAreaName))
            {
                title = oldAreaId.HasValue ? "Khu vực phụ trách của bạn đã thay đổi" : "Bạn được phân khu vực phụ trách";
                message = $"Ca {shiftName} - ngày {workDateText} · Khu vực: {newAreaName}";
            }
            else
            {
                title = "Khu vực phụ trách của bạn đã được gỡ";
                message = $"Ca {shiftName} - ngày {workDateText} hiện chưa phân khu vực";
            }

            await _notifications.CreateAsync(
                title,
                message,
                NotificationType.CaLam,
                targetUserId: targetUserId.Value,
                relatedEntityType: "ShiftAssignment",
                relatedEntityId: assignment.ShiftAssignmentId);
        }

        return Ok(new { Id = assignment.ShiftAssignmentId, assignment.AreaId });
    }

    [HttpGet("assignments/history")]
    public async Task<IActionResult> GetAssignmentHistory([FromQuery] int? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var toDate = DateOnly.FromDateTime((to ?? DateTime.Today).Date);
        var fromDate = DateOnly.FromDateTime((from ?? DateTime.Today.AddDays(-29)).Date);

        var query = _db.ShiftAssignments
            .Include(a => a.Employee)!.ThenInclude(e => e!.User)
            .Include(a => a.Shift)
            .Include(a => a.Area)
            .Where(a => a.WorkDate >= fromDate && a.WorkDate <= toDate)
            .AsNoTracking()
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.Employee != null && a.Employee.User != null && a.Employee.User.UserId == userId.Value);

        var history = await query
            .OrderByDescending(a => a.WorkDate)
            .ThenBy(a => a.Shift!.StartTime)
            .Select(a => new
            {
                Id = a.ShiftAssignmentId,
                UserId = a.Employee != null && a.Employee.User != null ? a.Employee.User.UserId : 0,
                UserName = a.Employee != null && a.Employee.User != null ? a.Employee.User.FullName : "",
                a.ShiftId,
                ShiftName = a.Shift != null ? a.Shift.ShiftName : "",
                ShiftStart = a.Shift != null ? a.Shift.StartTime : TimeSpan.Zero,
                ShiftEnd = a.Shift != null ? a.Shift.EndTime : TimeSpan.Zero,
                WorkDate = a.WorkDate.ToDateTime(TimeOnly.MinValue),
                a.AreaId,
                AreaName = a.Area != null ? a.Area.AreaName : null,
                Status = a.Status,
                a.CheckInTime,
                a.CheckOutTime
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpPut("assignments/{id}/status")]
    public async Task<IActionResult> UpdateAssignmentStatus(int id, [FromBody] AssignmentStatus status)
    {
        if (!Enum.IsDefined(typeof(AssignmentStatus), status))
            return BadRequest(new { message = "Trạng thái ca làm không hợp lệ" });

        var assignment = await _db.ShiftAssignments
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.ShiftAssignmentId == id);
        if (assignment is null) return NotFound();

        var workDay = assignment.WorkDate.ToDateTime(TimeOnly.MinValue);
        var shiftStart = workDay + (assignment.Shift?.StartTime ?? TimeSpan.Zero);
        var shiftEnd = workDay + (assignment.Shift?.EndTime ?? TimeSpan.Zero);
        var isToday = assignment.WorkDate == DateOnly.FromDateTime(DateTime.Today);

        assignment.Status = status;
        switch (status)
        {
            case AssignmentStatus.DaXepLich:
            case AssignmentStatus.VangMat:
                assignment.CheckInTime = null;
                assignment.CheckOutTime = null;
                break;

            case AssignmentStatus.DaDiemDanh:
                assignment.CheckInTime ??= isToday ? DateTime.Now : shiftStart;
                assignment.CheckOutTime = null;
                break;

            case AssignmentStatus.DaHoanThanh:
                assignment.CheckInTime ??= shiftStart <= DateTime.Now ? shiftStart : DateTime.Now;
                assignment.CheckOutTime ??= isToday ? DateTime.Now : shiftEnd;
                break;
        }

        await _db.SaveChangesAsync();
        return Ok(new
        {
            Id = assignment.ShiftAssignmentId,
            Status = assignment.Status,
            assignment.CheckInTime,
            assignment.CheckOutTime
        });
    }

    [HttpDelete("assignments/{id}")]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        var assignment = await _db.ShiftAssignments.FindAsync(id);
        if (assignment is null) return NotFound();

        _db.ShiftAssignments.Remove(assignment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GetAreaNameAsync(int? areaId)
    {
        if (!areaId.HasValue) return "";
        return await _db.Areas
            .Where(a => a.AreaId == areaId.Value)
            .Select(a => a.AreaName)
            .FirstOrDefaultAsync() ?? "";
    }
}
