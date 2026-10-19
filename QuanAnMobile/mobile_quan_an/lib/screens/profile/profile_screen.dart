import 'dart:async';

import 'package:flutter/material.dart';
import '../../app/theme.dart';
import '../../models/app_notification.dart';
import '../attendance/attendance_screen.dart';
import '../../models/attendance_state.dart';
import '../attendance/shift_history_screen.dart';
import 'personal_info_screen.dart';
import '../../models/notification_settings_state.dart';
import 'notification_settings_screen.dart';
import '../login/login_screen.dart';
import '../invoices/invoice_history_screen.dart';
import '../statistics/statistics_screen.dart';
import '../../models/employee_profile.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../services/mobile_access_policy.dart';
import '../../widgets/top_message.dart';

class ProfileScreen extends StatefulWidget {
  final List<AppNotification> notifications;

  const ProfileScreen({super.key, required this.notifications});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  StreamSubscription<AppNotification>? _notificationBadgeSubscription;
  bool _isLoadingAttendance = true;
  bool _isLoadingProfile = true;

  int get _unreadNotificationCount =>
      widget.notifications.where((item) => !item.isRead).length;

  @override
  void initState() {
    super.initState();
    _loadProfile();
    _loadAttendance();

    // ProfileScreen dùng chung chính List<AppNotification> mà TableScreen quản lý.
    // Khi SignalR nhận thông báo mới, TableScreen cập nhật list này; listener
    // ở đây chỉ yêu cầu redraw để badge luôn đồng bộ khi đang ở tab Cá nhân.
    _notificationBadgeSubscription = AppServices.realtimeService.notifications
        .listen((_) {
          if (!mounted) return;
          setState(() {});
        });
  }

  @override
  void dispose() {
    _notificationBadgeSubscription?.cancel();
    super.dispose();
  }

  Widget _notificationIcon() {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        const Icon(Icons.notifications_none),
        if (_unreadNotificationCount > 0)
          Positioned(
            right: -7,
            top: -6,
            child: Container(
              constraints: const BoxConstraints(minWidth: 17, minHeight: 17),
              padding: const EdgeInsets.symmetric(horizontal: 4),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: Colors.red,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                _unreadNotificationCount > 99
                    ? '99+'
                    : '$_unreadNotificationCount',
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 9,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
      ],
    );
  }

  Future<void> _loadProfile() async {
    try {
      final EmployeeProfile profile = await AppServices.profileService
          .getProfile(employeeCode: appEmployee.employeeCode);

      appEmployee = profile;

      if (!mounted) return;

      setState(() {
        _isLoadingProfile = false;
      });
    } on ApiException {
      if (!mounted) return;

      setState(() {
        _isLoadingProfile = false;
      });
    }
  }

  Future<void> _loadAttendance() async {
    try {
      await AppServices.attendanceService.loadToday(
        employeeCode: appEmployee.employeeCode,
        state: appAttendanceState,
      );

      if (!mounted) return;

      setState(() {
        _isLoadingAttendance = false;
      });
    } on ApiException {
      if (!mounted) return;

      setState(() {
        _isLoadingAttendance = false;
      });
    }
  }

  String _formatTime(DateTime? time) {
    if (time == null) {
      return '--:--';
    }

    final String hour = time.hour.toString().padLeft(2, '0');

    final String minute = time.minute.toString().padLeft(2, '0');

    return '$hour:$minute';
  }

