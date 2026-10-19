import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/app_notification.dart';
import '../../services/notification_service.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class NotificationScreen extends StatefulWidget {
  final List<AppNotification> notifications;
  final NotificationService notificationService;
  final String employeeCode;

  const NotificationScreen({
    super.key,
    required this.notifications,
    required this.notificationService,
    required this.employeeCode,
  });

  @override
  State<NotificationScreen> createState() =>
      _NotificationScreenState();
}

class _NotificationScreenState
    extends State<NotificationScreen> {

  StreamSubscription<AppNotification>?
      _realtimeSubscription;

  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addPostFrameCallback(
      (_) {
        _markAllAsRead();
      },
    );

    _realtimeSubscription =
        AppServices
            .realtimeService
            .notifications
            .listen(
      (notification) async {
        if (!mounted) {
          return;
        }

        final int existingIndex =
            notification.id == null
                ? -1
                : widget.notifications.indexWhere(
                    (item) =>
                        item.id ==
                        notification.id,
                  );

        setState(() {
          if (existingIndex >= 0) {
            widget.notifications[existingIndex] =
                notification;
          } else {
            widget.notifications.insert(
              0,
              notification,
            );
          }
        });

        // Đang đứng ngay màn Thông báo thì coi như đã xem.
        final int newIndex =
            notification.id == null
                ? -1
                : widget.notifications.indexWhere(
                    (item) =>
                        item.id ==
                        notification.id,
                  );

        if (newIndex >= 0) {
          await _markAsRead(newIndex);
        }
      },
    );
  }

  Future<void> _markAllAsRead() async {
    final List<int> unreadIndexes = [];

    for (int i = 0;
        i < widget.notifications.length;
        i++) {
      final AppNotification item =
          widget.notifications[i];

      if (!item.isRead &&
          item.id != null) {
        unreadIndexes.add(i);
      }
    }

    for (final int index in unreadIndexes) {
      if (!mounted ||
          index >= widget.notifications.length) {
        return;
      }

      await _markAsRead(
        index,
        showError: false,
      );
    }
  }

  @override
  void dispose() {
    _realtimeSubscription?.cancel();
    super.dispose();
  }

  Future<void> _markAsRead(
    int index, {
    bool showError = true,
  }) async {
    final AppNotification notification =
        widget.notifications[index];

    if (notification.isRead ||
        notification.id == null) {
      return;
    }

    try {
      await widget.notificationService.markAsRead(
        notificationId: notification.id!,
        employeeCode: widget.employeeCode,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        widget.notifications[index] =
            notification.copyWith(
          isRead: true,
        );
      });
    } catch (_) {
      if (!mounted || !showError) {
        return;
      }

      TopMessage.show(
        context,
        message:
            'Không thể cập nhật trạng thái thông báo',
        type: TopMessageType.error,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor:
          const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Thông báo',
          style: TextStyle(
            color: AppTheme.textColor,
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),

      body: widget.notifications.isEmpty
          ? const Center(
              child: Text(
                'Chưa có thông báo',
                style: TextStyle(
                  fontSize: 15,
                  color: Colors.grey,
                ),
              ),
            )
          : ListView.separated(
              padding:
                  const EdgeInsets.all(16),
              itemCount:
                  widget.notifications.length,
              separatorBuilder:
                  (context, index) {
                return const SizedBox(
                  height: 12,
                );
              },
              itemBuilder:
                  (context, index) {
                final AppNotification
                    notification =
                    widget.notifications[index];

                return InkWell(
                  borderRadius:
                      BorderRadius.circular(12),
                  onTap: () {
                    _markAsRead(index);
                  },
                  child: NotificationItem(
                    title:
                        notification.title,
                    message:
                        notification.message,
                    time:
                        notification.time,
                    isRead:
                        notification.isRead,
                  ),
                );
              },
            ),

      bottomNavigationBar:
          BottomNavigationBar(
        currentIndex: 2,
        type:
            BottomNavigationBarType.fixed,
        selectedItemColor:
            AppTheme.orangeColor,
        unselectedItemColor:
            Colors.grey,
        onTap: (index) {
          if (index == 2) {
            return;
          }

          Navigator.pop(
            context,
            index,
          );
        },
        items: const [
          BottomNavigationBarItem(
            icon:
                Icon(Icons.grid_view_rounded),
            label: 'Sơ đồ',
          ),
          BottomNavigationBarItem(
            icon:
                Icon(Icons.restaurant_menu),
            label: 'Thực đơn',
          ),
          BottomNavigationBarItem(
            icon:
                Icon(Icons.notifications_none),
            label: 'Thông báo',
          ),
          BottomNavigationBarItem(
            icon:
                Icon(Icons.person_outline),
            label: 'Cá nhân',
          ),
        ],
      ),
    );
  }
}

class NotificationItem extends StatelessWidget {
  final String title;
  final String message;
  final String time;
  final bool isRead;

  const NotificationItem({
    super.key,
    required this.title,
    required this.message,
    required this.time,
    required this.isRead,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding:
          const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isRead
            ? Colors.white
            : const Color(0xFFFFF6F1),
        borderRadius:
            BorderRadius.circular(12),
        border: Border.all(
          color: isRead
              ? Colors.grey.shade200
              : AppTheme.orangeColor
                  .withValues(alpha: 0.30),
        ),
      ),
      child: Row(
        crossAxisAlignment:
            CrossAxisAlignment.start,
        children: [
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: AppTheme.orangeColor
                  .withValues(alpha: 0.12),
              borderRadius:
                  BorderRadius.circular(21),
            ),
            child: const Icon(
              Icons.notifications_none,
              color:
                  AppTheme.orangeColor,
            ),
          ),

          const SizedBox(width: 12),

          Expanded(
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        title,
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight:
                              isRead
                                  ? FontWeight.w600
                                  : FontWeight.bold,
                        ),
                      ),
                    ),

                    if (!isRead)
                      Container(
                        width: 8,
                        height: 8,
                        decoration:
                            const BoxDecoration(
                          color: Colors.red,
                          shape:
                              BoxShape.circle,
                        ),
                      ),
                  ],
                ),

                const SizedBox(height: 4),

                Text(
                  message,
                  style: TextStyle(
                    fontSize: 13,
                    color:
                        Colors.grey.shade700,
                  ),
                ),

                const SizedBox(height: 6),

                Text(
                  time,
                  style: TextStyle(
                    fontSize: 11,
                    color:
                        Colors.grey.shade500,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
