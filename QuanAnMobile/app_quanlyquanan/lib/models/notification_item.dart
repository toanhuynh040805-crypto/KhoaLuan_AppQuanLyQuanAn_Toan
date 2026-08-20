enum NotificationType { monXong, yeuCauThanhToan, goiNhanVien, daThanhToan }

// TODO: hien chua co endpoint /api/notifications ben Backend.
// Trong ban hoan chinh, danh sach nay nen lay tu SignalR realtime
// (nhan duoc thi luu vao 1 list local) thay vi goi REST API rieng.
class NotificationItem {
  final int id;
  final NotificationType type;
  final String title;
  final String description;
  final DateTime time;
  final bool isRead;

  NotificationItem({
    required this.id,
    required this.type,
    required this.title,
    required this.description,
    required this.time,
    this.isRead = false,
  });
}
