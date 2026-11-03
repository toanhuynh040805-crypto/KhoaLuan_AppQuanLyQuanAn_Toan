using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
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
    private readonly ILogger<NotificationService> _logger;
    private readonly ReservationAudienceService _audience;

    public NotificationService(
        AppDbContext db,
        IHubContext<OrderHub> hub,
        ILogger<NotificationService> logger,
        ReservationAudienceService audience)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
        _audience = audience;
    }

    public async Task CreateAsync(
        string title,
        string content,
        NotificationType type,
        string? targetRole = null,
        int? targetUserId = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? eventKey = null)
    {
        if (!string.IsNullOrWhiteSpace(eventKey))
        {
            if (eventKey.Length > 200)
                throw new ArgumentException("Event key must contain at most 200 characters.", nameof(eventKey));

            if (await _db.Notifications.AnyAsync(n => n.EventKey == eventKey))
                return;
        }

        var noti = new Notification
        {
            Title = title,
            Content = content,
            NotificationType = type,
            TargetRole = targetUserId is null ? targetRole : null,
            TargetUserId = targetUserId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            EventKey = string.IsNullOrWhiteSpace(eventKey) ? null : eventKey,
            IsRead = false,
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(noti);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            !string.IsNullOrWhiteSpace(eventKey) &&
            exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            _db.Entry(noti).State = EntityState.Detached;
            return;
        }

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
            noti.EventKey,
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
            relatedEntityId = noti.RelatedEntityId,
            eventKey = noti.EventKey
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
                .Group($"role-{targetRole}")
                .SendAsync("NotificationReceived", mobilePayload);

            // Giữ tương thích client cũ từng join role dưới dạng dept-*.
            await _hub.Clients
                .Group($"dept-{targetRole}")
                .SendAsync("NotificationReceived", mobilePayload);
        }
    }

    public async Task CreateForCustomerAsync(
        int customerId,
        string title,
        string content,
        NotificationType type,
        string eventKey,
        string? relatedEntityType = null,
        int? relatedEntityId = null)
    {
        if (customerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(customerId));
        if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 200)
            throw new ArgumentException("Event key must contain 1 to 200 characters.", nameof(eventKey));

        var exists = await _db.Notifications.AnyAsync(n => n.EventKey == eventKey);
        if (exists) return;

        var notification = new Notification
        {
            CustomerId = customerId,
            Title = title,
            Content = content,
            NotificationType = type,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            EventKey = eventKey,
            IsRead = false,
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notification);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            _db.Entry(notification).State = EntityState.Detached;
            _logger.LogInformation(
                "Skipped duplicate customer notification for event {EventKey}.",
                eventKey);
            return;
        }

        try
        {
            await _hub.Clients
                .Group($"customer-{customerId}")
                .SendAsync("NewCustomerNotification", new
                {
                    id = notification.NotificationId,
                    title = notification.Title,
                    message = notification.Content,
                    notificationType = notification.NotificationType.ToString(),
                    relatedEntityType = notification.RelatedEntityType,
                    relatedEntityId = notification.RelatedEntityId,
                    isRead = notification.IsRead,
                    createdAt = notification.CreatedAt
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Customer notification {NotificationId} was saved, but realtime delivery failed for customer {CustomerId}.",
                notification.NotificationId,
                customerId);
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
    /// Thông báo gắn với một Order chỉ gửi cho đúng nhân viên đang sở hữu Order.
    /// Nếu Order chưa có EmployeeId/UserId thì fallback về Phục vụ đang được
    /// phân đúng ca + đúng khu vực của bàn.
    /// </summary>
    public async Task CreateForOrderResponsibleAsync(
        int orderId,
        string title,
        string content,
        NotificationType type,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? eventKeyPrefix = null)
    {
        var orderInfo = await _db.Orders
            .AsNoTracking()
            .Where(order => order.OrderId == orderId)
            .Select(order => new
            {
                UserId = order.Employee != null
                    ? (int?)order.Employee.UserId
                    : null,
                AreaId = order.Table != null
                    ? (int?)order.Table.AreaId
                    : null
            })
            .FirstOrDefaultAsync();

        if (orderInfo is null)
            return;

        var recipients = new HashSet<int>();

        if (orderInfo.UserId.HasValue && orderInfo.UserId.Value > 0)
        {
            var active = await _db.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == orderInfo.UserId.Value &&
                    user.IsActive);

            if (active)
                recipients.Add(orderInfo.UserId.Value);
        }

        if (recipients.Count == 0 &&
            orderInfo.AreaId.HasValue &&
            orderInfo.AreaId.Value > 0)
        {
            recipients = await _audience.GetOperationalRecipientUserIdsAsync(
                new[] { orderInfo.AreaId.Value },
                DateTime.Now);
        }

        foreach (var userId in recipients)
        {
            await CreateAsync(
                title,
                content,
                type,
                targetUserId: userId,
                relatedEntityType: relatedEntityType,
                relatedEntityId: relatedEntityId,
                eventKey: string.IsNullOrWhiteSpace(eventKeyPrefix)
                    ? null
                    : $"{eventKeyPrefix}:user:{userId}");
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
