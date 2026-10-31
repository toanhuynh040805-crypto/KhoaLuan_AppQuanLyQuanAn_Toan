using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Middleware;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/customers/me/notifications")]
[Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
[CustomerAccess]
public sealed class CustomerNotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerNotificationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);

        var total = await _db.Notifications
            .CountAsync(notification => notification.CustomerId == customerId, cancellationToken);
        var unreadCount = await _db.Notifications
            .CountAsync(notification => notification.CustomerId == customerId && !notification.IsRead, cancellationToken);

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(notification => notification.CustomerId == customerId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.NotificationId)
            .Skip(skip)
            .Take(take)
            .Select(notification => new
            {
                id = notification.NotificationId,
                title = notification.Title,
                message = notification.Content,
                notificationType = notification.NotificationType.ToString(),
                relatedEntityType = notification.RelatedEntityType,
                relatedEntityId = notification.RelatedEntityId,
                isRead = notification.IsRead,
                createdAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total,
            unreadCount,
            skip,
            take,
            hasMore = skip + notifications.Count < total,
            items = notifications
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var count = await _db.Notifications.CountAsync(
            notification => notification.CustomerId == customerId && !notification.IsRead);
        return Ok(new { count });
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var updated = await _db.Notifications
            .Where(notification =>
                notification.NotificationId == id &&
                notification.CustomerId == customerId &&
                !notification.IsRead)
            .ExecuteUpdateAsync(updates => updates.SetProperty(notification => notification.IsRead, true));

        if (updated == 0)
        {
            var belongsToCustomer = await _db.Notifications.AnyAsync(
                notification =>
                    notification.NotificationId == id &&
                    notification.CustomerId == customerId);
            if (!belongsToCustomer) return NotFound();
        }

        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var updated = await _db.Notifications
            .Where(notification => notification.CustomerId == customerId && !notification.IsRead)
            .ExecuteUpdateAsync(updates => updates.SetProperty(notification => notification.IsRead, true));

        return Ok(new { updated });
    }

    private bool TryGetCustomerId(out int customerId)
    {
        var resolved = CustomerTokens.GetCustomerId(User);
        customerId = resolved ?? 0;
        return customerId > 0;
    }
}
