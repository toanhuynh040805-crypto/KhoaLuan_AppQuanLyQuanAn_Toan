using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Authorization;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;

namespace QuanAnBackend.Controllers;

public record MobileReceiveTableRequest(
    string? Mode = null,
    int? ReservationId = null
);

/// <summary>
/// Endpoint nhận bàn dành cho Mobile trên database dùng chung.
///
/// Sơ đồ bàn Mobile đã đọc DiningTables bằng SQL trực tiếp để tương thích với
/// dữ liệu WinForms/legacy. Luồng nhận bàn cũng phải dùng cùng cách đọc/ghi này;
/// nếu materialize DiningTable qua EF, một giá trị enum cũ trong Status/TableType
/// có thể làm EF ném exception trước khi Mobile kịp mở bàn.
/// </summary>
[ApiController]
[Route("api/mobile/dining/tables")]
public sealed class MobileTableReceiveController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public MobileTableReceiveController(
        AppDbContext db,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPut("{id:int}/receive")]
    [RequireMobilePermission("table.manage")]
    public async Task<IActionResult> Receive(
        int id,
        [FromBody] MobileReceiveTableRequest? request)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        string? tableName = null;
        string? rawStatus = null;
        int? receivedReservationId = null;

        try
        {
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

            var status = NormalizeStatus(rawStatus);

            if (status == "occupied")
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Bàn đang có khách" });
            }

            if (status == "cleaning")
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Bàn đang dọn dẹp" });
            }

            // Một bàn có reservation tương lai vẫn có thể nhận khách thường.
            // Chỉ khi Mobile gửi Mode = "reservation" mới chuyển đúng lượt đặt
            // sang DaNhanBan. Mode = "walkin" giữ nguyên reservation để badge
            // ngày/giờ tiếp tục hiển thị trên sơ đồ bàn.
            var receiveMode = request?.Mode?.Trim().ToLowerInvariant();
            var receiveReservation =
                receiveMode == "reservation" ||
                (string.IsNullOrWhiteSpace(receiveMode) && status == "reserved");

            if (receiveReservation)
            {
                await using var reservation = connection.CreateCommand();
                reservation.Transaction = transaction;
                reservation.CommandText = """
                    DECLARE @SelectedReservationId INT = @RequestedReservationId;

                    IF @SelectedReservationId IS NULL
                    BEGIN
                        SELECT TOP 1 @SelectedReservationId = ReservationId
                        FROM dbo.Reservations
                        WHERE TableId = @TableId
                          AND CONVERT(NVARCHAR(50), Status) IN
                              (
                                  N'DaXacNhan',
                                  N'Đã xác nhận',
                                  N'Da Xac Nhan'
                              )
                          AND DATEADD(
                                MINUTE,
                                ISNULL(DurationMinutes, 30),
                                ReservationTime
                              ) >= GETDATE()
                        ORDER BY ReservationTime, ReservationId;
                    END;

                    IF @SelectedReservationId IS NOT NULL
                    BEGIN
                        UPDATE dbo.Reservations
                        SET Status = N'DaNhanBan'
                        WHERE ReservationId = @SelectedReservationId
                          AND CONVERT(NVARCHAR(50), Status) IN
                              (
                                  N'DaXacNhan',
                                  N'Đã xác nhận',
                                  N'Da Xac Nhan'
                              );

                        IF @@ROWCOUNT = 0
                            SET @SelectedReservationId = NULL;
                    END;

                    SELECT @SelectedReservationId;
                    """;
                AddParameter(reservation, "@TableId", id);
                AddParameter(
                    reservation,
                    "@RequestedReservationId",
                    request?.ReservationId.HasValue == true
                        ? request.ReservationId.Value
                        : DBNull.Value);

                var rawReservationId = await reservation.ExecuteScalarAsync();
                if (rawReservationId is not null &&
                    rawReservationId != DBNull.Value)
                {
                    receivedReservationId = Convert.ToInt32(rawReservationId);
                }

                if (!receivedReservationId.HasValue)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = "Lượt đặt bàn không còn khả dụng để nhận. Hãy tải lại sơ đồ bàn."
                    });
                }
            }

            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE dbo.DiningTables
                    SET Status = N'DangPhucVu'
                    WHERE TableId = @TableId;
                    """;
                AddParameter(update, "@TableId", id);

                var affected = await update.ExecuteNonQueryAsync();
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
                message = "Không thể nhận bàn trên cơ sở dữ liệu dùng chung",
                detail = ex.Message
            });
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        // Status gửi dạng số (1 = DangPhucVu) để WinForm deserialize được;
        // Mobile parse được cả số lẫn chuỗi.
        await _hub.Clients.All.SendAsync("TableStatusUpdated", new
        {
            TableId = id,
            TableName = tableName,
            Status = 1
        });

        if (receivedReservationId.HasValue)
        {
            await _hub.Clients.All.SendAsync(
                "ReservationUpdated",
                new
                {
                    ReservationId = receivedReservationId.Value,
                    Status = "DaNhanBan",
                    Stage = "Received"
                });
        }

        return Ok(new
        {
            id,
            tableName,
            status = "DangPhucVu",
            currentSessionType = receivedReservationId.HasValue
                ? "Reservation"
                : "WalkIn",
            receivedReservationId
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