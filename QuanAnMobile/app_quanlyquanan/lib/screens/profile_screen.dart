import 'package:flutter/material.dart';
import '../models/user_profile.dart';
import '../theme/app_colors.dart';
import 'checkin_screen.dart';
import 'login_screen.dart';

class ProfileScreen extends StatelessWidget {
  final UserProfile user;
  const ProfileScreen({super.key, required this.user});

  void _logout(BuildContext context) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Đăng xuất / Kết thúc ca'),
        content: const Text('Bạn có chắc muốn kết thúc ca làm việc?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Hủy')),
          TextButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              Navigator.of(context).pushAndRemoveUntil(
                MaterialPageRoute(builder: (_) => const LoginScreen()),
                (route) => false,
              );
            },
            child: const Text('Đồng ý', style: TextStyle(color: AppColors.error)),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Xin chào, ${user.fullName}'), automaticallyImplyLeading: false),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLow,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 32,
                  backgroundColor: AppColors.primaryContainer,
                  child: Text(user.fullName.isNotEmpty ? user.fullName[0].toUpperCase() : '?',
                      style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w700)),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(user.fullName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18)),
                      const SizedBox(height: 4),
                      Row(children: [
                        const Icon(Icons.badge_outlined, size: 15, color: AppColors.onSurfaceVariant),
                        const SizedBox(width: 4),
                        Text('${user.roleName} • ${user.username}',
                            style: const TextStyle(color: AppColors.onSurfaceVariant, fontSize: 13)),
                      ]),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          _menuTile(context, Icons.person_outline, AppColors.secondaryContainer, AppColors.secondary,
              'Thông tin cá nhân', onTap: () {}),
          const SizedBox(height: 10),
          _menuTile(context, Icons.history_rounded, const Color(0xFFCFEBD9), AppColors.tertiary,
              'Lịch sử ca làm', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const CheckinScreen()))),
          const SizedBox(height: 10),
          _menuTile(context, Icons.notifications_none_rounded, AppColors.tableDangDungBg, AppColors.primary,
              'Cài đặt thông báo', onTap: () {}),
          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: ElevatedButton.icon(
              onPressed: () => _logout(context),
              icon: const Icon(Icons.logout),
              label: const Text('Đăng xuất / Kết thúc ca'),
              style: ElevatedButton.styleFrom(backgroundColor: AppColors.error),
            ),
          ),
        ],
      ),
    );
  }

  Widget _menuTile(BuildContext context, IconData icon, Color iconBg, Color iconColor, String title,
      {required VoidCallback onTap}) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE9ECEF)),
      ),
      child: ListTile(
        onTap: onTap,
        leading: CircleAvatar(backgroundColor: iconBg, child: Icon(icon, color: iconColor, size: 20)),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
        trailing: const Icon(Icons.chevron_right, color: AppColors.onSurfaceVariant),
      ),
    );
  }
}
