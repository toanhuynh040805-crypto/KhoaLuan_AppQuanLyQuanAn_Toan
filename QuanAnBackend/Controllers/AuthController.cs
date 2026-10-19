using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string OldPassword, string NewPassword);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MinPasswordLength = 6;
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly MobilePermissionService _permissions;

    public AuthController(
        AppDbContext db,
        JwtTokenService jwt,
        MobilePermissionService permissions)
    {
        _db = db;
        _jwt = jwt;
        _permissions = permissions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Employee)
                .ThenInclude(e => e!.Station)
            .Include(u => u.UserRoles.Where(ur => ur.IsActive))
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Sai tài khoản hoặc tài khoản đã bị khóa" });

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Sai mật khẩu" });

        var activeRoles = user.UserRoles
            .Where(ur => ur.IsActive && ur.Role != null)
            .Select(ur => ur.Role!.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (activeRoles.Count == 0)
            return Unauthorized(new { message = "Tài khoản này chưa được gán vai trò (Role) nào" });

        var effectivePermissions = await _permissions.GetPermissionsForUserIdAsync(user.UserId);
        var primaryRole = activeRoles[0];
        var (token, expiresAt) = _jwt.GenerateToken(user, activeRoles);

        user.LastLoginAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            UserId = user.UserId,
            user.FullName,
            user.Username,
            EmployeeCode = user.Employee?.EmployeeCode ?? user.Username,
            Position = user.Employee?.Position,
            Phone = user.Phone,
            Email = user.Email,
            EmployeeStatus = user.Employee?.Status.ToString(),
            StationId = user.Employee?.StationId,
            StationName = user.Employee?.Station?.StationName,
            RoleName = primaryRole,
            Roles = activeRoles,
            Permissions = effectivePermissions.OrderBy(x => x).ToList(),
            Token = token,
            ExpiresAt = expiresAt
        });
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return BadRequest(new { message = $"Mật khẩu mới phải có ít nhất {MinPasswordLength} ký tự" });

        if (request.NewPassword == request.OldPassword)
            return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu cũ" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null || !user.IsActive) return Unauthorized();

        if (!PasswordHasher.Verify(request.OldPassword ?? "", user.PasswordHash))
            return BadRequest(new { message = "Mật khẩu cũ không đúng" });

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đổi mật khẩu thành công" });
    }
}
