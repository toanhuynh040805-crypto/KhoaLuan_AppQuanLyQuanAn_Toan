using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Authorization;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record MobileTableLockRequest(
    string? EmployeeCode,
    string? ClientToken
);

[ApiController]
[Route("api/mobile/table-locks")]
[RequireMobilePermission("table.manage")]
public class MobileTableLocksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TableEditLockService _locks;

    public MobileTableLocksController(
        AppDbContext db,
        TableEditLockService locks)
    {
        _db = db;
        _locks = locks;
    }

    [HttpPost("{tableId:int}/acquire")]
    public async Task<IActionResult> Acquire(
        int tableId,
        [FromBody] MobileTableLockRequest request)
    {
        var employeeCode = request.EmployeeCode?.Trim() ?? string.Empty;
        var clientToken = request.ClientToken?.Trim() ?? string.Empty;

        if (employeeCode.Length == 0 || clientToken.Length == 0)
            return BadRequest(new { message = "Thiếu thông tin để khóa bàn" });

        // Chỉ đọc đúng các cột cần cho thao tác khóa bàn. Trước đây endpoint
        // materialize toàn bộ DiningTable/Employee/User; trên database WinForms
        // hoặc database legacy chỉ cần một cột enum/schema khác phiên bản là EF
        // có thể ném 500 trước khi bàn được mở. Projection nhỏ giúp endpoint
        // tương thích với cùng database dùng chung mà không phụ thuộc các trường
        // Employee/User không liên quan tới khóa bàn.
        var table = await _db.Tables
            .AsNoTracking()
            .Where(x => x.TableId == tableId)
            .Select(x => new
            {
                x.TableId,
                x.TableName
            })
            .FirstOrDefaultAsync();

        if (table is null)
            return NotFound(new { message = "Không tìm thấy bàn" });

        var employee = await _db.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeCode == employeeCode)
            .Select(x => new
            {
                x.EmployeeCode,
                EmployeeName = x.User != null ? x.User.FullName : null
            })
            .FirstOrDefaultAsync();

        if (employee is null)
            return BadRequest(new { message = "Không tìm thấy nhân viên đang đăng nhập" });

        var employeeName = employee.EmployeeName?.Trim();
        if (string.IsNullOrWhiteSpace(employeeName))
            employeeName = employeeCode;

        if (!_locks.TryAcquire(
                tableId,
                employeeCode,
                employeeName,
                clientToken,
                out var lease,
                out var conflict))
        {
            return Conflict(new
            {
                message = $"{table.TableName} đang được {conflict!.EmployeeName} thao tác. Vui lòng chờ nhân viên đó thoát bàn rồi thử lại.",
                tableId,
                lockedByEmployeeCode = conflict.EmployeeCode,
                lockedByEmployeeName = conflict.EmployeeName,
                expiresAtUtc = conflict.ExpiresAtUtc
            });
        }

        return Ok(ToResponse(lease));
    }

    [HttpPost("{tableId:int}/heartbeat")]
    public IActionResult Heartbeat(
        int tableId,
        [FromBody] MobileTableLockRequest request)
    {
        var clientToken = request.ClientToken?.Trim() ?? string.Empty;
        if (clientToken.Length == 0)
            return BadRequest(new { message = "Thiếu mã phiên khóa bàn" });

        if (!_locks.TryHeartbeat(
                tableId,
                clientToken,
                out var lease,
                out var conflict))
        {
            var message = conflict is null
                ? "Quyền thao tác bàn đã hết hạn. Vui lòng mở lại bàn."
                : $"Bàn đang được {conflict.EmployeeName} thao tác trên thiết bị khác.";

            return Conflict(new
            {
                message,
                tableId,
                lockedByEmployeeCode = conflict?.EmployeeCode,
                lockedByEmployeeName = conflict?.EmployeeName,
                expiresAtUtc = conflict?.ExpiresAtUtc
            });
        }

        return Ok(ToResponse(lease!));
    }

    [HttpPost("{tableId:int}/release")]
    public IActionResult Release(
        int tableId,
        [FromBody] MobileTableLockRequest request)
    {
        var clientToken = request.ClientToken?.Trim() ?? string.Empty;
        if (clientToken.Length == 0)
            return BadRequest(new { message = "Thiếu mã phiên khóa bàn" });

        if (!_locks.Release(tableId, clientToken))
        {
            var current = _locks.GetActive(tableId);
            return Conflict(new
            {
                message = current is null
                    ? "Khóa bàn đã hết hạn"
                    : $"Bàn hiện thuộc quyền thao tác của {current.EmployeeName}",
                tableId
            });
        }

        return Ok(new { tableId, released = true });
    }

    private static object ToResponse(TableEditLockSnapshot lease)
    {
        return new
        {
            tableId = lease.TableId,
            employeeCode = lease.EmployeeCode,
            employeeName = lease.EmployeeName,
            clientToken = lease.ClientToken,
            expiresAtUtc = lease.ExpiresAtUtc
        };
    }
}
