using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record CreateUserRequest(
    string FullName,
    string Username,
    string Password,
    string? Phone,
    string? Email,
    string EmployeeCode,
    int RoleId,
    int? StationId = null);
public record UpdateUserRequest(
    string FullName,
    string? Phone,
    string? Email,
    int RoleId,
    int? StationId = null);
public record ResetPasswordRequest(string NewPassword);
public record UpdateEmployeeStationRequest(int? StationId);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "QuanLy")]
public class UsersController : ControllerBase
{
    private const int MinPasswordLength = 6;
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleId)
            .Select(r => new { r.RoleId, r.RoleName, r.Description })
            .ToListAsync();
        return Ok(roles);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var employees = await _db.Employees
            .AsNoTracking()
            .Select(e => new
            {
                Id = e.EmployeeId,
                UserId = e.UserId,
                FullName = e.User != null ? e.User.FullName : "",
                Username = e.User != null ? e.User.Username : "",
                Phone = e.User != null ? e.User.Phone : null,
                Email = e.User != null ? e.User.Email : null,
                e.EmployeeCode,
                e.StationId,
                StationName = e.Station != null ? e.Station.StationName : null,
                IsActive = e.User != null && e.User.IsActive,
                RoleId = e.User != null && e.User.UserRoles.Any(ur => ur.IsActive)
                    ? e.User.UserRoles.First(ur => ur.IsActive).RoleId
                    : 0,
                RoleName = e.User != null && e.User.UserRoles.Any(ur => ur.IsActive)
                    ? (e.User.UserRoles.First(ur => ur.IsActive).Role != null ? e.User.UserRoles.First(ur => ur.IsActive).Role!.RoleName : "")
                    : ""
            })
            .ToListAsync();

        return Ok(employees);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var username = request.Username.Trim();
        var employeeCode = request.EmployeeCode.Trim();

        if (string.IsNullOrWhiteSpace(request.FullName) || username.Length == 0 || employeeCode.Length == 0)
            return BadRequest(new { message = "Vui lòng nhập đủ họ tên, tên đăng nhập và mã nhân viên" });

        if (request.Password is null || request.Password.Length < MinPasswordLength)
            return BadRequest(new { message = $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự" });

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại" });

        if (await _db.Employees.AnyAsync(e => e.EmployeeCode == employeeCode))
            return BadRequest(new { message = "Mã nhân viên đã tồn tại" });

        var role = await _db.Roles.FindAsync(request.RoleId);
        if (role is null) return BadRequest(new { message = "Vai trò không tồn tại" });

        int? stationId = null;
        if (string.Equals(role.RoleName, "Bep", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.StationId.HasValue)
                return BadRequest(new { message = "Tài khoản chế biến phải chọn Bếp hoặc Quầy nước" });

            var stationExists = await _db.Stations
                .AsNoTracking()
                .AnyAsync(s => s.StationId == request.StationId.Value);
            if (!stationExists)
                return BadRequest(new { message = "Bộ phận chế biến không tồn tại" });

            stationId = request.StationId.Value;
        }
        else if (request.StationId.HasValue)
        {
            return BadRequest(new { message = "Chỉ tài khoản Bếp mới được gán Bếp/Quầy nước" });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                FullName = request.FullName.Trim(),
                Username = username,
                PasswordHash = PasswordHasher.Hash(request.Password),
                Phone = string.IsNullOrWhiteSpace(request.Phone)
                    ? null
                    : request.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email)
                    ? null
                    : request.Email.Trim(),
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var employee = new Employee
            {
                UserId = user.UserId,
                EmployeeCode = employeeCode,
                Position = role.RoleName,
                HireDate = DateTime.Now,
                Status = EmployeeStatus.DangLamViec,
                StationId = stationId
            };

            _db.Employees.Add(employee);

            _db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = request.RoleId,
                IsActive = true,
                AssignedBy = CurrentUserId,
                AssignedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                Id = employee.EmployeeId,
                UserId = user.UserId,
                employee.EmployeeCode,
                user.FullName,
                RoleId = request.RoleId,
                RoleName = role.RoleName,
                employee.StationId
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var employee = await _db.Employees.Include(e => e.User)!.ThenInclude(u => u!.UserRoles)
            .FirstOrDefaultAsync(e => e.EmployeeId == id);
        if (employee?.User is null) return NotFound();

        var role = await _db.Roles.FindAsync(request.RoleId);
        if (role is null) return BadRequest(new { message = "Vai trò không tồn tại" });

        int? stationId = null;
        if (string.Equals(role.RoleName, "Bep", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.StationId.HasValue)
                return BadRequest(new { message = "Tài khoản chế biến phải chọn Bếp hoặc Quầy nước" });

            var stationExists = await _db.Stations
                .AsNoTracking()
                .AnyAsync(s => s.StationId == request.StationId.Value);
            if (!stationExists)
                return BadRequest(new { message = "Bộ phận chế biến không tồn tại" });

            stationId = request.StationId.Value;
        }
        else if (request.StationId.HasValue)
        {
            return BadRequest(new { message = "Chỉ tài khoản Bếp mới được gán Bếp/Quầy nước" });
        }

        var activeRoleIds = employee.User.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.RoleId).ToList();
        var roleChanged = !(activeRoleIds.Count == 1 && activeRoleIds[0] == request.RoleId);

        if (roleChanged && employee.UserId == CurrentUserId && role.RoleName != "QuanLy")
            return BadRequest(new { message = "Bạn không thể tự đổi vai trò của chính mình" });

        employee.User.FullName = request.FullName.Trim();
        employee.User.Phone = request.Phone;
        employee.User.Email = request.Email;
        employee.User.UpdatedAt = DateTime.Now;

        if (roleChanged)
        {
            foreach (var ur in employee.User.UserRoles.Where(ur => ur.IsActive))
            {
                ur.IsActive = false;
                ur.RevokedAt = DateTime.Now;
            }

            var existingRole = employee.User.UserRoles.FirstOrDefault(ur => ur.RoleId == request.RoleId);
            if (existingRole is not null)
            {
                existingRole.IsActive = true;
                existingRole.RevokedAt = null;
                existingRole.AssignedBy = CurrentUserId;
                existingRole.AssignedAt = DateTime.Now;
            }
            else
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = employee.User.UserId,
                    RoleId = request.RoleId,
                    IsActive = true,
                    AssignedBy = CurrentUserId,
                    AssignedAt = DateTime.Now
                });
            }

            employee.Position = role.RoleName;
        }

        // Station là thuộc tính của nhân viên chế biến, không phải một Role riêng.
        // Khi đổi khỏi Role Bếp thì tự gỡ Station để không còn phân công cũ.
        employee.StationId = stationId;

        await _db.SaveChangesAsync();
        return Ok(new { Id = employee.EmployeeId });
    }

    [HttpPut("{id}/station")]
    public async Task<IActionResult> SetStation(int id, [FromBody] UpdateEmployeeStationRequest request)
    {
        var employee = await _db.Employees
            .Include(e => e.Station)
            .FirstOrDefaultAsync(e => e.EmployeeId == id);

        if (employee is null) return NotFound();

        if (request.StationId.HasValue)
        {
            if (!string.Equals(employee.Position, "Bep", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Chỉ tài khoản chế biến (Bep) mới được phân công Bếp/Quầy nước"
                });
            }

            var station = await _db.Stations.FindAsync(request.StationId.Value);
            if (station is null)
                return BadRequest(new { message = "Bộ phận chế biến không tồn tại" });

            employee.StationId = station.StationId;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                Id = employee.EmployeeId,
                employee.StationId,
                StationName = station.StationName
            });
        }

        employee.StationId = null;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            Id = employee.EmployeeId,
            StationId = (int?)null,
            StationName = (string?)null
        });
    }

    [HttpPut("{id}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] bool isActive)
    {
        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == id);
        if (employee?.User is null) return NotFound();

        if (!isActive && employee.UserId == CurrentUserId)
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản của chính mình" });

        employee.User.IsActive = isActive;
        employee.Status = isActive ? EmployeeStatus.DangLamViec : EmployeeStatus.TamNghi;
        await _db.SaveChangesAsync();
        return Ok(new { Id = employee.EmployeeId, IsActive = isActive });
    }

    [HttpPut("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (request.NewPassword is null || request.NewPassword.Length < MinPasswordLength)
            return BadRequest(new { message = $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự" });

        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == id);
        if (employee?.User is null) return NotFound();

        employee.User.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        employee.User.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { Id = employee.EmployeeId });
    }
}
