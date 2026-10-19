namespace QuanAnBackend.Models;

// Khớp bảng Notifications trong QuanAnManagementDB_Final.sql (nhóm 8: HỆ THỐNG
// - THÔNG BÁO REALTIME & AUDIT LOG). 5 giá trị theo đúng comment gốc trong SQL.
public enum NotificationType
{
    DonHang,
    DatBan,
    CaLam,
    HeThong,
    KhuyenMai
}

public class Notification
{
    public int NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; }

    // Chỉ dùng 1 trong 2: TargetRole (báo cho CẢ MỘT vai trò, vd mọi ThuNgan)
    // HOẶC TargetUserId (báo cho ĐÚNG 1 người, vd nhân viên vừa được giao việc).
    public string? TargetRole { get; set; }
    public int? TargetUserId { get; set; }

    public string? RelatedEntityType { get; set; } // "Order", "Reservation", "StaffTask"...
    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}