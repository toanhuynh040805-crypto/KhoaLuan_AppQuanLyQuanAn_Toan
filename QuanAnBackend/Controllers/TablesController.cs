using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Controllers;

public record TableRequest(string TableName, int Capacity, int AreaId);

public record TableOrderItemDto(
    int Id,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? Note,
    bool IsTakeaway = false
);

public record TableOrderGroupDto(
    int TableId,
    List<TableOrderItemDto> Items
);

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public TablesController(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // GET api/tables?areaId=1 -> vẽ sơ đồ bàn, kèm tên khu vực, lọc theo khu vực (tùy chọn)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? areaId)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    t.TableId,
                    t.TableName,
                    t.Capacity,
                    t.Status,
                    t.AreaId,
                    ISNULL(a.AreaName, N'') AS AreaName,
                    t.TableType,
                    COALESCE(
                        (
                            SELECT SUM(
                                oi.Quantity *
                                COALESCE(
                                    NULLIF(oi.UnitPrice, 0),
                                    mi.Price,
                                    cb.Price,
                                    0
                                )
                            )
                            FROM Orders o
                            INNER JOIN OrderItems oi
                                ON oi.OrderId = o.OrderId
                            LEFT JOIN MenuItems mi
                                ON mi.MenuItemId = oi.MenuItemId
                            LEFT JOIN Combos cb
                                ON cb.ComboId = oi.ComboId
                            WHERE o.TableId = t.TableId
                              AND o.Status = N'DangMo'
                              AND oi.Status <> N'DaHuy'
                        ),
                        0
                    ) AS Amount,
                    (
                        SELECT TOP 1
                            CASE
                                WHEN NULLIF(LTRIM(RTRIM(r.CustomerName)), N'') IS NOT NULL
                                    THEN r.CustomerName
                                ELSE NULL
                            END
                        FROM Reservations r
                        WHERE r.TableId = t.TableId
                          AND r.Status IN (N'DaXacNhan', N'DaNhanBan')
                        ORDER BY r.ReservationTime DESC, r.ReservationId DESC
                    ) AS ReservedCustomerName,
                    (
                        SELECT TOP 1 w.NumberOfGuests
                        FROM WaitingQueue w
                        WHERE w.AssignedTableId = t.TableId
                          AND w.Status = N'DaNhanBan'
                                                ORDER BY COALESCE(w.SeatedTime, w.CheckInTime) DESC, w.QueueId DESC
                    ) AS NumberOfGuests,
                    lk.OrderId AS LinkedOrderId,
                    lk.IsPrimaryTable AS IsLinkPrimary,
                    (
                        SELECT STRING_AGG(t2.TableName, N', ')
                        FROM OrderTables ot2
                        INNER JOIN DiningTables t2 ON t2.TableId = ot2.TableId
                        WHERE ot2.OrderId = lk.OrderId
                          AND ot2.TableId <> t.TableId
                    ) AS LinkedWith
                FROM DiningTables t
                LEFT JOIN Areas a ON a.AreaId = t.AreaId
                OUTER APPLY (
                    SELECT TOP 1 ot.OrderId, ot.IsPrimaryTable
                    FROM OrderTables ot
                    INNER JOIN Orders lo
                        ON lo.OrderId = ot.OrderId
                       AND lo.Status = N'DangMo'
                    WHERE ot.TableId = t.TableId
                      AND t.Status = N'DangPhucVu'
                      AND (
                          SELECT COUNT(*)
                          FROM OrderTables x
                          WHERE x.OrderId = ot.OrderId
                      ) > 1
                    ORDER BY ot.OrderTableId DESC
                ) lk
                WHERE (@AreaId IS NULL OR t.AreaId = @AreaId)
                ORDER BY t.TableId;
                """;

            var areaParameter = command.CreateParameter();
            areaParameter.ParameterName = "@AreaId";
            areaParameter.Value = areaId.HasValue ? areaId.Value : DBNull.Value;
            command.Parameters.Add(areaParameter);

            var result = new List<object>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var status = reader.GetString(3);
                var amount = reader.GetDecimal(7);
                var reservedCustomerName =
                    reader.IsDBNull(8) ? null : reader.GetString(8);
                var numberOfGuests =
                    reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
                                var linkedOrderId =
                    reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);
                var isLinkPrimary =
                    !reader.IsDBNull(11) && reader.GetBoolean(11);
                var linkedWith =
                    reader.IsDBNull(12) ? null : reader.GetString(12);
                string? customerName = reservedCustomerName;

                if (string.IsNullOrWhiteSpace(customerName) &&
                    numberOfGuests.HasValue &&
                    numberOfGuests.Value > 0)
                {
                    customerName = $"{numberOfGuests.Value} khách";
                }

                result.Add(new
                {
                    id = reader.GetInt32(0),
                    tableName = reader.GetString(1),
                    capacity = reader.GetInt32(2),
                    status = StatusToNumber(status),
                    areaId = reader.GetInt32(4),
                    areaName = reader.GetString(5),
                    tableType = reader.GetString(6),
                    amount = status == "DangPhucVu" ? amount : (decimal?)null,
                    customerName,
                    linkedOrderId,
                    isLinkPrimary,
                    linkedWith
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

    // WinForm đọc Status là số (khớp enum TableStatus: 0=Trong, 1=DangPhucVu,
    // 2=DaDat, 3=DangDonDep); Mobile parse được cả số lẫn chuỗi.
    private static int StatusToNumber(string? status) => status switch
    {
        "DangPhucVu" => (int)TableStatus.DangPhucVu,
        "DaDat" => (int)TableStatus.DaDat,
        "DangDonDep" => (int)TableStatus.DangDonDep,
        _ => (int)TableStatus.Trong
    };

    // =========================================================
    // GET api/tables/orders
    // Tai cac mon cua order dang mo de TableScreen co the khoi phuc
    // chi tiet khi reload app.
    // =========================================================
    [HttpGet("orders")]
    public async Task<IActionResult> GetOpenOrders()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    COALESCE(ot.TableId, o.TableId) AS TableId,
                    oi.OrderItemId,
                    COALESCE(mi.ItemName, cb.ComboName, N'Món') AS ItemName,
                    oi.Quantity,
                    COALESCE(
                        NULLIF(oi.UnitPrice, 0),
                        mi.Price,
                        cb.Price,
                        0
                    ) AS EffectiveUnitPrice,
                    oi.Status,
                    oi.Note,
                    oi.IsTakeaway
                FROM Orders o
                INNER JOIN OrderItems oi ON oi.OrderId = o.OrderId
                LEFT JOIN OrderTables ot ON ot.OrderId = o.OrderId
                LEFT JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
                LEFT JOIN Combos cb ON cb.ComboId = oi.ComboId
                WHERE o.Status = N'DangMo'
                  AND oi.Status <> N'DaHuy'
                  AND COALESCE(ot.TableId, o.TableId) IS NOT NULL
                ORDER BY COALESCE(ot.TableId, o.TableId), oi.OrderItemId;
                """;

            var groups = new Dictionary<int, List<TableOrderItemDto>>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var tableId = reader.GetInt32(0);

                if (!groups.TryGetValue(tableId, out var items))
                {
                    items = new List<TableOrderItemDto>();
                    groups[tableId] = items;
                }

                var status = reader.GetString(5) switch
                {
                    "DangCheBien" => "Đang chế biến",
                    "DaCheBien" => "Đã xong",
                    "DaPhucVu" => "Đã phục vụ",
                    _ => "Chờ chế biến"
                };

                items.Add(
                    new TableOrderItemDto(
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetDecimal(4),
                        status,
                        reader.IsDBNull(6) ? null : reader.GetString(6),
                        !reader.IsDBNull(7) && reader.GetBoolean(7)
                    )
                );
            }

            var result = groups
                .OrderBy(x => x.Key)
                .Select(x => new TableOrderGroupDto(x.Key, x.Value))
                .ToList();

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }


    // GET api/tables/available?time=2026-09-28T19:00:00&durationMinutes=120
    // Dung cho man "Dat ban truoc" ben WinForm: TRUOC DAY combo chon ban dung
    // _tables.Where(Status==0) (cache tinh, load 1 lan) nen 1 ban da bi dat cho
    // HOM NAY se bien mat khoi danh sach chon cho CA CAC NGAY SAU, du khong con
    // giao gio thuc te. Endpoint nay tinh lai theo TUNG khung gio duoc yeu cau:
    // - Neu khung gio nam trong nguong "sap toi" (<=3h tu bay gio) VA ban dang
    //   DangPhucVu/DangDonDep (dang co khach that su, khong the biet truoc se
    //   xong luc nao) -> tam thoi khong cho dat.
    // - Ngoai ra chi kiem tra trung voi cac dat ban truoc (Reservations) khac
    //   dang con hieu luc tren cung ban do, theo dung khung [time, time+duration).
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable([FromQuery] DateTime time, [FromQuery] int durationMinutes = Reservation.DefaultDurationMinutes)
    {
        if (durationMinutes < Reservation.MinDurationMinutes || durationMinutes > Reservation.MaxDurationMinutes)
            return BadRequest(new { message = $"Thời lượng giữ bàn phải từ {Reservation.MinDurationMinutes} đến {Reservation.MaxDurationMinutes} phút" });

        var requestedEnd = time.AddMinutes(durationMinutes);
        var isUpcoming = time <= DateTime.Now.AddHours(3);

        var tables = await _db.Tables.Include(t => t.Area).AsNoTracking().ToListAsync();

        // Chi lay dat ban con hieu luc (chua Huy/Khong den) - du xa ngay hom nay
        // bao nhieu cung phai xet, vi khach co the dat truoc hang tuan.
        var activeReservations = await _db.Reservations
            .Where(r => r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan)
            .Select(r => new
            {
                r.TableId,
                r.ReservationTime,
                r.DurationMinutes
            })
            .ToListAsync();

        var result = tables.Select(t =>
        {
            string? reason = null;

            // Luon kiem tra trung voi dat ban truoc khac tren cung ban, bat ke gan
            // hay xa (truoc day 2 dieu kien nay bi viet if/else nen bo sot truong
            // hop "sap toi + trung dat ban khac" - da sua lai thanh kiem tra doc lap).
            var overlapped = activeReservations
                .Where(r => r.TableId == t.TableId)
                .FirstOrDefault(r => time < r.ReservationTime.AddMinutes(r.DurationMinutes) && r.ReservationTime < requestedEnd);
            if (overlapped is not null)
                reason = $"Đã có khách đặt {overlapped.ReservationTime:HH:mm dd/MM} - {overlapped.ReservationTime.AddMinutes(overlapped.DurationMinutes):HH:mm dd/MM}";

            if (reason is null && isUpcoming && (t.Status == TableStatus.DangPhucVu || t.Status == TableStatus.DangDonDep))
            {
                reason = t.Status == TableStatus.DangPhucVu
                    ? "Bàn đang có khách, chưa rõ giờ trống"
                    : "Bàn đang dọn dẹp";
            }

            return new
            {
                Id = t.TableId,
                t.TableName,
                t.Capacity,
                t.AreaId,
                AreaName = t.Area != null ? t.Area.AreaName : "",
                CurrentStatus = t.Status,
                IsAvailable = reason is null,
                Reason = reason
            };
        })
        .OrderBy(t => t.AreaName).ThenBy(t => t.TableName)
        .ToList();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] TableRequest request)
    {
        if (!await _db.Areas.AnyAsync(a => a.AreaId == request.AreaId))
            return BadRequest(new { message = "Khu vực không tồn tại" });

        var table = new DiningTable
        {
            TableName = request.TableName,
            Capacity = request.Capacity,
            AreaId = request.AreaId,
            Status = TableStatus.Trong
        };
        _db.Tables.Add(table);
        await _db.SaveChangesAsync();
        return Ok(new { Id = table.TableId, table.TableName, table.Capacity, table.AreaId, Status = table.Status });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Update(int id, [FromBody] TableRequest request)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();

        table.TableName = request.TableName;
        table.Capacity = request.Capacity;
        table.AreaId = request.AreaId;
        await _db.SaveChangesAsync();
        return Ok(new { Id = table.TableId, table.TableName, table.Capacity, table.AreaId, Status = table.Status });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TableStatus status)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();

        table.Status = status;
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusUpdated", new
        {
            TableId = table.TableId,
            table.TableName,
            Status = table.Status
        });

        return Ok(new { Id = table.TableId, table.TableName, Status = table.Status });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Delete(int id)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();
        if (table.Status != TableStatus.Trong)
            return BadRequest(new { message = "Chỉ có thể xóa bàn đang trống" });

        _db.Tables.Remove(table);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}