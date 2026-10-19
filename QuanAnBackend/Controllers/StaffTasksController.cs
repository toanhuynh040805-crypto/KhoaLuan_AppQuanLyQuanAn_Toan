using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using QuanAnBackend.Services;
namespace QuanAnBackend.Controllers;

// "AssignedToUserId" giữ nguyên tên field để khớp ApiService.cs bên WinForm;
// đây là Users.UserId, backend tự tra Employee tương ứng để lưu vào Tasks.EmployeeId.
public record CreateTaskRequest(string Title, string? Description, int AssignedToUserId, DateTime? DueAt);
public record UpdateTaskRequest(string Title, string? Description, int AssignedToUserId, DateTime? DueAt);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "QuanLy")]
public class StaffTasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    public StaffTasksController(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // GET api/stafftasks?userId=5 -> bỏ trống để lấy tất cả (userId = Users.UserId)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? userId)
    {
        var query = _db.StaffTasks.Include(t => t.Employee)!.ThenInclude(e => e!.User).AsNoTracking().AsQueryable();
        if (userId.HasValue)
            query = query.Where(t => t.Employee != null && t.Employee.User != null && t.Employee.User.UserId == userId.Value);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                Id = t.TaskId,
                Title = t.TaskName,
                t.Description,
                AssignedToUserId = t.Employee != null && t.Employee.User != null ? t.Employee.User.UserId : 0,
                AssignedToName = t.Employee != null && t.Employee.User != null ? t.Employee.User.FullName : "",
                t.CreatedAt,
                DueAt = t.DueDate,
                Status = t.Status
            })
            .ToListAsync();

        return Ok(tasks);
    }

    // GET api/stafftasks/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var t = await _db.StaffTasks.Include(x => x.Employee)!.ThenInclude(e => e!.User).AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaskId == id);
        if (t is null) return NotFound();

        return Ok(new
        {
            Id = t.TaskId,
            Title = t.TaskName,
            t.Description,
            AssignedToUserId = t.Employee != null && t.Employee.User != null ? t.Employee.User.UserId : 0,
            AssignedToName = t.Employee != null && t.Employee.User != null ? t.Employee.User.FullName : "",
            t.CreatedAt,
            DueAt = t.DueDate,
            Status = t.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == request.AssignedToUserId);
        if (employee is null)
            return BadRequest(new { message = "Tài khoản này chưa được gắn với hồ sơ nhân viên (Employees)" });

        var task = new StaffTask
        {
            TaskName = request.Title,
            Description = request.Description,
            EmployeeId = employee.EmployeeId,
            DueDate = request.DueAt,
            Status = StaffTaskStatus.ChuaLam
        };
                _db.StaffTasks.Add(task);
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(
            "Bạn có công việc mới",
            task.TaskName,
            NotificationType.CaLam,
            targetUserId: request.AssignedToUserId,
            relatedEntityType: "StaffTask",
            relatedEntityId: task.TaskId);

        return Ok(new { Id = task.TaskId, Title = task.TaskName, AssignedToUserId = request.AssignedToUserId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskRequest request)
    {
        var task = await _db.StaffTasks.FindAsync(id);
        if (task is null) return NotFound();

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == request.AssignedToUserId);
        if (employee is null)
            return BadRequest(new { message = "Tài khoản này chưa được gắn với hồ sơ nhân viên (Employees)" });

        task.TaskName = request.Title;
        task.Description = request.Description;
        task.EmployeeId = employee.EmployeeId;
        task.DueDate = request.DueAt;
        await _db.SaveChangesAsync();
        return Ok(new { Id = task.TaskId, Title = task.TaskName });
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StaffTaskStatus status)
    {
        var task = await _db.StaffTasks.FindAsync(id);
        if (task is null) return NotFound();

        task.Status = status;
        await _db.SaveChangesAsync();
        return Ok(new { Id = task.TaskId, Status = task.Status });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _db.StaffTasks.FindAsync(id);
        if (task is null) return NotFound();

        _db.StaffTasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
