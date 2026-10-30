namespace QuanAnBackend.Models;

public enum ReservationStatus
{
    DaXacNhan = 0,
    DaNhanBan = 1,
    DaHuy = 2,
    KhongDen = 3,
    HoanTat = 4,
    ChoXacNhan = 5
}

// Khớp bảng Reservations trong QuanAnManagementDB_Final.sql.
public class Reservation
{
    public int ReservationId { get; set; }

    public int TableId { get; set; }
    public DiningTable? Table { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime ReservationTime { get; set; }

    // Thời gian dự kiến giữ bàn (phút). Mặc định 30 phút (có thể cấu hình từ 15 đến 45 phút).
    public int DurationMinutes { get; set; } = DefaultDurationMinutes;

    // Giờ dự kiến trả bàn = Giờ đến + thời lượng giữ bàn. Dùng để kiểm tra trùng lịch.
    public DateTime ReservationEndTime => ReservationTime.AddMinutes(DurationMinutes);

    public const int DefaultDurationMinutes = 30;
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 45;

    public int NumberOfGuests { get; set; } = 1;
    public ReservationStatus Status { get; set; } = ReservationStatus.ChoXacNhan;

    // Tiền cọc (chủ yếu dùng cho phòng VIP cần giữ chỗ) - mặc định 0.
    public decimal DepositAmount { get; set; } = 0;
    public bool DepositPaid { get; set; } = false;
    public DateTime? DepositPaidAt { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Truy vết nhân viên đã hủy/đánh dấu không đến.
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Nguồn tạo: "Web" hoặc "NhanVien" (xem ReservationSources).
    public string Source { get; set; } = ReservationSources.NhanVien;

    // Mã đặt bàn dùng chung cho một lượt đặt nhiều bàn = ReservationId của dòng đầu tiên.
    public int? BookingGroupId { get; set; }
}

public static class ReservationSources
{
    public const string Web = "Web";
    public const string NhanVien = "NhanVien";
}
