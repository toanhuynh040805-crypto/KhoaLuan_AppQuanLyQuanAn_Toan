using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

/// <summary>
/// Nguồn chuẩn để xác định trạng thái bàn sau các thao tác đóng/hủy đơn,
/// nhận/trả bàn và khi lịch đặt bàn đến gần.
/// </summary>
public sealed class TableStateService
{
    private static readonly TimeSpan UpcomingReservationWindow = TimeSpan.FromHours(3);

    private readonly AppDbContext _db;

    public TableStateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TableStatus> ResolveIdleStatusAsync(
        int tableId,
        DateTime? now = null,
        CancellationToken cancellationToken = default)
    {
        var current = now ?? DateTime.Now;

        var hasActiveOpenOrder = await _db.Orders
            .AsNoTracking()
            .AnyAsync(order =>
                order.Status == OrderStatus.DangMo &&
                (order.TableId == tableId ||
                 order.OrderTables.Any(link => link.TableId == tableId)),
                cancellationToken);

        if (hasActiveOpenOrder)
            return TableStatus.DangPhucVu;

        var hasRelevantReservation = await _db.Reservations
            .AsNoTracking()
            .AnyAsync(reservation =>
                reservation.TableId == tableId &&
                (reservation.Status == ReservationStatus.ChoXacNhan ||
                 reservation.Status == ReservationStatus.DaXacNhan) &&
                reservation.ReservationTime <= current.Add(UpcomingReservationWindow),
                cancellationToken);

        return hasRelevantReservation
            ? TableStatus.DaDat
            : TableStatus.Trong;
    }

    public async Task<Dictionary<int, TableStatus>> ResolveIdleStatusesAsync(
        IEnumerable<int> tableIds,
        DateTime? now = null,
        CancellationToken cancellationToken = default)
    {
        var ids = tableIds.Where(id => id > 0).Distinct().ToArray();
        var result = new Dictionary<int, TableStatus>();

        foreach (var tableId in ids)
        {
            result[tableId] = await ResolveIdleStatusAsync(
                tableId,
                now,
                cancellationToken);
        }

        return result;
    }
}
