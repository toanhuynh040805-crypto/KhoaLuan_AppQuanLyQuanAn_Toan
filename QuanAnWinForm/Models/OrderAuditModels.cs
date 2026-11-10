namespace QuanAnWinForm.Models;

public sealed class OrderAuditResponseDto
{
    public DateTime Date { get; set; }
    public int? TableId { get; set; }
    public int? PaymentRequestedByUserId { get; set; }
    public List<OrderAuditTableOptionDto> Tables { get; set; } = new();
    public List<OrderAuditRequesterOptionDto> PaymentRequesters { get; set; } = new();
    public List<OrderAuditRecordDto> Records { get; set; } = new();
}

public sealed class OrderAuditTableOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AreaName { get; set; } = "";

    public override string ToString() =>
        string.IsNullOrWhiteSpace(AreaName)
            ? Name
            : $"{Name} · {AreaName}";
}

public sealed class OrderAuditRequesterOptionDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";

    public override string ToString() => FullName;
}

public sealed class OrderAuditRecordDto
{
    public int OrderId { get; set; }
    public int? InvoiceId { get; set; }
    public DateTime OrderTime { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string OrderStatus { get; set; } = "";
    public string? InvoiceStatus { get; set; }
    public List<string> TableNames { get; set; } = new();
    public string WaiterName { get; set; } = "";
    public int? PaymentRequestedByUserId { get; set; }
    public string? PaymentRequestedByName { get; set; }
    public DateTime? PaymentRequestedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string Source { get; set; } = "";
    public List<OrderAuditBatchDto> Batches { get; set; } = new();
    public List<OrderAuditJourneyDto> TableJourney { get; set; } = new();
}

public sealed class OrderAuditBatchDto
{
    public int Batch { get; set; }
    public DateTime CalledAt { get; set; }
    public List<OrderAuditItemDto> Items { get; set; } = new();
}

public sealed class OrderAuditItemDto
{
    public int OrderItemId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string? Note { get; set; }
    public bool IsDeleted { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OrderAuditJourneyDto
{
    public int OperationLogId { get; set; }
    public string OperationType { get; set; } = "";
    public string OperationLabel { get; set; } = "";
    public int? SourceTableId { get; set; }
    public string? SourceTableName { get; set; }
    public int? TargetTableId { get; set; }
    public string? TargetTableName { get; set; }
    public string? ActorName { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
