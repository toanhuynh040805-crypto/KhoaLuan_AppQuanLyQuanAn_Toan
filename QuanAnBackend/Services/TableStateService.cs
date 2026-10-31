using System.Data;
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
                order.TableId == tableId,
                cancellationToken);

        if (!hasActiveOpenOrder)
        {
            hasActiveOpenOrder = await HasLinkedOpenOrderAsync(
                tableId,
                cancellationToken);
        }

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

    private async Task<bool> HasLinkedOpenOrderAsync(
        int tableId,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID(N'dbo.OrderTables', N'U') IS NULL
                BEGIN
                    SELECT CAST(0 AS bit);
                    RETURN;
                END;

                SELECT CAST(
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM dbo.OrderTables ot
                        INNER JOIN dbo.Orders o
                            ON o.OrderId = ot.OrderId
                        WHERE ot.TableId = @TableId
                          AND o.Status = N'DangMo'
                    )
                    THEN 1 ELSE 0 END
                AS bit);
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@TableId";
            parameter.Value = tableId;
            command.Parameters.Add(parameter);

            var raw = await command.ExecuteScalarAsync(cancellationToken);
            return raw is not null &&
                   raw != DBNull.Value &&
                   Convert.ToBoolean(raw);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
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
