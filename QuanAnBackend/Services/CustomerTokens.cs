using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QuanAnBackend.Middleware;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

/// <summary>
/// Sinh/kiểm tra token cho khách hàng web.
/// - Access token: JWT ngắn hạn, role "KhachHang", claim "CustomerId".
///   NameIdentifier cố ý là "c-{id}" (không phải số) để không bị nhầm với UserId nhân viên.
/// - Refresh token: chuỗi ngẫu nhiên, chỉ lưu SHA256 trong DB, gửi cho trình duyệt
///   qua cookie HttpOnly.
/// </summary>
public static class CustomerTokens
{
    public const string RefreshCookieName = "gusto_refresh";
    public const string CustomerIdClaim = "CustomerId";

    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public static (string Token, DateTime ExpiresAt) CreateAccessToken(
        IConfiguration config,
        Customer customer)
    {
        var jwt = config.GetSection("Jwt");
        var key = jwt["Key"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key");
        var minutes = int.TryParse(jwt["CustomerExpireMinutes"], out var m) ? m : 60;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"c-{customer.CustomerId}"),
            new(ClaimTypes.Name, customer.Phone),
            new("FullName", customer.FullName),
            new(CustomerIdClaim, customer.CustomerId.ToString()),
            new(ClaimTypes.Role, CustomerAccessMiddleware.CustomerRole)
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(minutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>Chuỗi ngẫu nhiên an toàn (dùng cho refresh token / token đặt lại mật khẩu).</summary>
    public static string NewRandomToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Lấy CustomerId từ token khách đang đăng nhập; null nếu không phải khách.</summary>
    public static int? GetCustomerId(ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(CustomerIdClaim), out var id) ? id : null;
}