  Widget _buildShiftStatus() {
    final AttendanceState state = appAttendanceState;

    String title;
    String subtitle;
    IconData icon;
    Color color;
    Color backgroundColor;

    if (_isLoadingAttendance) {
      title = 'Đang tải ca làm';
      subtitle = 'Đang đồng bộ dữ liệu chấm công';

      icon = Icons.sync;
      color = Colors.grey;
      backgroundColor = Colors.grey.shade100;
    } else if (!state.hasShift) {
      title = 'Chưa được xếp ca';
      subtitle = 'Hôm nay chưa có ca làm được phân công';

      icon = Icons.event_busy;
      color = Colors.grey;
      backgroundColor = Colors.grey.shade100;
    } else if (state.checkedIn && !state.checkedOut) {
      title = 'Đang trong ca';
      subtitle = 'Bắt đầu lúc ${_formatTime(state.checkInTime)}';

      icon = Icons.circle;
      color = Colors.green;
      backgroundColor = Colors.green.shade50;
    } else if (state.checkedOut) {
      title = 'Đã kết thúc ca';
      subtitle = 'Kết thúc lúc ${_formatTime(state.checkOutTime)}';

      icon = Icons.check_circle;
      color = Colors.blueGrey;
      backgroundColor = Colors.blueGrey.shade50;
    } else {
      title = 'Chưa điểm danh';
      subtitle = 'Bạn chưa bắt đầu ca làm hôm nay';

      icon = Icons.access_time;
      color = AppTheme.orangeColor;
      backgroundColor = const Color(0xFFFFF1E8);
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(icon, size: 18, color: color),

          const SizedBox(width: 10),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    color: color,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  subtitle,
                  style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRoleAccessSummary() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: const Color(0xFFF6F7F9),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.admin_panel_settings_outlined,
            size: 20,
            color: AppTheme.orangeColor,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Quyền Mobile - ${appEmployee.role}',
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  MobileAccessPolicy.roleSummary(appEmployee.role),
                  style: TextStyle(
                    fontSize: 12,
                    height: 1.35,
                    color: Colors.grey.shade700,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _handleLogout(BuildContext context) async {
    final bool isWorking =
        appAttendanceState.checkedIn && !appAttendanceState.checkedOut;

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: Text(isWorking ? 'Kết thúc ca' : 'Đăng xuất'),
          content: Text(
            isWorking
                ? 'Bạn đang trong ca làm. '
                      'Bạn có muốn kết thúc ca '
                      'và đăng xuất không?'
                : 'Bạn có chắc muốn đăng xuất không?',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.pop(dialogContext, false);
              },
              child: const Text('Hủy'),
            ),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(dialogContext, true);
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.red.shade700,
                foregroundColor: Colors.white,
              ),
              child: Text(isWorking ? 'Kết thúc ca' : 'Đăng xuất'),
            ),
          ],
        );
      },
    );

    if (confirmed != true) {
      return;
    }

    // Nếu đang trong ca thì kết thúc ca thật trên database
    // trước khi đăng xuất.
    if (isWorking) {
      try {
        await AppServices.attendanceService.checkOut(
          employeeCode: appEmployee.employeeCode,
          state: appAttendanceState,
        );
      } on ApiException catch (error) {
        if (!context.mounted) return;

        TopMessage.show(
          context,
          message: error.message,
          type: TopMessageType.error,
        );
        return;
      }
    }

    await AppServices.realtimeService.stop();

    appEmployee = const EmployeeProfile(
      employeeCode: '',
      fullName: '',
      role: '',
    );

    if (!context.mounted) {
      return;
    }

    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (context) => LoginScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Cá nhân',
          style: TextStyle(
            color: AppTheme.textColor,
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),

      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            // =========================================
            // THÔNG TIN NHÂN VIÊN
            // =========================================
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: Colors.grey.shade200),
              ),
              child: Row(
                children: [
                  Container(
                    width: 72,
                    height: 72,
                    decoration: const BoxDecoration(
                      color: Color(0xFFFFE9DE),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.person,
                      size: 42,
                      color: AppTheme.orangeColor,
                    ),
                  ),

                  const SizedBox(width: 16),

                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _isLoadingProfile
                              ? 'Đang tải...'
                              : appEmployee.fullName,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: AppTheme.textColor,
                          ),
                        ),

                        const SizedBox(height: 6),

                        Row(
                          children: [
                            const Icon(
                              Icons.badge_outlined,
                              size: 16,
                              color: AppTheme.orangeColor,
                            ),

                            const SizedBox(width: 5),

                            Expanded(
                              child: Text(
                                _isLoadingProfile
                                    ? 'Đang đồng bộ hồ sơ'
                                    : appEmployee.area.trim().isEmpty
                                    ? appEmployee.role
                                    : '${appEmployee.role} - ${appEmployee.area}',
                                style: const TextStyle(
                                  fontSize: 13,
                                  color: Colors.grey,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),

            const SizedBox(height: 10),

            _buildShiftStatus(),

            const SizedBox(height: 10),

            _buildRoleAccessSummary(),

            const SizedBox(height: 14),

            // =========================================
            // CÁC CHỨC NĂNG
            // =========================================
            _buildMenuItem(
              context: context,
              icon: Icons.person_outline,
              iconBackground: const Color(0xFFD9F7FB),
              iconColor: Colors.teal,
              title: 'Thông tin cá nhân',
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (context) => const PersonalInfoScreen(),
                  ),
                );

                if (!mounted) return;

                await _loadProfile();
              },
            ),

            const SizedBox(height: 10),

            _buildMenuItem(
              context: context,
              icon: Icons.fingerprint,
              iconBackground: const Color(0xFFFFE5D8),
              iconColor: AppTheme.orangeColor,
              title: 'Điểm danh',
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (context) =>
                        AttendanceScreen(attendanceState: appAttendanceState),
                  ),
                );

                if (!mounted) {
                  return;
                }

                await _loadAttendance();
              },
            ),

            const SizedBox(height: 10),

            _buildMenuItem(
              context: context,
              icon: Icons.history,
              iconBackground: const Color(0xFFDDF1E7),
              iconColor: Colors.green,
              title: 'Lịch sử ca làm',
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (context) =>
                        ShiftHistoryScreen(attendanceState: appAttendanceState),
                  ),
                );

                if (!mounted) return;
                setState(() {});
              },
            ),

            const SizedBox(height: 10),

            _buildMenuItem(
              context: context,
              icon: Icons.notifications_none,
              iconBackground: const Color(0xFFFFE7DE),
              iconColor: Colors.brown,
              title: 'Cài đặt thông báo',
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (context) => NotificationSettingsScreen(
                      settings: appNotificationSettingsState,
                    ),
                  ),
                );

                if (!mounted) return;
                setState(() {});
              },
            ),

            if (MobileAccessPolicy.canViewStatistics()) ...[
              const SizedBox(height: 10),
              _buildMenuItem(
                context: context,
                icon: Icons.bar_chart_rounded,
                iconBackground: const Color(0xFFFFE9DE),
                iconColor: AppTheme.orangeColor,
                title: 'Thống kê',
                onTap: () async {
                  await Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => const StatisticsScreen()),
                  );
                },
              ),
            ],

            if (MobileAccessPolicy.canCompletePayment(appEmployee.role)) ...[
              const SizedBox(height: 10),
              _buildMenuItem(
                context: context,
                icon: Icons.receipt_long_outlined,
                iconBackground: const Color(0xFFE4F0FF),
                iconColor: Colors.blue,
                title: 'Hóa đơn đã thanh toán',
                onTap: () async {
                  await Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => const InvoiceHistoryScreen(),
                    ),
                  );
                },
              ),
            ],

            const SizedBox(height: 28),

            // =========================================
            // ĐĂNG XUẤT
            // =========================================
            SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton.icon(
                onPressed: () async {
                  await _handleLogout(context);
                },

                icon: const Icon(Icons.logout),

                label: const Text(
                  'Đăng xuất',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),

                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.red.shade700,
                  foregroundColor: Colors.white,
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(8),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),

      // =========================================
      // BOTTOM NAVIGATION
      // =========================================
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: 3,
        type: BottomNavigationBarType.fixed,
        selectedItemColor: AppTheme.orangeColor,
        unselectedItemColor: Colors.grey,

        onTap: (index) {
          // Đang ở Cá nhân
          if (index == 3) {
            return;
          }

          // Trả tab cần chuyển cho màn đã mở ProfileScreen
          Navigator.pop(context, index);
        },

        items: [
          const BottomNavigationBarItem(
            icon: Icon(Icons.grid_view_rounded),
            label: 'Sơ đồ',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.restaurant_menu),
            label: 'Thực đơn',
          ),
          BottomNavigationBarItem(
            icon: _notificationIcon(),
            label: 'Thông báo',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.person_outline),
            label: 'Cá nhân',
          ),
        ],
      ),
    );
  }

  static Widget _buildMenuItem({
    required BuildContext context,
    required IconData icon,
    required Color iconBackground,
    required Color iconColor,
    required String title,
    required VoidCallback onTap,
  }) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: Colors.grey.shade200),
          ),
          child: Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: iconBackground,
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, color: iconColor, size: 22),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),

              const Icon(Icons.chevron_right, color: Colors.grey),
            ],
          ),
        ),
      ),
    );
  }
}
