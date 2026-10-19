using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record SetAreaAssignmentsRequest(
    List<int> AreaIds,
    DateTime? WorkDate = null
);

[ApiController]
[Route("api/area-assignments")]
public class AreaAssignmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AreaAccessService _areaAccess;
    private readonly MobilePermissionService _permissions;

    public AreaAssignmentsController(
        AppDbContext db,
        AreaAccessService areaAccess,
        MobilePermissionService permissions)
    {
        _db = db;
        _areaAccess = areaAccess;
        _permissions = permissions;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] DateTime? workDate)
    {
        var access = await _areaAccess.GetAccessAsync(User, workDate);
        var ids = access.AreaIds.ToList();

        var areas = access.Restricted
            ? await _db.Areas.AsNoTracking()
                .Where(a => ids.Contains(a.AreaId))
                .OrderBy(a => a.AreaName)
                .Select(a => new { a.AreaId, a.AreaName })
                .ToListAsync()
            : await _db.Areas.AsNoTracking()
                .OrderBy(a => a.AreaName)
                .Select(a => new { a.AreaId, a.AreaName })
                .ToListAsync();

        return Ok(new
        {
            restricted = access.Restricted,
            areaIds = access.Restricted ? ids : areas.Select(a => a.AreaId).ToList(),
            areas
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? workDate)
    {
        if (!await _permissions.HasPermissionAsync(User, "area.assign"))
            return Forbid();

        var day = workDate?.Date;
        var employees = await _db.Employees
            .AsNoTracking()
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new
            {
                e.EmployeeId,
                e.EmployeeCode,
                FullName = e.User != null ? e.User.FullName : e.EmployeeCode,
                Position = e.Position
            })
            .ToListAsync();

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            var assignmentMap = new Dictionary<int, List<int>>();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EmployeeId, AreaId
                FROM EmployeeAreaAssignments
                WHERE IsActive = 1
                  AND ((@WorkDate IS NULL AND WorkDate IS NULL) OR WorkDate = @WorkDate)
                ORDER BY EmployeeId, AreaId;
                """;

            var p = command.CreateParameter();
            p.ParameterName = "@WorkDate";
            p.Value = day.HasValue ? day.Value : DBNull.Value;
            command.Parameters.Add(p);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var employeeId = reader.GetInt32(0);
                if (!assignmentMap.TryGetValue(employeeId, out var list))
                {
                    list = new List<int>();
                    assignmentMap[employeeId] = list;
                }
                list.Add(reader.GetInt32(1));
            }

            return Ok(employees.Select(e => new
            {
                e.EmployeeId,
                e.EmployeeCode,
                e.FullName,
                e.Position,
                workDate = day,
                areaIds = assignmentMap.TryGetValue(e.EmployeeId, out var ids)
                    ? ids
                    : new List<int>()
            }));
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    [HttpPut("{employeeCode}")]
    public async Task<IActionResult> SetAssignments(
        string employeeCode,
        [FromBody] SetAreaAssignmentsRequest request)
    {
        if (!await _permissions.HasPermissionAsync(User, "area.assign"))
            return Forbid();

        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode);

        if (employee is null)
            return NotFound(new { message = "Không tìm thấy nhân viên" });

        var distinctAreaIds = request.AreaIds.Where(id => id > 0).Distinct().ToList();
        var validAreaIds = await _db.Areas.AsNoTracking()
            .Where(a => distinctAreaIds.Contains(a.AreaId))
            .Select(a => a.AreaId)
            .ToListAsync();

        if (validAreaIds.Count != distinctAreaIds.Count)
            return BadRequest(new { message = "Có khu vực không tồn tại" });

        var assignedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? assignedByUserId = int.TryParse(assignedBy, out var uid) ? uid : null;
        var day = request.WorkDate?.Date;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using (var deactivate = connection.CreateCommand())
            {
                deactivate.Transaction = transaction;
                deactivate.CommandText = """
                    UPDATE EmployeeAreaAssignments
                    SET IsActive = 0
                    WHERE EmployeeId = @EmployeeId
                      AND ((@WorkDate IS NULL AND WorkDate IS NULL) OR WorkDate = @WorkDate);
                    """;
                AddParameter(deactivate, "@EmployeeId", employee.EmployeeId);
                AddParameter(deactivate, "@WorkDate", day);
                await deactivate.ExecuteNonQueryAsync();
            }

            foreach (var areaId in validAreaIds)
            {
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO EmployeeAreaAssignments
                        (EmployeeId, AreaId, WorkDate, IsActive, AssignedBy, AssignedAt)
                    VALUES
                        (@EmployeeId, @AreaId, @WorkDate, 1, @AssignedBy, GETDATE());
                    """;
                AddParameter(insert, "@EmployeeId", employee.EmployeeId);
                AddParameter(insert, "@AreaId", areaId);
                AddParameter(insert, "@WorkDate", day);
                AddParameter(insert, "@AssignedBy", assignedByUserId);
                await insert.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return Ok(new { employee.EmployeeCode, workDate = day, areaIds = validAreaIds });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
