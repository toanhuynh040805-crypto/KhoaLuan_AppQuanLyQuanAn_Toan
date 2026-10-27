using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using QuanAnBackend.Services;
namespace QuanAnBackend.Controllers;

public record ShiftRequest(string Name, TimeSpan StartTime, TimeSpan EndTime);
public record AssignShiftRequest(
    int UserId,
    int ShiftId,
    DateTime WorkDate,
    List<int>? AreaIds = null,
    int? AreaId = null);
public record UpdateAssignmentAreasRequest(
    List<int>? AreaIds);
public record ShiftAssignmentDto(
    int Id,
    int UserId,
    string UserName,
    int ShiftId,
    string ShiftName,
    TimeSpan ShiftStart,
    TimeSpan ShiftEnd,
    DateTime WorkDate,
    int? AreaId,
    string? AreaName,
    List<int> AreaIds,
    List<string> AreaNames,
    AssignmentStatus Status,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    int LateMinutes,
    int EarlyLeaveMinutes,
    string CheckInStatus,
    string CheckOutStatus,
    string AttendanceStatus);

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
    public async Task<IActionResult> GetAssignments(
        [FromQuery] DateTime date)
    {
        var day =
            DateOnly.FromDateTime(date.Date);

        var rows = await _db.ShiftAssignments
            .Include(a => a.Employee)!
                .ThenInclude(e => e!.User)
            .Include(a => a.Shift)
            .Include(a => a.Area)
            .Include(a => a.AssignedAreas)
                .ThenInclude(x => x.Area)
            .Where(a => a.WorkDate == day)
            .AsNoTracking()
            .OrderBy(a => a.Shift!.StartTime)
            .ToListAsync();

        return Ok(
            rows.Select(ToAssignmentDto)
                .ToList()
        );
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign(
        [FromBody] AssignShiftRequest request)
    {
        var employee =
            await _db.Employees
                .FirstOrDefaultAsync(
                    e => e.UserId ==
                        request.UserId
                );

        if (employee is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Tài khoản này chưa được gắn với hồ sơ nhân viên (Employees)"
                }
            );
        }

        var shift = await _db.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.ShiftId ==
                    request.ShiftId
            );

        if (shift is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Ca làm không tồn tại"
                }
            );
        }

        var areaIds =
            NormalizeAreaIds(
                request.AreaIds,
                request.AreaId
            );

        if (!await AreAreasValidAsync(areaIds))
        {
            return BadRequest(
                new
                {
                    message =
                        "Có khu vực không tồn tại"
                }
            );
        }

        var workDate =
            DateOnly.FromDateTime(
                request.WorkDate.Date
            );

        var exists =
            await _db.ShiftAssignments
                .AnyAsync(
                    a =>
                        a.EmployeeId ==
                            employee.EmployeeId &&
                        a.ShiftId ==
                            request.ShiftId &&
                        a.WorkDate ==
                            workDate
                );

        if (exists)
        {
            return BadRequest(
                new
                {
                    message =
                        "Nhân viên đã được phân vào ca này trong ngày"
                }
            );
        }

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var assignment =
                new ShiftAssignment
                {
                    EmployeeId =
                        employee.EmployeeId,
                    ShiftId =
                        request.ShiftId,
                    WorkDate = workDate,
                    // Giữ khu đầu tiên để WinForms/API cũ vẫn đọc được.
                    AreaId = areaIds
                        .Cast<int?>()
                        .FirstOrDefault(),
                    Status =
                        AssignmentStatus
                            .DaXepLich
                };

            _db.ShiftAssignments.Add(
                assignment
            );

            await _db.SaveChangesAsync();

            foreach (var areaId in areaIds)
            {
                _db.ShiftAssignmentAreas.Add(
                    new ShiftAssignmentArea
                    {
                        ShiftAssignmentId =
                            assignment
                                .ShiftAssignmentId,
                        AreaId = areaId
                    }
                );
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var areaNames =
                await GetAreaNamesAsync(
                    areaIds
                );

            var message =
                $"Ca {shift.ShiftName} - " +
                $"ngày {request.WorkDate:dd/MM/yyyy}";

            if (areaNames.Count > 0)
            {
                message +=
                    $" · Khu vực: " +
                    string.Join(
                        ", ",
                        areaNames
                    );
            }

            await _notifications.CreateAsync(
                "Bạn vừa được xếp ca mới",
                message,
                NotificationType.CaLam,
                targetUserId:
                    request.UserId,
                relatedEntityType:
                    "ShiftAssignment",
                relatedEntityId:
                    assignment
                        .ShiftAssignmentId
            );

            return Ok(
                new
                {
                    Id =
                        assignment
                            .ShiftAssignmentId,
                    UserId =
                        request.UserId,
                    assignment.ShiftId,
                    WorkDate =
                        request
                            .WorkDate.Date,
                    assignment.AreaId,
                    AreaIds = areaIds,
                    AreaNames = areaNames,
                    Status =
                        assignment.Status
                }
            );
        }
        catch
        {
            await transaction
                .RollbackAsync();
            throw;
        }
    }

    [HttpPut("assignments/{id}/area")]
    public Task<IActionResult>
        UpdateAssignmentArea(
            int id,
            [FromBody] int? areaId)
    {
        var areaIds = areaId.HasValue
            ? new List<int>
            {
                areaId.Value
            }
            : new List<int>();

        return UpdateAssignmentAreasInternal(
            id,
            areaIds
        );
    }

    [HttpPut("assignments/{id}/areas")]
    public Task<IActionResult>
        UpdateAssignmentAreas(
            int id,
            [FromBody]
            UpdateAssignmentAreasRequest request)
    {
        return UpdateAssignmentAreasInternal(
            id,
            NormalizeAreaIds(
                request.AreaIds,
                null
            )
        );
    }

    private async Task<IActionResult>
        UpdateAssignmentAreasInternal(
            int id,
            List<int> areaIds)
    {
        if (!await AreAreasValidAsync(areaIds))
        {
            return BadRequest(
                new
                {
                    message =
                        "Có khu vực không tồn tại"
                }
            );
        }

        var assignment =
            await _db.ShiftAssignments
                .Include(a => a.Employee)
                .Include(a => a.Shift)
                .Include(a =>
                    a.AssignedAreas)
                .FirstOrDefaultAsync(
                    a =>
                        a.ShiftAssignmentId ==
                        id
                );

        if (assignment is null)
            return NotFound();

        var oldAreaIds =
            assignment.AssignedAreas
                .Select(x => x.AreaId)
                .ToHashSet();

        if (oldAreaIds.Count == 0 &&
            assignment.AreaId.HasValue)
        {
            oldAreaIds.Add(
                assignment.AreaId.Value
            );
        }

        var newAreaIds =
            areaIds.ToHashSet();

        var changed =
            !oldAreaIds.SetEquals(
                newAreaIds
            );

        var existingRows =
            assignment.AssignedAreas
                .ToList();

        var rowsToRemove =
            existingRows
                .Where(
                    row =>
                        !newAreaIds.Contains(
                            row.AreaId
                        )
                )
                .ToList();

        _db.ShiftAssignmentAreas
            .RemoveRange(rowsToRemove);

        var existingIds =
            existingRows
                .Select(
                    row => row.AreaId
                )
                .ToHashSet();

        foreach (
            var areaId in
                newAreaIds.Where(
                    id =>
                        !existingIds
                            .Contains(id)
                )
        )
        {
            _db.ShiftAssignmentAreas.Add(
                new ShiftAssignmentArea
                {
                    ShiftAssignmentId =
                        assignment
                            .ShiftAssignmentId,
                    AreaId = areaId
                }
            );
        }

        // Khu đầu tiên tiếp tục làm giá trị tương thích cho client cũ.
        assignment.AreaId =
            areaIds
                .Cast<int?>()
                .FirstOrDefault();

        await _db.SaveChangesAsync();

        var areaNames =
            await GetAreaNamesAsync(
                areaIds
            );

        var isUpcoming =
            assignment.WorkDate >=
            DateOnly.FromDateTime(
                DateTime.Today
            );

        var targetUserId =
            assignment.Employee?.UserId;

        if (changed &&
            isUpcoming &&
            targetUserId.HasValue)
        {
            var shiftName =
                assignment.Shift
                    ?.ShiftName ?? "";

            var workDateText =
                assignment.WorkDate
                    .ToString(
                        "dd/MM/yyyy"
                    );

            var message =
                areaNames.Count > 0
                    ? $"Ca {shiftName} - ngày {workDateText} · Khu vực: " +
                        string.Join(
                            ", ",
                            areaNames
                        )
                    : $"Ca {shiftName} - ngày {workDateText} hiện chưa phân khu vực";

            await _notifications
                .CreateAsync(
                    areaNames.Count > 0
                        ? "Khu vực phụ trách của bạn đã thay đổi"
                        : "Khu vực phụ trách của bạn đã được gỡ",
                    message,
                    NotificationType.CaLam,
                    targetUserId:
                        targetUserId.Value,
                    relatedEntityType:
                        "ShiftAssignment",
                    relatedEntityId:
                        assignment
                            .ShiftAssignmentId
                );
        }

        return Ok(
            new
            {
                Id =
                    assignment
                        .ShiftAssignmentId,
                assignment.AreaId,
                AreaIds = areaIds,
                AreaNames = areaNames
            }
        );
    }

    [HttpGet("assignments/history")]
    public async Task<IActionResult>
        GetAssignmentHistory(
            [FromQuery] int? userId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
    {
        var toDate =
            DateOnly.FromDateTime(
                (to ?? DateTime.Today).Date
            );

        var fromDate =
            DateOnly.FromDateTime(
                (
                    from ??
                    DateTime.Today
                        .AddDays(-29)
                ).Date
            );

        var query =
            _db.ShiftAssignments
                .Include(a => a.Employee)!
                    .ThenInclude(e =>
                        e!.User)
                .Include(a => a.Shift)
                .Include(a => a.Area)
                .Include(a =>
                    a.AssignedAreas)
                    .ThenInclude(x =>
                        x.Area)
                .Where(
                    a =>
                        a.WorkDate >=
                            fromDate &&
                        a.WorkDate <=
                            toDate
                )
                .AsNoTracking()
                .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(
                a =>
                    a.Employee != null &&
                    a.Employee.User !=
                        null &&
                    a.Employee.User
                        .UserId ==
                        userId.Value
            );
        }

        var rows = await query
            .OrderByDescending(
                a => a.WorkDate
            )
            .ThenBy(
                a =>
                    a.Shift!.StartTime
            )
            .ToListAsync();

        return Ok(
            rows.Select(ToAssignmentDto)
                .ToList()
        );
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
        var assignment = await _db.ShiftAssignments
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.ShiftAssignmentId == id);
        if (assignment is null) return NotFound();

        var targetUserId = assignment.Employee?.UserId;
        var shiftName = assignment.Shift?.ShiftName ?? "";
        var workDate = assignment.WorkDate;
        var removedId = assignment.ShiftAssignmentId;

        _db.ShiftAssignments.Remove(assignment);
        await _db.SaveChangesAsync();

        if (targetUserId.HasValue && workDate >= DateOnly.FromDateTime(DateTime.Today))
        {
            await _notifications.CreateAsync(
                "Ca làm của bạn đã bị hủy",
                $"Ca {shiftName} - ngày {workDate:dd/MM/yyyy} đã được gỡ khỏi lịch của bạn",
                NotificationType.CaLam,
                targetUserId: targetUserId.Value,
                relatedEntityType: "ShiftAssignment",
                relatedEntityId: removedId);
        }

        return NoContent();
    }

    private static List<int>
        NormalizeAreaIds(
            IEnumerable<int>? areaIds,
            int? legacyAreaId)
    {
        var result =
            (areaIds ??
                Enumerable.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (result.Count == 0 &&
            legacyAreaId.HasValue &&
            legacyAreaId.Value > 0)
        {
            result.Add(
                legacyAreaId.Value
            );
        }

        return result;
    }

    private async Task<bool>
        AreAreasValidAsync(
            IReadOnlyCollection<int>
                areaIds)
    {
        if (areaIds.Count == 0)
            return true;

        var count = await _db.Areas
            .CountAsync(
                a =>
                    areaIds.Contains(
                        a.AreaId
                    )
            );

        return count ==
            areaIds.Count;
    }

    private async Task<List<string>>
        GetAreaNamesAsync(
            IReadOnlyCollection<int>
                areaIds)
    {
        if (areaIds.Count == 0)
        {
            return new List<string>();
        }

        return await _db.Areas
            .Where(
                a =>
                    areaIds.Contains(
                        a.AreaId
                    )
            )
            .OrderBy(a => a.AreaName)
            .Select(a => a.AreaName)
            .ToListAsync();
    }

    private sealed record PunctualityStatus(
        int LateMinutes,
        int EarlyLeaveMinutes,
        string CheckInStatus,
        string CheckOutStatus,
        string AttendanceStatus);

    private static PunctualityStatus
        BuildPunctualityStatus(
            AssignmentStatus status,
            DateTime shiftStart,
            DateTime shiftEnd,
            DateTime? checkInTime,
            DateTime? checkOutTime)
    {
        if (status ==
            AssignmentStatus.VangMat)
        {
            return new PunctualityStatus(
                0,
                0,
                "VangMat",
                "KhongApDung",
                "VangMat"
            );
        }

        var lateMinutes = 0;
        var earlyLeaveMinutes = 0;

        if (checkInTime.HasValue &&
            checkInTime.Value > shiftStart)
        {
            lateMinutes = Math.Max(
                1,
                (int)Math.Ceiling(
                    (
                        checkInTime.Value -
                        shiftStart
                    ).TotalMinutes
                )
            );
        }

        if (checkOutTime.HasValue &&
            checkOutTime.Value < shiftEnd)
        {
            earlyLeaveMinutes = Math.Max(
                1,
                (int)Math.Ceiling(
                    (
                        shiftEnd -
                        checkOutTime.Value
                    ).TotalMinutes
                )
            );
        }

        var checkInStatus =
            !checkInTime.HasValue
                ? "ChuaDiemDanh"
                : lateMinutes > 0
                    ? $"DiTre:{lateMinutes}"
                    : "DungGio";

        var checkOutStatus =
            !checkOutTime.HasValue
                ? "ChuaKetThuc"
                : earlyLeaveMinutes > 0
                    ? $"VeSom:{earlyLeaveMinutes}"
                    : "DuGio";

        var attendanceStatus =
            !checkInTime.HasValue
                ? "ChuaDiemDanh"
                : !checkOutTime.HasValue
                    ? lateMinutes > 0
                        ? "DangLam_DiTre"
                        : "DangLam_DungGio"
                    : lateMinutes > 0 &&
                        earlyLeaveMinutes > 0
                        ? "DiTre_VeSom"
                        : lateMinutes > 0
                            ? "DiTre"
                            : earlyLeaveMinutes > 0
                                ? "VeSom"
                                : "DungGio_DuGio";

        return new PunctualityStatus(
            lateMinutes,
            earlyLeaveMinutes,
            checkInStatus,
            checkOutStatus,
            attendanceStatus
        );
    }

    private static ShiftAssignmentDto
        ToAssignmentDto(
            ShiftAssignment a)
    {
        var assignedAreas =
            a.AssignedAreas
                .Where(
                    x =>
                        x.Area != null
                )
                .OrderBy(
                    x =>
                        x.Area!.AreaName
                )
                .ToList();

        var areaIds =
            assignedAreas.Count > 0
                ? assignedAreas
                    .Select(
                        x => x.AreaId
                    )
                    .ToList()
                : a.AreaId.HasValue
                    ? new List<int>
                    {
                        a.AreaId.Value
                    }
                    : new List<int>();

        var areaNames =
            assignedAreas.Count > 0
                ? assignedAreas
                    .Select(
                        x =>
                            x.Area!
                                .AreaName
                    )
                    .ToList()
                : a.Area != null
                    ? new List<string>
                    {
                        a.Area.AreaName
                    }
                    : new List<string>();

        var workDate =
            a.WorkDate.ToDateTime(
                TimeOnly.MinValue
            );

        var shiftStart =
            workDate +
            (a.Shift?.StartTime ??
                TimeSpan.Zero);

        var shiftEnd =
            workDate +
            (a.Shift?.EndTime ??
                TimeSpan.Zero);

        if (shiftEnd <= shiftStart)
        {
            shiftEnd =
                shiftEnd.AddDays(1);
        }

        var punctuality =
            BuildPunctualityStatus(
                a.Status,
                shiftStart,
                shiftEnd,
                a.CheckInTime,
                a.CheckOutTime
            );

        return new ShiftAssignmentDto(
            a.ShiftAssignmentId,
            a.Employee?.User
                ?.UserId ?? 0,
            a.Employee?.User
                ?.FullName ?? "",
            a.ShiftId,
            a.Shift?.ShiftName ?? "",
            a.Shift?.StartTime ??
                TimeSpan.Zero,
            a.Shift?.EndTime ??
                TimeSpan.Zero,
            workDate,
            areaIds
                .Cast<int?>()
                .FirstOrDefault(),
            areaNames.FirstOrDefault(),
            areaIds,
            areaNames,
            a.Status,
            a.CheckInTime,
            a.CheckOutTime,
            punctuality.LateMinutes,
            punctuality.EarlyLeaveMinutes,
            punctuality.CheckInStatus,
            punctuality.CheckOutStatus,
            punctuality.AttendanceStatus
        );
    }
}
