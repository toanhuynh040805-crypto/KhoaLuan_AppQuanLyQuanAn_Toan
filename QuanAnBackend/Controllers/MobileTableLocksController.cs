using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Authorization;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record MobileTableLockRequest(
    string? EmployeeCode,
    string? ClientToken
);

[ApiController]
[Route("api/mobile/table-locks")]
[Route("api/table-locks")]
[Authorize(Roles = "QuanLy,ThuNgan,PhucVu")]
public class MobileTableLocksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TableEditLockService _locks;
    private readonly IHubContext<OrderHub> _hub;

    public MobileTableLocksController(
        AppDbContext db,
        TableEditLockService locks,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _locks = locks;
        _hub = hub;
    }

    [HttpPost("{tableId:int}/acquire")]
    public async Task<IActionResult> Acquire(
        int tableId,
        [FromBody] MobileTableLockRequest request)
    {
        var clientToken = request.ClientToken?.Trim() ?? string.Empty;

        if (clientToken.Length == 0)
            return BadRequest(new { message = "Thiếu thông tin để khóa bàn" });

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("UserId");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ" });

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

        var identity = await _db.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId && user.IsActive)
            .Select(user => new
            {
                EmployeeCode = user.Employee != null
                    ? user.Employee.EmployeeCode
                    : user.Username,
                EmployeeName = user.FullName
            })
            .FirstOrDefaultAsync();

        if (identity is null)
            return Unauthorized(new { message = "Tài khoản không còn hoạt động" });

        var employeeCode = identity.EmployeeCode?.Trim() ?? string.Empty;
        if (employeeCode.Length == 0)
            return BadRequest(new { message = "Không xác định được mã nhân viên" });

        var employeeName = identity.EmployeeName?.Trim();
        if (string.IsNullOrWhiteSpace(employeeName))
            employeeName = employeeCode;

        _locks.TryAcquire(
            tableId,
            employeeCode,
            employeeName,
            clientToken,
            out var lease,
            out var previousLease);

        // Tài khoản/thiết bị vào sau được tiếp quản ngay, không phải chờ tài
        // khoản trước xác nhận. Phiên cũ nhận realtime và tự quay về sơ đồ bàn.
        if (previousLease is not null)
        {
            await _hub.Clients
                .Group($"employee-{previousLease.EmployeeCode}")
                .SendAsync("TableLockTakenOver", new
                {
                    tableId,
                    tableName = table.TableName,
                    previousClientToken = previousLease.ClientToken,
                    takenOverByEmployeeCode = employeeCode,
                    takenOverByEmployeeName = employeeName
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
