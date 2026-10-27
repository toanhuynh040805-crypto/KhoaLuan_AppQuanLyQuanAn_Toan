using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Middleware;
using QuanAnBackend.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
// Ảnh món dùng chung cho WinForms/Mobile. Tạo thư mục trước khi host khởi động
// để StaticFileMiddleware luôn nhận đúng WebRoot.
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "menu"));

var builder = WebApplication.CreateBuilder(args);

// Cloud hosting platforms commonly expose the listening port via PORT.
// Local development keeps using launchSettings.json (localhost:5000).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) &&
    int.TryParse(port, out var parsedPort) &&
    parsedPort > 0)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

// EF Core + SQL Server. .NET configuration automatically lets
// ConnectionStrings__DefaultConnection override appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Thiếu ConnectionStrings:DefaultConnection. " +
        "Hãy cấu hình appsettings hoặc biến môi trường ConnectionStrings__DefaultConnection."
    );
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// SignalR realtime.
builder.Services.AddSignalR();

// Khóa thao tác bàn theo lease để tránh hai nhân viên sửa cùng lúc.
builder.Services.AddSingleton<TableEditLockService>();

// Business services dùng chung WinForms + Mobile.
builder.Services.AddScoped<OrderRoutingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<MobilePermissionService>();
builder.Services.AddScoped<MobilePermissionSchemaService>();
builder.Services.AddScoped<AreaAccessService>();
builder.Services.AddScoped<AreaAssignmentSchemaService>();
builder.Services.AddScoped<AttendanceSettingsService>();
builder.Services.AddScoped<CustomerAccountSchemaService>();
builder.Services.AddScoped<MobileOperationAuditService>();
builder.Services.AddScoped<WinFormsCompatibilitySchemaService>();
builder.Services.AddScoped<SharedMenuCatalogService>();
builder.Services.AddScoped<BundledMenuImageService>();
builder.Services.AddHostedService<TimeoutMonitorService>();

// JWT for both Mobile and WinForms.
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // SignalR can pass JWT using access_token while upgrading to WebSocket.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// Require JWT by default; public endpoints explicitly use [AllowAnonymous].
builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Nhập token từ /api/auth/login; không cần gõ chữ Bearer."
        }
    );

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        }
    );
});

// CORS chỉ áp dụng cho trình duyệt (web khách, Swagger). WinForms/Mobile gọi native nên không bị ảnh hưởng.
// Origin cho phép = Cors:AllowedOrigins + origin của Web:BaseUrl (cùng web dùng trong link đặt lại mật khẩu).
// Biến môi trường: Cors__AllowedOrigins__0, Cors__AllowedOrigins__1, ...
var corsOrigins = new List<string>(
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>());

if (Uri.TryCreate(builder.Configuration["Web:BaseUrl"], UriKind.Absolute, out var webBaseUri))
    corsOrigins.Add(webBaseUri.GetLeftPart(UriPartial.Authority));

var allowedOrigins = corsOrigins
    .Select(o => o.Trim().TrimEnd('/'))
    .Where(o => o.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SharedClients", policy =>
    {
        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    return true;

                // Chỉ khi phát triển: cho mọi cổng localhost / 127.0.0.1 (Live Server...).
                return isDevelopment
                    && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && (uri.Host == "localhost" || uri.Host == "127.0.0.1");
            });
    });
});

if (!isDevelopment && allowedOrigins.Length == 0)
{
    Console.WriteLine("[CORS] Chưa cấu hình Cors:AllowedOrigins/Web:BaseUrl: trình duyệt sẽ bị chặn, " +
                      "chỉ WinForms/Mobile hoạt động.");
}

// Giới hạn tần suất theo IP cho các API đặt bàn công khai.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút." }, ct);
    };

    static string ClientKey(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Đặt bàn / tra cứu / hủy: 20 lượt mỗi 10 phút.
    options.AddPolicy("web-booking", c => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(c),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));

    // Xem bàn trống: 60 lượt mỗi phút.
    options.AddPolicy("web-read", c => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(c),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

// Bổ sung schema tương thích theo cách idempotent; không xóa dữ liệu hiện có.
using (var scope = app.Services.CreateScope())
{
    var winFormsCompatibility =
        scope.ServiceProvider.GetRequiredService<WinFormsCompatibilitySchemaService>();
    await winFormsCompatibility.EnsureAsync();

    // Đồng bộ bộ thực đơn mới nhất của WinForms vào database dùng chung.
    // Chỉ thêm món/ảnh còn thiếu nên không ghi đè dữ liệu vận hành hiện tại.
    var sharedMenuCatalog =
        scope.ServiceProvider.GetRequiredService<SharedMenuCatalogService>();
    await sharedMenuCatalog.EnsureAsync();

    // Khôi phục bộ ảnh WinForms đi kèm source nếu máy mới chưa có ảnh vật lý.
    var bundledMenuImages =
        scope.ServiceProvider.GetRequiredService<BundledMenuImageService>();
    await bundledMenuImages.EnsureAsync();

    var mobilePermissionSchema =
        scope.ServiceProvider.GetRequiredService<MobilePermissionSchemaService>();
    await mobilePermissionSchema.EnsureAsync();

    var areaAssignmentSchema =
        scope.ServiceProvider.GetRequiredService<AreaAssignmentSchemaService>();
    await areaAssignmentSchema.EnsureAsync();

    var attendanceSettings =
        scope.ServiceProvider.GetRequiredService<AttendanceSettingsService>();
    await attendanceSettings.EnsureAsync();

    var customerAccountSchema =
        scope.ServiceProvider.GetRequiredService<CustomerAccountSchemaService>();
    await customerAccountSchema.EnsureAsync();

    var operationAudit =
        scope.ServiceProvider.GetRequiredService<MobileOperationAuditService>();
    await operationAudit.EnsureAsync();
}

// Bắt mọi exception chưa xử lý: ghi log đầy đủ ra console và trả JSON { message }
// để WinForm/Mobile hiện đúng nguyên nhân thay vì chỉ "Internal Server Error".
// (Khi đưa lên production nên bỏ phần chi tiết lỗi ở message.)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalError");
        logger.LogError(ex, "Unhandled error {Method} {Path}",
            context.Request.Method, context.Request.Path);

        if (context.Response.HasStarted) throw;

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            message = $"Lỗi máy chủ: {ex.GetBaseException().Message}"
        });
    }
});

// Respect HTTPS scheme supplied by Cloudflare/reverse proxies.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

var swaggerEnabled =
    app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ảnh món được tải trực tiếp từ cả WinForms và Mobile, không yêu cầu JWT.
// Nếu database còn trỏ tới ảnh lịch sử chưa có trên máy mới thì trả PNG
// placeholder thay vì để WinForms/Mobile nhận lỗi 404.
app.UseMiddleware<MenuImageFallbackMiddleware>();
app.UseStaticFiles();

app.UseCors("SharedClients");
app.UseRateLimiter();
app.UseAuthentication();

// Token khách hàng web (role KhachHang) chỉ được vào endpoint [CustomerAccess]
// hoặc [AllowAnonymous]; mọi API nhân viên và SignalR hub đều bị chặn 403.
app.UseMiddleware<CustomerAccessMiddleware>();

// Phân công khu vực chỉ để điều phối/nhắc việc; không khóa thao tác ngoài khu.
app.UseMiddleware<AreaAccessMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/order");

app.Run();
