using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

// Legacy password-reset implementation that stored password hashes directly
// on Customers. Canonical reset flow is CustomerPasswordResetController and
// CustomerAccounts, matching CustomerAuthController.
[Route("api/auth-legacy")]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[AllowAnonymous]
public class WebPasswordController : ControllerBase
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(15);
    private const string GenericForgotMessage = "N\u1EBFu email ph\u00F9 h\u1EE3p v\u1EDBi t\u00E0i kho\u1EA3n GustoPOS, h\u01B0\u1EDBng d\u1EABn \u0111\u1EB7t l\u1EA1i m\u1EADt kh\u1EA9u s\u1EBD \u0111\u01B0\u1EE3c g\u1EEDi.";
    private const string DeliveryErrorMessage = "D\u1ECBch v\u1EE5 email t\u1EA1m th\u1EDDi kh\u00F4ng kh\u1EA3 d\u1EE5ng. Vui l\u00F2ng th\u1EED l\u1EA1i sau.";

    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WebPasswordController> _logger;

    public WebPasswordController(
        AppDbContext context,
        IConfiguration config,
        IWebHostEnvironment environment,
        ILogger<WebPasswordController> logger)
    {
        _context = context;
        _config = config;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("forgot-password")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Vui l\u00F2ng cung c\u1EA5p email." });

        string frontendBaseUrl;
        try
        {
            frontendBaseUrl = GetFrontendBaseUrl();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Password reset frontend URL is not configured safely.");
            return StatusCode(500, new { message = DeliveryErrorMessage });
        }

        var normalizedEmail = request.Email.Trim();
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        // Keep the response identical for unknown accounts and accounts without a local password.
        if (customer == null || string.IsNullOrWhiteSpace(customer.PasswordHash))
            return Ok(new { message = GenericForgotMessage });

        var resetToken = GenerateResetToken(customer.Email!, customer.PasswordHash);
        var resetPage = new Uri(new Uri(frontendBaseUrl.TrimEnd('/') + "/"), "pages/auth/reset-password.html");
        var resetLink = $"{resetPage}?token={Uri.EscapeDataString(resetToken)}";

        if (!await SendEmailAsync(customer.Email!, resetLink))
            return StatusCode(503, new { message = DeliveryErrorMessage });

        return Ok(new { message = GenericForgotMessage });
    }

    [HttpPost("reset-password")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrEmpty(request.NewPassword))
            return BadRequest(new { message = "D\u1EEF li\u1EC7u kh\u00F4ng h\u1EE3p l\u1EC7." });

        if (request.NewPassword.Length < 8 ||
            request.NewPassword.Length > 128 ||
            !request.NewPassword.Any(char.IsLetter) ||
            !request.NewPassword.Any(char.IsDigit))
        {
            return BadRequest(new
            {
                message = "M\u1EADt kh\u1EA9u ph\u1EA3i c\u00F3 t\u1EEB 8 \u0111\u1EBFn 128 k\u00FD t\u1EF1, g\u1ED3m \u00EDt nh\u1EA5t m\u1ED9t ch\u1EEF c\u00E1i v\u00E0 m\u1ED9t ch\u1EEF s\u1ED1."
            });
        }

        var tokenPayload = ValidateResetToken(request.Token);
        if (tokenPayload == null)
            return BadRequest(new { message = "Li\u00EAn k\u1EBFt kh\u00F4ng h\u1EE3p l\u1EC7, \u0111\u00E3 h\u1EBFt h\u1EA1n ho\u1EB7c \u0111\u00E3 \u0111\u01B0\u1EE3c s\u1EED d\u1EE5ng." });

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Email == tokenPayload.Email);
        if (customer == null || string.IsNullOrWhiteSpace(customer.PasswordHash))
            return BadRequest(new { message = "Li\u00EAn k\u1EBFt kh\u00F4ng h\u1EE3p l\u1EC7 ho\u1EB7c \u0111\u00E3 h\u1EBFt h\u1EA1n." });

        var currentVersion = CreatePasswordVersion(customer.PasswordHash);
        if (!VersionsMatch(tokenPayload.PasswordVersion, currentVersion))
            return BadRequest(new { message = "Li\u00EAn k\u1EBFt kh\u00F4ng h\u1EE3p l\u1EC7, \u0111\u00E3 h\u1EBFt h\u1EA1n ho\u1EB7c \u0111\u00E3 \u0111\u01B0\u1EE3c s\u1EED d\u1EE5ng." });

        var previousPasswordHash = customer.PasswordHash;
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Compare-and-swap makes token use one-time, including concurrent reset requests.
        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Customers SET PasswordHash = {0} WHERE CustomerId = {1} AND PasswordHash = {2}",
            newPasswordHash,
            customer.CustomerId,
            previousPasswordHash);

        if (rowsAffected != 1)
            return BadRequest(new { message = "Li\u00EAn k\u1EBFt kh\u00F4ng h\u1EE3p l\u1EC7, \u0111\u00E3 h\u1EBFt h\u1EA1n ho\u1EB7c \u0111\u00E3 \u0111\u01B0\u1EE3c s\u1EED d\u1EE5ng." });

        return Ok(new { message = "M\u1EADt kh\u1EA9u \u0111\u00E3 \u0111\u01B0\u1EE3c c\u1EADp nh\u1EADt th\u00E0nh c\u00F4ng." });
    }

    private string GetFrontendBaseUrl()
    {
        var configured = _config["Frontend:BaseUrl"]?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!_environment.IsDevelopment())
                throw new InvalidOperationException("Frontend:BaseUrl must be configured outside Development.");
            configured = "http://localhost:5500";
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Frontend:BaseUrl must be an absolute HTTP(S) base URL without query or fragment.");
        }

        if (!_environment.IsDevelopment() &&
            (uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback))
        {
            throw new InvalidOperationException("Frontend:BaseUrl must use a public HTTPS URL outside Development.");
        }

        return uri.ToString().TrimEnd('/');
    }

    private string GenerateResetToken(string email, string passwordHash)
    {
        var key = GetSigningKey();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("purpose", "reset_password"),
            new Claim("password_version", CreatePasswordVersion(passwordHash)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: now.Add(ResetTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ResetTokenPayload? ValidateResetToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(GetSigningKey()),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
                return null;

            var purpose = jwtToken.Claims.FirstOrDefault(c => c.Type == "purpose")?.Value;
            var email = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email)?.Value;
            var passwordVersion = jwtToken.Claims.FirstOrDefault(c => c.Type == "password_version")?.Value;

            if (purpose != "reset_password" ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(passwordVersion))
                return null;

            return new ResetTokenPayload(email, passwordVersion);
        }
        catch (Exception ex) when (
            ex is SecurityTokenException ||
            ex is ArgumentException ||
            ex is FormatException)
        {
            return null;
        }
    }

    private byte[] GetSigningKey()
    {
        var configuredKey = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(configuredKey) || Encoding.UTF8.GetByteCount(configuredKey) < 32)
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 UTF-8 bytes.");

        return Encoding.UTF8.GetBytes(configuredKey);
    }

    private string CreatePasswordVersion(string passwordHash)
    {
        var key = GetSigningKey();
        var version = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(passwordHash));
        return Convert.ToHexString(version);
    }

    private static bool VersionsMatch(string suppliedVersion, string currentVersion)
    {
        try
        {
            var supplied = Convert.FromHexString(suppliedVersion);
            var current = Convert.FromHexString(currentVersion);
            return CryptographicOperations.FixedTimeEquals(supplied, current);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<bool> SendEmailAsync(string toEmail, string resetLink)
    {
        try
        {
            var smtpHost = _config["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.TryParse(_config["EmailSettings:SmtpPort"], out var port) ? port : 587;
            var emailUser = _config["EmailSettings:Email"] ?? "";
            var appPassword = _config["EmailSettings:AppPassword"] ?? "";
            var senderName = _config["EmailSettings:SenderName"] ?? "GustoPOS";

            using var message = new MailMessage
            {
                From = new MailAddress(emailUser, senderName),
                Subject = "\u0059\u00EAu c\u1EA7u \u0111\u1EB7t l\u1EA1i m\u1EADt kh\u1EA9u t\u00E0i kho\u1EA3n GustoPOS",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            var safeLink = WebUtility.HtmlEncode(resetLink);
            var minutes = ((int)ResetTokenLifetime.TotalMinutes).ToString();
            var htmlBody = """
                <!doctype html>
                <html lang="vi">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width,initial-scale=1">
                  <title>GustoPOS - &#272;&#7863;t l&#7841;i m&#7853;t kh&#7849;u</title>
                  <style>
                    @media only screen and (max-width:600px) {
                      .card { width:100% !important; }
                      .inner { padding:28px 22px !important; }
                    }
                  </style>
                </head>
                <body style="margin:0;padding:0;background:#f9f9f9;font-family:Arial,Helvetica,sans-serif;color:#292522;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f9f9f9;padding:28px 12px;">
                    <tr><td align="center">
                      <table role="presentation" class="card" width="600" cellspacing="0" cellpadding="0" style="width:600px;max-width:600px;background:#fff;border:1px solid #eee8e5;border-radius:14px;overflow:hidden;">
                        <tr><td align="center" style="padding:28px 24px;background:#ac2d00;">
                          <div style="font-size:25px;font-weight:700;letter-spacing:.4px;color:#fff;">GustoPOS</div>
                          <div style="margin-top:6px;font-size:12px;letter-spacing:1.2px;color:#ffe4d8;">&#272;&#7890; &#258;N NGON &middot; D&#7882;CH V&#7908; T&#7852;N T&Acirc;M</div>
                        </td></tr>
                        <tr><td class="inner" style="padding:38px 44px 30px;">
                          <h1 style="margin:0 0 18px;font-size:24px;line-height:1.3;color:#292522;">Y&ecirc;u c&#7847;u &#273;&#7863;t l&#7841;i m&#7853;t kh&#7849;u</h1>
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.7;color:#554d49;">Xin ch&agrave;o,</p>
                          <p style="margin:0 0 22px;font-size:15px;line-height:1.7;color:#554d49;">Ch&uacute;ng t&ocirc;i nh&#7853;n &#273;&#432;&#7907;c y&ecirc;u c&#7847;u &#273;&#7863;t l&#7841;i m&#7853;t kh&#7849;u cho t&agrave;i kho&#7843;n GustoPOS c&#7911;a b&#7841;n. Nh&#7845;n n&uacute;t b&ecirc;n d&#432;&#7899;i &#273;&#7875; t&#7841;o m&#7853;t kh&#7849;u m&#7899;i.</p>
                          <div style="margin:26px 0 23px;text-align:center;">
                            <a href="RESET_URL" style="display:inline-block;padding:16px 28px;border-radius:9px;background:#ac2d00;color:#fff;text-decoration:none;font-size:15px;font-weight:700;letter-spacing:.25px;">&#272;&#7862;T L&#7840;I M&#7852;T KH&#7848;U</a>
                          </div>
                          <p style="margin:0 0 18px;font-size:14px;line-height:1.7;color:#554d49;">V&igrave; l&yacute; do b&#7843;o m&#7853;t, li&ecirc;n k&#7871;t n&agrave;y c&oacute; hi&#7879;u l&#7921;c trong <strong>RESET_MINUTES ph&uacute;t</strong>.</p>
                          <p style="margin:0 0 16px;font-size:13px;line-height:1.7;color:#766e69;">N&#7871;u n&uacute;t kh&ocirc;ng ho&#7841;t &#273;&#7897;ng, b&#7841;n c&oacute; th&#7875; sao ch&eacute;p v&agrave; m&#7903; li&ecirc;n k&#7871;t d&#432;&#7899;i &#273;&acirc;y:<br><a href="RESET_URL" style="color:#ac2d00;word-break:break-all;">RESET_URL</a></p>
                          <hr style="height:1px;border:0;background:#eee8e5;margin:24px 0 20px;">
                          <p style="margin:0 0 16px;font-size:13px;line-height:1.7;color:#766e69;">N&#7871;u b&#7841;n kh&ocirc;ng y&ecirc;u c&#7847;u &#273;&#7863;t l&#7841;i m&#7853;t kh&#7849;u, h&atilde;y b&#7887; qua email n&agrave;y v&agrave; kh&ocirc;ng chia s&#7867; li&ecirc;n k&#7871;t n&agrave;y v&#7899;i ng&#432;&#7901;i kh&aacute;c. M&#7853;t kh&#7849;u hi&#7879;n t&#7841;i kh&ocirc;ng b&#7883; thay &#273;&#7893;i b&#7903;i y&ecirc;u c&#7847;u n&agrave;y.</p>
                          <p style="margin:0;font-size:13px;line-height:1.7;color:#554d49;">C&#7843;m &#417;n b&#7841;n &#273;&atilde; tin t&#432;&#7903;ng v&agrave; l&#7921;a ch&#7885;n GustoPOS.<br>Tr&acirc;n tr&#7885;ng,<br><strong>&#272;&#7897;i ng&#361; GustoPOS</strong></p>
                        </td></tr>
                        <tr><td align="center" style="padding:18px 24px;background:#fcfaf9;border-top:1px solid #eee8e5;">
                          <p style="margin:0;font-size:12px;line-height:1.6;color:#8b817c;">Email t&#7921; &#273;&#7897;ng t&#7915; GustoPOS. Vui l&ograve;ng kh&ocirc;ng tr&#7843; l&#7901;i tr&#7921;c ti&#7871;p email n&agrave;y.</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;
            htmlBody = htmlBody
                .Replace("RESET_URL", safeLink)
                .Replace("RESET_MINUTES", minutes);

            var textBody =
                "GustoPOS - Yeu cau dat lai mat khau" + Environment.NewLine + Environment.NewLine +
                "Chung toi nhan duoc yeu cau dat lai mat khau cho tai khoan GustoPOS cua ban." + Environment.NewLine +
                "Lien ket co hieu luc trong " + minutes + " phut." + Environment.NewLine + Environment.NewLine +
                "Mo lien ket nay de dat lai mat khau: " + resetLink + Environment.NewLine + Environment.NewLine +
                "Neu ban khong yeu cau, hay bo qua email va khong chia se lien ket nay. Mat khau hien tai khong bi thay doi boi yeu cau nay." + Environment.NewLine + Environment.NewLine +
                "Cam on ban da tin tuong va lua chon GustoPOS." + Environment.NewLine +
                "Doi ngu GustoPOS" + Environment.NewLine + Environment.NewLine +
                "Email tu dong; vui long khong tra loi truc tiep.";

            message.Body = textBody;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

            using var smtpClient = new SmtpClient(smtpHost, smtpPort);
            smtpClient.Credentials = new NetworkCredential(
                _config["EmailSettings:Email"] ?? "",
                _config["EmailSettings:AppPassword"] ?? "");
            smtpClient.EnableSsl = true;
            await smtpClient.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email.");
            return false;
        }
    }

    private sealed record ResetTokenPayload(string Email, string PasswordVersion);
}

public class ForgotPasswordDto { public string? Email { get; set; } }
public class ResetPasswordDto { public string? Token { get; set; } public string? NewPassword { get; set; } }