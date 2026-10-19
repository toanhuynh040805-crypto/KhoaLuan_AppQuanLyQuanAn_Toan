using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record RegisterCustomerRequest(string? FullName, string? Phone, string? Email, string? Password);
public record CustomerLoginRequest(string? Identifier, string? Password);

// Đăng ký / đăng nhập khách hàng web. Route khớp những gì web đang gọi:
// /api/customers/register | login | refresh | logout
[ApiController]
[Route("api/customers")]
[AllowAnonymous]
public class CustomerAuthController : ControllerBase
{
    private const int MinPasswordLength = 6;
    private const int MaxFailedLogins = 5;
    private const string BadCredentials = "Tài khoản hoặc mật khẩu không chính xác.";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public CustomerAuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private sealed class AccountRow
    {
        public int CustomerId { get; set; }
        public string PasswordHash { get; set; } = "";
        public bool IsActive { get; set; }
        public int FailedLoginCount { get; set; }
        public bool IsLocked { get; set; }
    }

    // ---------------------------------------------------------------
    // POST api/customers/register
    // Nếu SĐT đã có trong Customers (thu ngân tạo trước) mà chưa có tài khoản
    // thì nối tài khoản vào khách đó (giữ điểm tích lũy).
    // LƯU Ý: chưa xác minh SĐT/email bằng OTP - nên bổ sung nếu đưa vào vận hành thật.
    // ---------------------------------------------------------------
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request)
    {
        var fullName = request.FullName?.Trim() ?? "";
        var phone = NormalizePhone(request.Phone);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var password = request.Password ?? "";

        if (fullName.Length < 2 || fullName.Length > 100)
            return BadRequest(new { message = "Họ và tên phải từ 2 đến 100 ký tự." });
        if (phone.Length < 9 || phone.Length > 11)
            return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        if (email is not null && !IsValidEmail(email))
            return BadRequest(new { message = "Email không hợp lệ." });
        if (password.Length < MinPasswordLength)
            return BadRequest(new { message = $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự." });

        if (email is not null &&
            await _db.Customers.AnyAsync(c => c.Email == email && c.Phone != phone))
            return Conflict(new { message = "Email này đã được sử dụng." });

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer is not null)
        {
            var hasAccount = await _db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.CustomerAccounts WHERE CustomerId = {customer.CustomerId}")
                .SingleAsync();
            if (hasAccount > 0)
                return Conflict(new { message = "Số điện thoại này đã được đăng ký tài khoản." });

            if (string.IsNullOrWhiteSpace(customer.Email) && email is not null)
                customer.Email = email;
        }
        else
        {
            customer = new Customer { FullName = fullName, Phone = phone, Email = email };
            _db.Customers.Add(customer);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Số điện thoại này đã được đăng ký." });
        }

        var hash = PasswordHasher.Hash(password);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.CustomerAccounts (CustomerId, PasswordHash)
            VALUES ({customer.CustomerId}, {hash})
            """);

        return Ok(new { message = "Đăng ký thành công." });
    }

    // ---------------------------------------------------------------
    // POST api/customers/login  { identifier (SĐT hoặc email), password }
    // ---------------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] CustomerLoginRequest request)
    {
        var identifier = request.Identifier?.Trim() ?? "";
        var password = request.Password ?? "";
        if (identifier.Length == 0 || password.Length == 0)
            return BadRequest(new { message = "Vui lòng nhập tài khoản và mật khẩu." });

        List<int> ids;
        if (identifier.Contains('@'))
        {
            ids = await _db.Database.SqlQuery<int>($"""
                SELECT c.CustomerId AS Value
                FROM dbo.Customers c
                INNER JOIN dbo.CustomerAccounts a ON a.CustomerId = c.CustomerId
                WHERE c.Email = {identifier}
                """).ToListAsync();
        }
        else
        {
            var phone = NormalizePhone(identifier);
            ids = await _db.Database.SqlQuery<int>($"""
                SELECT c.CustomerId AS Value
                FROM dbo.Customers c
                INNER JOIN dbo.CustomerAccounts a ON a.CustomerId = c.CustomerId
                WHERE c.Phone = {phone}
                """).ToListAsync();
        }

        if (ids.Count != 1)
            return Unauthorized(new { message = BadCredentials });

        var customerId = ids[0];
        var account = await _db.Database.SqlQuery<AccountRow>($"""
            SELECT CustomerId, PasswordHash, IsActive, FailedLoginCount,
                   CAST(CASE WHEN LockedUntil > GETDATE() THEN 1 ELSE 0 END AS BIT) AS IsLocked
            FROM dbo.CustomerAccounts
            WHERE CustomerId = {customerId}
            """).SingleAsync();

        if (!account.IsActive)
            return StatusCode(403, new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quán." });
        if (account.IsLocked)
            return StatusCode(429, new { message = "Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút." });

        if (!PasswordHasher.Verify(password, account.PasswordHash))
        {
            var failed = account.FailedLoginCount + 1;
            if (failed >= MaxFailedLogins)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.CustomerAccounts
                    SET FailedLoginCount = 0, LockedUntil = DATEADD(MINUTE, 15, GETDATE())
                    WHERE CustomerId = {customerId}
                    """);
            }
            else
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.CustomerAccounts SET FailedLoginCount = {failed}
                    WHERE CustomerId = {customerId}
                    """);
            }
            return Unauthorized(new { message = BadCredentials });
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerAccounts
            SET FailedLoginCount = 0, LockedUntil = NULL, LastLoginAt = GETDATE()
            WHERE CustomerId = {customerId};

            DELETE FROM dbo.CustomerRefreshTokens
            WHERE ExpiresAt < GETDATE() OR RevokedAt < DATEADD(DAY, -7, GETDATE());
            """);

        var customer = await _db.Customers.AsNoTracking()
            .FirstAsync(c => c.CustomerId == customerId);
        return await IssueSessionAsync(customer);
    }

    // ---------------------------------------------------------------
    // POST api/customers/refresh  (đọc refresh token từ cookie HttpOnly)
    // Token cũ vẫn dùng được thêm 30 giây để nhiều tab/trang cùng refresh không bị đá ra.
    // ---------------------------------------------------------------
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var raw = Request.Cookies[CustomerTokens.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(raw))
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });

        var hash = CustomerTokens.Hash(raw);
        var customerId = await _db.Database.SqlQuery<int>($"""
            SELECT TOP 1 CustomerId AS Value
            FROM dbo.CustomerRefreshTokens
            WHERE TokenHash = {hash}
              AND ExpiresAt > GETDATE()
              AND (RevokedAt IS NULL OR RevokedAt > DATEADD(SECOND, -30, GETDATE()))
            """).FirstOrDefaultAsync();

        if (customerId == 0)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        }

        var active = await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.CustomerAccounts
            WHERE CustomerId = {customerId} AND IsActive = 1
            """).SingleAsync();
        if (active == 0)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.CustomerRefreshTokens SET RevokedAt = GETDATE()
            WHERE TokenHash = {hash} AND RevokedAt IS NULL
            """);

        var customer = await _db.Customers.AsNoTracking()
            .FirstAsync(c => c.CustomerId == customerId);
        return await IssueSessionAsync(customer);
    }

    // ---------------------------------------------------------------
    // POST api/customers/logout
    // ---------------------------------------------------------------
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var raw = Request.Cookies[CustomerTokens.RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var hash = CustomerTokens.Hash(raw);
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.CustomerRefreshTokens SET RevokedAt = GETDATE()
                WHERE TokenHash = {hash} AND RevokedAt IS NULL
                """);
        }

        ClearRefreshCookie();
        return Ok(new { message = "Đã đăng xuất." });
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private async Task<IActionResult> IssueSessionAsync(Customer customer)
    {
        var (access, expiresAt) = CustomerTokens.CreateAccessToken(_config, customer);
        var refresh = CustomerTokens.NewRandomToken();
        var refreshHash = CustomerTokens.Hash(refresh);
        var days = (int)CustomerTokens.RefreshLifetime.TotalDays;

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.CustomerRefreshTokens (CustomerId, TokenHash, ExpiresAt)
            VALUES ({customer.CustomerId}, {refreshHash}, DATEADD(DAY, {days}, GETDATE()))
            """);

        Response.Cookies.Append(
            CustomerTokens.RefreshCookieName,
            refresh,
            BuildCookieOptions(DateTimeOffset.UtcNow.Add(CustomerTokens.RefreshLifetime)));

        return Ok(new { accessToken = access, expiresAt, tokenType = "Bearer" });
    }

    // Web ở origin khác (vd 127.0.0.1:5500) cần SameSite=None + Secure (HTTPS).
    // Trên HTTP localhost dùng Lax: hãy mở web bằng http://localhost:5500, không dùng 127.0.0.1.
    private CookieOptions BuildCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
        Path = "/api/customers",
        Expires = expires
    };

    private void ClearRefreshCookie()
        => Response.Cookies.Delete(CustomerTokens.RefreshCookieName, BuildCookieOptions(null));

    private static string NormalizePhone(string? value)
        => new string((value ?? "").Where(char.IsDigit).ToArray());

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value).Address == value; }
        catch { return false; }
    }
}