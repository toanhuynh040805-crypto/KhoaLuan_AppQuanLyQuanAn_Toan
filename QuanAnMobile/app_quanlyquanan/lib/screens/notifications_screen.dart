import 'package:flutter/material.dart';
import '../models/notification_item.dart';
import '../theme/app_colors.dart';

// TODO: hien dang dung du lieu mau. Trong ban hoan chinh, danh sach nay
// nen duoc cap nhat realtime qua SignalR (OnNewTicket, OnPaymentRequested...)
// thay vi goi REST API tinh mot lan.
class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key});

  List<NotificationItem> get _sampleData => [
        NotificationItem(
          id: 1,
          type: NotificationType.monXong,
          title: 'Bàn #12: Phở Bò đã xong!',
          description: 'Món ăn đã sẵn sàng để phục vụ. Vui lòng mang ra bàn.',
          time: DateTime.now(),
        ),
        NotificationItem(
          id: 2,
          type: NotificationType.yeuCauThanhToan,
          title: 'Bàn #05: Yêu cầu thanh toán',
          description: 'Khách hàng yêu cầu hóa đơn. Vui lòng kiểm tra lại đơn hàng.',
          time: DateTime.now().subtract(const Duration(minutes: 5)),
        ),
        NotificationItem(
          id: 3,
          type: NotificationType.goiNhanVien,
          title: 'Bàn #08: Gọi nhân viên',
          description: 'Khách hàng cần hỗ trợ thêm tại bàn.',
          time: DateTime.now().subtract(const Duration(minutes: 12)),
        ),
        NotificationItem(
          id: 4,
          type: NotificationType.daThanhToan,
          title: 'Bàn #02: Đã thanh toán',
          description: '',
          time: DateTime.now().subtract(const Duration(minutes: 30)),
          isRead: true,
        ),
      ];

  ({IconData icon, Color bg, Color fg}) _styleFor(NotificationType type) {
    switch (type) {
      case NotificationType.monXong:
        return (icon: Icons.restaurant, bg: AppColors.tableDangDungBg, fg: AppColors.primary);
      case NotificationType.yeuCauThanhToan:
        return (icon: Icons.payments_outlined, bg: AppColors.secondaryContainer, fg: AppColors.secondary);
      case NotificationType.goiNhanVien:
        return (icon: Icons.support_agent_outlined, bg: const Color(0xFFE3F2E8), fg: AppColors.tertiary);
      case NotificationType.daThanhToan:
        return (icon: Icons.done_all, bg: AppColors.surfaceContainerHigh, fg: AppColors.onSurfaceVariant);
    }
  }

  String _timeAgo(DateTime time) {
    final diff = DateTime.now().difference(time);
    if (diff.inMinutes < 1) return 'vừa xong';
    if (diff.inMinutes < 60) return '${diff.inMinutes} phút trước';
    return '${diff.inHours} giờ trước';
  }

  @override
  Widget build(BuildContext context) {
    final items = _sampleData;
    return Scaffold(
      appBar: AppBar(title: const Text('Thông báo'), automaticallyImplyLeading: false, actions: [
        IconButton(icon: const Icon(Icons.notifications_rounded), onPressed: () {}),
      ]),
      body: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(height: 12),
        itemBuilder: (context, index) {
          final item = items[index];
          final style = _styleFor(item.type);
          return Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: item.isRead ? const Color(0xFFFCFCFC) : AppColors.surfaceContainerLowest,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFE9ECEF)),
              boxShadow: item.isRead
                  ? null
                  : [BoxShadow(color: AppColors.primaryContainer, offset: const Offset(-4, 0), blurRadius: 0)],
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(radius: 22, backgroundColor: style.bg, child: Icon(style.icon, color: style.fg, size: 20)),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: Text(item.title,
                                style: TextStyle(fontWeight: FontWeight.w700, color: item.isRead ? AppColors.onSurfaceVariant : AppColors.onSurface)),
                          ),
                          Text(_timeAgo(item.time),
                              style: TextStyle(fontSize: 12, color: item.isRead ? AppColors.onSurfaceVariant : AppColors.primary)),
                        ],
                      ),
                      if (item.description.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Text(item.description, style: const TextStyle(fontSize: 13, color: AppColors.onSurfaceVariant)),
                      ],
                      if (item.type == NotificationType.monXong || item.type == NotificationType.yeuCauThanhToan) ...[
                        const SizedBox(height: 10),
                        item.type == NotificationType.monXong
                            ? ElevatedButton(
                                onPressed: () {},
                                style: ElevatedButton.styleFrom(minimumSize: const Size(100, 36), padding: const EdgeInsets.symmetric(horizontal: 16)),
                                child: const Text('Xác nhận'))
                            : OutlinedButton(
                                onPressed: () {},
                                style: OutlinedButton.styleFrom(minimumSize: const Size(100, 36), padding: const EdgeInsets.symmetric(horizontal: 16)),
                                child: const Text('In hóa đơn')),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
