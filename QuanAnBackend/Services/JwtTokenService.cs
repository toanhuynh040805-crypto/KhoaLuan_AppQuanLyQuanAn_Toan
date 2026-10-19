using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

// Sinh JWT cho phien dang nhap. Token mang theo UserId, Username, FullName va
// TAT CA cac Role dang active cua User (1 User co the co nhieu Role cung luc -
// xem UserRoles). Backend dung [Authorize(Roles = "...")] de kiem tra quyen
// truy cap tung endpoint dua vao cac Role claim nay.
public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(User user, IEnumerable<string> roles)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key trong appsettings.json");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var expireMinutes = int.TryParse(jwtSection["ExpireMinutes"], out var m) ? m : 480;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expiresAt = DateTime.UtcNow.AddMinutes(expireMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}