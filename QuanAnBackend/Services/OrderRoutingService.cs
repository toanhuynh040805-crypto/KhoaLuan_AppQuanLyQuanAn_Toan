using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

public class OrderRoutingService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public OrderRoutingService(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task RouteNewOrderItemsAsync(int orderId, List<int> newOrderItemIds)
    {
        var items = await _db.OrderItems
            .Include(oi => oi.MenuItem)!
                .ThenInclude(mi => mi!.Station)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Table)
            .Where(oi => newOrderItemIds.Contains(oi.OrderItemId))
            .ToListAsync();

        var groupedByStation = items
            .Where(oi => oi.MenuItem is not null)
            .GroupBy(oi => oi.MenuItem!.StationId);

        foreach (var group in groupedByStation)
        {
            if (group.Key is null) continue;

            var station = await _db.Stations.FindAsync(group.Key.Value);
            if (station is null) continue;

            var ticketPayload = group.Select(oi => new
            {
                Id = oi.OrderItemId,
                MenuItemName = oi.MenuItem!.ItemName,
                oi.Quantity,
                oi.Note,
                // Món mang về: cờ của món hoặc cả đơn là đơn mang về.
                IsTakeaway = oi.IsTakeaway || (oi.Order != null && oi.Order.OrderType == OrderTypes.MangVe),
                oi.CreatedAt
            }).ToList();

            foreach (var oi in group)
            {
                _db.KitchenTickets.Add(new KitchenTicket
                {
                    OrderItemId = oi.OrderItemId,
                    StationId = station.StationId,
                    SentTime = DateTime.Now,
                    Status = KitchenTicketStatus.ChoXuLy
                });
            }

            await _db.SaveChangesAsync();

            var firstOrder = items.First().Order;
            var orderLabel = firstOrder?.Table?.TableName
                ?? (firstOrder?.TakeawayCode is not null
                    ? $"Mang về {firstOrder.TakeawayCode}"
                    : "N/A");

            await _hub.Clients.Group($"dept-{station.StationName}")
                .SendAsync("NewTicket", new
                {
                    OrderId = orderId,
                    TableName = orderLabel,
                    Department = station.StationName,
                    Items = ticketPayload
                });
        }
    }
}