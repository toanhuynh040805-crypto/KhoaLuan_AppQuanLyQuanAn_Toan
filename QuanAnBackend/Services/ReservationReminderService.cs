using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

public sealed class ReservationReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ILogger<ReservationReminderService> _logger;

    public ReservationReminderService(
        IServiceScopeFactory scopeFactory,
        IHubContext<OrderHub> hub,
        ILogger<ReservationReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Reservation reminder scan failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var audience = scope.ServiceProvider.GetRequiredService<ReservationAudienceService>();
        var tableState = scope.ServiceProvider.GetRequiredService<TableStateService>();

        var now = DateTime.Now;
        var activeRows = await db.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.Table)!
                .ThenInclude(table => table!.Area)
            .Where(reservation =>
                (reservation.Status == ReservationStatus.DaXacNhan ||
                 reservation.Status == ReservationStatus.ChoXacNhan) &&
                reservation.ReservationTime <= now &&
                reservation.ReservationTime >= now.AddDays(-1))
            .OrderBy(reservation => reservation.ReservationTime)
            .ToListAsync(cancellationToken);

        // Synchronize physical table statuses with the same rule used by
        // payment/release flows. A reservation reaching its time must become
        // DaDat on every client unless the table is currently serving an open order.
        var reservationTableIds = activeRows
            .Select(reservation => reservation.TableId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (reservationTableIds.Count > 0)
        {
            var resolvedStatuses = await tableState.ResolveIdleStatusesAsync(
                reservationTableIds,
                now,
                cancellationToken);

            var tables = await db.Tables
                .Where(table => reservationTableIds.Contains(table.TableId))
                .ToListAsync(cancellationToken);

            var changedTables = new List<DiningTable>();
            foreach (var table in tables)
            {
                if (!resolvedStatuses.TryGetValue(table.TableId, out var status) ||
                    table.Status == status)
                {
                    continue;
                }

                // Never downgrade an actively occupied table just because its
                // reservation reminder reached the scheduled time.
                if (table.Status == TableStatus.DangPhucVu &&
                    status != TableStatus.DangPhucVu)
                {
                    continue;
                }

                table.Status = status;
                changedTables.Add(table);
            }

            if (changedTables.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);

                foreach (var table in changedTables)
                {
                    await _hub.Clients.All.SendAsync(
                        "TableStatusUpdated",
                        new
                        {
                            TableId = table.TableId,
                            table.TableName,
                            Status = table.Status
                        },
                        cancellationToken);
                }
            }
        }

        var groups = activeRows.GroupBy(reservation =>
            reservation.BookingGroupId ?? reservation.ReservationId);

        foreach (var group in groups)
        {
            var rows = group.ToList();
            if (rows.Count == 0)
                continue;

            var first = rows[0];
            var groupId = group.Key;
            var reservationTime = rows.Min(row => row.ReservationTime);
            var holdUntil = rows.Max(row =>
                row.ReservationTime.AddMinutes(row.DurationMinutes));
            var tableNames = string.Join(", ", rows
                .Select(row => row.Table?.TableName ?? $"Bàn {row.TableId}")
                .Distinct());
            var areaIds = rows
                .Select(row => row.Table?.AreaId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (now < holdUntil)
            {
                var stageAlreadyBroadcast = await db.Notifications.AnyAsync(
                    notification =>
                        notification.EventKey != null &&
                        notification.EventKey.StartsWith($"reservation-due:{groupId}:"),
                    cancellationToken);

                await SendDueAsync(
                    notifications,
                    audience,
                    groupId,
                    first.CustomerId,
                    areaIds,
                    tableNames,
                    reservationTime,
                    holdUntil,
                    now,
                    !stageAlreadyBroadcast,
                    cancellationToken);
            }
            else
            {
                var stageAlreadyBroadcast = await db.Notifications.AnyAsync(
                    notification =>
                        notification.EventKey != null &&
                        notification.EventKey.StartsWith($"reservation-overdue:{groupId}:"),
                    cancellationToken);

                await SendOverdueAsync(
                    notifications,
                    audience,
                    groupId,
                    first.CustomerId,
                    areaIds,
                    tableNames,
                    reservationTime,
                    holdUntil,
                    now,
                    !stageAlreadyBroadcast,
                    cancellationToken);
            }
        }
    }

    private async Task SendDueAsync(
        NotificationService notifications,
        ReservationAudienceService audience,
        int groupId,
        int? customerId,
        int[] areaIds,
        string tableNames,
        DateTime reservationTime,
        DateTime holdUntil,
        DateTime now,
        bool broadcastStage,
        CancellationToken cancellationToken)
    {
        var userIds = await audience.GetRecipientUserIdsAsync(
            areaIds,
            reservationTime,
            cancellationToken);

        foreach (var userId in userIds)
        {
            await notifications.CreateAsync(
                "Đã đến giờ khách đặt bàn",
                $"{tableNames} có lịch đặt lúc {reservationTime:HH:mm}. " +
                $"Bàn được giữ đến {holdUntil:HH:mm}. Vui lòng kiểm tra khách đã đến nhận bàn chưa.",
                NotificationType.DatBan,
                targetUserId: userId,
                relatedEntityType: "Reservation",
                relatedEntityId: groupId,
                eventKey: $"reservation-due:{groupId}:user:{userId}");
        }

        if (customerId.HasValue)
        {
            await notifications.CreateForCustomerAsync(
                customerId.Value,
                "Đã đến giờ đặt bàn của bạn",
                $"Lịch đặt {tableNames} là {reservationTime:HH:mm}. " +
                $"Nhà hàng giữ bàn cho bạn đến {holdUntil:HH:mm}.",
                NotificationType.DatBan,
                $"reservation-due:{groupId}:customer:{customerId.Value}",
                "Reservation",
                groupId);
        }

        if (broadcastStage)
        {
            await _hub.Clients.All.SendAsync(
                "ReservationUpdated",
                new
                {
                    reservationId = groupId,
                    stage = "Due",
                    reservationTime,
                    holdUntil
                },
                cancellationToken);
        }
    }

    private async Task SendOverdueAsync(
        NotificationService notifications,
        ReservationAudienceService audience,
        int groupId,
        int? customerId,
        int[] areaIds,
        string tableNames,
        DateTime reservationTime,
        DateTime holdUntil,
        DateTime now,
        bool broadcastStage,
        CancellationToken cancellationToken)
    {
        var userIds = await audience.GetRecipientUserIdsAsync(
            areaIds,
            now,
            cancellationToken);

        foreach (var userId in userIds)
        {
            await notifications.CreateAsync(
                "Đặt bàn quá giờ giữ chỗ",
                $"{tableNames} đặt lúc {reservationTime:HH:mm} đã quá thời gian giữ đến {holdUntil:HH:mm}. " +
                "Nếu khách chưa đến, bạn có thể đánh dấu Không đến để nhả bàn.",
                NotificationType.DatBan,
                targetUserId: userId,
                relatedEntityType: "Reservation",
                relatedEntityId: groupId,
                eventKey: $"reservation-overdue:{groupId}:user:{userId}");
        }

        if (customerId.HasValue)
        {
            await notifications.CreateForCustomerAsync(
                customerId.Value,
                "Đặt bàn đã quá thời gian giữ chỗ",
                $"Lịch đặt {tableNames} lúc {reservationTime:HH:mm} được giữ đến {holdUntil:HH:mm}. " +
                "Thời gian giữ chỗ đã hết; nhà hàng có thể hủy lượt đặt nếu bạn chưa nhận bàn.",
                NotificationType.DatBan,
                $"reservation-overdue:{groupId}:customer:{customerId.Value}",
                "Reservation",
                groupId);
        }

        if (broadcastStage)
        {
            await _hub.Clients.All.SendAsync(
                "ReservationUpdated",
                new
                {
                    reservationId = groupId,
                    stage = "Overdue",
                    reservationTime,
                    holdUntil
                },
                cancellationToken);
        }
    }
}
