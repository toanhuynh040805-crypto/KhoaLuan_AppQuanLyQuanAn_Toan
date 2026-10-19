using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Authorization;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;

namespace QuanAnBackend.Controllers;

/// <summary>
/// Trả bàn dành cho Mobile trên database dùng chung WinForms/Mobile.
/// Dùng SQL trực tiếp giống luồng nhận bàn để không materialize các enum legacy
/// qua EF. Không xóa order/lịch sử; chỉ đóng order rỗng, hoàn tất reservation
/// đã nhận bàn và đưa DiningTables về Trong.
/// </summary>
[ApiController]
[Route("api/mobile/dining/tables")]
public sealed class MobileTableReleaseController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public MobileTableReleaseController(
        AppDbContext db,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPut("{id:int}/release")]
    [RequireMobilePermission("table.manage")]
    public async Task<IActionResult> Release(int id)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        string? tableName = null;

        try
        {
            string rawStatus;

            await using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = """
                    SELECT TOP 1
                        TableName,
                        CONVERT(NVARCHAR(50), Status) AS Status
                    FROM dbo.DiningTables WITH (UPDLOCK, ROWLOCK)
                    WHERE TableId = @TableId;
                    """;
                AddParameter(select, "@TableId", id);

                await using var reader = await select.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { message = "Bàn không tồn tại" });
                }

                tableName = reader.IsDBNull(0) ? $"Bàn #{id}" : reader.GetString(0);
                rawStatus = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }

            if (NormalizeStatus(rawStatus) == "empty")
            {
                await transaction.CommitAsync();
                return Ok(new
                {
                    id,
                    tableName,
                    status = "Trong"
                });
            }

            // Không cho trả bàn nếu còn bất kỳ món chưa hủy nào trong order đang mở.
            // Xét cả Orders.TableId và OrderTables để giữ đúng luồng ghép/gộp bàn.
            var hasActiveItems = false;
            await using (var activeItems = connection.CreateCommand())
            {
                activeItems.Transaction = transaction;
                activeItems.CommandText = """
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM dbo.Orders o
                        INNER JOIN dbo.OrderItems oi ON oi.OrderId = o.OrderId
                        WHERE CONVERT(NVARCHAR(50), o.Status) IN
                              (N'DangMo', N'Đang mở', N'Dang Mo')
                          AND CONVERT(NVARCHAR(50), oi.Status) NOT IN
                              (N'DaHuy', N'Đã hủy', N'Da Huy')
                          AND (
                              o.TableId = @TableId
                              OR EXISTS (
                                  SELECT 1
                                  FROM dbo.OrderTables ot
                                  WHERE ot.OrderId = o.OrderId
                                    AND ot.TableId = @TableId
                              )
                          )
                    ) THEN 1 ELSE 0 END;
                    """;
                AddParameter(activeItems, "@TableId", id);

                var value = await activeItems.ExecuteScalarAsync();
                hasActiveItems = Convert.ToInt32(value ?? 0) == 1;
            }

            if (hasActiveItems)
            {
                await transaction.RollbackAsync();
                return Conflict(new
                {
                    message = "Bàn còn món chưa thanh toán, không thể trả bàn"
                });
            }

            // Đóng các order đang mở nhưng không còn món hoạt động. Không DELETE
            // để hóa đơn/lịch sử thao tác cũ vẫn giữ được liên kết.
            await using (var closeOrders = connection.CreateCommand())
            {
                closeOrders.Transaction = transaction;
                closeOrders.CommandText = """
                    UPDATE o
                    SET Status = N'DaHuy'
                    FROM dbo.Orders o
                    WHERE CONVERT(NVARCHAR(50), o.Status) IN
                          (N'DangMo', N'Đang mở', N'Dang Mo')
                      AND (
                          o.TableId = @TableId
                          OR EXISTS (
                              SELECT 1
                              FROM dbo.OrderTables ot
                              WHERE ot.OrderId = o.OrderId
                                AND ot.TableId = @TableId
                          )
                      );
                    """;
                AddParameter(closeOrders, "@TableId", id);
                await closeOrders.ExecuteNonQueryAsync();
            }

            // Reservation đã nhận bàn được đánh dấu hoàn tất. Giá trị HoanTat đã
            // được backend dùng từ trước; không xóa reservation để giữ lịch sử.
            await using (var finishReservations = connection.CreateCommand())
            {
                finishReservations.Transaction = transaction;
                finishReservations.CommandText = """
                    UPDATE dbo.Reservations
                    SET Status = N'HoanTat'
                    WHERE TableId = @TableId
                      AND CONVERT(NVARCHAR(50), Status) IN
                          (N'DaNhanBan', N'Đã nhận bàn', N'Da Nhan Ban');
                    """;
                AddParameter(finishReservations, "@TableId", id);
                await finishReservations.ExecuteNonQueryAsync();
            }

            // Khách vãng lai đã ngồi bàn vẫn được giữ trong lịch sử hàng chờ,
            // nhưng bỏ liên kết bàn để lần mở bàn sau không lấy nhầm tên/số khách cũ.
            await using (var detachWaiting = connection.CreateCommand())
            {
                detachWaiting.Transaction = transaction;
                detachWaiting.CommandText = """
                    UPDATE dbo.WaitingQueue
                    SET AssignedTableId = NULL
                    WHERE AssignedTableId = @TableId
                      AND CONVERT(NVARCHAR(50), Status) IN
                          (N'DaNhanBan', N'Đã nhận bàn', N'Da Nhan Ban');
                    """;
                AddParameter(detachWaiting, "@TableId", id);
                await detachWaiting.ExecuteNonQueryAsync();
            }

            await using (var updateTable = connection.CreateCommand())
            {
                updateTable.Transaction = transaction;
                updateTable.CommandText = """
                    UPDATE dbo.DiningTables
                    SET Status = N'Trong'
                    WHERE TableId = @TableId;
                    """;
                AddParameter(updateTable, "@TableId", id);

                var affected = await updateTable.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { message = "Bàn không tồn tại" });
                }
            }

            await transaction.CommitAsync();
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new
            {
                message = "Không thể trả bàn trên cơ sở dữ liệu dùng chung",
                detail = ex.Message
            });
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        // Status gửi dạng số (0 = Trong) để WinForm deserialize được;
        // Mobile parse được cả số lẫn chuỗi.
        await _hub.Clients.All.SendAsync("TableStatusUpdated", new
        {
            TableId = id,
            TableName = tableName,
            Status = 0
        });

        return Ok(new
        {
            id,
            tableName,
            status = "Trong"
        });
    }

    private static string NormalizeStatus(string? value)
    {
        var status = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        return status switch
        {
            "1" or "dangphucvu" or "dang phuc vu" or "đang phục vụ" or
            "dang dung" or "đang dùng" or "occupied" => "occupied",

            "2" or "dadat" or "da dat" or "đã đặt" or "reserved" =>
                "reserved",

            "3" or "dangdondep" or "dang don dep" or "đang dọn dẹp" or
            "cleaning" => "cleaning",

            _ => "empty"
        };
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}