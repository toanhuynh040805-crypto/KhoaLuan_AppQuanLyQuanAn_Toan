using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record LoginRequest(string Username, string Password);

// Endpoint dang nhap dung chung cho tat ca Role (WinForm goi POST /api/auth/login).
// Truoc day project chua co Controller nay -> WinForm luon bao loi ket noi/404 khi dang nhap.
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuthController(AppDbContext db) => _db = db;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Sai tài khoản hoặc tài khoản đã bị khóa" });

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Sai mật khẩu" });

        // Tra ve dung shape ma WinForm dang mong doi (LoginResultDto):
        // UserId, FullName, Username, RoleName
        return Ok(new
        {
            UserId = user.Id,
            user.FullName,
            user.Username,
            RoleName = user.Role?.Name ?? ""
        });
    }
}
