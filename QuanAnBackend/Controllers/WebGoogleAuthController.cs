using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

[Route("api/auth")]
[ApiController]
public class WebGoogleAuthController : ControllerBase
{
    public const string ExternalGoogleScheme = "ExternalGoogleAuth";
    private const string DefaultReturnPage = "pages/profile/index.html";

    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebGoogleAuthController> _logger;

    public WebGoogleAuthController(
        AppDbContext context,
        JwtTokenService jwtTokenService,
        IConfiguration configuration,
        ILogger<WebGoogleAuthController> logger)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("google/login")]
    [AllowAnonymous]
    public IActionResult LoginWithGoogle([FromQuery] string? returnUrl = null)
    {
        var safeReturn = NormalizeReturnUrl(returnUrl);
        _logger.LogInformation("[Google Auth] Khởi tạo phiên đăng nhập Google OAuth. returnUrl chuẩn hóa: {ReturnUrl}", safeReturn);
        
        var redirectUrl = Url.Action(nameof(GoogleCallback), "WebGoogleAuth", new { returnUrl = safeReturn });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        properties.Items["returnUrl"] = safeReturn;
        
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5500";

        var authenticateResult = await HttpContext.AuthenticateAsync(ExternalGoogleScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            _logger.LogWarning("[Google Auth] Xác thực {Scheme} thất bại hoặc không tìm thấy Principal.", ExternalGoogleScheme);
            return Redirect($"{frontendBaseUrl}/pages/auth/login_register.html?oauthError=auth_failed");
        }

        var effectiveReturnUrl = returnUrl;
        if (string.IsNullOrWhiteSpace(effectiveReturnUrl) &&
            authenticateResult.Properties?.Items.TryGetValue("returnUrl", out var propReturn) == true)
        {
            effectiveReturnUrl = propReturn;
        }

        var safeReturnPage = NormalizeReturnUrl(effectiveReturnUrl);

        var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);
        var fullName = authenticateResult.Principal.FindFirstValue(ClaimTypes.Name) ?? "Khách hàng Google";

        // Xóa cookie xác thực tạm thời sau khi trích xuất claims an toàn
        await HttpContext.SignOutAsync(ExternalGoogleScheme);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("[Google Auth] Principal không chứa ClaimTypes.Email.");
            return Redirect($"{frontendBaseUrl}/pages/auth/login_register.html?oauthError=no_email");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.Trim().ToLower() == normalizedEmail);

        if (customer == null)
        {
            _logger.LogInformation("[Google Auth] Email {Email} chưa đăng ký tài khoản. Chuyển hướng tới hoàn tất hồ sơ.", normalizedEmail);
            var encodedEmail = Uri.EscapeDataString(normalizedEmail);
            var encodedName = Uri.EscapeDataString(fullName);
            var encodedReturn = Uri.EscapeDataString("/" + safeReturnPage);
            return Redirect($"{frontendBaseUrl}/pages/auth/complete-profile.html?email={encodedEmail}&name={encodedName}&returnUrl={encodedReturn}");
        }

        _logger.LogInformation("[Google Auth] Đăng nhập thành công cho Customer ID: {CustomerId}, Email: {Email}.", customer.CustomerId, normalizedEmail);

        var userForToken = new User
        {
            UserId = customer.CustomerId,
            Username = customer.Email ?? customer.Phone ?? normalizedEmail,
            FullName = customer.FullName ?? fullName
        };
        var (token, _) = _jwtTokenService.GenerateToken(userForToken, new List<string> { "Customer" });

        var encodedToken = Uri.EscapeDataString(token);
        var separator = safeReturnPage.Contains('?') ? "&" : "?";

        return Redirect($"{frontendBaseUrl}/{safeReturnPage}{separator}accessToken={encodedToken}&token={encodedToken}");
    }

    [HttpPost("google/complete-profile")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "Email và số điện thoại không được để trống." });
        }

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        var trimmedPhone = request.Phone.Trim();
        var trimmedFullName = string.IsNullOrWhiteSpace(request.FullName) ? "Khách hàng" : request.FullName.Trim();

        var phoneExists = await _context.Customers
            .AnyAsync(c => c.Phone == trimmedPhone);

        if (phoneExists)
        {
            return BadRequest(new { message = "Số điện thoại này đã được sử dụng. Vui lòng nhập số khác." });
        }

        var emailExists = await _context.Customers
            .AnyAsync(c => c.Email != null && c.Email.Trim().ToLower() == trimmedEmail);

        if (emailExists)
        {
            return BadRequest(new { message = "Email này đã được đăng ký." });
        }

        var newCustomer = new Customer
        {
            FullName = trimmedFullName,
            Phone = trimmedPhone,
            Email = trimmedEmail,
            MemberLevel = MemberLevel.ThanhVien,
            LoyaltyPoint = 0,
            CreatedAt = DateTime.Now
        };

        _context.Customers.Add(newCustomer);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Google Auth] Đăng ký tài khoản mới thành công: Customer ID {CustomerId}, Phone {Phone}, Email {Email}.", newCustomer.CustomerId, trimmedPhone, trimmedEmail);

        var userForToken = new User
        {
            UserId = newCustomer.CustomerId,
            Username = newCustomer.Email ?? newCustomer.Phone ?? trimmedEmail,
            FullName = newCustomer.FullName
        };
        var (token, _) = _jwtTokenService.GenerateToken(userForToken, new List<string> { "Customer" });

        return Ok(new
        {
            accessToken = token,
            message = "Đăng ký thành công!",
            user = new
            {
                id = newCustomer.CustomerId,
                fullName = newCustomer.FullName,
                email = newCustomer.Email,
                phone = newCustomer.Phone
            }
        });
    }

    /// <summary>
    /// Chuẩn hóa đường dẫn returnUrl, chống Open Redirect và tự động sửa các lỗi đường dẫn tương đối (như /profile/index.html).
    /// </summary>
    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return DefaultReturnPage;
        }

        var decoded = Uri.UnescapeDataString(returnUrl).Trim();

        // Chống Open Redirect: Ngăn chặn domain ngoài hoặc scheme lạ
        if (decoded.StartsWith("//") || decoded.Contains("://") || decoded.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultReturnPage;
        }

        // Tách query string hoặc hash fragment
        var queryOrHashIndex = decoded.IndexOfAny(new[] { '?', '#' });
        var pathPart = queryOrHashIndex >= 0 ? decoded[..queryOrHashIndex] : decoded;
        var suffixPart = queryOrHashIndex >= 0 ? decoded[queryOrHashIndex..] : string.Empty;

        // Bỏ các tiền tố điều hướng tương đối hoặc slash đầu
        while (pathPart.StartsWith("../") || pathPart.StartsWith("./") || pathPart.StartsWith("/"))
        {
            if (pathPart.StartsWith("../")) pathPart = pathPart[3..];
            else if (pathPart.StartsWith("./")) pathPart = pathPart[2..];
            else if (pathPart.StartsWith("/")) pathPart = pathPart[1..];
        }

        // Không cho phép quay lại chính trang đăng nhập/đăng ký
        if (pathPart.Contains("login_register", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultReturnPage;
        }

        // Khắc phục triệt để lỗi đường dẫn thiếu tiền tố pages/ (ví dụ profile/index.html)
        if (pathPart.Equals("profile/index.html", StringComparison.OrdinalIgnoreCase) ||
            pathPart.StartsWith("profile/", StringComparison.OrdinalIgnoreCase))
        {
            pathPart = "pages/" + pathPart;
        }

        if (string.IsNullOrWhiteSpace(pathPart))
        {
            return DefaultReturnPage;
        }

        return pathPart + suffixPart;
    }
}

public class CompleteProfileDto
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}
