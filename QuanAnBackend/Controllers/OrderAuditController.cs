using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/order-audit")]
[Authorize(Roles = "QuanLy,Admin")]
public sealed class OrderAuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrderAuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? date,
        [FromQuery] int? tableId,
        [FromQuery] int? paymentRequestedByUserId)
    {
        var day = (date ?? DateTime.Today).Date;
        var nextDay = day.AddDays(1);

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(order => order.Table)!
                .ThenInclude(table => table!.Area)
            .Include(order => order.Employee)!
                .ThenInclude(employee => employee!.User)
            .Include(order => order.PaymentRequestedByUser)
            .Include(order => order.OrderItems)
                .ThenInclude(item => item.MenuItem)
            .Include(order => order.OrderItems)
                .ThenInclude(item => item.CancelledByUser)
            .Include(order => order.Invoices)
                .ThenInclude(invoice => invoice.RequestedByUser)
            .Where(order =>
                (order.OrderTime >= day && order.OrderTime < nextDay) ||
                order.Invoices.Any(invoice =>
                    invoice.InvoiceDate >= day &&
                    invoice.InvoiceDate < nextDay))
            .OrderByDescending(order =>
                order.Invoices
                    .OrderByDescending(invoice => invoice.InvoiceDate)
                    .Select(invoice => (DateTime?)invoice.InvoiceDate)
                    .FirstOrDefault() ?? order.OrderTime)
            .ToListAsync();

        var orderIds = orders.Select(order => order.OrderId).ToArray();
        var linkedTables = await LoadLinkedTablesAsync(orderIds);
        var operations = await LoadTableOperationsAsync(orderIds);

        if (tableId.HasValue && tableId.Value > 0)
        {
            var selectedTableId = tableId.Value;
            orders = orders
                .Where(order =>
                    order.TableId == selectedTableId ||
                    linkedTables.Any(link =>
                        link.OrderId == order.OrderId &&
                        link.TableId == selectedTableId) ||
                    operations.Any(operation =>
                        (operation.SourceOrderId == order.OrderId ||
                         operation.TargetOrderId == order.OrderId) &&
                        (
                            operation.SourceTableId == selectedTableId ||
                            operation.TargetTableId == selectedTableId ||
                            ContainsRelatedTable(
                                operation.RelatedTableIds,
                                selectedTableId)
                        )))
                .ToList();
        }

        if (paymentRequestedByUserId.HasValue &&
            paymentRequestedByUserId.Value > 0)
        {
            var selectedUserId = paymentRequestedByUserId.Value;
            orders = orders
                .Where(order =>
                    order.PaymentRequestedByUserId == selectedUserId ||
                    order.Invoices.Any(invoice =>
                        invoice.RequestedByUserId == selectedUserId))
                .ToList();
        }

        var tableOptions = await _db.Tables
            .AsNoTracking()
            .Include(table => table.Area)
            .Where(table =>
                table.Area == null ||
                table.Area.AreaName != "Mang về")
            .OrderBy(table =>
                table.Area != null ? table.Area.AreaName : "")
            .ThenBy(table => table.TableName)
            .Select(table => new
            {
                id = table.TableId,
                name = table.TableName,
                areaName = table.Area != null
                    ? table.Area.AreaName
                    : ""
            })
            .ToListAsync();

        var requesterOptions = await _db.Orders
            .AsNoTracking()
            .Where(order =>
                order.PaymentRequestedByUserId.HasValue &&
                (
                    (order.OrderTime >= day &&
                     order.OrderTime < nextDay) ||
                    order.Invoices.Any(invoice =>
                        invoice.InvoiceDate >= day &&
                        invoice.InvoiceDate < nextDay)
                ))
            .Select(order => new
            {
                userId = order.PaymentRequestedByUserId!.Value,
                fullName = order.PaymentRequestedByUser != null
                    ? order.PaymentRequestedByUser.FullName
                    : ""
            })
            .Distinct()
            .OrderBy(item => item.fullName)
            .ToListAsync();

        var records = orders.Select(order =>
        {
            var invoice = order.Invoices
                .OrderByDescending(item => item.InvoiceDate)
                .ThenByDescending(item => item.InvoiceId)
                .FirstOrDefault();

            var requesterId =
                order.PaymentRequestedByUserId ??
                invoice?.RequestedByUserId;
            var requesterName =
                order.PaymentRequestedByUser?.FullName ??
                invoice?.RequestedByUser?.FullName;

            var tableNames = linkedTables
                .Where(link => link.OrderId == order.OrderId)
                .Select(link => link.TableName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (!string.IsNullOrWhiteSpace(order.Table?.TableName))
                tableNames.Add(order.Table.TableName);

            foreach (var operation in operations.Where(operation =>
                         operation.SourceOrderId == order.OrderId ||
                         operation.TargetOrderId == order.OrderId))
            {
                if (!string.IsNullOrWhiteSpace(operation.SourceTableName))
                    tableNames.Add(operation.SourceTableName!);
                if (!string.IsNullOrWhiteSpace(operation.TargetTableName))
                    tableNames.Add(operation.TargetTableName!);
            }

            tableNames = tableNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            var batches = order.OrderItems
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.OrderItemId)
                .GroupBy(item => item.CreatedAt)
                .Select((group, index) => new
                {
                    batch = index + 1,
                    calledAt = group.Key,
                    items = group.Select(item => new
                    {
                        orderItemId = item.OrderItemId,
                        name = item.MenuItem?.ItemName ??
                            "(Món đã xóa khỏi thực đơn)",
                        item.Quantity,
                        item.UnitPrice,
                        status = item.Status.ToString(),
                        statusLabel = KitchenStatusLabel(item.Status),
                        item.Note,
                        isDeleted = item.Status == KitchenStatus.DaHuy,
                        item.CancelReason,
                        cancelledBy = item.CancelledByUser?.FullName,
                        item.CreatedAt
                    }).ToList()
                })
                .ToList();

            var tableJourney = operations
                .Where(operation =>
                    operation.SourceOrderId == order.OrderId ||
                    operation.TargetOrderId == order.OrderId)
                .OrderBy(operation => operation.CreatedAt)
                .Select(operation => new
                {
                    operation.OperationLogId,
                    operation.OperationType,
                    operationLabel =
                        TableOperationLabel(operation.OperationType),
                    operation.SourceTableId,
                    operation.SourceTableName,
                    operation.TargetTableId,
                    operation.TargetTableName,
                    operation.ActorName,
                    operation.Details,
                    operation.CreatedAt
                })
                .ToList();

            return new
            {
                orderId = order.OrderId,
                invoiceId = invoice?.InvoiceId,
                orderTime = order.OrderTime,
                invoiceDate = invoice?.InvoiceDate,
                orderStatus = order.Status.ToString(),
                invoiceStatus = invoice?.Status.ToString(),
                tableNames,
                waiterName = order.Employee?.User?.FullName ?? "(Không rõ)",
                paymentRequestedByUserId = requesterId,
                paymentRequestedByName = requesterName,
                paymentRequestedAt = order.PaymentRequestedAt,
                totalAmount = invoice?.TotalAmount ?? order.TotalAmount,
                depositAmount = invoice?.DepositAmount ?? 0m,
                source = order.Source,
                batches,
                tableJourney
            };
        }).ToList();

        return Ok(new
        {
            date = day,
            tableId,
            paymentRequestedByUserId,
            tables = tableOptions,
            paymentRequesters = requesterOptions,
            records
        });
    }

    private async Task<List<LinkedTableRow>> LoadLinkedTablesAsync(
        int[] orderIds)
    {
        if (orderIds.Length == 0)
            return new List<LinkedTableRow>();

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            var parameterNames = AddIntListParameters(
                command,
                "OrderId",
                orderIds);
            command.CommandText = $"""
                IF OBJECT_ID(N'dbo.OrderTables', N'U') IS NOT NULL
                BEGIN
                    SELECT
                        ot.OrderId,
                        ot.TableId,
                        COALESCE(t.TableName, N'Bàn #' +
                            CONVERT(nvarchar(20), ot.TableId)) AS TableName
                    FROM dbo.OrderTables ot
                    LEFT JOIN dbo.DiningTables t
                        ON t.TableId = ot.TableId
                    WHERE ot.OrderId IN ({parameterNames})
                END
                """;

            var result = new List<LinkedTableRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new LinkedTableRow
                {
                    OrderId = reader.GetInt32(0),
                    TableId = reader.GetInt32(1),
                    TableName = reader.GetString(2)
                });
            }

            return result;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<TableOperationRow>> LoadTableOperationsAsync(
        int[] orderIds)
    {
        if (orderIds.Length == 0)
            return new List<TableOperationRow>();

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            var parameterNames = AddIntListParameters(
                command,
                "AuditOrderId",
                orderIds);

            command.CommandText = $"""
                IF OBJECT_ID(N'dbo.TableOperationLogs', N'U') IS NOT NULL
                BEGIN
                    SELECT
                        l.OperationLogId,
                        l.OperationType,
                        l.SourceTableId,
                        sourceTable.TableName,
                        l.TargetTableId,
                        targetTable.TableName,
                        l.RelatedTableIds,
                        l.SourceOrderId,
                        l.TargetOrderId,
                        l.ActorName,
                        l.Details,
                        l.CreatedAt
                    FROM dbo.TableOperationLogs l
                    LEFT JOIN dbo.DiningTables sourceTable
                        ON sourceTable.TableId = l.SourceTableId
                    LEFT JOIN dbo.DiningTables targetTable
                        ON targetTable.TableId = l.TargetTableId
                    WHERE
                        l.SourceOrderId IN ({parameterNames})
                        OR l.TargetOrderId IN ({parameterNames})
                    ORDER BY l.CreatedAt
                END
                """;

            var result = new List<TableOperationRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TableOperationRow
                {
                    OperationLogId = reader.GetInt32(0),
                    OperationType = reader.GetString(1),
                    SourceTableId = reader.IsDBNull(2)
                        ? null
                        : reader.GetInt32(2),
                    SourceTableName = reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),
                    TargetTableId = reader.IsDBNull(4)
                        ? null
                        : reader.GetInt32(4),
                    TargetTableName = reader.IsDBNull(5)
                        ? null
                        : reader.GetString(5),
                    RelatedTableIds = reader.IsDBNull(6)
                        ? null
                        : reader.GetString(6),
                    SourceOrderId = reader.IsDBNull(7)
                        ? null
                        : reader.GetInt32(7),
                    TargetOrderId = reader.IsDBNull(8)
                        ? null
                        : reader.GetInt32(8),
                    ActorName = reader.IsDBNull(9)
                        ? null
                        : reader.GetString(9),
                    Details = reader.IsDBNull(10)
                        ? null
                        : reader.GetString(10),
                    CreatedAt = reader.GetDateTime(11)
                });
            }

            return result;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string AddIntListParameters(
        DbCommand command,
        string prefix,
        IEnumerable<int> values)
    {
        var names = new List<string>();
        var index = 0;

        foreach (var value in values.Distinct())
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@{prefix}{index}";
            parameter.Value = value;
            command.Parameters.Add(parameter);
            names.Add(parameter.ParameterName);
            index++;
        }

        return string.Join(", ", names);
    }

    private static bool ContainsRelatedTable(
        string? relatedTableIds,
        int tableId)
    {
        if (string.IsNullOrWhiteSpace(relatedTableIds))
            return false;

        return relatedTableIds
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Any(value =>
                int.TryParse(value, out var parsed) &&
                parsed == tableId);
    }

    private static string KitchenStatusLabel(KitchenStatus status) =>
        status switch
        {
            KitchenStatus.MoiGoi => "Mới gọi",
            KitchenStatus.DangCheBien => "Đang chế biến",
            KitchenStatus.DaCheBien => "Đã chế biến",
            KitchenStatus.DaPhucVu => "Đã phục vụ",
            KitchenStatus.DaHuy => "Đã xóa / hủy",
            _ => status.ToString()
        };

    private static string TableOperationLabel(string operationType) =>
        operationType switch
        {
            "ChuyenBan" => "Chuyển bàn",
            "GopDon" => "Gộp đơn",
            "GhepBan" => "Ghép bàn",
            "TachBan" => "Tách bàn",
            _ => operationType
        };

    private sealed class LinkedTableRow
    {
        public int OrderId { get; init; }
        public int TableId { get; init; }
        public string TableName { get; init; } = "";
    }

    private sealed class TableOperationRow
    {
        public int OperationLogId { get; init; }
        public string OperationType { get; init; } = "";
        public int? SourceTableId { get; init; }
        public string? SourceTableName { get; init; }
        public int? TargetTableId { get; init; }
        public string? TargetTableName { get; init; }
        public string? RelatedTableIds { get; init; }
        public int? SourceOrderId { get; init; }
        public int? TargetOrderId { get; init; }
        public string? ActorName { get; init; }
        public string? Details { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
