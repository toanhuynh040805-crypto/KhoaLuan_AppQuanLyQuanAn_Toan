namespace QuanAnWinForm.Models;

// Cac DTO khop voi JSON tra ve tu Backend (camelCase mac dinh cua ASP.NET Core)

public class OrderDto
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public TableDto? Table { get; set; }
    public int Status { get; set; } // 0=DangPhucVu,1=ChoThanhToan,2=HoanTat,3=DaHuy
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItemDto? MenuItem { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public int KitchenStatus { get; set; }
}

public class MenuItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DepartmentId { get; set; }
    // Con hang / Het hang. Neu Backend chua tra ve field nay trong JSON,
    // gia tri se mac dinh la true (PropertyNameCaseInsensitive khong bao gio bao loi thieu field).
    public bool IsAvailable { get; set; } = true;
}

public class TableDto
{
    public int Id { get; set; }
    public string TableName { get; set; } = "";
    public int Capacity { get; set; }
    public int Status { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public int Method { get; set; } // 0=TienMat, 1=VietQR
    public string? QRCodeContent { get; set; }
    public bool IsPaid { get; set; }
}

public class LoginResultDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string RoleName { get; set; } = "";
}
// Payload nhan tu SignalR khi co phieu moi gui toi bo phan (khop voi payload
// duoc gui trong OrderRoutingService.RouteNewOrderItemsAsync ben Backend)
public class NewTicketPayload
{
    public int OrderId { get; set; }
    public string TableName { get; set; } = "";
    public string Department { get; set; } = "";
    public List<TicketItem> Items { get; set; } = new();
}

public class TicketItem
{
    public int Id { get; set; }
    public string MenuItemName { get; set; } = "";
    public int Quantity { get; set; }
    public string? Note { get; set; }
}
