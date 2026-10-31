using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

public sealed class ReservationAudienceService
{
    private readonly AppDbContext _db;

    public ReservationAudienceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<int>> GetRecipientUserIdsAsync(
        IEnumerable<int> areaIds,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        var areas = areaIds.Where(id => id > 0).Distinct().ToHashSet();
        var recipients = await _db.UserRoles
            .AsNoTracking()
            .Where(ur =>
                ur.IsActive &&
                ur.User != null &&
                ur.User.IsActive &&
                ur.Role != null &&
                (ur.Role.RoleName == "QuanLy" || ur.Role.RoleName == "ThuNgan"))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = recipients.ToHashSet();
        if (areas.Count == 0)
            return result;

        var servers = await _db.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.Status == EmployeeStatus.DangLamViec &&
                employee.User != null &&
                employee.User.IsActive &&
                employee.User.UserRoles.Any(ur =>
                    ur.IsActive &&
                    ur.Role != null &&
                    ur.Role.RoleName == "PhucVu"))
            .Select(employee => new
            {
                employee.EmployeeId,
                employee.UserId
            })
            .ToListAsync(cancellationToken);

        if (servers.Count == 0)
            return result;

        var employeeIds = servers.Select(item => item.EmployeeId).ToList();
        var workDate = DateOnly.FromDateTime(at.Date);

        var assignments = await _db.ShiftAssignments
            .AsNoTracking()
            .Include(assignment => assignment.Shift)
            .Include(assignment => assignment.AssignedAreas)
            .Where(assignment =>
                employeeIds.Contains(assignment.EmployeeId) &&
                assignment.WorkDate == workDate &&
                assignment.Status != AssignmentStatus.VangMat)
            .ToListAsync(cancellationToken);

        foreach (var server in servers)
        {
            var currentAssignments = assignments
                .Where(assignment =>
                    assignment.EmployeeId == server.EmployeeId &&
                    assignment.Shift is not null &&
                    IsWithinShift(assignment.Shift, at.TimeOfDay))
                .ToList();

            if (currentAssignments.Count == 0)
                continue;

            var worksInArea = currentAssignments.Any(assignment =>
                (assignment.AreaId.HasValue && areas.Contains(assignment.AreaId.Value)) ||
                assignment.AssignedAreas.Any(area => areas.Contains(area.AreaId)));

            if (worksInArea)
                result.Add(server.UserId);
        }

        return result;
    }

    public async Task<bool> CanHandleOverdueAsync(
        int userId,
        int areaId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || areaId <= 0)
            return false;

        var recipients = await GetRecipientUserIdsAsync(
            new[] { areaId },
            at,
            cancellationToken);

        return recipients.Contains(userId);
    }

    private static bool IsWithinShift(Shift shift, TimeSpan now)
    {
        if (shift.StartTime <= shift.EndTime)
            return now >= shift.StartTime && now <= shift.EndTime;

        // Ca qua đêm, ví dụ 18:00 - 02:00.
        return now >= shift.StartTime || now <= shift.EndTime;
    }
}
