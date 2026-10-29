using System.ComponentModel.DataAnnotations.Schema;

namespace QuanAnBackend.Models;

public enum OrderStatus
{
    DangMo,
    DaThanhToan,
    DaHuy
}

public static class OrderTypes
{
    public const string TaiBan = "TaiBan";
    public const string MangVe = "MangVe";
}

public class Order
{
    public int OrderId { get; set; }

    // Null chỉ với đơn mang về thật của WinForms mới. Mobile hiện tại vẫn có thể
    // tiếp tục dùng bàn/khu "Mang về" như trước, nên hai luồng cùng tồn tại.
    public int? TableId { get; set; }
    public DiningTable? Table { get; set; }

    public string OrderType { get; set; } = OrderTypes.TaiBan;
    public int? TakeawayNumber { get; set; }

    [NotMapped]
    public bool IsTakeaway => OrderType == OrderTypes.MangVe;

    [NotMapped]
    public string? TakeawayCode => TakeawayNumber.HasValue ? $"MV-{TakeawayNumber.Value:000}" : null;

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime OrderTime { get; set; } = DateTime.Now;
    public OrderStatus Status { get; set; } = OrderStatus.DangMo;

    // Audit thanh toán: tài khoản thực sự bấm "Gọi thanh toán".
    public int? PaymentRequestedByUserId { get; set; }
    public User? PaymentRequestedByUser { get; set; }
    public DateTime? PaymentRequestedAt { get; set; }

    public string? Note { get; set; }
    // Khóa chống trùng khi web gửi lại cùng một yêu cầu (idempotent). Null với đơn của nhân viên.
    public string? ClientRequestId { get; set; }

    // Nguồn tạo đơn: "Web" hoặc "NhanVien" (xem OrderSources).
    public string Source { get; set; } = OrderSources.NhanVien;
    public decimal TotalAmount { get; set; } = 0;

    public byte[]? RowVer { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
public static class OrderSources
{
    public const string Web = "Web";
    public const string NhanVien = "NhanVien";

    // EmployeeCode của nhân viên hệ thống dùng làm người tạo cho đơn từ web.
    public const string WebEmployeeCode = "WEB";
}