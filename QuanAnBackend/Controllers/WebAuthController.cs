using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Legacy auth implementation kept only for rollback/reference.
// Canonical customer authentication is CustomerAuthController at /api/customers/*.
// Keeping a separate route prevents Swagger/ASP.NET endpoint conflicts.
[Route("api/customers-legacy")]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebAuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;

    public WebAuthController(AppDbContext context, JwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    // 1. API ĐĂNG KÝ
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] WebRegisterDto request)
    {
        if (string.IsNullOrEmpty(request.Phone) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Số điện thoại và mật khẩu là bắt buộc." });

        string safePhone = request.Phone;
        string safeEmail = request.Email ?? "";

        // SỬA LỖI SQL 500: Dùng LINQ thay vì SqlQueryRaw để đếm số lượng
        var exists = await _context.Customers
            .AnyAsync(c => c.Phone == safePhone || (!string.IsNullOrEmpty(safeEmail) && c.Email == safeEmail));

        if (exists)
            return BadRequest(new { message = "Số điện thoại hoặc Email này đã được đăng ký." });

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        object emailParam = string.IsNullOrEmpty(request.Email) ? DBNull.Value : request.Email;
        object fullNameParam = string.IsNullOrEmpty(request.FullName) ? "Khách hàng" : request.FullName;

        // Vẫn có thể dùng ExecuteSqlRawAsync cho lệnh INSERT vì nó không cần map kết quả trả về
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Customers (FullName, Phone, Email, PasswordHash, MemberLevel, LoyaltyPoint, CreatedAt) VALUES ({0}, {1}, {2}, {3}, N'ThanhVien', 0, GETDATE())",
            fullNameParam, safePhone, emailParam, passwordHash
        );

        return Ok(new { message = "Đăng ký tài khoản thành công! Vui lòng đăng nhập." });
    }

    // 2. API ĐĂNG NHẬP
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] WebLoginDto request)
    {
        if (string.IsNullOrEmpty(request.Identifier) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Vui lòng nhập tài khoản và mật khẩu." });

        string safeIdentifier = request.Identifier;

        // SỬA LỖI SQL: Dùng LINQ để lấy thông tin đăng nhập một cách an toàn
        var customerInfo = await _context.Customers
            .Where(c => c.Phone == safeIdentifier || c.Email == safeIdentifier)
            .Select(c => new CustomerAuthResult
            {
                CustomerId = c.CustomerId,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                PasswordHash = c.PasswordHash
            })
            .FirstOrDefaultAsync();

        if (customerInfo == null || string.IsNullOrEmpty(customerInfo.PasswordHash))
            return Unauthorized(new { message = "Tài khoản không tồn tại hoặc chưa cài đặt mật khẩu." });

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, customerInfo.PasswordHash);
        if (!isPasswordValid)
            return Unauthorized(new { message = "Mật khẩu không chính xác." });

        var userForToken = new User
        {
            UserId = customerInfo.CustomerId,
            Username = customerInfo.Email ?? customerInfo.Phone ?? "Unknown",
            FullName = customerInfo.FullName ?? "Khách hàng"
        };

        var (token, _) = _jwtTokenService.GenerateToken(userForToken, new List<string> { "Customer" });

        return Ok(new
        {
            accessToken = token,
            message = "Đăng nhập thành công",
            user = new
            {
                id = customerInfo.CustomerId,
                fullName = customerInfo.FullName,
                email = customerInfo.Email,
                phone = customerInfo.Phone
            }
        });
    }

    // 3. API REFRESH TOKEN
    [HttpPost("refresh")]
    [Authorize(Roles = "Customer")]
    public IActionResult Refresh()
    {
        var userIdString = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var userForToken = new User
        {
            UserId = userId,
            Username = username ?? "Unknown",
            FullName = "Khách hàng"
        };

        var (newToken, _) = _jwtTokenService.GenerateToken(userForToken, new List<string> { "Customer" });

        return Ok(new { accessToken = newToken, message = "Đã làm mới phiên đăng nhập." });
    }

    // 4. API LOGOUT
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        return Ok(new { message = "Đăng xuất thành công." });
    }
}

// ================= DTOs =================

public class WebLoginDto
{
    public string? Identifier { get; set; }
    public string? Password { get; set; }
}

public class WebRegisterDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public class CustomerAuthResult
{
    public int CustomerId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
}