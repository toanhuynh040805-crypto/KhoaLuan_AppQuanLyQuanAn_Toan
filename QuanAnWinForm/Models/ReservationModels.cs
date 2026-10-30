namespace QuanAnWinForm.Models;

// Khop enum Models.ReservationStatus ben Backend (QuanAnBackend/Models/Reservation.cs)
public enum ReservationStatusEnum
{
    DaXacNhan = 0,  // vua dat, cho den gio
    DaNhanBan = 1,  // khach da den, da chuyen thanh Order that su
    DaHuy = 2,      // Thu ngan/Le tan chu dong huy
    KhongDen = 3,   // qua gio hen kha lau ma khach khong den (no-show)
    HoanTat = 4,    // BO SUNG: khach da an xong & thanh toan (hoac Order bi huy) -> Backend tu chuyen sang trang thai nay
    ChoXacNhan = 5  // Khach dat qua Web cho nha hang xac nhan
}

public class ReservationDto
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public string TableName { get; set; } = "";
    public string AreaName { get; set; } = "";
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime ReservationTime { get; set; }

    // BO SUNG: truoc day thieu hoan toan thoi luong giu ban -> khong biet ban
    // duoc dat toi may gio, khong the chan trung lich. Gia tri fallback nay chi
    // dung khi Backend cu chua tra ve field nay; nen giu khop voi
    // Reservation.DefaultDurationMinutes ben Backend (sua ca 2 neu doi).
    public int DurationMinutes { get; set; } = 30;
    public DateTime ReservationEndTime { get; set; }

    public int NumberOfGuests { get; set; }
    public int Status { get; set; } // khop ReservationStatusEnum o tren
    public decimal DepositAmount { get; set; }
    public bool DepositPaid { get; set; }
    public string? Note { get; set; }

    // BO SUNG: xem lai "ai da huy dat ban nay" cho man Lich su dat ban.
    // Chi co gia tri khi Status = DaHuy/KhongDen.
    public string? CancelledByUserName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Source { get; set; }
    public int? CustomerId { get; set; }
    public bool IsFromWeb => string.Equals(Source, DataSources.Web, StringComparison.OrdinalIgnoreCase);
}

// Ket qua GET /api/tables/available?time=..&durationMinutes=.. - danh sach TAT
// CA ban kem co the dat duoc hay khong TAI DUNG khung gio duoc chon, thay vi
// dua vao TableDto.Status (tinh, khong biet ngay/gio) nhu truoc day.
public class TableAvailabilityDto
{
    public int Id { get; set; }
    public string TableName { get; set; } = "";
    public int Capacity { get; set; }
    public int AreaId { get; set; }
    public string AreaName { get; set; } = "";
    public int CurrentStatus { get; set; }
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
}

// Kết quả GET /api/reservations/last-by-phone?phone=.. - gợi ý thông tin khách cũ
// khi nhân viên nhập SĐT lúc đặt bàn.
public class ReservationHintDto
{
    public bool Found { get; set; }
    public bool IsMember { get; set; }
    public int? CustomerId { get; set; }
    public int? MemberLevel { get; set; }   // khớp MemberLevelEnum
    public int? LoyaltyPoint { get; set; }
    public string? CustomerName { get; set; }
    public int ReservationCount { get; set; }
    public string? LastTableName { get; set; }
    public DateTime? LastReservationTime { get; set; }
    public int? LastNumberOfGuests { get; set; }
    public string? LastNote { get; set; }
}