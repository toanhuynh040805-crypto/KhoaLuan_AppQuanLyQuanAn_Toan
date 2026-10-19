namespace QuanAnWinForm.Models;

// Khớp enum Models.NotificationType bên Backend (QuanAnBackend/Models/Notification.cs)
public enum NotificationTypeEnum
{
    DonHang = 0,
    DatBan = 1,
    CaLam = 2,
    HeThong = 3,
    KhuyenMai = 4
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int NotificationType { get; set; } // khớp enum NotificationTypeEnum ở trên
    public string? TargetRole { get; set; }
    public int? TargetUserId { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}