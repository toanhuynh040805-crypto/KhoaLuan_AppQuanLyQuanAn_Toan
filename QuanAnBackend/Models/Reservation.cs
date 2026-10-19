namespace QuanAnBackend.Models;

public enum ReservationStatus
{
    DaXacNhan,  // vua dat, cho den gio
    DaNhanBan,  // khach da den, da chuyen thanh Order that su (xem OrdersController.CheckIn)
    DaHuy,      // Thu ngan/Le tan chu dong huy
    KhongDen,   // qua gio hen kha lau ma khach khong den (no-show)

    // BO SUNG: khach da an xong VA da thanh toan (hoac Order bi huy giua chung) ->
    // luot dat ban nay xem nhu KET THUC, KHONG con giu cho/chan trung lich nua.
    // Truoc day thieu trang thai nay nen 1 luot DaNhanBan cu (dan du ban da tra
    // tu lau) van bi tinh la "con hieu luc" va chan khach moi dat CHINH khung gio
    // do -> xem PaymentsController.Create va OrdersController.Cancel, noi Status
    // duoc chuyen sang HoanTat ngay khi ban tra ve Trong.
    HoanTat
}

// Khớp bảng Reservations trong QuanAnManagementDB_Final.sql.
// Không map CustomerId (chưa có module khách hàng thành viên) -> luôn NULL,
// dùng CustomerName/CustomerPhone cho khách đặt bàn qua điện thoại/vãng lai.
public class Reservation
{
    public int ReservationId { get; set; }

    public int TableId { get; set; }
    public DiningTable? Table { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime ReservationTime { get; set; }

    // Thoi gian du kien giu ban (phut) - TRUOC DAY BI THIEU, dan den khong biet
    // ban duoc giu toi may gio => khong the tinh trung lich. Mac dinh 30 phut (co the cau hinh tu 15 den 45 phut).
    public int DurationMinutes { get; set; } = DefaultDurationMinutes;

    // Gio du kien tra ban = Gio den + thoi luong giu ban. Dung de kiem tra
    // trung lich giua cac luot dat ban tren cung 1 ban (xem ReservationsController).
    public DateTime ReservationEndTime => ReservationTime.AddMinutes(DurationMinutes);

    public const int DefaultDurationMinutes = 30;
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 45;

    public int NumberOfGuests { get; set; } = 1;
    public ReservationStatus Status { get; set; } = ReservationStatus.DaXacNhan;

    // Tien coc (chu yeu dung cho phong VIP can giu cho) - module VIP chua
    // trien khai o backend nay nen mac dinh 0, chi luu du lieu thuan tuy.
    public decimal DepositAmount { get; set; } = 0;
    public bool DepositPaid { get; set; } = false;
    public DateTime? DepositPaidAt { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Truy vết nhân viên đã hủy/đánh dấu không đến. Các cột tương ứng được
    // WinFormsCompatibilitySchemaService bổ sung tự động cho database cũ.
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAt { get; set; }
        // Đặt bàn từ web gắn với khách đã đăng nhập (null nếu khách vãng lai/nhân viên đặt hộ).
    public int? CustomerId { get; set; }

    // Nguồn tạo: "Web" hoặc "NhanVien" (xem ReservationSources).
    public string Source { get; set; } = ReservationSources.NhanVien;

    // Mã đặt bàn dùng chung cho một lượt đặt nhiều bàn = ReservationId của dòng đầu tiên.
    // Null với đặt 1 bàn (mã chính là ReservationId).
    public int? BookingGroupId { get; set; }
}
public static class ReservationSources
{
    public const string Web = "Web";
    public const string NhanVien = "NhanVien";
}