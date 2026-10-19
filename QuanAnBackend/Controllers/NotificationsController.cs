using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    // GET /api/notifications?userId=5&role=PhucVu&status=unread -> danh sach
    // thong bao danh cho user nay: gom ca thong bao RIENG (TargetUserId = userId)
    // lan thong bao CHUNG theo Role (TargetRole = role, TargetUserId = null).
    // Dung ngay sau khi dang nhap va moi lan bam chuong 🔔.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? userId,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? employeeCode)
    {
        // Mobile luôn gọi bằng employeeCode -> trả dạng chuỗi (notificationType = "DatBan"...).
        // WinForm gọi bằng userId + role -> trả dạng số (notificationType = 0, 1...) và đủ
        // các trường NotificationDto đang đọc (Content, TargetRole, TargetUserId...).
        var isMobileClient = !string.IsNullOrWhiteSpace(employeeCode);

        if ((!userId.HasValue || string.IsNullOrWhiteSpace(role)) &&
            !string.IsNullOrWhiteSpace(employeeCode))
        {
            var code = employeeCode.Trim();

            var employee = await _db.Employees
                .AsNoTracking()
                .Include(e => e.User)
                    .ThenInclude(u => u!.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(e => e.EmployeeCode == code);

            if (employee?.User is null)
                return NotFound(new { message = "Không tìm thấy nhân viên" });

            userId = employee.UserId;
            role = employee.User.UserRoles
                .Where(ur => ur.IsActive && ur.Role != null)
                .Select(ur => ur.Role!.RoleName)
                .FirstOrDefault();
        }

        if (!userId.HasValue || string.IsNullOrWhiteSpace(role))
            return BadRequest(new { message = "Thiếu thông tin người dùng" });

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP 50
                    NotificationId,
                    Title,
                    Content,
                    NotificationType,
                    RelatedEntityType,
                    RelatedEntityId,
                    IsRead,
                    CreatedAt,
                    TargetRole,
                    TargetUserId
                FROM Notifications
                WHERE (
                    TargetUserId = @UserId
                    OR (
                        TargetUserId IS NULL
                        AND TargetRole = @Role
                    )
                )
                AND (
                    @UnreadOnly = 0
                    OR IsRead = 0
                )
                ORDER BY CreatedAt DESC, NotificationId DESC;
                """;

            AddParameter(command, "@UserId", userId.Value);
            AddParameter(command, "@Role", role.Trim());
            AddParameter(
                command,
                "@UnreadOnly",
                string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0
            );

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var createdAt = reader.IsDBNull(7)
                    ? (DateTime?)null
                    : reader.GetDateTime(7);

                var content = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var typeText = reader.IsDBNull(3) ? "" : reader.GetString(3);

                // Cột NotificationType lưu chuỗi tên enum; WinForm cần số tương ứng.
                var typeNumber = Enum.TryParse<NotificationType>(typeText, true, out var parsedType)
                    ? (int)parsedType
                    : (int)NotificationType.HeThong;

                result.Add(new
                {
                    id = reader.GetInt32(0),
                    title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    content,
                    message = content,
                    notificationType = isMobileClient ? (object)typeText : typeNumber,
                    targetRole = reader.IsDBNull(8) ? null : reader.GetString(8),
                    targetUserId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                    relatedEntityType = reader.IsDBNull(4) ? null : reader.GetString(4),
                    relatedEntityId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                    isRead = !reader.IsDBNull(6) && reader.GetBoolean(6),
                    createdAt,
                    time = FormatRelativeTime(createdAt)
                });
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Notifications SET IsRead = 1 WHERE NotificationId = {id}"
        );

        if (affected == 0)
            return NotFound();

        return Ok();
    }

    // PUT /api/notifications/read-all?userId=5&role=PhucVu
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead([FromQuery] int userId, [FromQuery] string role)
    {
        var items = await _db.Notifications
            .Where(n => !n.IsRead && (n.TargetUserId == userId || (n.TargetUserId == null && n.TargetRole == role)))
            .ToListAsync();

        foreach (var n in items) n.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new { updated = items.Count });
    }
    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string FormatRelativeTime(DateTime? createdAt)
    {
        if (!createdAt.HasValue)
            return "";

        var value = createdAt.Value;
        var now = DateTime.Now;

        // Neu DB luu UTC, chuyen ve local de tranh hien thoi gian am/sai.
        if (value.Kind == DateTimeKind.Utc)
            value = value.ToLocalTime();

        var diff = now - value;

        if (diff.TotalSeconds < 0)
            diff = TimeSpan.Zero;

        if (diff.TotalMinutes < 1)
            return "Vừa xong";

        if (diff.TotalHours < 1)
            return $"{Math.Max(1, (int)diff.TotalMinutes)} phút trước";

        if (diff.TotalDays < 1)
            return $"{Math.Max(1, (int)diff.TotalHours)} giờ trước";

        if (diff.TotalDays < 2)
            return "Hôm qua";

        if (diff.TotalDays < 7)
            return $"{Math.Max(2, (int)diff.TotalDays)} ngày trước";

        return value.ToString("dd/MM/yyyy HH:mm");
    }

}