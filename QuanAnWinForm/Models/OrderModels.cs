using System.Text.Json.Serialization;

namespace QuanAnWinForm.Models;

// Khớp OrderSources / ReservationSources bên backend
public static class DataSources
{
    public const string Web = "Web";
    public const string NhanVien = "NhanVien";

    public static string Label(string? source) =>
        string.Equals(source, Web, StringComparison.OrdinalIgnoreCase) ? "Web" : "Tại quán";
}
// Các DTO khớp với JSON trả về từ Backend (camelCase mặc định của ASP.NET Core)

// Khớp với enum Models.OrderStatus bên Backend (QuanAnBackend/Models/Order.cs)
public enum OrderStatus
{
    DangMo = 0,
    DaThanhToan = 1,
    DaHuy = 2
}

// Khớp với enum Models.KitchenStatus bên Backend (QuanAnBackend/Models/OrderItem.cs)
public enum KitchenStatus
{
    MoiGoi = 0,
    DangCheBien = 1,
    DaCheBien = 2,
    DaPhucVu = 3,
    DaHuy = 4
}

// Khớp với enum Models.TableStatus bên Backend (QuanAnBackend/Models/DiningTable.cs)
public enum TableStatusEnum
{
    Trong = 0,
    DangPhucVu = 1,
    DaDat = 2,
    DangDonDep = 3
}

public class OrderDto
{
    public int Id { get; set; }
    public int? TableId { get; set; } // null = don mang ve (khong gan ban)
    public TableDto? Table { get; set; }
    public int Status { get; set; } // 0=DangMo, 1=DaThanhToan, 2=DaHuy (khớp enum OrderStatus)
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Source { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItemDto? MenuItem { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public int KitchenStatus { get; set; } // khớp enum KitchenStatus ở trên
    public bool IsTakeaway { get; set; }

    // Bổ sung 2 thuộc tính này để nhận lý do hủy và RowVer từ Backend
    public string? CancelReason { get; set; }
    public byte[]? RowVer { get; set; }
}

public class MenuItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; } // dang tuong doi, vd /images/menu/menu-5-ab12cd34.jpg
    public bool IsAvailable { get; set; } = true;
}

public class TableDto
{
    public int Id { get; set; }
    public string TableName { get; set; } = "";
    public int Capacity { get; set; }
    public int Status { get; set; } // khớp enum TableStatusEnum ở trên
    public int AreaId { get; set; }
    public string AreaName { get; set; } = "";

    // Ghép bàn: null nếu bàn không nằm trong nhóm ghép
    public int? LinkedOrderId { get; set; }
    public bool IsLinkPrimary { get; set; }
    public string? LinkedWith { get; set; }   // vd "T1-A2, T1-A3"

    public bool IsLinked => LinkedOrderId.HasValue && !string.IsNullOrWhiteSpace(LinkedWith);
}

public class OrderItemRequest
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsTakeaway { get; set; } // món khách yêu cầu mang về
}

public class AddItemsResultDto
{
    public int OrderId { get; set; }
}

// Đơn ĐANG MỞ của 1 bàn (GET /api/orders/table/{tableId}) - dùng cho màn Thanh toán
// để hiện lại các món khách đã gọi (vd gọi từ Mobile), tránh giỏ hàng trống.
public class OpenTableOrderDto
{
    public int OrderId { get; set; }
    public int TableId { get; set; }
    public string TableName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public List<OpenTableOrderItemDto> Items { get; set; } = new();
}

public class OpenTableOrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int Status { get; set; } // khớp enum KitchenStatus ở trên
    public string? Note { get; set; }
    public bool IsTakeaway { get; set; }
}

// Đơn mang về đang mở (GET /api/orders/takeaway/open) - dùng cho tab "Mang về"
public class TakeawayOrderSummaryDto
{
    public int Id { get; set; }
    public int? TakeawayNumber { get; set; }
    public string? TakeawayCode { get; set; } // MV-001
    public DateTime OrderTime { get; set; }
    public string EmployeeName { get; set; } = "";
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public bool IsMobileOrder { get; set; }      // đơn do nhân viên Mobile order hộ khách
    public bool PaymentRequested { get; set; }   // nhân viên Mobile đã bấm "Khách gọi thanh toán"
    public string? Source { get; set; }
    public bool IsFromWeb => string.Equals(Source, DataSources.Web, StringComparison.OrdinalIgnoreCase);
    public int ItemCount { get; set; }
    public int PendingItemCount { get; set; } // số món chưa phục vụ
    public decimal TotalAmount { get; set; }
    public List<TakeawayOrderItemDto> Items { get; set; } = new();
}

public class TakeawayOrderItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public int KitchenStatus { get; set; } // 0=Mới gọi, 1=Đang chế biến, 2=Đã chế biến, 3=Đã phục vụ, 4=Đã hủy
}

public class OrderSummaryDto
{
    public int Id { get; set; }
    public int? TableId { get; set; } // null = don mang ve (khong gan ban)
    public string TableName { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public DateTime OrderTime { get; set; }
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
    public int ItemCount { get; set; }
    public string? Source { get; set; }
    public string? OrderType { get; set; }
    public bool IsFromWeb => string.Equals(Source, DataSources.Web, StringComparison.OrdinalIgnoreCase);
}

public class PaymentDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int InvoiceId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int Method { get; set; }
    public string? QRCodeContent { get; set; }
    public bool IsPaid { get; set; }

    // Thông tin khách hàng thân thiết (null/0 nếu là khách vãng lai, không gắn khách)
    public int? CustomerId { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }
    public decimal LoyaltyDiscount { get; set; }
    public int? LoyaltyPointBalance { get; set; }
    public bool EmailSent { get; set; }
}

public class LoginResultDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string RoleName { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

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
    public bool IsTakeaway { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ItemStatusUpdatedPayload
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string TableName { get; set; } = "";
    public string MenuItemName { get; set; } = "";
    public int Status { get; set; }
}
public class OrderItemStatusResultDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int KitchenStatus { get; set; }
    public byte[]? RowVer { get; set; }
}