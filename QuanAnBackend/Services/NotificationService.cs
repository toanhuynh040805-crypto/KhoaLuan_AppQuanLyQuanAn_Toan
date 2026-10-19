using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

// Nơi trung tâm tạo + phát thông báo nội bộ.
// Các luồng mới nên target theo permission khi người nhận là một capability
// nghiệp vụ, thay vì gán cứng tên Role.
public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task CreateAsync(
        string title,
        string content,
        NotificationType type,
        string? targetRole = null,
        int? targetUserId = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null)
    {
        var noti = new Notification
        {
            Title = title,
            Content = content,
            NotificationType = type,
            TargetRole = targetUserId is null ? targetRole : null,
            TargetUserId = targetUserId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(noti);
        await _db.SaveChangesAsync();

        var payload = new
        {
            Id = noti.NotificationId,
            noti.Title,
            noti.Content,
            NotificationType = (int)noti.NotificationType,
            noti.TargetRole,
            noti.TargetUserId,
            noti.RelatedEntityType,
            noti.RelatedEntityId,
            noti.IsRead,
            noti.CreatedAt
        };

        var mobilePayload = new
        {
            id = noti.NotificationId,
            title = noti.Title,
            message = noti.Content,
            notificationType = noti.NotificationType.ToString(),
            isRead = noti.IsRead,
            time = "Vừa xong",
            relatedEntityType = noti.RelatedEntityType,
            relatedEntityId = noti.RelatedEntityId
        };

        if (targetUserId.HasValue)
        {
            await _hub.Clients
                .Group($"user-{targetUserId.Value}")
                .SendAsync("NewNotification", payload);

            var employeeCode = await _db.Employees
                .Where(e => e.UserId == targetUserId.Value)
                .Select(e => e.EmployeeCode)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                await _hub.Clients
                    .Group($"employee-{employeeCode}")
                    .SendAsync("NotificationReceived", mobilePayload);
            }
        }
        else if (!string.IsNullOrWhiteSpace(targetRole))
        {
            // Giữ API role-target cũ cho các module legacy chưa refactor.
            await _hub.Clients
                .Group($"role-{targetRole}")
                .SendAsync("NewNotification", payload);

            await _hub.Clients
                .Group($"dept-{targetRole}")
                .SendAsync("NotificationReceived", mobilePayload);
        }
    }

    public async Task CreateForRolesAsync(
        string title,
        string content,
        NotificationType type,
        IEnumerable<string> targetRoles,
        string? relatedEntityType = null,
        int? relatedEntityId = null)
    {
        foreach (var role in targetRoles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await CreateAsync(
                title,
                content,
                type,
                targetRole: role,
                relatedEntityType: relatedEntityType,
                relatedEntityId: relatedEntityId
            );
        }
    }

    /// <summary>
    /// Gửi cho mọi User đang active có capability tương ứng thông qua bất kỳ
    /// Role active nào. Việc thêm Role mới hoặc đổi ma trận quyền không cần sửa code.
    /// </summary>
    public async Task CreateForPermissionAsync(
        string title,
        string content,
        NotificationType type,
        string permissionKey,
        string? relatedEntityType = null,
        int? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return;

        var userIds = await _db.Database.SqlQuery<int>($"""
            SELECT DISTINCT ur.UserId AS Value
            FROM dbo.UserRoles ur
            INNER JOIN dbo.Users u
                ON u.UserId = ur.UserId
               AND u.IsActive = 1
            INNER JOIN dbo.RoleMobilePermissions rp
                ON rp.RoleId = ur.RoleId
               AND rp.IsAllowed = 1
            INNER JOIN dbo.MobilePermissions p
                ON p.PermissionKey = rp.PermissionKey
               AND p.IsActive = 1
            WHERE ur.IsActive = 1
              AND rp.PermissionKey = {permissionKey.Trim()};
            """).ToListAsync();

        foreach (var userId in userIds.Distinct())
        {
            await CreateAsync(
                title,
                content,
                type,
                targetUserId: userId,
                relatedEntityType: relatedEntityType,
                relatedEntityId: relatedEntityId
            );
        }
    }
}
