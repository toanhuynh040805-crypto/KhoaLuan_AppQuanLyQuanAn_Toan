using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record CustomerForgotPasswordRequest(string? Email);
public record CustomerResetPasswordRequest(string? Token, string? NewPassword);

// Quên / đặt lại mật khẩu cho khách web.
// Route khớp web: POST /api/auth/forgot-password và /api/auth/reset-password
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class CustomerPasswordResetController : ControllerBase
{
    private const int MinPasswordLength = 6;
    private const int TokenMinutes = 30;
    private const int ResendCooldownSeconds = 60;
    private const string GenericOk =
        "Nếu email tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi.";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly ILogger<CustomerPasswordResetController> _logger;

    public CustomerPasswordResetController(
        AppDbContext db,
        IConfiguration config,
        IEmailService email,
        ILogger<CustomerPasswordResetController> logger)
    {
        _db = db;
        _config = config;
        _email = email;
        _logger = logger;
    }

    // POST api/auth/forgot-password  { email }
    // Luôn trả cùng một thông điệp để không lộ email nào đã đăng ký.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] CustomerForgotPasswordRequest request)
    {
        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0 || !email.Contains('@'))
            return BadRequest(new { message = "Vui lòng nhập email hợp lệ." });

        var ids = await _db.Database.SqlQuery<int>($"""
            SELECT c.CustomerId AS Value
            FROM dbo.Customers c
            INNER JOIN dbo.CustomerAccounts a ON a.CustomerId = c.CustomerId
            WHERE c.Email = {email} AND a.IsActive = 1
            """).ToListAsync();

        if (ids.Count != 1)
            return Ok(new { message = GenericOk });

        var customerId = ids[0];

        // Chống spam: token vừa được cấp trong vòng ResendCooldownSeconds thì bỏ qua.
        var recentlyIssued = await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.CustomerAccounts
            WHERE CustomerId = {customerId}
              AND PasswordResetExpiresAt > DATEADD(SECOND, {ResendCooldownSeconds - TokenMinutes * 60}, GETDATE())
            """).SingleAsync();
        if (recentlyIssued > 0)
            return Ok(new { message = GenericOk });

        var token = CustomerTokens.NewRandomToken();
        var hash = CustomerTokens.Hash(token);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerAccounts
            SET PasswordResetTokenHash = {hash},
                PasswordResetExpiresAt = DATEADD(MINUTE, {TokenMinutes}, GETDATE())
            WHERE CustomerId = {customerId}
            """);

        var customer = await _db.Customers.AsNoTracking()
            .FirstAsync(c => c.CustomerId == customerId);

        var baseUrl = (_config["Web:BaseUrl"] ?? "http://localhost:5501").TrimEnd('/');
        var link = $"{baseUrl}/pages/auth/reset-password.html?token={Uri.EscapeDataString(token)}";
        var name = WebUtility.HtmlEncode(customer.FullName);

        var html = $"""
            <p>Xin chào {name},</p>
            <p>Bạn vừa yêu cầu đặt lại mật khẩu tại GustoPOS. Nhấn vào liên kết bên dưới (có hiệu lực {TokenMinutes} phút):</p>
            <p><a href="{link}">Đặt lại mật khẩu</a></p>
            <p>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</p>
            """;

        try
        {
            await _email.SendAsync(email, customer.FullName, "Đặt lại mật khẩu GustoPOS", html);
        }
        catch (Exception ex)
        {
            // Không báo lỗi cho client để không lộ trạng thái tài khoản.
            _logger.LogError(ex, "Gửi email đặt lại mật khẩu thất bại cho khách {CustomerId}", customerId);
        }

        return Ok(new { message = GenericOk });
    }

    // POST api/auth/reset-password  { token, newPassword }
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] CustomerResetPasswordRequest request)
    {
        var token = request.Token?.Trim() ?? "";
        var newPassword = request.NewPassword ?? "";

        if (token.Length == 0)
            return BadRequest(new { message = "Liên kết không hợp lệ." });
        if (newPassword.Length < MinPasswordLength)
            return BadRequest(new { message = $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự." });

        var hash = CustomerTokens.Hash(token);
        var newHash = PasswordHasher.Hash(newPassword);

        var customerId = await _db.Database.SqlQuery<int>($"""
            SELECT TOP 1 CustomerId AS Value
            FROM dbo.CustomerAccounts
            WHERE PasswordResetTokenHash = {hash}
              AND PasswordResetExpiresAt > GETDATE()
              AND IsActive = 1
            """).FirstOrDefaultAsync();

        if (customerId == 0)
            return BadRequest(new { message = "Liên kết đã hết hạn hoặc không hợp lệ." });

        // Điều kiện hash trong WHERE bảo đảm token chỉ dùng được 1 lần.
        var updated = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerAccounts
            SET PasswordHash = {newHash},
                PasswordResetTokenHash = NULL,
                PasswordResetExpiresAt = NULL,
                FailedLoginCount = 0,
                LockedUntil = NULL
            WHERE CustomerId = {customerId}
              AND PasswordResetTokenHash = {hash}
            """);

        if (updated == 0)
            return BadRequest(new { message = "Liên kết đã hết hạn hoặc không hợp lệ." });

        // Đăng xuất mọi thiết bị.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerRefreshTokens SET RevokedAt = GETDATE()
            WHERE CustomerId = {customerId} AND RevokedAt IS NULL
            """);

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }
}